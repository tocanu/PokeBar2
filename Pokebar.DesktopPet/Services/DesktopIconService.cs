using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Interop;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Serviço de interação com ícones do Desktop (estilo Desktop Goose).
/// Gerencia o ciclo de brincadeira: selecionar ícone, carregar, arrastar e soltar.
/// Suporta 3 modos: off, fake (overlay visual), real (move ícone real).
/// </summary>
public sealed class DesktopIconService
{
    private readonly DesktopIconConfig _config;
    private readonly Random _random = new();

    // ── State machine ──
    private IconPlayState _state = IconPlayState.Idle;
    private double _playTimer;
    private double _playDuration;
    private double _cooldownTimer;

    // ── Current play data ──
    private DesktopIconInterop.DesktopIcon? _targetIcon;
    private int _targetOriginalX, _targetOriginalY;
    private double _carryOffsetX; // How far the pet has walked since picking up
    private int _carryDirection; // 1 or -1
    private double _petStartX;   // Pet's X when the carry started

    // ── Icon handle (cached, refreshed periodically) ──
    private IntPtr _listViewHandle;
    private double _handleRefreshTimer;
    private const double HANDLE_REFRESH_INTERVAL = 30.0; // seconds

    // ── Safety ──
    private Dictionary<string, (int X, int Y)>? _savedLayout;
    private bool _autoArrangeDetected;

    // ── Timing ──
    private const double COOLDOWN_RARELY = 25.0;
    private const double COOLDOWN_SOMETIMES = 12.0;
    private const double COOLDOWN_ALWAYS = 5.0;

    public enum IconPlayState
    {
        Idle,           // Not playing, waiting for trigger
        PickingUp,      // Brief pause at icon (picking up animation)
        Carrying,       // Walking with icon in "hand"
        Dropping,       // Brief pause while "dropping" icon
        Cooldown        // Post-play cooldown before next play
    }

    /// <summary>Current state of the icon play system.</summary>
    public IconPlayState State => _state;

    /// <summary>Whether the pet is currently in an icon play behavior.</summary>
    public bool IsPlaying => _state != IconPlayState.Idle && _state != IconPlayState.Cooldown;

