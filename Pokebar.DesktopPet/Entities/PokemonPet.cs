using System;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet.Entities;

public class PokemonPet : BaseEntity
{
    private AnimationClip? _walkRightClip;
    private AnimationClip? _walkLeftClip;
    private AnimationClip? _walkFallbackClip;
    private AnimationClip? _idleClip;
    private AnimationClip? _fightFallbackClip;
    private bool _isWalking;
    private bool _useDirectionalWalk;
    private bool _hasRealIdleClip;
    private bool _hasFightClip;

    // Fight shake effect — creative fallback for Pokémon without fight animation
    private bool _fightShakeActive;
    private double _fightShakeTimer;
    private double _fightShakeBaseX;
    private const double FIGHT_SHAKE_AMPLITUDE = 3.0;   // pixels
    private const double FIGHT_SHAKE_FREQUENCY = 18.0;  // Hz

    // FASE 7: Clips de comportamento
    private AnimationClip? _sleepClip;
    private AnimationClip? _hopClip;
    private AnimationClip? _sitClip;
    private AnimationClip? _eatClip;
    private AnimationClip? _lookUpClip;
    private AnimationClip? _layClip;
    private AnimationClip? _deepBreathClip;
    private AnimationClip? _poseClip;
    private AnimationClip? _nodClip;
    private AnimationClip? _shockClip;

    public PokemonPet(int dex, string formId = "0000") : base(dex, formId)
    {
    }

    public bool ShouldFlip
    {
        get
        {
            // Durante combate, sempre usar flip (não tem animações direcionais de fight)
            if (State == EntityState.Fighting)
                return true;
            
            // Durante walk, só usa flip se não tiver animações direcionais
            if (_useDirectionalWalk && _isWalking)
                return false;
            
            return true;
        }
    }

