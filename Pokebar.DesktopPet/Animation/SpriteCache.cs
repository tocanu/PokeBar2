using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Serilog;

namespace Pokebar.DesktopPet.Animation;

/// <summary>
/// Cache LRU de AnimationClips carregados pelo SpriteLoader.
/// Evita recarregar/recortar sprites do disco quando vários Pokémon do mesmo dex são usados.
/// Thread-safe não é necessário (tudo roda na UI thread do WPF).
/// </summary>
public class SpriteCache
{
    private readonly SpriteLoader _loader;
    private readonly int _maxEntries;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly LinkedList<string> _lruOrder = new();
    private readonly Dictionary<string, Task<PokemonAnimationSet>> _pendingLoads = new();

    /// <summary>
    /// Cria um cache LRU para sprites.
    /// </summary>
    /// <param name="loader">SpriteLoader subjacente que faz o carregamento real.</param>
    /// <param name="maxEntries">Máximo de Pokémon (UniqueId) mantidos em cache. Default 30.</param>
    public SpriteCache(SpriteLoader loader, int maxEntries = 30)
    {
        _loader = loader;
        _maxEntries = Math.Max(5, maxEntries);
    }

    /// <summary>
    /// Obtém todos os clips de animação para um Pokémon, usando cache quando disponível.
    /// </summary>
    public PokemonAnimationSet GetAnimations(int dex, string formId, GameplayConfig config)
    {
        var uniqueId = new PokemonVariant(dex, formId).UniqueId;

        if (_cache.TryGetValue(uniqueId, out var cached))
        {
            Touch(uniqueId);
            Log.Debug("SpriteCache HIT: {UniqueId} ({Count} entries cached)", uniqueId, _cache.Count);
            return cached.Animations;
        }

        // Cache miss — carregar do disco
        var animations = LoadAnimationSet(dex, formId, config);
        var entry = new CacheEntry(animations);
        _cache[uniqueId] = entry;
        _lruOrder.AddFirst(uniqueId);

        // Evict se necessário
        while (_cache.Count > _maxEntries)
        {
            Evict();
        }

        Log.Debug("SpriteCache MISS: {UniqueId} loaded ({Count} entries cached)", uniqueId, _cache.Count);
        return animations;
    }

    /// <summary>
    /// Pré-carrega animações para um Pokémon sem retornar (útil para preparar player no boot).
    /// </summary>
    public void Preload(int dex, string formId, GameplayConfig config)
    {
        GetAnimations(dex, formId, config);
    }

    /// <summary>
    /// Carrega animações em background thread (I/O + decode + crop).
    /// Frames Freeze()d são cross-thread safe.
    /// Retorna cache hit imediato ou carrega assíncronamente e chama onLoaded no thread pool.
    /// O chamador deve fazer o marshal para UI thread se necessário.
    /// </summary>
    public Task<PokemonAnimationSet> GetAnimationsAsync(int dex, string formId, GameplayConfig config)
    {
        var uniqueId = new PokemonVariant(dex, formId).UniqueId;

        // Cache hit — retornar imediato
        if (_cache.TryGetValue(uniqueId, out var cached))
        {
            Touch(uniqueId);
            Log.Debug("SpriteCache ASYNC HIT: {UniqueId}", uniqueId);
            return Task.FromResult(cached.Animations);
        }

        // Se já tem um loading pendente, retornar a mesma task
        if (_pendingLoads.TryGetValue(uniqueId, out var pending))
        {
            Log.Debug("SpriteCache ASYNC COALESCE: {UniqueId}", uniqueId);
            return pending;
        }

        // Cache miss — carregar em background
        var task = Task.Run(() => LoadAnimationSet(dex, formId, config))
            .ContinueWith(t =>
            {
                _pendingLoads.Remove(uniqueId);

                if (t.IsFaulted)
                {
                    Log.Error(t.Exception, "SpriteCache ASYNC FAIL: {UniqueId}", uniqueId);
                    // Retornar set vazio como fallback
                    return new PokemonAnimationSet(null, null, null, null, null, false, null);
                }

                var animations = t.Result;
                // Inserir no cache (marshall-safe: chamado de volta no UI thread via Dispatcher)
                if (!_cache.ContainsKey(uniqueId))
                {
                    var entry = new CacheEntry(animations);
                    _cache[uniqueId] = entry;
                    _lruOrder.AddFirst(uniqueId);

                    while (_cache.Count > _maxEntries)
                        Evict();
                }

                Log.Debug("SpriteCache ASYNC LOADED: {UniqueId} ({Count} entries)", uniqueId, _cache.Count);
                return animations;
            }, TaskScheduler.FromCurrentSynchronizationContext());

        _pendingLoads[uniqueId] = task;
        return task;
    }

