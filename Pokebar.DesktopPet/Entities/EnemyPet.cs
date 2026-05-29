using System;
using Pokebar.Core.Models;

namespace Pokebar.DesktopPet.Entities;

public class EnemyPet : PokemonPet
{
    private readonly Random _random;
    private double _patrolTimer;
    private double _nextDirectionChange;
    private double _faintedTimer;
    private bool _patrolPaused;
    private double _patrolPauseDuration;
    private double _patrolPauseTimer;
    private bool _patrolBehaviorActive; // true quando está numa animação de comportamento durante pausa

    /// <summary>
    /// Tempo em segundos que um inimigo fainted fica antes de despawnar.
    /// O timer só corre quando não há captura em progresso.
    /// Com cooldown de 1.5s entre tentativas, 6s = ~4 tentativas antes de escapar sozinho.
    /// </summary>
    public const double FaintedDespawnSeconds = 6.0;

    public EnemyPet(int dex, string formId = "0000", int level = 1, int? maxHp = null, int? seed = null, bool isShiny = false, RarityTier rarity = RarityTier.Common) : base(dex, formId)
    {
        Level = Math.Max(1, level);
        MaxHp = maxHp ?? BuildStat(12, Level, dex);
        CurrentHp = MaxHp;
        Attack = BuildStat(6, Level, dex);
        Defense = BuildStat(6, Level, dex);
        PatrolSpeed = 30.0;
        IsShiny = isShiny;
        Rarity = rarity;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _nextDirectionChange = NextWalkSeconds();
    }

    public int Level { get; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Attack { get; }
    public int Defense { get; }
    public double PatrolSpeed { get; set; }
    public bool IsCaptureInProgress { get; internal set; }

    /// <summary>Se este Pokémon é shiny (FASE 7).</summary>
    public bool IsShiny { get; }

    /// <summary>Tier de raridade do Pokémon (afeta dificuldade de captura).</summary>
    public RarityTier Rarity { get; }

    /// <summary>Efeito de status ativo (persiste após combate para afetar captura).</summary>
    public StatusEffectType ActiveStatus { get; set; }

    /// <summary>Turnos restantes de sono (decrementa a cada rodada).</summary>
    public int SleepTurnsLeft { get; set; }

    /// <summary>Fração de HP restante (0.0–1.0). Usado na fórmula de captura.</summary>
    public double HpRatio => MaxHp > 0 ? (double)CurrentHp / MaxHp : 0;

    public bool IsCapturable => State == EntityState.Fainted;

    public override void Update(double deltaTime)
    {
        UpdatePatrol(deltaTime);
        UpdateBehaviorTimeout(deltaTime);
        UpdateFaintedDespawn(deltaTime);
        base.Update(deltaTime);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || State == EntityState.Dead || State == EntityState.Captured)
            return;

        CurrentHp = Math.Max(0, CurrentHp - damage);
        if (CurrentHp == 0)
        {
            State = EntityState.Fainted;
            VelocityX = 0;
            StartIdle(false);
        }
    }

    /// <summary>
    /// Força o estado Fainted mantendo o HP atual (para derrota por comparação de HP).
    /// </summary>
    public void Faint()
    {
        if (State == EntityState.Dead || State == EntityState.Captured)
            return;
        State = EntityState.Fainted;
        VelocityX = 0;
        StartIdle(false);
    }

    /// <summary>
    /// Aplica dano de combate interno (usado na simulação de rodadas).
    /// Não muda estado — isso é feito por Faint() ou TakeDamage() no final.
    /// </summary>
    public void SetHp(int hp)
    {
        CurrentHp = Math.Clamp(hp, 0, MaxHp);
    }

    public void MarkCaptured()
    {
        State = EntityState.Captured;
        VelocityX = 0;
        IsCaptureInProgress = false;
        StartIdle(false);
    }

    public void Despawn()
    {
        State = EntityState.Dead;
        VelocityX = 0;
    }

    public void BeginCapture()
    {
        if (State == EntityState.Fainted)
            IsCaptureInProgress = true;
    }