    /// <summary>True if the icon interaction system is enabled (not "off").</summary>
    public bool IsEnabled => !_config.Mode.Equals("off", StringComparison.OrdinalIgnoreCase)
                             && !_config.Frequency.Equals("never", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if using fake (overlay) mode.</summary>
    public bool IsFakeMode => _config.Mode.Equals("fake", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if using real (move) mode.</summary>
    public bool IsRealMode => _config.Mode.Equals("real", StringComparison.OrdinalIgnoreCase);

    /// <summary>The icon currently being targeted/carried, or null.</summary>
    public DesktopIconInterop.DesktopIcon? TargetIcon => _targetIcon;

    /// <summary>True if auto-arrange was detected (blocks real mode).</summary>
    public bool AutoArrangeDetected => _autoArrangeDetected;

    /// <summary>Fires when a play sequence begins (icon picked up). Args: icon name.</summary>
    public event Action<string>? PlayStarted;

    /// <summary>Fires when a play sequence ends (icon dropped).</summary>
    public event Action? PlayEnded;

    /// <summary>Fires when auto-arrange is detected and real mode falls back to fake.</summary>
    public event Action? AutoArrangeFallback;

    public DesktopIconService(DesktopIconConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Initialize the service. Should be called once after construction.
    /// </summary>
    public void Initialize()
    {
        if (!IsEnabled) return;

        _listViewHandle = DesktopIconInterop.GetDesktopListViewHandle();
        if (_listViewHandle == IntPtr.Zero)
        {
            Log.Warning("DesktopIconService: Could not find desktop ListView. Icon interaction disabled.");
            return;
        }

        // Check auto-arrange
        _autoArrangeDetected = DesktopIconInterop.IsAutoArrangeEnabled(_listViewHandle);
        if (_autoArrangeDetected && IsRealMode)
        {
            Log.Warning("DesktopIconService: Auto-arrange detected! Falling back to fake mode.");
            AutoArrangeFallback?.Invoke();
        }

        // Save initial layout for restore (real mode only)
        if (IsRealMode && !_autoArrangeDetected)
        {
            _savedLayout = DesktopIconInterop.SaveLayout(_listViewHandle);
            PersistLayout(_savedLayout);
            Log.Information("DesktopIconService: Saved initial layout ({Count} icons)", _savedLayout.Count);
        }

        Log.Information("DesktopIconService: Initialized. Mode={Mode}, Frequency={Freq}, Icons={Count}, AutoArrange={AA}",
            _config.Mode, _config.Frequency, DesktopIconInterop.GetIconCount(_listViewHandle), _autoArrangeDetected);
    }

    /// <summary>
    /// Main update loop. Called every frame from MainWindow.
    /// Returns true if the pet's movement is being controlled by this service.
    /// </summary>
    public bool Update(double deltaTime, double petScreenX, double petScreenY,
        bool isFullscreen, bool isPaused)
    {
        if (!IsEnabled || _listViewHandle == IntPtr.Zero || isPaused) return false;

        // Refresh handle periodically
        _handleRefreshTimer += deltaTime;
        if (_handleRefreshTimer >= HANDLE_REFRESH_INTERVAL)
        {
            _handleRefreshTimer = 0;
            var newHandle = DesktopIconInterop.GetDesktopListViewHandle();
            if (newHandle != IntPtr.Zero) _listViewHandle = newHandle;
        }

        switch (_state)
        {
            case IconPlayState.Idle:
                if (isFullscreen) return false;
                return false;

            case IconPlayState.PickingUp:
                _playTimer -= deltaTime;
                if (_playTimer <= 0)
                {
                    TransitionToCarrying(petScreenX);
                }
                return true; // Pet should stay still

            case IconPlayState.Carrying:
                _playTimer -= deltaTime;
                _carryOffsetX += _carryDirection * _config.CarryWalkSpeed * deltaTime;

                if (Math.Abs(_carryOffsetX) >= _config.MaxCarryDistance || _playTimer <= 0)
                {
                    TransitionToDropping(petScreenX);
                }
                else if (IsRealMode && !_autoArrangeDetected && _targetIcon != null)
                {
                    // Real mode: move the real icon to follow the pet (same X, original Y)
                    var newIconX = _targetOriginalX + (int)_carryOffsetX;
                    DesktopIconInterop.SetIconPosition(_listViewHandle, _targetIcon.Index,
                        newIconX, _targetOriginalY);
                }
                return true; // Service controls pet movement

            case IconPlayState.Dropping:
                _playTimer -= deltaTime;
                if (_playTimer <= 0)
                {
                    FinishPlay();
                }
                return true;

            case IconPlayState.Cooldown:
                _cooldownTimer -= deltaTime;
                if (_cooldownTimer <= 0)
                {
                    _state = IconPlayState.Idle;
                }
                return false;
        }

        return false;
    }

    /// <summary>
    /// Try to trigger a new icon play behavior. Called from game loop.
    /// Returns true if a play was initiated.
    /// </summary>
    public bool TryStartPlay(double petScreenX, double petScreenY)
    {
        if (!IsEnabled || _state != IconPlayState.Idle || _listViewHandle == IntPtr.Zero)
            return false;

        // Roll frequency check
        if (!RollFrequencyCheck())
            return false;

        // Get available icons
        var icons = DesktopIconInterop.GetAllIcons(_listViewHandle);
        if (icons.Count == 0) return false;

        // Filter blocklisted icons
        var eligible = icons.Where(ic => !IsBlocklisted(ic.Name)).ToList();
        if (eligible.Count == 0) return false;

        // Pick a random icon
        _targetIcon = eligible[_random.Next(eligible.Count)];
        if (_targetIcon == null) return false;

        _targetOriginalX = _targetIcon.X;
        _targetOriginalY = _targetIcon.Y;
        _carryOffsetX = 0;
        _carryDirection = _random.Next(2) == 0 ? 1 : -1;
        _petStartX = petScreenX;
        _playDuration = _config.PlayDurationMin + _random.NextDouble() * (_config.PlayDurationMax - _config.PlayDurationMin);

        // Go straight to PickingUp (skip approach — pet is on taskbar, icon is on desktop)
        _state = IconPlayState.PickingUp;
        _playTimer = 0.5; // Half second to "grab"

        PlayStarted?.Invoke(_targetIcon.Name);
        Log.Debug("DesktopIconService: Picking up icon '{Name}' from ({X},{Y})",
            _targetIcon.Name, _targetIcon.X, _targetIcon.Y);

        return true;
    }

    /// <summary>
    /// Cancel the current play and restore icon if in real mode.
    /// </summary>
    public void CancelPlay()
    {
        if (_state == IconPlayState.Idle || _state == IconPlayState.Cooldown) return;

        // Restore icon to original position if real mode
        if (IsRealMode && !_autoArrangeDetected && _targetIcon != null)
        {
            DesktopIconInterop.SetIconPosition(_listViewHandle, _targetIcon.Index,
                _targetOriginalX, _targetOriginalY);
        }

        _targetIcon = null;
        _state = IconPlayState.Cooldown;
        _cooldownTimer = GetCooldown();
        PlayEnded?.Invoke();
    }

    /// <summary>
    /// Restore all icons to their saved layout positions (for Settings button).
    /// </summary>
    public int RestoreLayout()
    {
        if (_savedLayout == null || _savedLayout.Count == 0 || _listViewHandle == IntPtr.Zero)
            return 0;

        var persisted = LoadPersistedLayout();
        var layout = persisted ?? _savedLayout;

        return DesktopIconInterop.RestoreLayout(_listViewHandle, layout);
    }

    /// <summary>Check if the saved layout data exists.</summary>
    public bool HasSavedLayout => _savedLayout != null && _savedLayout.Count > 0;

    /// <summary>The carry direction (-1 or 1) for pet movement during Carrying state.</summary>
    public int CarryDirection => _carryDirection;

    // ── Private helpers ──

    private void TransitionToCarrying(double petScreenX)
    {
        _state = IconPlayState.Carrying;
        _playTimer = _playDuration;
        _petStartX = petScreenX;
        _carryOffsetX = 0;

        Log.Debug("DesktopIconService: Now carrying '{Name}' (direction={Dir})", _targetIcon?.Name, _carryDirection);
    }

    private void TransitionToDropping(double petScreenX)
    {
        _state = IconPlayState.Dropping;
        _playTimer = 0.3;
        Log.Debug("DesktopIconService: Dropping '{Name}' (carried {Dist:F0}px)", _targetIcon?.Name, Math.Abs(_carryOffsetX));
    }

    private void FinishPlay()
    {
        if (IsRealMode && !_autoArrangeDetected && _targetIcon != null)
        {
            // Real mode: icon stays at new position, update saved layout
            var newX = _targetOriginalX + (int)_carryOffsetX;
            if (_savedLayout != null)
            {
                _savedLayout[_targetIcon.Name] = (newX, _targetOriginalY);
            }
        }

        Log.Debug("DesktopIconService: Play finished with '{Name}', entering cooldown", _targetIcon?.Name);

        _targetIcon = null;
        _state = IconPlayState.Cooldown;
        _cooldownTimer = GetCooldown();
        PlayEnded?.Invoke();
        _sfxService?.PlayConfirm();
    }

    private UiSfxService? _sfxService;
    public void SetSfxService(UiSfxService? sfx) => _sfxService = sfx;

    private double GetCooldown()
    {
        return _config.Frequency.ToLowerInvariant() switch
        {
            "always" => COOLDOWN_ALWAYS,
            "sometimes" => COOLDOWN_SOMETIMES,
            _ => COOLDOWN_RARELY
        };
    }

    private bool RollFrequencyCheck()
    {
        // Per-call chance (called when pet pauses from SmartMovement or when idle)
        double chance = _config.Frequency.ToLowerInvariant() switch
        {
            "rarely" => 0.08,
            "sometimes" => 0.25,
            "always" => 0.60,
            _ => 0.0
        };
        return _random.NextDouble() < chance;
    }

    private bool IsBlocklisted(string iconName)
    {
        if (_config.Blocklist == null || _config.Blocklist.Length == 0) return false;
        return _config.Blocklist.Any(b =>
            iconName.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    // ── Layout persistence (JSON file in %AppData%/Pokebar/) ──

    private static readonly string LayoutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Pokebar", "desktop_icon_layout.json");

    private static void PersistLayout(Dictionary<string, (int X, int Y)> layout)
    {
        try
        {
            var dir = Path.GetDirectoryName(LayoutPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var dict = layout.ToDictionary(kv => kv.Key, kv => new int[] { kv.Value.X, kv.Value.Y });
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LayoutPath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopIconService: Failed to persist layout");
        }
    }

    private static Dictionary<string, (int X, int Y)>? LoadPersistedLayout()
    {
        try
        {
            if (!File.Exists(LayoutPath)) return null;
            var json = File.ReadAllText(LayoutPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, int[]>>(json);
            if (dict == null) return null;
            return dict.ToDictionary(kv => kv.Key, kv => (kv.Value[0], kv.Value[1]));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopIconService: Failed to load persisted layout");
            return null;
        }
    }
}