    /// <summary>
    /// Marca um UniqueId como "em uso" para não ser evicted pelo LRU.
    /// Chamado automaticamente por GetAnimations.
    /// </summary>
    public void Pin(string uniqueId)
    {
        if (_cache.TryGetValue(uniqueId, out var entry))
        {
            entry.Pinned = true;
            Touch(uniqueId);
        }
    }

    /// <summary>
    /// Remove o pin de um UniqueId, permitindo eviction.
    /// </summary>
    public void Unpin(string uniqueId)
    {
        if (_cache.TryGetValue(uniqueId, out var entry))
        {
            entry.Pinned = false;
        }
    }

    /// <summary>
    /// Remove uma entrada específica do cache.
    /// </summary>
    public void Invalidate(string uniqueId)
    {
        if (_cache.Remove(uniqueId))
        {
            _lruOrder.Remove(uniqueId);
            Log.Debug("SpriteCache INVALIDATE: {UniqueId}", uniqueId);
        }
    }

    /// <summary>
    /// Limpa todo o cache.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _lruOrder.Clear();
        Log.Debug("SpriteCache CLEARED");
    }

    /// <summary>
    /// Número de entradas atualmente no cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Acesso ao SpriteLoader subjacente (para TryGetOffset etc).
    /// </summary>
    public SpriteLoader Loader => _loader;

    private PokemonAnimationSet LoadAnimationSet(int dex, string formId, GameplayConfig config)
    {
        var walkRowRight = config.Sprite.WalkRowRight;
        var walkRowLeft = config.Sprite.WalkRowLeft;
        var walkFrameTime = config.Animation.WalkFrameTimeSeconds;
        var idleFrameTime = config.Animation.IdleFrameTimeSeconds;
        var fightFrameTime = config.Animation.AttackFrameTimeSeconds;
        var sleepFrameTime = config.Animation.SleepFrameTimeSeconds;

        var walkRight = _loader.LoadAnimation(dex, formId, AnimationType.Walk, new[] { walkRowRight }, requireSelection: true, frameTime: walkFrameTime);
        var walkLeft = _loader.LoadAnimation(dex, formId, AnimationType.Walk, new[] { walkRowLeft }, requireSelection: true, frameTime: walkFrameTime);
        var useDirectionalWalk = walkRight != null && walkLeft != null;

        AnimationClip? walkFallback = null;
        if (!useDirectionalWalk)
        {
            walkFallback = _loader.LoadAnimation(dex, formId, AnimationType.Walk, frameTime: walkFrameTime);
            walkRight = null;
            walkLeft = null;
        }

        var idle = _loader.LoadAnimation(dex, formId, AnimationType.Idle, frameTime: idleFrameTime);
        var fight = _loader.LoadAnimation(dex, formId, AnimationType.Fight, frameTime: fightFrameTime);

        // FASE 7: Carregar animações de comportamento (opcionais)
        var sleep = _loader.LoadAnimation(dex, formId, AnimationType.Sleep, frameTime: sleepFrameTime);
        var hop = _loader.LoadAnimation(dex, formId, AnimationType.Hop, frameTime: idleFrameTime);
        var sit = _loader.LoadAnimation(dex, formId, AnimationType.Sit, frameTime: idleFrameTime);
        var eat = _loader.LoadAnimation(dex, formId, AnimationType.Eat, frameTime: idleFrameTime);
        var lookUp = _loader.LoadAnimation(dex, formId, AnimationType.LookUp, frameTime: idleFrameTime);
        var lay = _loader.LoadAnimation(dex, formId, AnimationType.Lay, frameTime: sleepFrameTime);
        var deepBreath = _loader.LoadAnimation(dex, formId, AnimationType.DeepBreath, frameTime: idleFrameTime);
        var pose = _loader.LoadAnimation(dex, formId, AnimationType.Pose, frameTime: idleFrameTime);
        var nod = _loader.LoadAnimation(dex, formId, AnimationType.Nod, frameTime: idleFrameTime);
        var shock = _loader.LoadAnimation(dex, formId, AnimationType.Shock, frameTime: idleFrameTime);

        var uniqueId = new PokemonVariant(dex, formId).UniqueId;
        _loader.TryGetOffset(uniqueId, out var offset);

        return new PokemonAnimationSet(
            walkRight, walkLeft, walkFallback,
            idle, fight,
            useDirectionalWalk,
            offset,
            sleep, hop, sit, eat, lookUp, lay, deepBreath, pose, nod, shock);
    }

    private void Touch(string uniqueId)
    {
        _lruOrder.Remove(uniqueId);
        _lruOrder.AddFirst(uniqueId);
    }

    private void Evict()
    {
        // Remove o menos recente que não esteja pinned
        var node = _lruOrder.Last;
        while (node != null)
        {
            var key = node.Value;
            if (_cache.TryGetValue(key, out var entry) && entry.Pinned)
            {
                node = node.Previous;
                continue;
            }

            _cache.Remove(key);
            _lruOrder.Remove(node);
            Log.Debug("SpriteCache EVICT: {UniqueId}", key);
            return;
        }

        // Se todos estão pinned, remove o último mesmo assim (fallback)
        if (_lruOrder.Last != null)
        {
            var key = _lruOrder.Last.Value;
            _cache.Remove(key);
            _lruOrder.RemoveLast();
            Log.Debug("SpriteCache FORCE-EVICT (all pinned): {UniqueId}", key);
        }
    }

    private sealed class CacheEntry
    {
        public CacheEntry(PokemonAnimationSet animations)
        {
            Animations = animations;
        }

        public PokemonAnimationSet Animations { get; }
        public bool Pinned { get; set; }
    }
}