    private void UpdateFaintedDespawn(double deltaTime)
    {
        if (State != EntityState.Fainted || IsCaptureInProgress)
            return;

        _faintedTimer += deltaTime;
        if (_faintedTimer >= FaintedDespawnSeconds)
        {
            Despawn();
        }
    }

    private void UpdatePatrol(double deltaTime)
    {
        // Apenas Idle e Walking passam por aqui.
        // Sleeping/SpecialIdle são tratados exclusivamente em UpdateBehaviorTimeout.
        if (State != EntityState.Idle && State != EntityState.Walking)
            return;

        // ── Pausa simples (sem animação de comportamento ativa) ──
        if (_patrolPaused)
        {
            _patrolPauseTimer += deltaTime;

            if (_patrolPauseTimer >= _patrolPauseDuration)
            {
                _patrolPaused = false;
                var speedVariation = 0.8 + (_random.NextDouble() * 0.4);
                var dir = _random.Next(0, 2) == 0 ? -1.0 : 1.0;
                VelocityX = PatrolSpeed * dir * speedVariation;
                StartWalking();
                _patrolTimer = 0;
                _nextDirectionChange = NextWalkSeconds();
            }
            return;
        }

        // ── Walking ──
        _patrolTimer += deltaTime;
        if (_patrolTimer < _nextDirectionChange)
            return;

        // Hora de pausar
        _patrolPaused = true;
        _patrolPauseTimer = 0;
        _patrolBehaviorActive = false;
        VelocityX = 0;
        BeginPauseBehavior();
    }

    /// <summary>
    /// Gerencia o timeout de animações de comportamento (Sleeping, SpecialIdle).
    /// Separado de UpdatePatrol para que o guard simples (Idle/Walking) não precise
    /// ser relaxado — evita behaviors executando enquanto o pet caminha.
    /// </summary>
    private void UpdateBehaviorTimeout(double deltaTime)
    {
        if (!_patrolBehaviorActive || !_patrolPaused)
            return;

        _patrolPauseTimer += deltaTime;

        // Animação não-loop (SpecialIdle) terminou antes do timer → encerra
        if (State == EntityState.SpecialIdle && !AnimationPlayer.IsPlaying)
        {
            EndBehaviorAndResume();
            return;
        }

        // Duração máxima atingida → encerra comportamento e retoma patrulha
        if (_patrolPauseTimer >= _patrolPauseDuration)
        {
            EndBehaviorAndResume();
        }
    }

    private void EndBehaviorAndResume()
    {
        StartIdle();
        _patrolBehaviorActive = false;
        _patrolPaused = false;
        var speedVariation = 0.8 + (_random.NextDouble() * 0.4);
        var dir = _random.Next(0, 2) == 0 ? -1.0 : 1.0;
        VelocityX = PatrolSpeed * dir * speedVariation;
        StartWalking();
        _patrolTimer = 0;
        _nextDirectionChange = NextWalkSeconds();
    }

    /// <summary>
    /// Decide o que fazer durante a pausa de patrulha:
    /// ~35% de chance de usar uma animação de comportamento disponível.
    /// </summary>
    private void BeginPauseBehavior()
    {
        if (HasBehaviorAnimations && _random.NextDouble() < 0.35)
        {
            // Dormir é mais raro e dura mais
            if (HasSleepAnimation && _random.NextDouble() < 0.25)
            {
                if (StartSleeping())
                {
                    _patrolBehaviorActive = true;
                    _patrolPauseDuration = 6.0 + _random.NextDouble() * 10.0; // 6–16 s
                    return;
                }
            }

            // Qualquer outro comportamento disponível (sentar, deitar, olhar pra cima…)
            if (StartRandomIdleBehavior(_random))
            {
                _patrolBehaviorActive = true;
                _patrolPauseDuration = 2.0 + _random.NextDouble() * 4.0; // 2–6 s
                return;
            }
        }

        // Padrão: idle normal curto
        StartIdle();
        _patrolPauseDuration = 1.0 + _random.NextDouble() * 2.5;
    }

    private double NextWalkSeconds()
    {
        return 3 + (_random.NextDouble() * 5); // 3-8s walk
    }

    private static int BuildStat(int baseValue, int level, int dex)
    {
        return Math.Max(1, baseValue + level + (dex % 5));
    }
}