    public void LoadAnimations(SpriteLoader loader, GameplayConfig config)
    {
        var walkRowRight = config.Sprite.WalkRowRight;
        var walkRowLeft = config.Sprite.WalkRowLeft;
        var walkFrameTime = config.Animation.WalkFrameTimeSeconds;
        var idleFrameTime = config.Animation.IdleFrameTimeSeconds;
        var fightFrameTime = config.Animation.AttackFrameTimeSeconds;
        
        _walkRightClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk, new[] { walkRowRight }, requireSelection: true, frameTime: walkFrameTime);
        _walkLeftClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk, new[] { walkRowLeft }, requireSelection: true, frameTime: walkFrameTime);
        _useDirectionalWalk = _walkRightClip != null && _walkLeftClip != null;

        if (!_useDirectionalWalk)
        {
            _walkFallbackClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk, frameTime: walkFrameTime);
            _walkRightClip = null;
            _walkLeftClip = null;
        }

        _idleClip = loader.LoadAnimation(Dex, FormId, AnimationType.Idle, frameTime: idleFrameTime);
        _hasRealIdleClip = _idleClip != null;
        
        // Para fight, não tentar carregar direcional - sempre usar fallback com flip
        _fightFallbackClip = loader.LoadAnimation(Dex, FormId, AnimationType.Fight, frameTime: fightFrameTime);
        _hasFightClip = _fightFallbackClip != null;
        
        if (loader.TryGetOffset(UniqueId, out var offset))
            SetHitbox(offset.HitboxX, offset.HitboxY, offset.HitboxWidth, offset.HitboxHeight);

        if (_walkFallbackClip == null && !_useDirectionalWalk)
            _walkFallbackClip = _idleClip;
        _idleClip ??= _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;

        var startClip = _idleClip ?? _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;
        if (startClip != null)
            AnimationPlayer.Play(startClip);
    }

    /// <summary>
    /// Aplica animações de um PokemonAnimationSet (obtido do SpriteCache).
    /// Mais eficiente que LoadAnimations — reutiliza clips já processados.
    /// </summary>
    public void ApplyAnimations(PokemonAnimationSet set)
    {
        _walkRightClip = set.WalkRight;
        _walkLeftClip = set.WalkLeft;
        _walkFallbackClip = set.WalkFallback;
        _idleClip = set.Idle;
        _fightFallbackClip = set.Fight;
        _useDirectionalWalk = set.UseDirectionalWalk;
        _hasRealIdleClip = set.Idle != null;
        _hasFightClip = set.Fight != null;

        // FASE 7: Animações de comportamento
        _sleepClip = set.Sleep;
        _hopClip = set.Hop;
        _sitClip = set.Sit;
        _eatClip = set.Eat;
        _lookUpClip = set.LookUp;
        _layClip = set.Lay;
        _deepBreathClip = set.DeepBreath;
        _poseClip = set.Pose;
        _nodClip = set.Nod;
        _shockClip = set.Shock;

        if (set.Offset != null)
            SetHitbox(set.Offset.HitboxX, set.Offset.HitboxY, set.Offset.HitboxWidth, set.Offset.HitboxHeight);

        var startClip = _idleClip ?? _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;
        if (startClip != null)
            AnimationPlayer.Play(startClip);
    }

    public void StartWalking()
    {
        _isWalking = true;
        _fightShakeActive = false;
        State = EntityState.Walking;
        AnimationPlayer.Resume();  // ensure not paused from idle freeze
        ApplyWalkClip();
    }

    public void StartIdle(bool updateState = true)
    {
        _isWalking = false;
        _fightShakeActive = false;
        if (updateState)
            State = EntityState.Idle;

        if (_hasRealIdleClip && _idleClip != null)
        {
            AnimationPlayer.Play(_idleClip);
        }
        else
        {
            // No real idle clip — freeze on first frame of walk so they don't "walk in place"
            var freezeClip = _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;
            if (freezeClip != null)
                AnimationPlayer.Play(freezeClip);  // will show frame 0
            AnimationPlayer.Pause();  // freeze on that frame
        }
    }

    public void StartFighting()
    {
        _isWalking = false;
        State = EntityState.Fighting;

        if (_hasFightClip && _fightFallbackClip != null)
        {
            // Has a real fight animation — play it
            _fightShakeActive = false;
            AnimationPlayer.Play(_fightFallbackClip, restart: true);
        }
        else
        {
            // No fight animation — use creative "shake/jiggle" effect
            // Play the idle/walk clip (frozen first frame) and shake the sprite
            _fightShakeActive = true;
            _fightShakeTimer = 0;
            _fightShakeBaseX = X;

            var fallback = _idleClip ?? _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;
            if (fallback != null)
                AnimationPlayer.Play(fallback);
        }
    }

    // FASE 7: Métodos de comportamento
    public bool HasSleepAnimation => _sleepClip != null;
    public bool HasHopAnimation => _hopClip != null;
    public bool HasSitAnimation => _sitClip != null;
    public bool HasLookUpAnimation => _lookUpClip != null;
    public bool HasLayAnimation => _layClip != null;
    public bool HasPoseAnimation => _poseClip != null;
    public bool HasNodAnimation => _nodClip != null;
    public bool HasShockAnimation => _shockClip != null;

    /// <summary>Verifica se tem pelo menos uma animação de comportamento extra.</summary>
    public bool HasBehaviorAnimations =>
        HasSleepAnimation || HasHopAnimation || HasSitAnimation ||
        HasLookUpAnimation || HasLayAnimation || HasPoseAnimation;

    /// <summary>Inicia animação de dormir (loop).</summary>
    public bool StartSleeping()
    {
        if (_sleepClip == null) return false;
        _isWalking = false;
        State = EntityState.Sleeping;
        VelocityX = 0;
        AnimationPlayer.Play(_sleepClip);
        return true;
    }

    /// <summary>Inicia animação de pulos (non-loop, volta para idle ao terminar).</summary>
    public bool StartHopping()
    {
        var clip = _hopClip;
        if (clip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(clip, restart: true);
        return true;
    }

    /// <summary>Inicia animação de sentar (loop).</summary>
    public bool StartSitting()
    {
        if (_sitClip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(_sitClip);
        return true;
    }

    /// <summary>Inicia animação de olhar para cima (non-loop).</summary>
    public bool StartLookingUp()
    {
        if (_lookUpClip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(_lookUpClip, restart: true);
        return true;
    }

    /// <summary>Inicia animação de deitar (loop).</summary>
    public bool StartLaying()
    {
        if (_layClip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(_layClip);
        return true;
    }

    /// <summary>Inicia animação de pose (non-loop).</summary>
    public bool StartPosing()
    {
        if (_poseClip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(_poseClip, restart: true);
        return true;
    }

    /// <summary>Inicia animação de aceno/concordar (non-loop, reação a clique).</summary>
    public bool StartNodding()
    {
        var clip = _nodClip;
        if (clip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(clip, restart: true);
        return true;
    }

    /// <summary>Inicia animação de choque/surpresa (non-loop, reação a clique).</summary>
    public bool StartShocked()
    {
        var clip = _shockClip;
        if (clip == null) return false;
        _isWalking = false;
        State = EntityState.SpecialIdle;
        VelocityX = 0;
        AnimationPlayer.Play(clip, restart: true);
        return true;
    }

    /// <summary>Inicia uma animação de reação aleatória (para clique do usuário).</summary>
    public bool StartRandomReaction(Random random)
    {
        var available = new List<Action>();
        if (HasHopAnimation) available.Add(() => StartHopping());
        if (HasNodAnimation) available.Add(() => StartNodding());
        if (HasShockAnimation) available.Add(() => StartShocked());
        if (HasPoseAnimation) available.Add(() => StartPosing());
        if (available.Count == 0) return false;
        available[random.Next(available.Count)]();
        return true;
    }

    /// <summary>Inicia um comportamento idle aleatório (para ociosidade prolongada).</summary>
    public bool StartRandomIdleBehavior(Random random)
    {
        var available = new List<Action>();
        if (HasSitAnimation) available.Add(() => StartSitting());
        if (HasLookUpAnimation) available.Add(() => StartLookingUp());
        if (HasLayAnimation) available.Add(() => StartLaying());
        if (HasSleepAnimation) available.Add(() => StartSleeping());
        if (available.Count == 0) return false;
        available[random.Next(available.Count)]();
        return true;
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);

        // Fight shake effect — oscillate X rapidly to simulate combat trembling
        if (_fightShakeActive && State == EntityState.Fighting)
        {
            _fightShakeTimer += deltaTime;
            X = _fightShakeBaseX + Math.Sin(_fightShakeTimer * FIGHT_SHAKE_FREQUENCY * 2 * Math.PI) * FIGHT_SHAKE_AMPLITUDE;
            return;
        }

        // Durante combate, sleeping ou special idle, não atualizar animação nem direção
        if (State == EntityState.Fighting || State == EntityState.Sleeping || State == EntityState.SpecialIdle)
            return;

        if (_isWalking)
            ApplyWalkClip();
    }

    private void ApplyWalkClip()
    {
        if (_useDirectionalWalk)
        {
            var target = FacingRight ? _walkRightClip : _walkLeftClip;
            if (target != null && AnimationPlayer.CurrentClip != target)
                AnimationPlayer.Play(target);
        }
        else if (_walkFallbackClip != null && AnimationPlayer.CurrentClip != _walkFallbackClip)
        {
            AnimationPlayer.Play(_walkFallbackClip);
        }
    }

    private AnimationClip? ResolveFightClip()
    {
        // Only return the actual fight clip — no idle/walk fallback
        return _fightFallbackClip;
    }
}