/// <summary>
/// Conjunto completo de animações carregadas para um Pokémon.
/// Imutável após criação — pode ser compartilhado entre múltiplas entidades.
/// </summary>
public sealed class PokemonAnimationSet
{
    public PokemonAnimationSet(
        AnimationClip? walkRight,
        AnimationClip? walkLeft,
        AnimationClip? walkFallback,
        AnimationClip? idle,
        AnimationClip? fight,
        bool useDirectionalWalk,
        OffsetAdjustment? offset,
        AnimationClip? sleep = null,
        AnimationClip? hop = null,
        AnimationClip? sit = null,
        AnimationClip? eat = null,
        AnimationClip? lookUp = null,
        AnimationClip? lay = null,
        AnimationClip? deepBreath = null,
        AnimationClip? pose = null,
        AnimationClip? nod = null,
        AnimationClip? shock = null)
    {
        WalkRight = walkRight;
        WalkLeft = walkLeft;
        WalkFallback = walkFallback;
        Idle = idle;
        Fight = fight;
        UseDirectionalWalk = useDirectionalWalk;
        Offset = offset;
        Sleep = sleep;
        Hop = hop;
        Sit = sit;
        Eat = eat;
        LookUp = lookUp;
        Lay = lay;
        DeepBreath = deepBreath;
        Pose = pose;
        Nod = nod;
        Shock = shock;

        // Aplicar fallback chains
        if (WalkFallback == null && !UseDirectionalWalk)
            WalkFallback = Idle;
        Idle ??= WalkFallback ?? WalkRight ?? WalkLeft;
    }

    public AnimationClip? WalkRight { get; }
    public AnimationClip? WalkLeft { get; }
    public AnimationClip? WalkFallback { get; }
    public AnimationClip? Idle { get; }
    public AnimationClip? Fight { get; }
    public bool UseDirectionalWalk { get; }
    public OffsetAdjustment? Offset { get; }

    // FASE 7: Animações de comportamento
    public AnimationClip? Sleep { get; }
    public AnimationClip? Hop { get; }
    public AnimationClip? Sit { get; }
    public AnimationClip? Eat { get; }
    public AnimationClip? LookUp { get; }
    public AnimationClip? Lay { get; }
    public AnimationClip? DeepBreath { get; }
    public AnimationClip? Pose { get; }
    public AnimationClip? Nod { get; }
    public AnimationClip? Shock { get; }

    /// <summary>Retorna true se tem pelo menos uma animação de comportamento extra.</summary>
    public bool HasBehaviorAnimations =>
        Sleep != null || Hop != null || Sit != null || Eat != null ||
        LookUp != null || Lay != null || DeepBreath != null || Pose != null;
}
