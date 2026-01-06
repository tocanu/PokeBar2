using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _gameTimer;
    private readonly EntityManager _entityManager;
    private readonly CombatManager _combatManager;
    private readonly CaptureManager _captureManager;
    private readonly Random _random = new();
    private readonly Dictionary<EnemyPet, PetWindow> _enemyWindows = new();
    private readonly Dictionary<EnemyPet, TaskbarService.TaskbarInfo> _enemyTaskbars = new();
    private readonly GameplayConfig _config;
    private double _spawnTimer;
    private double _nextSpawnDelay;
    private bool _allowEnemyMonitorTravel = true;
    private readonly bool _debugOverlayEnabled;
    private double _fpsSmooth;
    private readonly List<TaskbarService.TaskbarInfo> _taskbars = new();
    private DateTime _lastUpdate;
    private TaskbarService.TaskbarInfo? _currentTaskbar;
    private HashSet<IntPtr> _fullscreenMonitors = new();
    private DateTime _lastFullscreenCheck;
    private double _currentGroundLineY;
    private IntPtr _windowHwnd;

    private const double TASKBAR_MARGIN = 0;
    private readonly bool _allowMonitorTravel;

    private static readonly (int Dex, int Weight)[] EnemyDexWeights =
    {
        // Muito comuns (Gen 1)
        (16, 60),  // Pidgey
        (19, 60),  // Rattata
        (21, 50),  // Spearow
        (23, 45),  // Ekans
        (27, 45),  // Sandshrew
        (29, 40),  // Nidoranâ™€
        (32, 40),  // Nidoranâ™‚
        (41, 50),  // Zubat
        (43, 40),  // Oddish
        (48, 40),  // Venonat
        (52, 35),  // Meowth
        (54, 35),  // Psyduck
        (56, 30),  // Mankey
        (60, 35),  // Poliwag
        (69, 35),  // Bellsprout
        (72, 30),  // Tentacool
        (74, 30),  // Geodude
        (84, 30),  // Doduo
        (96, 25),  // Drowzee
        (98, 25),  // Krabby
        
        // Incomuns
        (10, 25),  // Caterpie
        (13, 25),  // Weedle
        (37, 20),  // Vulpix
        (46, 20),  // Paras
        (58, 18),  // Growlithe
        (77, 18),  // Ponyta
        (79, 18),  // Slowpoke
        (83, 15),  // Farfetch'd
        (86, 15),  // Seel
        (90, 15),  // Shellder
        (92, 15),  // Gastly
        (100, 15), // Voltorb
        (102, 12), // Exeggcute
        (104, 12), // Cubone
        (108, 12), // Lickitung
        (109, 12), // Koffing
        (111, 12), // Rhyhorn
        (115, 10), // Kangaskhan
        (116, 10), // Horsea
        (118, 10), // Goldeen
        (120, 10), // Staryu
        (129, 10), // Magikarp
        
        // Raros
        (25, 8),   // Pikachu
        (39, 6),   // Jigglypuff
        (63, 6),   // Abra
        (66, 6),   // Machop
        (133, 5),  // Eevee
        (138, 5),  // Omanyte
        (140, 5),  // Kabuto
        (147, 3),  // Dratini
        
        // Ultra raros (evoluÃ§Ãµes/fortes)
        (17, 2),   // Pidgeotto
        (20, 2),   // Raticate
        (26, 2),   // Raichu
        (28, 2),   // Sandslash
        (59, 2),   // Arcanine
        (62, 2),   // Poliwrath
        (65, 2),   // Alakazam
        (68, 2),   // Machamp
        (76, 2),   // Golem
        (78, 2),   // Rapidash
        (80, 2),   // Slowbro
        (91, 2),   // Cloyster
        (94, 2),   // Gengar
        (103, 2),  // Exeggutor
        (112, 2),  // Rhydon
        (130, 1),  // Gyarados
        (131, 1),  // Lapras
        (134, 1),  // Vaporeon
        (135, 1),  // Jolteon
        (136, 1),  // Flareon
        (142, 1),  // Aerodactyl
        (143, 1),  // Snorlax
        (148, 1)   // Dragonair
    };

    public MainWindow()
    {
        InitializeComponent();

        Log.Debug("MainWindow constructor started");

        // Carregar configurações
        _config = GameplayConfigLoader.LoadOrCreateDefault();
        Log.Information("Gameplay config loaded. Player Dex: {PlayerDex}, Enemy Max: {MaxEnemies}", 
            _config.Player.DefaultDex, _config.Enemy.MaxSimultaneous);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;

        var offsetsPath = ResolveOffsetsPath();
        var spritesPath = ResolveSpritesPath();
        Log.Debug("Sprite paths - Offsets: {OffsetsPath}, Sprites: {SpritesPath}", offsetsPath, spritesPath);
        _spriteLoader = new SpriteLoader(offsetsPath, spritesPath);

        _allowMonitorTravel = _config.Player.AllowMonitorTravel;
        _debugOverlayEnabled = _config.Performance.DebugOverlay;
        _pokemon = new PlayerPet(_config.Player.DefaultDex);
        _pokemon.AnimationPlayer.FrameChanged += OnFrameChanged;
        _pokemon.LoadAnimations(_spriteLoader, _config);
        Log.Information("Player pet initialized. Dex: {Dex}", _pokemon.Dex);
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
        _nextSpawnDelay = NextSpawnDelay();

        _gameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_config.Performance.TickRateMs)
        };
        _gameTimer.Tick += GameLoop;
        _lastUpdate = DateTime.Now;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _windowHwnd = helper.Handle;
        WindowHelper.MakeTransparentWindow(this);
        WindowHelper.SetClickThrough(this, true);
    }

    protected override void OnClosed(EventArgs e)
    {
        Log.Information("MainWindow closing");
        base.OnClosed(e);
        foreach (var window in _enemyWindows.Values)
        {
            window.Close();
        }
        _enemyWindows.Clear();
        _enemyTaskbars.Clear();

        _captureManager.Shutdown();
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

        _gameTimer.Start();
        Log.Information("Game loop started. Tick rate: {TickRateMs}ms", _config.Performance.TickRateMs);
    }

    private void GameLoop(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Garante que a janela fique sempre no topo (corrige problema ao clicar na taskbar)
        WindowHelper.EnsureTopmost(_windowHwnd);

        _entityManager.Update(deltaTime);

        UpdateFullscreenMonitors();
        UpdateTaskbarTravel();
        UpdateEnemyMovement();
        UpdateSpawn(deltaTime);
        _combatManager.Update(deltaTime, _pokemon, _entityManager.Enemies);
        UpdateCapture(deltaTime);
        UpdateAutoHideVisibility();
        UpdateWindowPosition();
        UpdateEnemyWindowPosition();

        if (_debugOverlayEnabled && deltaTime > 0)
        {
            var fps = 1.0 / deltaTime;
            _fpsSmooth = _fpsSmooth <= 0 ? fps : (_fpsSmooth * 0.9) + (fps * 0.1);
            UpdatePlayerDebugPanel();
            UpdateEnemyDebugPanels();
        }
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

        if (target == null)
        {
            ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            return;
        }

        if (target != _currentTaskbar)
        {
            if (IsMonitorBlocked(target))
            {
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            }
            else
            {
                SetCurrentTaskbar(target);
            }
            return;
        }

        if (movingRight && _pokemon.X >= maxX)
        {
            var next = GetNeighbor(_currentTaskbar, true);
            if (next == null || IsMonitorBlocked(next))
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
            return;
        }

        if (!movingRight && _pokemon.X <= minX)
        {
            var prev = GetNeighbor(_currentTaskbar, false);
            if (prev == null || IsMonitorBlocked(prev))
                ClampToTaskbar(_pokemon, _currentTaskbar, halfWidth, true);
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

        _fullscreenMonitors = FullscreenService.GetFullscreenMonitors(ignoreWindows);
    }

    private bool IsMonitorBlocked(TaskbarService.TaskbarInfo target)
    {
        if (target.MonitorHandle == IntPtr.Zero)
            return false;

        return _fullscreenMonitors.Contains(target.MonitorHandle);
    }

    private void SetCurrentTaskbar(TaskbarService.TaskbarInfo taskbar)
    {
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
                BounceRight(entity);
        }
        else if (entity.X > maxX)
        {
            entity.X = maxX;
            if (bounce)
                BounceLeft(entity);
        }
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
                ClampToTaskbar(enemy, taskbar, halfWidth, true);
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
                continue;
            }

            if (!movingRight && enemy.X <= minX)
            {
                var prev = GetNeighbor(taskbar, false);
                if (prev == null || IsMonitorBlocked(prev))
                    ClampToTaskbar(enemy, taskbar, halfWidth, true);
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
                _captureManager.TryStartCapture(_pokemon, enemy, window, scale);
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

        var dex = SelectEnemyDex();
        var enemy = new EnemyPet(dex);
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
        
        Log.Debug("Enemy spawned: Dex {Dex} at X: {X:F0}, Y: {Y:F0}, Monitor: {MonitorIndex}", 
            dex, startX, taskbar.GroundYPx, taskbar.MonitorIndex);
    }

    private void RegisterEnemy(EnemyPet enemy, TaskbarService.TaskbarInfo taskbar)
    {
        var window = new PetWindow();
        window.SetHidden(TaskbarService.IsTaskbarHidden(taskbar));
        window.Show();

        _enemyWindows[enemy] = window;
        _enemyTaskbars[enemy] = taskbar;

        enemy.AnimationPlayer.FrameChanged += (frame, groundLineY) => OnEnemyFrameChanged(enemy, frame, groundLineY);
        enemy.LoadAnimations(_spriteLoader, _config);
        _entityManager.Add(enemy);
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
            $"Ground: {_currentGroundLineY:0}"
        });
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
                $"Ground: {enemy.FrameGroundLine:0}"
            }));
        }
    }

    private int SelectEnemyDex()
    {
        var weights = _config.Enemy.SpawnWeights;
        if (weights.Length == 0)
            return _config.Enemy.DefaultDex;

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

    private TaskbarService.TaskbarInfo? GetRandomTaskbar()
    {
        if (_taskbars.Count == 0)
            return _currentTaskbar;

        return _taskbars[_random.Next(_taskbars.Count)];
    }

    private double NextSpawnDelay()
    {
        var range = _config.Spawn.DelayMaxSeconds - _config.Spawn.DelayMinSeconds;
        return _config.Spawn.DelayMinSeconds + (_random.NextDouble() * range);
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
