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
    public WindowsIntegrationConfig Windows { get; init; } = new();
    public MoodConfig Mood { get; init; } = new();
    public ShinyConfig Shiny { get; init; } = new();
    public QuestConfig Quests { get; init; } = new();
    public ModConfig Mods { get; init; } = new();
    public LevelConfig Level { get; init; } = new();
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
    public double BallSizePx { get; init; } = 32.0;
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
    public int IdleTickRateMs { get; init; } = 50;
    public int FullscreenCheckMs { get; init; } = 500;
    public bool VsyncEnabled { get; init; } = true;
    public bool DebugOverlay { get; init; } = false;
    public int SpriteCacheMaxEntries { get; init; } = 30;
}

/// <summary>
/// Configurações de integração com Windows (FASE 5).
/// </summary>
public record WindowsIntegrationConfig
{
    public FullscreenConfig Fullscreen { get; init; } = new();
    public HotkeyConfig Hotkeys { get; init; } = new();
    public bool TrayIconEnabled { get; init; } = true;
    public bool ToastNotificationsEnabled { get; init; } = true;
    public bool RespectReducedMotion { get; init; } = true;
    public bool RespectHighContrast { get; init; } = true;

    /// <summary>Silenciar notificações toast ao iniciar.</summary>
    public bool SilenceNotifications { get; init; }

    /// <summary>Bloquear spawns e combate ao iniciar.</summary>
    public bool BlockSpawns { get; init; }

    /// <summary>Modo minimalista — reduz movimento e efeitos visuais.</summary>
    public bool MinimalMode { get; init; }

    /// <summary>Hotkey para screenshot do pet. Formato: "Ctrl+Shift+S"</summary>
    public string ScreenshotHotkey { get; init; } = "Ctrl+Shift+S";

    /// <summary>Hotkey para abrir PC/Box. Formato: "Ctrl+Shift+B"</summary>
    public string PcBoxHotkey { get; init; } = "Ctrl+Shift+B";
}

public record FullscreenConfig
{
    /// <summary>
    /// Modo de comportamento fullscreen:
    /// "hide" = esconde em fullscreen (padrão)
    /// "show" = mostra sempre
    /// "whitelist" = mostra apenas nos apps da whitelist
    /// "blacklist" = esconde apenas nos apps da blacklist
    /// </summary>
    public string Mode { get; init; } = "hide";

    /// <summary>
    /// Nomes de processos em que o pet DEVE aparecer mesmo em fullscreen.
    /// Exemplo: ["discord", "spotify"]
    /// </summary>
    public string[] Whitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Nomes de processos em que o pet NÃO deve aparecer em fullscreen.
    /// Exemplo: ["game.exe", "vlc"]
    /// </summary>
    public string[] Blacklist { get; init; } = Array.Empty<string>();
}

public record HotkeyConfig
{
    /// <summary>Hotkey para pausar/retomar o pet. Formato: "Ctrl+Shift+P"</summary>
    public string PauseResume { get; init; } = "Ctrl+Shift+P";

    /// <summary>Hotkey para gerar zip de diagnóstico. Formato: "Ctrl+Shift+D"</summary>
    public string Diagnostic { get; init; } = "Ctrl+Shift+D";
}

/// <summary>
/// Configurações de humor/amizade (FASE 7).
/// </summary>
public record MoodConfig
{
    /// <summary>Ganho de amizade ao vencer uma batalha.</summary>
    public int FriendshipOnBattleWin { get; init; } = 3;

    /// <summary>Perda de amizade ao perder uma batalha.</summary>
    public int FriendshipOnBattleLoss { get; init; } = 1;

    /// <summary>Ganho de amizade ao capturar um Pokémon.</summary>
    public int FriendshipOnCapture { get; init; } = 5;

    /// <summary>Ganho de amizade ao acariciar (clicar).</summary>
    public int FriendshipOnPet { get; init; } = 2;

    /// <summary>Limite de amizade para mood Happy.</summary>
    public int HappyThreshold { get; init; } = 150;

    /// <summary>Limite de amizade para mood Sad (abaixo disso).</summary>
    public int SadThreshold { get; init; } = 30;

    /// <summary>Tempo mínimo idle (s) antes de ficar Sleepy.</summary>
    public double SleepyIdleSeconds { get; init; } = 120.0;

    /// <summary>Cooldown entre carícias (s) para evitar spam.</summary>
    public double PetCooldownSeconds { get; init; } = 3.0;
}

/// <summary>
/// Configurações do sistema shiny (FASE 7).
/// </summary>
public record ShinyConfig
{
    /// <summary>Chance de shiny ao spawnar (1 em N). Default: 1/512.</summary>
    public int ShinyChanceDenominator { get; init; } = 512;

    /// <summary>Se deve mostrar partículas de brilho no shiny.</summary>
    public bool ShowSparkleEffect { get; init; } = true;
}

/// <summary>
/// Configurações de missões rápidas (FASE 7).
/// </summary>
public record QuestConfig
{
    /// <summary>Máximo de missões ativas simultâneas.</summary>
    public int MaxActiveQuests { get; init; } = 3;

    /// <summary>Se missões estão habilitadas.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Intervalo em minutos para gerar nova missão.</summary>
    public double NewQuestIntervalMinutes { get; init; } = 30.0;
}

/// <summary>
/// Configurações de mods/packs (FASE 7).
/// </summary>
public record ModConfig
{
    /// <summary>Se mods estão habilitados.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Caminho da pasta de mods (relativo ao AppData/Pokebar).</summary>
    public string ModsFolder { get; init; } = "mods";

    /// <summary>Máximo de mods carregados simultâneamente.</summary>
    public int MaxLoadedMods { get; init; } = 5;
}

/// <summary>
/// Configurações do sistema de XP e nível.
/// </summary>
public record LevelConfig
{
    /// <summary>Nível máximo atingível.</summary>
    public int MaxLevel { get; init; } = 100;

    /// <summary>XP base necessário para subir do nível 1 para o 2.</summary>
    public int BaseXp { get; init; } = 100;

    /// <summary>Fator de crescimento exponencial da curva de XP. XP(n) = BaseXp * n^GrowthFactor.</summary>
    public double GrowthFactor { get; init; } = 1.5;

    /// <summary>XP ganho ao vencer uma batalha.</summary>
    public int XpPerBattleWin { get; init; } = 30;

    /// <summary>XP ganho ao perder uma batalha (participação).</summary>
    public int XpPerBattleLoss { get; init; } = 5;

    /// <summary>XP ganho ao capturar um Pokémon.</summary>
    public int XpPerCapture { get; init; } = 50;

    /// <summary>XP ganho ao completar uma quest.</summary>
    public int XpPerQuestComplete { get; init; } = 80;

    /// <summary>XP ganho por segundo de caminhada.</summary>
    public double XpPerWalkSecond { get; init; } = 0.5;

    /// <summary>XP ganho ao acariciar o pet (com cooldown do MoodService).</summary>
    public int XpPerPet { get; init; } = 2;

    /// <summary>Multiplicador de XP para capturas shiny.</summary>
    public double ShinyXpMultiplier { get; init; } = 3.0;
}
