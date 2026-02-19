using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pokebar.Core.Localization;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.DesktopPet.Animation;
using Pokebar.DesktopPet.Capture;
using Pokebar.DesktopPet.Combat;
using Pokebar.DesktopPet.Entities;
using Pokebar.DesktopPet.Interop;
using Pokebar.DesktopPet.Services;
using Serilog;

namespace Pokebar.DesktopPet;

public partial class MainWindow : Window
{
    private readonly PlayerPet _pokemon;
    private readonly SpriteLoader _spriteLoader;
    private readonly SpriteCache _spriteCache;
    private readonly EntityManager _entityManager;
    private readonly CombatManager _combatManager;
    private readonly CaptureManager _captureManager;
    private readonly Random _random = new();
    private readonly Dictionary<EnemyPet, PetWindow> _enemyWindows = new();
    private readonly Dictionary<EnemyPet, TaskbarService.TaskbarInfo> _enemyTaskbars = new();
    private GameplayConfig _config;
    private AppSettings _appSettings;
    private SaveData _saveData;
    private double _autoSaveTimer;
    private const double AUTO_SAVE_INTERVAL = 60.0;
    private double _spawnTimer;
    private double _nextSpawnDelay;
    private bool _allowEnemyMonitorTravel = true;
    private readonly bool _debugOverlayEnabled;
    private double _fpsSmooth;
    private readonly List<TaskbarService.TaskbarInfo> _taskbars = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastTimestamp;
    private TaskbarService.TaskbarInfo? _currentTaskbar;
    private HashSet<IntPtr> _fullscreenMonitors = new();
    private DateTime _lastFullscreenCheck;
    private double _currentGroundLineY;
    private IntPtr _windowHwnd;

    // FASE 5: Serviços de integração Windows
    private TrayIconService? _trayIcon;
    private NotificationService? _notificationService;
    private HotkeyService? _hotkeyService;
    private SystemPreferencesService? _systemPrefs;
    private bool _isPaused;

    // FASE 6: UX & Produto
    private AchievementService? _achievementService;
    private bool _silenceNotifications;
    private bool _blockSpawns;

    // FASE 7: Conteúdo & Gameplay
    private MoodService? _moodService;
    private IdleBehaviorService? _idleBehaviorService;
    private SmartMovementService? _smartMovement;
    private QuestService? _questService;
    private ModLoader? _modLoader;
    private LevelService? _levelService;
    private EvolutionService? _evolutionService;
    private EnemySpawnWeight[]? _dynamicSpawnPool;
    private bool _forceRareSpawn;

    // Chase-target: when the player clicks an enemy, the pet walks toward it
    private EnemyPet? _chaseTarget;
    private const double CHASE_ARRIVE_DISTANCE = 40.0;  // px — close enough to trigger combat
    private const double CHASE_SPEED_MULTIPLIER = 1.35;  // run slightly faster when chasing

    // Click-to-stop: idle timer after clicking the pet
    private double _clickIdleTimer;
    private const double CLICK_IDLE_DURATION = 3.0;  // seconds the pet stays put after click

    private const double TASKBAR_MARGIN = 0;
    private readonly bool _allowMonitorTravel;

    public MainWindow()
    {
        InitializeComponent();

        Log.Debug("MainWindow constructor started");

        // Carregar configurações via perfil ativo
        _appSettings = ProfileManager.LoadSettings();
        _config = ProfileManager.LoadActiveProfile(_appSettings);
        Log.Information("Profile: {ProfileId}. Player Dex: {PlayerDex}, Enemy Max: {MaxEnemies}", 
            _appSettings.ActiveProfileId, _config.Player.DefaultDex, _config.Enemy.MaxSimultaneous);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;

        var offsetsPath = ResolveOffsetsPath();
        var spritesPath = ResolveSpritesPath();
        Log.Debug("Sprite paths - Offsets: {OffsetsPath}, Sprites: {SpritesPath}", offsetsPath, spritesPath);
        _spriteLoader = new SpriteLoader(offsetsPath, spritesPath);
        _spriteCache = new SpriteCache(_spriteLoader, _config.Performance.SpriteCacheMaxEntries);

        _allowMonitorTravel = _config.Player.AllowMonitorTravel;
        _allowEnemyMonitorTravel = _config.Enemy.AllowMonitorTravel;
        _debugOverlayEnabled = _config.Performance.DebugOverlay;

        // Carregar save ou criar com defaults
        var loaded = SaveManager.Load();
        if (loaded != null)
        {
            _saveData = loaded;
            Log.Information("Save loaded: Dex={ActiveDex}, Pokeballs={Pokeballs}, Party={PartyCount}, Captured={Captured}", 
                _saveData.ActiveDex, _saveData.Pokeballs, _saveData.Party.Count, _saveData.Stats.TotalCaptured);
        }
        else
        {
            _saveData = new SaveData
            {
                ActiveDex = _config.Player.DefaultDex,
                Pokeballs = _config.Capture.StarterPokeballs,
                Party = new System.Collections.Generic.List<int> { _config.Player.DefaultDex }
            };
            Log.Information("No save found, starting fresh. Dex={Dex}, Pokeballs={Pokeballs}", 
                _saveData.ActiveDex, _saveData.Pokeballs);
        }

        _pokemon = new PlayerPet(_saveData.ActiveDex, _saveData.Pokeballs);
        _pokemon.RestoreFromSave(_saveData);
        _pokemon.AnimationPlayer.FrameChanged += OnFrameChanged;
        var playerAnims = _spriteCache.GetAnimations(_pokemon.Dex, _pokemon.FormId, _config);
        _pokemon.ApplyAnimations(playerAnims);
        _spriteCache.Pin(_pokemon.UniqueId);
        Log.Information("Player pet initialized. Dex: {Dex}, Pokeballs: {Pokeballs}, Party: {PartyCount}", 
            _pokemon.Dex, _pokemon.Pokeballs, _pokemon.Party.Count);
        _entityManager = new EntityManager();
        _entityManager.Add(_pokemon);
        _combatManager = new CombatManager(
            collisionTolerance: _config.Combat.CollisionTolerance,
            spacing: _config.Combat.FacingSpacing,
            roundDuration: _config.Combat.RoundDurationSeconds,
            rounds: _config.Combat.RoundsPerFight,
            retreatDistance: _config.Combat.RetreatDistance,
            cooldownDuration: _config.Combat.CooldownSeconds);
        _captureManager = new CaptureManager(_config);
        _captureManager.CaptureCompleted += OnCaptureCompleted;
        _captureManager.CaptureFailed += OnCaptureFailed;
        _combatManager.BattleEnded += OnBattleEnded;
        _nextSpawnDelay = NextSpawnDelay();

        _lastTimestamp = _stopwatch.ElapsedTicks;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _windowHwnd = helper.Handle;
        WindowHelper.MakeTransparentWindow(this);
        // Não usar SetClickThrough — usamos WM_NCHITTEST para permitir cliques no sprite
        // mas deixar o resto passar (ver HitTestHook)
        var source = HwndSource.FromHwnd(_windowHwnd);
        source?.AddHook(HitTestHook);

        // FASE 5: Inicializar serviços de integração Windows
        InitializeWindowsIntegration();
    }

    /// <summary>
    /// Hook Win32 para permitir cliques no sprite do pet mas passar clicks na área transparente.
    /// Retorna HTTRANSPARENT (-1) quando o mouse está fora do sprite,
    /// permitindo que o click passe para a janela abaixo.
    /// </summary>
    private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;
        const int HTCLIENT = 1;

        if (msg == WM_NCHITTEST)
        {
            // Extrair coordenadas do mouse (screen pixels)
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            // Converter para coordenadas da janela WPF
            var point = PointFromScreen(new System.Windows.Point(x, y));

            // Verificar se está dentro da imagem do sprite (com pixels não-transparentes)
            if (IsPointOnSprite(point))
            {
                handled = true;
                return new IntPtr(HTCLIENT);
            }

            // Fora do sprite → transparente, clique passa
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }

        return IntPtr.Zero;
    }

    /// <summary>Verifica se um ponto (coordenadas da janela WPF) está sobre um pixel não-transparente do sprite.</summary>
    private bool IsPointOnSprite(System.Windows.Point point)
    {
        // Verificar bounds da imagem
        var imgLeft = Canvas.GetLeft(PokemonImage);
        var imgTop = Canvas.GetTop(PokemonImage);
        if (double.IsNaN(imgLeft)) imgLeft = 0;
        if (double.IsNaN(imgTop)) imgTop = 0;
        var imgWidth = PokemonImage.ActualWidth;
        var imgHeight = PokemonImage.ActualHeight;

        if (imgWidth <= 0 || imgHeight <= 0)
            return false;

        // Verificar se o ponto está dentro do bounding box da imagem
        if (point.X < imgLeft || point.X > imgLeft + imgWidth ||
            point.Y < imgTop || point.Y > imgTop + imgHeight)
            return false;

        // Está dentro do bounding box do sprite — considerar como hit
        // (Não precisa checar pixel alpha pois os sprites já são recortados)
        return true;
    }

    private void InitializeWindowsIntegration()
    {
        var winConfig = _config.Windows;

        // System preferences (high contrast, reduced motion)
        _systemPrefs = new SystemPreferencesService();
        _systemPrefs.PreferencesChanged += OnSystemPreferencesChanged;

        // Tray icon
        if (winConfig.TrayIconEnabled)
        {
            _trayIcon = new TrayIconService();
            _trayIcon.SetPokeballCount(_pokemon.Pokeballs);
            UpdateTrayTooltip();
            _trayIcon.PauseResumeRequested += TogglePause;
            _trayIcon.DiagnosticRequested += OnDiagnosticRequested;
            _trayIcon.QuitRequested += () => Dispatcher.Invoke(Close);

            // FASE 6: Novos itens do tray
            _trayIcon.PcBoxRequested += () => Dispatcher.Invoke(OnPcBoxRequested);
            _trayIcon.SettingsRequested += () => Dispatcher.Invoke(OnSettingsRequested);
            _trayIcon.ScreenshotRequested += () => Dispatcher.Invoke(OnScreenshotRequested);
            _trayIcon.SilenceNotificationsToggled += () => Dispatcher.Invoke(OnSilenceNotificationsToggled);
            _trayIcon.BlockSpawnsToggled += () => Dispatcher.Invoke(OnBlockSpawnsToggled);
            _trayIcon.UseStoneRequested += stoneVal => Dispatcher.Invoke(() => OnUseStoneRequested(stoneVal));
            
            // FASE 7: Acariciar via tray
            _trayIcon.PetRequested += () => Dispatcher.Invoke(OnPetClicked);
        }

        // Notification service
        _notificationService = new NotificationService(_trayIcon);
        _notificationService.Enabled = winConfig.ToastNotificationsEnabled;

        // Global hotkeys (FASE 6: extra hotkeys para screenshot e PC box)
        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize(_windowHwnd, winConfig.Hotkeys, winConfig.ScreenshotHotkey, winConfig.PcBoxHotkey);
        _hotkeyService.PauseResumePressed += TogglePause;
        _hotkeyService.DiagnosticPressed += OnDiagnosticRequested;
        _hotkeyService.ScreenshotPressed += () => Dispatcher.Invoke(OnScreenshotRequested);
        _hotkeyService.PcBoxPressed += () => Dispatcher.Invoke(OnPcBoxRequested);

        // FASE 6: Achievement service
        _achievementService = new AchievementService(_saveData.Achievements);
        _achievementService.AchievementUnlocked += a =>
        {
            Dispatcher.Invoke(() =>
            {
                var title = Localizer.Get(a.TitleKey);
                _notificationService?.Notify(
                    Localizer.Get("toast.achievement.title"),
                    Localizer.Get("achievement.unlocked", title));
            });
        };

        // FASE 6: DND inicial — restaurar do save (prioridade) ou config
        _silenceNotifications = _saveData.SilenceNotifications || winConfig.SilenceNotifications;
        _blockSpawns = _saveData.BlockSpawns || winConfig.BlockSpawns;
        _trayIcon?.SetSilenceNotifications(_silenceNotifications);
        _trayIcon?.SetBlockSpawns(_blockSpawns);
        if (_notificationService != null)
            _notificationService.Enabled = !_silenceNotifications && winConfig.ToastNotificationsEnabled;

        // FASE 7: Inicializar serviços de conteúdo & gameplay
        _moodService = new MoodService(_config.Mood);
        _idleBehaviorService = new IdleBehaviorService();
        _smartMovement = new SmartMovementService();
        _questService = new QuestService(_config.Quests);
        _questService.RestoreFromSave(_saveData);
        _questService.EnsureInitialQuests();
        _questService.QuestCompleted += (quest, progress) =>
        {
            Dispatcher.Invoke(() =>
            {
                var title = Localizer.Get(quest.TitleKey);
                _notificationService?.Notify(
                    Localizer.Get("toast.quest.title"),
                    Localizer.Get("toast.quest.completed", title));

                // Auto-claim reward
                var claimed = _questService?.ClaimReward(quest.Id);
                if (claimed != null)
                {
                    ApplyQuestReward(claimed);
                }

                // XP por quest completada
                _levelService?.OnQuestCompleted();
            });
        };

        _modLoader = new ModLoader(_config.Mods);
        _modLoader.EnsureModsFolder();
        _modLoader.LoadMods(_saveData.EnabledMods);
        _spriteLoader.SetModPathResolver(_modLoader.GetModSpritePath);

        // Sistema de evolução
        _evolutionService = new EvolutionService();
        var evoDataPath = ResolveAssetPath("evolution_data.json");
        if (evoDataPath != null)
            _evolutionService.LoadFromFile(evoDataPath);

        // Sistema de XP e nível
        _levelService = new LevelService(_config.Level);
        _levelService.RestoreFromSave(_saveData);
        _levelService.LevelUp += newLevel =>
        {
            Dispatcher.Invoke(() =>
            {
                _pokemon.Level = newLevel;
                _notificationService?.Notify(
                    Localizer.Get("toast.levelup.title"),
                    Localizer.Get("toast.levelup.message", newLevel.ToString()));
                UpdateTrayTooltip();
                PerformSave("level_up");
                CheckEvolution();
            });
        };
        UpdateTrayTooltip();
        UpdateStonesMenu();

        // FASE 7: AnimationFinished handler — retornar ao idle/walking após animações one-shot
        _pokemon.AnimationPlayer.AnimationFinished += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_pokemon.State == EntityState.SpecialIdle)
                {
                    _idleBehaviorService?.Interrupt(_pokemon);
                    _pokemon.StartIdle();
                }
            });
        };

        Log.Information("Windows integration initialized. Tray={Tray}, Toast={Toast}, Hotkeys=yes, SysPrefs=yes, Silence={Silence}, BlockSpawns={Block}",
            winConfig.TrayIconEnabled, winConfig.ToastNotificationsEnabled, _silenceNotifications, _blockSpawns);
    }

    private void TogglePause()
    {
        Dispatcher.Invoke(() =>
        {
            _isPaused = !_isPaused;
            _trayIcon?.SetPaused(_isPaused);
            _notificationService?.NotifyPauseState(_isPaused);
            Log.Information("Pet {State}", _isPaused ? "paused" : "resumed");
        });
    }

    private void OnDiagnosticRequested()
    {
        Dispatcher.Invoke(() =>
        {
            var path = DiagnosticService.GenerateDiagnosticZip();
            Log.Information("Diagnostic zip generated: {Path}", path);
        });
    }

    private void OnSystemPreferencesChanged()
    {
        Dispatcher.Invoke(() =>
        {
            if (_systemPrefs == null) return;

            if (_config.Windows.RespectReducedMotion && _systemPrefs.ReducedMotion)
            {
                Log.Information("Reduced motion detected — throttling animations");
            }

            if (_config.Windows.RespectHighContrast && _systemPrefs.HighContrast)
            {
                Log.Information("High contrast detected — throttling animations");
            }
        });
    }

    // ── FASE 6: Handlers de UX ──────────────────────────────────────────

    private void OnPcBoxRequested()
    {
        if (_combatManager.IsActive || _captureManager.IsActive)
        {
            Log.Debug("PcBox blocked: combat/capture active");
            return;
        }

        var existingPcBox = System.Windows.Application.Current.Windows
            .OfType<PcBoxWindow>()
            .FirstOrDefault(w => w.IsVisible);
        if (existingPcBox != null)
        {
            existingPcBox.Activate();
            return;
        }

        var pcBox = new PcBoxWindow(
            _pokemon.Party,
            _pokemon.Dex,
            _spriteCache,
            _config,
            _pokemon.Pokeballs,
            _isPaused,
            _silenceNotifications,
            _blockSpawns);
        pcBox.Owner = null; // Transparent window can't be owner
        pcBox.PetRequested += OnPetClicked;
        pcBox.PauseResumeRequested += TogglePause;
        pcBox.DiagnosticRequested += OnDiagnosticRequested;
        pcBox.SettingsRequested += OnSettingsRequested;
        pcBox.ScreenshotRequested += OnScreenshotRequested;
        pcBox.SilenceNotificationsToggled += OnSilenceNotificationsToggled;
        pcBox.BlockSpawnsToggled += OnBlockSpawnsToggled;
        pcBox.QuitRequested += () => Dispatcher.Invoke(Close);

        if (pcBox.ShowDialog() == true && pcBox.ChosenDex > 0 && pcBox.ChosenDex != _pokemon.Dex)
        {
            _pokemon.ChangePokemon(pcBox.ChosenDex, _spriteCache, _config);
            _dynamicSpawnPool = null; // Force spawn pool regeneration for new dex
            _saveData = _saveData with { ActiveDex = _pokemon.Dex, ActiveFormId = _pokemon.FormId, EvolutionCancelled = false };
            PerformSave("pcbox");
            UpdateTrayTooltip();
            UpdateStonesMenu();
            _notificationService?.Notify("Pokebar", Localizer.Get("pcbox.changed", _pokemon.Dex.ToString()));
            Log.Information("PC Box: switched to Dex={Dex}", _pokemon.Dex);
        }
    }

    private void OnSettingsRequested()
    {
        var allAchievements = _achievementService?.AllAchievements.ToList() ?? new List<Achievement>();
        var settings = new SettingsWindow(_config, _appSettings, _saveData, _spriteCache, _pokemon.Dex, allAchievements);
        if (settings.ShowDialog() == true)
        {
            if (settings.EditedConfig != null)
            {
                var targetProfileId = settings.EditedSettings?.ActiveProfileId ?? _appSettings.ActiveProfileId;
                ProfileManager.SaveProfile(targetProfileId, settings.EditedConfig);
                _config = settings.EditedConfig;
                ApplyConfigChanges();
                Log.Information("Settings: config saved and applied in-memory");
            }
            if (settings.EditedSettings != null)
            {
                ProfileManager.SaveSettings(settings.EditedSettings);
                _appSettings = settings.EditedSettings;
                Log.Information("Settings: app settings saved");
            }

            _notificationService?.Notify("Pokebar", Localizer.Get("settings.saved"));

            if (settings.NeedsRestart)
            {
                _notificationService?.Notify("Pokebar", Localizer.Get("settings.restart"));
                Log.Information("Settings: restart recommended");
            }
        }
    }

    /// <summary>
    /// Aplica mudanças de config em memória sem precisar reiniciar.
    /// </summary>
    private void ApplyConfigChanges()
    {
        // Atualizar flags de silenciar/bloquear
        _silenceNotifications = _config.Windows.SilenceNotifications;
        _blockSpawns = _config.Windows.BlockSpawns;
        _trayIcon?.SetSilenceNotifications(_silenceNotifications);
        _trayIcon?.SetBlockSpawns(_blockSpawns);

        // Atualizar notificações: desliga se silenciado ou se desabilitado nas settings
        if (_notificationService != null)
            _notificationService.Enabled = _config.Windows.ToastNotificationsEnabled && !_silenceNotifications;

        // Atualizar velocidade do player
        _pokemon.VelocityX = _pokemon.VelocityX >= 0
            ? _config.Player.WalkSpeed
            : -_config.Player.WalkSpeed;

        Log.Debug("Config changes applied in-memory: Silence={Silence}, BlockSpawns={Block}, Toast={Toast}, Speed={Speed}",
            _silenceNotifications, _blockSpawns, _config.Windows.ToastNotificationsEnabled, _config.Player.WalkSpeed);
    }

    private void OnScreenshotRequested()
    {
        var frame = PokemonImage.Source as BitmapSource;
        var path = ScreenshotService.CaptureToClipboard(frame, _pokemon.Dex);
        if (path != null)
        {
            _notificationService?.Notify(
                Localizer.Get("toast.screenshot.title"),
                Localizer.Get("toast.screenshot.saved"));
        }
    }

    private void OnSilenceNotificationsToggled()
    {
        _silenceNotifications = !_silenceNotifications;
        _trayIcon?.SetSilenceNotifications(_silenceNotifications);

        if (_notificationService != null)
            _notificationService.Enabled = !_silenceNotifications && _config.Windows.ToastNotificationsEnabled;

        var msg = _silenceNotifications ? Localizer.Get("toast.silence.on") : Localizer.Get("toast.silence.off");
        _trayIcon?.ShowNotification("Pokebar", msg);
        Log.Information("Silence Notifications: {State}", _silenceNotifications ? "ON" : "OFF");

        _saveData = _saveData with { SilenceNotifications = _silenceNotifications };
        PerformSave("silence");
    }

    private void OnBlockSpawnsToggled()
    {
        _blockSpawns = !_blockSpawns;
        _trayIcon?.SetBlockSpawns(_blockSpawns);

        var msg = _blockSpawns ? Localizer.Get("toast.blockspawns.on") : Localizer.Get("toast.blockspawns.off");
        _trayIcon?.ShowNotification("Pokebar", msg);
        Log.Information("Block Spawns: {State}", _blockSpawns ? "ON" : "OFF");

        _saveData = _saveData with { BlockSpawns = _blockSpawns };
        PerformSave("blockspawns");
    }

    private void CheckAchievements()
    {
        if (_achievementService == null) return;
        var newlyUnlocked = _achievementService.CheckAndUnlock(
            _saveData.Stats, _pokemon.Party.Count, _pokemon.Pokeballs);
        if (newlyUnlocked.Count > 0)
        {
            _saveData = _saveData with { Achievements = _achievementService.GetUnlockedIds() };
            PerformSave("achievement");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Log.Information("MainWindow closing");
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        PerformSave("shutdown");
        base.OnClosed(e);
        foreach (var window in _enemyWindows.Values)
        {
            window.Close();
        }
        _enemyWindows.Clear();
        _enemyTaskbars.Clear();

        _captureManager.Shutdown();

        // FASE 5: Limpar serviços de integração Windows
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _systemPrefs?.Dispose();

        Log.Debug("MainWindow closed, enemy windows cleared");
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Log.Debug("MainWindow loaded event");
        InitializeTaskbars();

        if (_currentTaskbar != null)
        {
            _pokemon.X = _currentTaskbar.BoundsPx.Left + (_currentTaskbar.BoundsPx.Width / 2);
            _pokemon.Y = _currentTaskbar.GroundYPx - TASKBAR_MARGIN;
            Log.Debug("Player spawned at X: {X:F0}, Y: {Y:F0} on monitor {MonitorIndex}", 
                _pokemon.X, _pokemon.Y, _currentTaskbar.MonitorIndex);
        }

        _pokemon.VelocityX = _config.Player.WalkSpeed;
        _pokemon.StartWalking();

        // FASE 6: Onboarding na primeira execução
        if (!_saveData.OnboardingCompleted)
        {
            try
            {
                var onboarding = new OnboardingWindow(_spriteCache, _config);
                onboarding.ShowDialog();
                _saveData = _saveData with { OnboardingCompleted = true };
                PerformSave("onboarding");
                Log.Information("Onboarding completed");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Onboarding failed, skipping");
                _saveData = _saveData with { OnboardingCompleted = true };
            }
        }

        CompositionTarget.Rendering += OnCompositionTargetRendering;
        Log.Information("Game loop started via CompositionTarget.Rendering");
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        var now = _stopwatch.ElapsedTicks;
        var deltaTime = (now - _lastTimestamp) / (double)Stopwatch.Frequency;

        // Throttle: pular frames se o intervalo mínimo não passou
        var minInterval = GetCurrentTickInterval();
        if (deltaTime < minInterval)
            return;

        _lastTimestamp = now;
        // Clamp para evitar teleporte em spikes de lag
        deltaTime = Math.Min(deltaTime, 0.1);

        // FASE 5: Pausar game loop quando solicitado
        if (_isPaused)
        {
            // Mesmo pausado, mantém topmost e acumula playtime
            WindowHelper.EnsureTopmost(_windowHwnd);
            return;
        }

        // Garante que a janela fique sempre no topo (corrige problema ao clicar na taskbar)
        WindowHelper.EnsureTopmost(_windowHwnd);

        _entityManager.Update(deltaTime);

        UpdateFullscreenMonitors();
        UpdateTaskbarTravel();
        UpdateEnemyMovement();
        if (!_blockSpawns)
        {
            UpdateSpawn(deltaTime);
            _combatManager.Update(deltaTime, _pokemon, _entityManager.Enemies);
        }
        UpdateCapture(deltaTime);
        CleanupDeadEnemies();
        UpdateAutoHideVisibility();
        UpdateWindowPosition();
        UpdateEnemyWindowPosition();
        UpdateAutoSave(deltaTime);

        // FASE 7: Atualizar humor, comportamento idle e movimento inteligente
        UpdateMoodAndBehavior(deltaTime);
        _questService?.Update(deltaTime);

        if (_debugOverlayEnabled && deltaTime > 0)
        {
            var fps = 1.0 / deltaTime;
            _fpsSmooth = _fpsSmooth <= 0 ? fps : (_fpsSmooth * 0.9) + (fps * 0.1);
            UpdatePlayerDebugPanel();
            UpdateEnemyDebugPanels();
        }
    }

    private double GetCurrentTickInterval()
    {
        var hasActivity = _combatManager.IsActive
                          || _captureManager.IsActive
                          || GetEnemyCount() > 0
                          || _pokemon.State == EntityState.Walking;

        var targetMs = hasActivity
            ? _config.Performance.TickRateMs
            : _config.Performance.IdleTickRateMs;

        // FASE 5: Se reduced motion está ativo, dobrar o intervalo para reduzir animações
        if (_config.Windows.RespectReducedMotion && _systemPrefs?.ReducedMotion == true)
            targetMs = Math.Max(targetMs * 2, _config.Performance.IdleTickRateMs);

        // FASE 7: RespectHighContrast — throttle when system high-contrast is active
        if (_config.Windows.RespectHighContrast && _systemPrefs?.HighContrast == true)
            targetMs = Math.Max(targetMs * 2, _config.Performance.IdleTickRateMs);

        // FASE 7: MinimalMode — always throttle animations when enabled
        if (_config.Windows.MinimalMode)
            targetMs = Math.Max(targetMs * 2, _config.Performance.IdleTickRateMs);

        // FASE 7: VsyncEnabled — when disabled, skip idle throttling (always use active tick rate)
        if (!_config.Performance.VsyncEnabled)
            targetMs = _config.Performance.TickRateMs;

        return targetMs / 1000.0;
    }

    private void OnFrameChanged(BitmapSource frame, double groundLineY)
    {
        PokemonImage.Source = frame;
        _currentGroundLineY = groundLineY;

        var scaleX = _pokemon.ShouldFlip ? (_pokemon.FacingRight ? 1 : -1) : 1;
        FlipTransform.ScaleX = scaleX;

        if (RootCanvas.Width != frame.PixelWidth || RootCanvas.Height != frame.PixelHeight)
        {
            RootCanvas.Width = frame.PixelWidth;
            RootCanvas.Height = frame.PixelHeight;
        }

        if (_debugOverlayEnabled)
        {
            DebugPanel.Visibility = Visibility.Visible;
        }
    }

    private void InitializeTaskbars()
    {
        _taskbars.Clear();
        _taskbars.AddRange(TaskbarService.GetAllTaskbars());
        _currentTaskbar = null;

        foreach (var taskbar in _taskbars)
        {
            if (taskbar.IsPrimary)
            {
                _currentTaskbar = taskbar;
                break;
            }
        }

        if (_currentTaskbar == null && _taskbars.Count > 0)
            _currentTaskbar = _taskbars[0];

        if (_currentTaskbar == null)
        {
            _currentTaskbar = TaskbarService.GetPrimaryTaskbar();
            _taskbars.Clear();
            _taskbars.Add(_currentTaskbar);
        }

        // Log multi-monitor layout for debugging
        Log.Information("Taskbar layout: {Count} monitors found", _taskbars.Count);
        for (int i = 0; i < _taskbars.Count; i++)
        {
            var tb = _taskbars[i];
            Log.Information("  Monitor {Index}: BoundsPx=[{Left},{Right}] Primary={Primary} Handle={Handle:X}",
                i, tb.BoundsPx.Left, tb.BoundsPx.Right, tb.IsPrimary, tb.MonitorHandle.ToInt64());
        }
    }

    private void UpdateTaskbarTravel()
    {
        if (_currentTaskbar == null || _taskbars.Count == 0)
            return;
        if (_pokemon.State == EntityState.Fighting)
            return;

        var halfWidth = GetHalfWidthPx(_pokemon, _currentTaskbar);
        var minX = _currentTaskbar.BoundsPx.Left + halfWidth;
        var maxX = _currentTaskbar.BoundsPx.Right - halfWidth;
        var movingRight = _pokemon.VelocityX >= 0;
        var target = FindTaskbarForX(_pokemon.X);

        if (!_allowMonitorTravel)
        {
            ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            return;
        }

        // Pet is in the pixel gap between monitors — bridge to the neighbor
        if (target == null)
        {
            var neighbor = movingRight
                ? GetNeighbor(_currentTaskbar, true)
                : GetNeighbor(_currentTaskbar, false);

            if (neighbor != null && !IsMonitorBlocked(neighbor))
                SetCurrentTaskbar(neighbor);
            else
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            return;
        }

        // Pet entered a different monitor's taskbar (overlapping bounds)
        if (target != _currentTaskbar)
        {
            if (IsMonitorBlocked(target))
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            else
                SetCurrentTaskbar(target);
            return;
        }

        // Pet reached the RIGHT edge — try to bridge to next monitor
        if (movingRight && _pokemon.X >= maxX)
        {
            var next = GetNeighbor(_currentTaskbar, true);
            if (next == null || IsMonitorBlocked(next))
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            else
                SetCurrentTaskbar(next);  // proactively adopt neighbor
            return;
        }

        // Pet reached the LEFT edge — try to bridge to previous monitor
        if (!movingRight && _pokemon.X <= minX)
        {
            var prev = GetNeighbor(_currentTaskbar, false);
            if (prev == null || IsMonitorBlocked(prev))
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            else
                SetCurrentTaskbar(prev);  // proactively adopt neighbor
        }
    }

    private void UpdateFullscreenMonitors()
    {
        if (_taskbars.Count == 0)
            return;

        var now = DateTime.Now;
        if ((now - _lastFullscreenCheck).TotalMilliseconds < _config.Performance.FullscreenCheckMs)
            return;

        _lastFullscreenCheck = now;
        var ignoreWindows = new List<IntPtr> { _windowHwnd };
        foreach (var window in _enemyWindows.Values)
        {
            var enemyHandle = new WindowInteropHelper(window).Handle;
            if (enemyHandle != IntPtr.Zero)
                ignoreWindows.Add(enemyHandle);
        }

        foreach (var taskbar in _taskbars)
        {
            if (taskbar.Hwnd != IntPtr.Zero)
                ignoreWindows.Add(taskbar.Hwnd);
        }

        var prev = _fullscreenMonitors;
        _fullscreenMonitors = FullscreenService.GetFullscreenMonitors(ignoreWindows, _config.Windows.Fullscreen);

        // Log changes to fullscreen state
        if (prev.Count != _fullscreenMonitors.Count || !prev.SetEquals(_fullscreenMonitors))
        {
            Log.Information("Fullscreen monitors changed: {Count} blocked ({Handles})",
                _fullscreenMonitors.Count,
                string.Join(", ", _fullscreenMonitors.Select(h => h.ToString("X"))));
        }
    }

    private bool IsMonitorBlocked(TaskbarService.TaskbarInfo target)
    {
        if (target.MonitorHandle == IntPtr.Zero)
            return false;

        return _fullscreenMonitors.Contains(target.MonitorHandle);
    }

    private void SetCurrentTaskbar(TaskbarService.TaskbarInfo taskbar)
    {
        if (_currentTaskbar != taskbar)
        {
            Log.Debug("Pet adopted taskbar {Index} (monitor handle {Handle:X})",
                taskbar.MonitorIndex, taskbar.MonitorHandle.ToInt64());
        }
        _currentTaskbar = taskbar;
        _pokemon.Y = taskbar.GroundYPx - TASKBAR_MARGIN;
    }

    private TaskbarService.TaskbarInfo? FindTaskbarForX(double x)
    {
        foreach (var taskbar in _taskbars)
        {
            if (x >= taskbar.BoundsPx.Left && x <= taskbar.BoundsPx.Right)
                return taskbar;
        }

        return null;
    }

    private TaskbarService.TaskbarInfo? GetNeighbor(TaskbarService.TaskbarInfo current, bool toRight)
    {
        var index = _taskbars.IndexOf(current);
        if (index < 0)
            return null;

        var neighborIndex = toRight ? index + 1 : index - 1;
        if (neighborIndex < 0 || neighborIndex >= _taskbars.Count)
            return null;

        return _taskbars[neighborIndex];
    }

    private void ClampToTaskbar(BaseEntity entity, TaskbarService.TaskbarInfo taskbar, double halfWidth, bool bounce)
    {
        var minX = taskbar.BoundsPx.Left + halfWidth;
        var maxX = taskbar.BoundsPx.Right - halfWidth;

        if (entity.X < minX)
        {
            entity.X = minX;
            if (bounce)
            {
                if (entity == _pokemon)
                    StartBounceIdle(true);   // brief pause then walk right
                else
                    BounceRight(entity);
            }
        }
        else if (entity.X > maxX)
        {
            entity.X = maxX;
            if (bounce)
            {
                if (entity == _pokemon)
                    StartBounceIdle(false);  // brief pause then walk left
                else
                    BounceLeft(entity);
            }
        }
    }

    /// <summary>Player bounced off edge — idle briefly, then resume in the opposite direction.</summary>
    private double _bounceIdleTimer;
    private double _bounceIdleDuration;
    private double _bounceResumeDirection;
    private const double BOUNCE_IDLE_MIN = 0.4;
    private const double BOUNCE_IDLE_MAX = 1.2;

    private void StartBounceIdle(bool resumeRight)
    {
        // Don't interrupt ongoing chase
        if (_chaseTarget != null) 
        {
            if (resumeRight) BounceRight(_pokemon);
            else BounceLeft(_pokemon);
            return;
        }
        _pokemon.VelocityX = 0;
        _pokemon.StartIdle();
        _bounceResumeDirection = resumeRight ? 1.0 : -1.0;
        _bounceIdleDuration = BOUNCE_IDLE_MIN + (_random.NextDouble() * (BOUNCE_IDLE_MAX - BOUNCE_IDLE_MIN));
        _bounceIdleTimer = _bounceIdleDuration;
        _smartMovement?.CancelPause();  // reset smart-movement timer so it doesn't double-idle
    }

    private void BounceLeft(BaseEntity entity)
    {
        entity.VelocityX = -Math.Abs(entity.VelocityX);
        entity.FacingRight = false;
    }

    private void BounceRight(BaseEntity entity)
    {
        entity.VelocityX = Math.Abs(entity.VelocityX);
        entity.FacingRight = true;
    }

    private void UpdateAutoHideVisibility()
    {
        if (_currentTaskbar == null)
            return;

        var hide = TaskbarService.IsTaskbarHidden(_currentTaskbar);
        Opacity = hide ? 0 : 1;
        foreach (var pair in _enemyWindows)
        {
            var taskbar = GetEnemyTaskbar(pair.Key);
            var enemyHide = taskbar != null && TaskbarService.IsTaskbarHidden(taskbar);
            pair.Value.SetHidden(enemyHide);
        }
        _captureManager.SetHidden(hide);
    }

    private void UpdateWindowPosition()
    {
        if (_currentTaskbar == null)
            return;

        var scale = _currentTaskbar.DpiScale > 0 ? _currentTaskbar.DpiScale : 1.0;
        var windowX = (_pokemon.X / scale) - (RootCanvas.Width / 2);
        var groundLine = _currentGroundLineY > 0 ? _currentGroundLineY : RootCanvas.Height;
        var windowY = (_pokemon.Y / scale) - groundLine;

        Left = windowX;
        Top = windowY;
    }

    private void UpdateEnemyMovement()
    {
        if (_currentTaskbar == null)
            return;

        foreach (var enemy in _entityManager.Enemies)
        {
            if (enemy.State != EntityState.Idle && enemy.State != EntityState.Walking)
                continue;

            var taskbar = GetEnemyTaskbar(enemy);
            if (taskbar == null)
                continue;

            var halfWidth = GetHalfWidthPx(enemy, taskbar);
            var minX = taskbar.BoundsPx.Left + halfWidth;
            var maxX = taskbar.BoundsPx.Right - halfWidth;
            var movingRight = enemy.VelocityX >= 0;
            var target = FindTaskbarForX(enemy.X);

            if (!_allowEnemyMonitorTravel)
            {
                if (ShouldAllowOutside(enemy, minX, maxX))
                    continue;
                ClampToTaskbar(enemy, taskbar, halfWidth, true);
                continue;
            }

            if (target == null)
            {
                if (ShouldAllowOutside(enemy, minX, maxX))
                    continue;

                // Enemy is in the pixel gap between monitors — bridge to neighbor
                var neighbor = movingRight
                    ? GetNeighbor(taskbar, true)
                    : GetNeighbor(taskbar, false);

                if (neighbor != null && !IsMonitorBlocked(neighbor))
                {
                    SetEnemyTaskbar(enemy, neighbor);
                    taskbar = neighbor;
                    minX = taskbar.BoundsPx.Left + halfWidth;
                    maxX = taskbar.BoundsPx.Right - halfWidth;
                }
                else
                {
                    ClampToTaskbar(enemy, taskbar, halfWidth, true);
                }
                continue;
            }

            if (target != taskbar)
            {
                if (IsMonitorBlocked(target))
                {
                    ClampToTaskbar(enemy, taskbar, halfWidth, true);
                    continue;
                }

                SetEnemyTaskbar(enemy, target);
                taskbar = target;
                minX = taskbar.BoundsPx.Left + halfWidth;
                maxX = taskbar.BoundsPx.Right - halfWidth;
            }

            if (movingRight && enemy.X >= maxX)
            {
                var next = GetNeighbor(taskbar, true);
                if (next == null || IsMonitorBlocked(next))
                    ClampToTaskbar(enemy, taskbar, halfWidth, true);
                else
                    SetEnemyTaskbar(enemy, next);
                continue;
            }

            if (!movingRight && enemy.X <= minX)
            {
                var prev = GetNeighbor(taskbar, false);
                if (prev == null || IsMonitorBlocked(prev))
                    ClampToTaskbar(enemy, taskbar, halfWidth, true);
                else
                    SetEnemyTaskbar(enemy, prev);
            }
        }
    }

    private void UpdateEnemyWindowPosition()
    {
        foreach (var pair in _enemyWindows)
        {
            var enemy = pair.Key;
            var window = pair.Value;
            var taskbar = GetEnemyTaskbar(enemy);
            var scale = taskbar?.DpiScale ?? _currentTaskbar?.DpiScale ?? 1.0;
            window.UpdatePosition(enemy.X, enemy.Y, scale);
        }
    }

    private void UpdateCapture(double deltaTime)
    {
        if (!_captureManager.IsActive && _currentTaskbar != null)
        {
            foreach (var enemy in _entityManager.Enemies)
            {
                if (enemy.State != EntityState.Fainted || enemy.IsCaptureInProgress)
                    continue;

                _enemyWindows.TryGetValue(enemy, out var window);
                var taskbar = GetEnemyTaskbar(enemy) ?? _currentTaskbar;
                var scale = taskbar?.DpiScale ?? 1.0;
                // Passar posição de tela real do jogador (centro da janela em DIPs)
                var playerScreenCenterX = this.Left + RootCanvas.Width / 2;
                var playerScreenCenterY = this.Top + RootCanvas.Height / 2;
                _captureManager.TryStartCapture(_pokemon, enemy, window, scale,
                    playerScreenCenterX, playerScreenCenterY);
                break;
            }
        }

        _captureManager.Update(deltaTime);
    }

    private double GetHalfWidthPx(BaseEntity entity, TaskbarService.TaskbarInfo taskbar)
    {
        if (entity.FrameWidth <= 0)
            return 0;

        var scale = taskbar.DpiScale > 0 ? taskbar.DpiScale : 1.0;
        return (entity.FrameWidth * scale) / 2;
    }

    private TaskbarService.TaskbarInfo? GetEnemyTaskbar(EnemyPet enemy)
    {
        if (_enemyTaskbars.TryGetValue(enemy, out var taskbar))
            return taskbar;

        var target = FindTaskbarForX(enemy.X);
        if (target != null)
        {
            _enemyTaskbars[enemy] = target;
            return target;
        }

        if (_currentTaskbar != null)
        {
            _enemyTaskbars[enemy] = _currentTaskbar;
            return _currentTaskbar;
        }

        return null;
    }

    private void SetEnemyTaskbar(EnemyPet enemy, TaskbarService.TaskbarInfo taskbar)
    {
        _enemyTaskbars[enemy] = taskbar;
        enemy.Y = taskbar.GroundYPx - TASKBAR_MARGIN;
    }

    private static bool ShouldAllowOutside(EnemyPet enemy, double minX, double maxX)
    {
        if (enemy.X < minX && enemy.VelocityX > 0)
            return true;
        if (enemy.X > maxX && enemy.VelocityX < 0)
            return true;
        return false;
    }

    private int GetEnemyCount()
    {
        var count = 0;
        foreach (var enemy in _entityManager.Enemies)
            count++;
        return count;
    }

    private void UpdateSpawn(double deltaTime)
    {
        if (_currentTaskbar == null || _taskbars.Count == 0)
            return;

        _spawnTimer += deltaTime;
        if (_spawnTimer < _nextSpawnDelay)
            return;

        _spawnTimer = 0;
        _nextSpawnDelay = NextSpawnDelay();

        if (GetEnemyCount() >= _config.Enemy.MaxSimultaneous)
            return;

        if (_random.NextDouble() > _config.Spawn.ChanceWhenReady)
            return;

        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        var taskbar = GetRandomTaskbar();
        if (taskbar == null)
            return;

        var dex = _forceRareSpawn ? SelectRareDex() : SelectEnemyDex();
        _forceRareSpawn = false;

        // FASE 7: Rolar shiny
        var isShiny = RollShiny();
        var enemy = new EnemyPet(dex, isShiny: isShiny);
        enemy.PatrolSpeed = _config.Enemy.WalkSpeed;
        RegisterEnemy(enemy, taskbar);

        var spawnLeft = _random.Next(0, 2) == 0;
        var startX = spawnLeft
            ? taskbar.BoundsPx.Left - _config.Spawn.OffsetFromEdge
            : taskbar.BoundsPx.Right + _config.Spawn.OffsetFromEdge;
        enemy.X = startX;
        enemy.Y = taskbar.GroundYPx - TASKBAR_MARGIN;
        enemy.VelocityX = spawnLeft ? _config.Enemy.WalkSpeed : -_config.Enemy.WalkSpeed;
        enemy.StartWalking();

        if (isShiny)
        {
            _notificationService?.Notify(
                Localizer.Get("toast.shiny.title"),
                Localizer.Get("toast.shiny.spawned", dex.ToString()));
            Log.Information("SHINY Enemy spawned: Dex {Dex} at X: {X:F0}", dex, startX);
        }
        else
        {
            Log.Debug("Enemy spawned: Dex {Dex} at X: {X:F0}, Y: {Y:F0}, Monitor: {MonitorIndex}", 
                dex, startX, taskbar.GroundYPx, taskbar.MonitorIndex);
        }
    }

    private void RegisterEnemy(EnemyPet enemy, TaskbarService.TaskbarInfo taskbar)
    {
        var window = new PetWindow();
        window.SetHidden(TaskbarService.IsTaskbarHidden(taskbar));
        window.EnemyClicked += () => OnEnemyClicked(enemy);
        window.Show();

        _enemyWindows[enemy] = window;
        _enemyTaskbars[enemy] = taskbar;

        enemy.AnimationPlayer.FrameChanged += (frame, groundLineY) => OnEnemyFrameChanged(enemy, frame, groundLineY);
        _entityManager.Add(enemy);

        // Carregar sprites async — inimigo existe no mundo mas fica invisível até carregar
        _spriteCache.GetAnimationsAsync(enemy.Dex, enemy.FormId, _config)
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && _enemyWindows.ContainsKey(enemy))
                {
                    enemy.ApplyAnimations(t.Result);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnEnemyFrameChanged(EnemyPet enemy, BitmapSource frame, double groundLineY)
    {
        if (!_enemyWindows.TryGetValue(enemy, out var window))
            return;

        var scaleX = enemy.ShouldFlip ? (enemy.FacingRight ? 1 : -1) : 1;
        window.UpdateFrame(frame, groundLineY, scaleX);

        if (_debugOverlayEnabled)
        {
            window.ShowDebugOverlay = true;
        }
    }

    private void UpdatePlayerDebugPanel()
    {
        DebugPanel.Visibility = Visibility.Visible;
        DebugText.Text = string.Join("\n", new[]
        {
            $"FPS: {_fpsSmooth:0.0}",
            $"State: {_pokemon.State}",
            $"Pos: {_pokemon.X:0},{_pokemon.Y:0}",
            $"Ground: {_currentGroundLineY:0}",
            $"Hitbox: {_pokemon.HitboxX:0},{_pokemon.HitboxY:0} {_pokemon.HitboxWidth:0}x{_pokemon.HitboxHeight:0}",
            $"Pokeballs: {_pokemon.Pokeballs}"
        });

        UpdateDebugHitbox(_pokemon.FrameWidth, _pokemon.FrameHeight, _pokemon.HitboxX, _pokemon.HitboxY, _pokemon.HitboxWidth, _pokemon.HitboxHeight, _pokemon.ShouldFlip && !_pokemon.FacingRight);
    }

    private void UpdateEnemyDebugPanels()
    {
        foreach (var enemy in _entityManager.Enemies)
        {
            if (!_enemyWindows.TryGetValue(enemy, out var window))
                continue;

            window.ShowDebugOverlay = true;
            window.SetDebugText(string.Join("\n", new[]
            {
                $"State: {enemy.State}",
                $"Pos: {enemy.X:0},{enemy.Y:0}",
                $"Ground: {enemy.FrameGroundLine:0}",
                $"Hitbox: {enemy.HitboxX:0},{enemy.HitboxY:0} {enemy.HitboxWidth:0}x{enemy.HitboxHeight:0}"
            }));
            window.SetDebugHitbox(enemy.FrameWidth, enemy.FrameHeight, enemy.HitboxX, enemy.HitboxY, enemy.HitboxWidth, enemy.HitboxHeight, enemy.ShouldFlip && !enemy.FacingRight);
        }
    }

    private void UpdateDebugHitbox(double frameWidth, double frameHeight, double hitboxX, double hitboxY, double hitboxWidth, double hitboxHeight, bool flipped)
    {
        if (!_debugOverlayEnabled)
        {
            DebugHitbox.Visibility = Visibility.Collapsed;
            return;
        }

        if (frameWidth <= 0 || frameHeight <= 0)
        {
            DebugHitbox.Visibility = Visibility.Collapsed;
            return;
        }

        var width = hitboxWidth > 0 ? hitboxWidth : frameWidth;
        var height = hitboxHeight > 0 ? hitboxHeight : frameHeight;
        var left = hitboxWidth > 0 ? hitboxX : 0;
        var top = hitboxHeight > 0 ? hitboxY : 0;

        if (flipped)
        {
            left = frameWidth - left - width;
        }

        DebugHitbox.Width = Math.Max(0, width);
        DebugHitbox.Height = Math.Max(0, height);
        Canvas.SetLeft(DebugHitbox, left);
        Canvas.SetTop(DebugHitbox, top);
        DebugHitbox.Visibility = Visibility.Visible;
    }

    // ── FASE 7: Mood, Behavior & Smart Movement ─────────────────────────

    private void UpdateMoodAndBehavior(double deltaTime)
    {
        // Não interferir se em combate ou captura
        if (_combatManager.IsActive || _captureManager.IsActive)
        {
            _idleBehaviorService?.Cancel(_pokemon);
            _smartMovement?.CancelPause();
            _chaseTarget = null;
            _clickIdleTimer = 0;
            return;
        }

        // Atualizar humor/amizade
        _moodService?.Update(_pokemon, deltaTime);

        var mood = _pokemon.Mood;

        // ── Click-idle: pet stays put after being clicked ──
        if (_clickIdleTimer > 0)
        {
            _clickIdleTimer -= deltaTime;
            if (_clickIdleTimer > 0)
            {
                // Keep the pet idle, don't let smart movement override
                if (_pokemon.State == EntityState.Walking)
                {
                    _pokemon.VelocityX = 0;
                    _pokemon.StartIdle();
                }
                return;
            }
            // Timer expired — resume normal behavior below
        }

        // ── Bounce-idle: brief pause after hitting edge ──
        if (_bounceIdleTimer > 0)
        {
            _bounceIdleTimer -= deltaTime;
            if (_bounceIdleTimer > 0)
            {
                if (_pokemon.State == EntityState.Walking)
                {
                    _pokemon.VelocityX = 0;
                    _pokemon.StartIdle();
                }
                return;
            }
            // Resume walking in the bounce direction
            _pokemon.VelocityX = _config.Player.WalkSpeed * _bounceResumeDirection;
            _pokemon.StartWalking();
        }

        // ── Chase target: walk toward the clicked enemy ──
        if (_chaseTarget != null)
        {
            // Clean up if target despawned, died, was captured, or already fighting
            if (_chaseTarget.State == EntityState.Dead ||
                _chaseTarget.State == EntityState.Captured ||
                !_entityManager.Enemies.Contains(_chaseTarget))
            {
                _chaseTarget = null;
            }
            else
            {
                var dx = _chaseTarget.X - _pokemon.X;
                var dist = Math.Abs(dx);

                if (dist <= CHASE_ARRIVE_DISTANCE)
                {
                    // Close enough — stop and let CombatManager handle collision
                    _chaseTarget = null;
                }
                else
                {
                    // Keep walking toward target
                    var dir = dx > 0 ? 1.0 : -1.0;
                    _pokemon.VelocityX = _config.Player.WalkSpeed * dir * CHASE_SPEED_MULTIPLIER;
                    if (_pokemon.State != EntityState.Walking)
                        _pokemon.StartWalking();
                    return;
                }
            }
        }

        // Atualizar comportamentos idle (yawn, sit, lay, sleep)
        _idleBehaviorService?.Update(_pokemon, deltaTime, mood);

        // Atualizar movimento inteligente (pausas, edge slowdown, mood speed)
        if (_smartMovement != null && _currentTaskbar != null)
        {
            var tb = _currentTaskbar;
            var speedMultiplier = _smartMovement.Update(_pokemon, deltaTime, mood, tb.BoundsPx.Left, tb.BoundsPx.Right);

            if (speedMultiplier == 0.0 && _pokemon.State == EntityState.Walking)
            {
                // Smart movement quer parar o pet
                _pokemon.StartIdle();
            }
            else if (speedMultiplier > 0.0 && _smartMovement.IsResting == false &&
                     _pokemon.State == EntityState.Idle && !(_idleBehaviorService?.IsInBehavior ?? false))
            {
                // Retomar walk se não está em comportamento idle
                var direction = _pokemon.FacingRight ? 1.0 : -1.0;
                _pokemon.VelocityX = _config.Player.WalkSpeed * direction * speedMultiplier;
                _pokemon.StartWalking();
            }
            else if (_pokemon.State == EntityState.Walking && speedMultiplier > 0.0 && speedMultiplier < 1.0)
            {
                // Aplicar slowdown na velocidade atual
                var baseSpeed = _config.Player.WalkSpeed;
                _pokemon.VelocityX = (_pokemon.VelocityX > 0 ? baseSpeed : -baseSpeed) * speedMultiplier;
            }
        }

        // Rastrear tempo de walk para quests e XP
        if (_pokemon.State == EntityState.Walking)
        {
            _questService?.OnWalkTime(deltaTime);
            _levelService?.OnWalkTime(deltaTime);
        }
    }

    /// <summary>FASE 7: Handler de clique direto no sprite do pet.</summary>
    private void OnPokemonMouseClick(object sender, MouseButtonEventArgs e)
    {
        OnPetClicked();
        e.Handled = true;
    }

    /// <summary>FASE 7: Handler de clique no pet (carícia).</summary>
    private void OnPetClicked()
    {
        // Não aceitar durante combate/captura
        if (_combatManager.IsActive || _captureManager.IsActive)
            return;

        // Cancel any chase in progress
        _chaseTarget = null;

        // Se está dormindo/em behavior, acordar
        if (_pokemon.State == EntityState.Sleeping || _pokemon.State == EntityState.SpecialIdle)
        {
            _idleBehaviorService?.Cancel(_pokemon);
            _smartMovement?.CancelPause();
        }

        // STOP the pet
        _pokemon.VelocityX = 0;
        _pokemon.StartIdle();
        _smartMovement?.CancelPause();
        _clickIdleTimer = CLICK_IDLE_DURATION;

        // Registrar carícia no mood service
        var accepted = _moodService?.OnPet(_pokemon) ?? false;
        if (accepted)
        {
            // Tentar tocar animação de reação
            _pokemon.StartRandomReaction(_random);
            _questService?.OnPet();
            _levelService?.OnPet();
            _saveData = _saveData with { TotalPets = _saveData.TotalPets + 1 };
            Log.Debug("Pet clicked! Friendship={Friendship}, Mood={Mood}, TotalPets={TotalPets}", _pokemon.Friendship, _pokemon.Mood, _saveData.TotalPets);
        }
    }

    /// <summary>FASE 7: Determina se o spawn deve ser shiny.</summary>
    private bool RollShiny()
    {
        var chance = _config.Shiny.ShinyChanceDenominator;
        if (chance <= 0) return false;
        return _random.Next(0, chance) == 0;
    }

    /// <summary>Called when the player clicks an enemy Pokémon — start chasing it.</summary>
    private void OnEnemyClicked(EnemyPet enemy)
    {
        if (_combatManager.IsActive || _captureManager.IsActive)
            return;
        if (enemy.State == EntityState.Dead || enemy.State == EntityState.Captured)
            return;

        // Cancel click-idle and bounce-idle so the pet resumes movement
        _clickIdleTimer = 0;
        _bounceIdleTimer = 0;
        _chaseTarget = enemy;
        _smartMovement?.CancelPause();
        _idleBehaviorService?.Cancel(_pokemon);

        // Immediately orient and start walking toward target
        var dir = enemy.X > _pokemon.X ? 1.0 : -1.0;
        _pokemon.VelocityX = _config.Player.WalkSpeed * dir * CHASE_SPEED_MULTIPLIER;
        _pokemon.StartWalking();

        Log.Debug("Chasing enemy Dex={Dex} at X={X:F0}", enemy.Dex, enemy.X);
    }

    /// <summary>FASE 7: Aplica a recompensa de uma quest completada.</summary>
    private void ApplyQuestReward(Quest quest)
    {
        switch (quest.RewardType)
        {
            case QuestRewardType.Pokeballs:
                _pokemon.AddPokeballs(quest.RewardAmount);
                _saveData = _saveData with { Pokeballs = _pokemon.Pokeballs };
                _trayIcon?.SetPokeballCount(_pokemon.Pokeballs);
                _notificationService?.Notify(
                    Localizer.Get("toast.quest.reward"),
                    Localizer.Get("toast.quest.reward_pokeballs", quest.RewardAmount.ToString()));
                Log.Information("Quest reward: +{Amount} Pokéballs (total: {Total})", quest.RewardAmount, _pokemon.Pokeballs);
                break;

            case QuestRewardType.RareSpawn:
                _forceRareSpawn = true;
                _notificationService?.Notify(
                    Localizer.Get("toast.quest.reward"),
                    Localizer.Get("toast.quest.reward_rare"));
                Log.Information("Quest reward: Rare spawn queued");
                break;

            case QuestRewardType.FriendshipBoost:
                _pokemon.AddFriendship(quest.RewardAmount);
                _notificationService?.Notify(
                    Localizer.Get("toast.quest.reward"),
                    Localizer.Get("toast.quest.reward_friendship"));
                Log.Information("Quest reward: +{Amount} Friendship (total: {Total})", quest.RewardAmount, _pokemon.Friendship);
                break;

            case QuestRewardType.EvolutionStone:
                var stone = EvolutionService.GetRandomStone(_random);
                AddStone(stone);
                _notificationService?.Notify(
                    Localizer.Get("toast.quest.reward"),
                    Localizer.Get("toast.quest.reward_stone", stone.ToString()));
                break;
        }
        PerformSave("quest_reward");
    }

    private EnemySpawnWeight[] GetSpawnPool()
    {
        // Se o config já tem weights explícitos, usar eles
        if (_config.Enemy.SpawnWeights.Length > 0)
            return _config.Enemy.SpawnWeights;

        // Gerar dinamicamente a partir dos offsets disponíveis
        _dynamicSpawnPool ??= SpawnPoolBuilder.BuildFromOffsets(_spriteLoader, _pokemon.Dex);
        return _dynamicSpawnPool;
    }

    private int SelectEnemyDex()
    {
        var weights = GetSpawnPool();

        var total = weights.Sum(w => w.Weight);
        if (total <= 0)
            return _config.Enemy.DefaultDex;

        var roll = _random.Next(0, total);
        foreach (var item in weights)
        {
            if (roll < item.Weight)
                return item.Dex;
            roll -= item.Weight;
        }

        return _config.Enemy.DefaultDex;
    }

    /// <summary>FASE 7: Seleciona um dex raro (lendário/pseudo-lendário) para quest reward.</summary>
    private int SelectRareDex()
    {
        var weights = GetSpawnPool();
        var rareWeights = weights.Where(w =>
            w.Comment == "Legendary/Mythical" || w.Comment == "Pseudo-Legendary" || w.Comment == "Starter").ToArray();

        if (rareWeights.Length == 0)
            return SelectEnemyDex(); // Fallback

        return rareWeights[_random.Next(rareWeights.Length)].Dex;
    }

    private TaskbarService.TaskbarInfo? GetRandomTaskbar()
    {
        if (_taskbars.Count == 0)
            return _currentTaskbar;

        // Prefer unblocked monitors so enemies spawn where the pet can reach
        var unblocked = _taskbars.Where(t => !IsMonitorBlocked(t)).ToList();
        if (unblocked.Count > 0)
            return unblocked[_random.Next(unblocked.Count)];

        return _taskbars[_random.Next(_taskbars.Count)];
    }

    private double NextSpawnDelay()
    {
        var range = _config.Spawn.DelayMaxSeconds - _config.Spawn.DelayMinSeconds;
        return _config.Spawn.DelayMinSeconds + (_random.NextDouble() * range);
    }

    private void CleanupDeadEnemies()
    {
        // Coleta inimigos em estado Dead ou Captured para limpar windows e referências
        var toRemove = new List<EnemyPet>();
        foreach (var enemy in _entityManager.Enemies)
        {
            if (enemy.State == EntityState.Dead)
                toRemove.Add(enemy);
        }

        foreach (var enemy in toRemove)
        {
            if (_enemyWindows.TryGetValue(enemy, out var window))
            {
                window.Close();
                _enemyWindows.Remove(enemy);
            }
            _enemyTaskbars.Remove(enemy);
        }

        _entityManager.RemoveInactive();
    }

    private void OnCaptureCompleted(EnemyPet enemy)
    {
        _entityManager.Remove(enemy);
        _pokemon.AddToParty(enemy.Dex);
        if (_enemyWindows.TryGetValue(enemy, out var window))
        {
            window.Close();
            _enemyWindows.Remove(enemy);
        }
        _enemyTaskbars.Remove(enemy);

        // Atualizar stats
        _saveData = _saveData with
        {
            Pokeballs = _pokemon.Pokeballs,
            Stats = _saveData.Stats with
            {
                TotalCaptured = _saveData.Stats.TotalCaptured + 1,
                TotalPokeballsUsed = _saveData.Stats.TotalPokeballsUsed + 1
            }
        };
        PerformSave("capture");

        // FASE 5: Notificação toast e atualizar tray
        _notificationService?.NotifyCaptureSuccess(enemy.Dex);
        _trayIcon?.SetPokeballCount(_pokemon.Pokeballs);

        // FASE 6: Verificar conquistas
        CheckAchievements();

        // FASE 7: Atualizar mood e quests
        _moodService?.OnCapture(_pokemon);
        _questService?.OnCapture(enemy.IsShiny);
        _questService?.OnPokeballUsed();
        _levelService?.OnCapture(enemy.IsShiny);

        // FASE 7: Se capturou shiny, registrar na lista
        if (enemy.IsShiny)
        {
            var shinies = new List<int>(_saveData.ShinyCaptured) { enemy.Dex };
            _saveData = _saveData with { ShinyCaptured = shinies };
            Log.Information("SHINY captured! Dex={Dex}, Total shinies={Total}", enemy.Dex, shinies.Count);
        }
    }

    private void OnCaptureFailed(EnemyPet enemy)
    {
        _saveData = _saveData with
        {
            Pokeballs = _pokemon.Pokeballs,
            Stats = _saveData.Stats with
            {
                TotalCaptureFailed = _saveData.Stats.TotalCaptureFailed + 1,
                TotalPokeballsUsed = _saveData.Stats.TotalPokeballsUsed + 1
            }
        };
        Log.Debug("Capture failed. Stats updated: Failed={Failed}, PokeballsUsed={Used}", 
            _saveData.Stats.TotalCaptureFailed, _saveData.Stats.TotalPokeballsUsed);

        // FASE 5: Notificação toast e atualizar tray
        _notificationService?.NotifyCaptureFailed(enemy.Dex);
        _trayIcon?.SetPokeballCount(_pokemon.Pokeballs);

        // FASE 7: Quest progress
        _questService?.OnPokeballUsed();
    }

    private void OnBattleEnded(bool playerWon)
    {
        _saveData = _saveData with
        {
            Stats = _saveData.Stats with
            {
                TotalBattles = _saveData.Stats.TotalBattles + 1,
                TotalBattlesWon = _saveData.Stats.TotalBattlesWon + (playerWon ? 1 : 0)
            }
        };

        // Recompensar com pokébola ao vencer batalha
        if (playerWon)
        {
            _pokemon.AddPokeballs(1);
            _saveData = _saveData with { Pokeballs = _pokemon.Pokeballs };
            _trayIcon?.SetPokeballCount(_pokemon.Pokeballs);
            Log.Debug("Battle won! +1 Pokéball awarded (total: {Pokeballs})", _pokemon.Pokeballs);
        }

        Log.Debug("Battle ended. PlayerWon={Won}. Stats: Battles={Battles}, Won={Won2}", 
            playerWon, _saveData.Stats.TotalBattles, _saveData.Stats.TotalBattlesWon);

        // FASE 5: Notificação toast
        _notificationService?.NotifyBattleResult(playerWon);

        // FASE 6: Verificar conquistas
        CheckAchievements();

        // FASE 7: Atualizar mood e quests
        if (playerWon)
        {
            _moodService?.OnBattleWon(_pokemon);
            _questService?.OnBattleWon();
            _levelService?.OnBattleWon();

            // Chance de drop de pedra de evolução (10%)
            if (_random.Next(10) == 0)
            {
                var droppedStone = EvolutionService.GetRandomStone(_random);
                AddStone(droppedStone);
                _notificationService?.Notify(
                    Localizer.Get("evolution.title"),
                    Localizer.Get("evolution.stone_drop", droppedStone.ToString()));
            }
        }
        else
        {
            _moodService?.OnBattleLost(_pokemon);
            _levelService?.OnBattleLoss();
        }

        // FASE 7: Interromper qualquer comportamento idle pendente
        _idleBehaviorService?.Cancel(_pokemon);
        _smartMovement?.CancelPause();
    }

    private void UpdateAutoSave(double deltaTime)
    {
        // Acumular playtime
        _saveData = _saveData with
        {
            Stats = _saveData.Stats with
            {
                TotalPlayTimeSeconds = _saveData.Stats.TotalPlayTimeSeconds + deltaTime
            }
        };

        _autoSaveTimer += deltaTime;
        if (_autoSaveTimer >= AUTO_SAVE_INTERVAL)
        {
            _autoSaveTimer = 0;
            PerformSave("auto");
        }
    }

    private void PerformSave(string reason)
    {
        // Sincronizar estado do player → saveData, preservando campos FASE 6 e 7
        var baseSave = _pokemon.ToSaveData(_saveData.Stats);
        _saveData = baseSave with
        {
            ActiveProfileId = _appSettings.ActiveProfileId,
            OnboardingCompleted = _saveData.OnboardingCompleted,
            Achievements = _achievementService?.GetUnlockedIds() ?? _saveData.Achievements,
            // FASE 7: Persistir dados de quests e mods
            ActiveQuests = _questService?.ActiveQuests ?? _saveData.ActiveQuests,
            CompletedQuests = _questService?.CompletedQuests ?? _saveData.CompletedQuests,
            ShinyCaptured = _saveData.ShinyCaptured,
            EnabledMods = _saveData.EnabledMods,
            TotalPets = _saveData.TotalPets,
            SilenceNotifications = _silenceNotifications,
            BlockSpawns = _blockSpawns,
            Stones = _saveData.Stones,
            EvolutionCancelled = _saveData.EvolutionCancelled
        };
        // Persistir XP e nível
        if (_levelService != null)
            _saveData = _levelService.ApplyToSave(_saveData);
        var ok = SaveManager.Save(_saveData);
        if (ok)
            Log.Debug("Save successful ({Reason}). Dex={Dex}, Pokeballs={Balls}, Captured={Captured}, PlayTime={Time:F0}s", 
                reason, _saveData.ActiveDex, _saveData.Pokeballs, _saveData.Stats.TotalCaptured, _saveData.Stats.TotalPlayTimeSeconds);
        else
            Log.Warning("Save failed ({Reason})", reason);
    }

    /// <summary>Atualiza o tooltip do tray icon com dex e nível.</summary>
    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null) return;
        var level = _levelService?.Level ?? _saveData.Level;
        _trayIcon.SetTooltip($"Pokebar — #{_pokemon.Dex} (Lv.{level})");
    }

    /// <summary>
    /// Verifica se o Pokémon ativo pode evoluir (por nível ou amizade).
    /// Se sim, mostra toast perguntando. Evolução por pedra é via menu.
    /// </summary>
    private void CheckEvolution()
    {
        if (_evolutionService == null || _saveData.EvolutionCancelled) return;

        var level = _levelService?.Level ?? _saveData.Level;
        var entry = _evolutionService.CheckLevelEvolution(_pokemon.Dex, level)
                    ?? _evolutionService.CheckFriendshipEvolution(_pokemon.Dex, _pokemon.Friendship);

        if (entry == null) return;

        // Verificar se o Pokémon destino tem sprite disponível
        var uniqueId = entry.ToDex.ToString("D4");
        if (!_spriteLoader.TryGetOffset(uniqueId, out _))
        {
            Log.Debug("Evolution to #{ToDex} skipped — no sprite available", entry.ToDex);
            return;
        }

        Log.Information("Evolution available! #{FromDex} → #{ToDex} (method={Method}, level={Level}, friendship={Friendship})",
            entry.FromDex, entry.ToDex, entry.Method, level, _pokemon.Friendship);

        // Mostrar diálogo perguntando
        var result = System.Windows.MessageBox.Show(
            Localizer.Get("evolution.confirm", _pokemon.Dex.ToString(), entry.ToDex.ToString()),
            Localizer.Get("evolution.title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ExecuteEvolution(entry);
        }
        else
        {
            // Marcar como cancelada — reseta ao trocar de Pokémon no PC Box
            _saveData = _saveData with { EvolutionCancelled = true };
            _notificationService?.Notify(
                Localizer.Get("evolution.title"),
                Localizer.Get("evolution.cancelled"));
            Log.Information("Evolution cancelled by player for #{FromDex} → #{ToDex}", entry.FromDex, entry.ToDex);
        }
    }

    /// <summary>
    /// Executa a evolução: troca o Pokémon para o novo dex.
    /// </summary>
    private void ExecuteEvolution(EvolutionEntry entry)
    {
        var oldDex = _pokemon.Dex;
        _pokemon.ChangePokemon(entry.ToDex, _spriteCache, _config);
        _dynamicSpawnPool = null;
        _saveData = _saveData with
        {
            ActiveDex = _pokemon.Dex,
            ActiveFormId = _pokemon.FormId,
            EvolutionCancelled = false
        };

        _notificationService?.Notify(
            Localizer.Get("evolution.title"),
            Localizer.Get("evolution.success", oldDex.ToString(), entry.ToDex.ToString()));

        UpdateTrayTooltip();
        UpdateStonesMenu();
        PerformSave("evolution");

        Log.Information("EVOLUTION! #{OldDex} → #{NewDex}", oldDex, entry.ToDex);
    }

    /// <summary>
    /// Handler para quando o jogador usa uma pedra de evolução via menu tray.
    /// </summary>
    private void OnUseStoneRequested(int stoneValue)
    {
        var stone = (EvolutionStone)stoneValue;
        if (_evolutionService == null) return;

        var entry = _evolutionService.CheckStoneEvolution(_pokemon.Dex, stone);
        if (entry == null)
        {
            _notificationService?.Notify(
                Localizer.Get("evolution.title"),
                Localizer.Get("evolution.stone_no_effect"));
            return;
        }

        // Verificar se tem a pedra
        var stones = _saveData.Stones;
        if (!stones.TryGetValue(stone, out var count) || count <= 0)
        {
            Log.Warning("Stone {Stone} use requested but count is 0", stone);
            return;
        }

        // Verificar se o Pokémon destino tem sprite
        var uniqueId = entry.ToDex.ToString("D4");
        if (!_spriteLoader.TryGetOffset(uniqueId, out _))
        {
            _notificationService?.Notify(
                Localizer.Get("evolution.title"),
                Localizer.Get("evolution.no_sprite"));
            return;
        }

        // Consumir a pedra
        var newStones = new Dictionary<EvolutionStone, int>(stones);
        newStones[stone] = count - 1;
        if (newStones[stone] <= 0)
            newStones.Remove(stone);
        _saveData = _saveData with { Stones = newStones };

        Log.Information("Using {Stone} stone on #{Dex}", stone, _pokemon.Dex);
        ExecuteEvolution(entry);
    }

    /// <summary>
    /// Atualiza o submenu de pedras no tray icon.
    /// </summary>
    private void UpdateStonesMenu()
    {
        if (_trayIcon == null || _evolutionService == null) return;

        var stoneEvos = _evolutionService.GetStoneEvolutions(_pokemon.Dex);
        var available = stoneEvos
            .Select(e => (e.Stone, e.ToDex))
            .Where(x => _spriteLoader.TryGetOffset(x.ToDex.ToString("D4"), out _))
            .ToList();

        _trayIcon.SetStones(_saveData.Stones, available);
    }

    /// <summary>
    /// Adiciona uma pedra de evolução ao inventário.
    /// </summary>
    private void AddStone(EvolutionStone stone)
    {
        var stones = new Dictionary<EvolutionStone, int>(_saveData.Stones);
        stones.TryGetValue(stone, out var current);
        stones[stone] = current + 1;
        _saveData = _saveData with { Stones = stones };
        UpdateStonesMenu();
        Log.Information("Stone added: {Stone} (total: {Count})", stone, stones[stone]);
    }

    /// <summary>Resolve caminho de asset na pasta Assets/.</summary>
    private static string? ResolveAssetPath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Assets", filename)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", filename)),
            Path.GetFullPath(Path.Combine("Assets", filename))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        Log.Warning("Asset not found: {Filename}", filename);
        return null;
    }

    private static string ResolveOffsetsPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Assets", "Final", "pokemon_offsets_runtime.json")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "Final", "pokemon_offsets_runtime.json")),
            Path.GetFullPath(Path.Combine("Assets", "Final", "pokemon_offsets_runtime.json"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("pokemon_offsets_runtime.json not found");
    }

    private static string ResolveSpritesPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "SpriteCollab", "sprite")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "SpriteCollab", "sprite")),
            Path.GetFullPath(Path.Combine("SpriteCollab", "sprite"))
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
                return path;
        }

        throw new DirectoryNotFoundException("SpriteCollab/sprite directory not found");
    }
}
