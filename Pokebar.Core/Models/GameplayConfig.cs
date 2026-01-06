namespace Pokebar.Core.Models;

/// <summary>
/// Configurações de gameplay carregadas de JSON.
/// </summary>
public record GameplayConfig
{
    public PlayerConfig Player { get; init; } = new();
    public EnemyConfig Enemy { get; init; } = new();
    public SpawnConfig Spawn { get; init; } = new();
    public CombatConfig Combat { get; init; } = new();
    public CaptureConfig Capture { get; init; } = new();
    public AnimationConfig Animation { get; init; } = new();
    public SpriteConfig Sprite { get; init; } = new();
    public PerformanceConfig Performance { get; init; } = new();
}

public record PlayerConfig
{
    public int DefaultDex { get; init; } = 25;
    public double WalkSpeed { get; init; } = 50.0;
    public double RunSpeed { get; init; } = 80.0;
    public bool AllowMonitorTravel { get; init; } = true;
    public double TaskbarMargin { get; init; } = 0;
}

public record EnemyConfig
{
    public int DefaultDex { get; init; } = 19;
    public double WalkSpeed { get; init; } = 30.0;
    public int MaxSimultaneous { get; init; } = 3;
    public bool AllowMonitorTravel { get; init; } = true;
    public EnemySpawnWeight[] SpawnWeights { get; init; } = Array.Empty<EnemySpawnWeight>();
}

public record EnemySpawnWeight(int Dex, int Weight, string? Comment = null);

public record SpawnConfig
{
    public double OffsetFromEdge { get; init; } = 200.0;
    public double ChanceWhenReady { get; init; } = 0.7;
    public double DelayMinSeconds { get; init; } = 6.0;
    public double DelayMaxSeconds { get; init; } = 12.0;
}

public record CombatConfig
{
    public double CollisionTolerance { get; init; } = 20.0;
    public double FacingSpacing { get; init; } = 50.0;
    public double RoundDurationSeconds { get; init; } = 3.0;
    public int RoundsPerFight { get; init; } = 3;
    public double RetreatDistance { get; init; } = 200.0;
    public double CooldownSeconds { get; init; } = 1.5;
}

public record CaptureConfig
{
    public double BallSizePx { get; init; } = 20.0;
    public double ThrowSpeedMin { get; init; } = 200.0;
    public double ThrowSpeedMax { get; init; } = 600.0;
    public double Gravity { get; init; } = 500.0;
    public double CaptureDistance { get; init; } = 50.0;
    public double ShrinkDuration { get; init; } = 0.8;
    public double BaseSuccessRate { get; init; } = 0.5;
    public int StarterPokeballs { get; init; } = 10;
}

public record AnimationConfig
{
    public double DefaultFrameTimeSeconds { get; init; } = 0.1;
    public double WalkFrameTimeSeconds { get; init; } = 0.1;
    public double IdleFrameTimeSeconds { get; init; } = 0.15;
    public double SleepFrameTimeSeconds { get; init; } = 0.2;
    public double AttackFrameTimeSeconds { get; init; } = 0.08;
}

public record SpriteConfig
{
    public int WalkRowRight { get; init; } = 2;  // 0-based sprite sheet row for right walk
    public int WalkRowLeft { get; init; } = 6;   // 0-based sprite sheet row for left walk
}

public record PerformanceConfig
{
    public int TickRateMs { get; init; } = 16;
    public int FullscreenCheckMs { get; init; } = 500;
    public bool VsyncEnabled { get; init; } = true;
    public bool DebugOverlay { get; init; } = false;
}
