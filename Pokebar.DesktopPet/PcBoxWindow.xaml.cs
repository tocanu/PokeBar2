using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Pokebar.Core.Localization;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;
using Pokebar.DesktopPet.Services;

namespace Pokebar.DesktopPet;

public partial class PcBoxWindow : Window
{
    private readonly SpriteCache _spriteCache;
    private readonly GameplayConfig _config;
    private readonly int _activeDex;
    private readonly IReadOnlyList<int> _party;
    private readonly ContextMenu _gearMenu = new();
    private MenuItem _pauseMenuItem = null!;
    private MenuItem _pokeballMenuItem = null!;
    private MenuItem _silenceMenuItem = null!;
    private MenuItem _blockSpawnsMenuItem = null!;
    private bool _isPaused;
    private bool _isSilenceNotifications;
    private bool _isBlockSpawns;
    private int _pokeballCount;
    private int _selectedDex = -1;
    private int _currentPage;
    private const int PAGE_SIZE = 30; // 6 columns x 5 rows
    private bool _showingHistory;
    private readonly IReadOnlyList<CaptureHistoryEntry> _captureHistory;
    private readonly UiSfxService? _sfx;

    // Clip cache: evita chamar GetAnimations repetidamente para o mesmo dex
    private readonly Dictionary<int, AnimationClip?> _clipByDex = new();
    private System.Windows.Threading.DispatcherTimer? _animTimer;

    // Lista atual de itens — mantida como campo para atualização pontual sem recriar tudo
    private List<PcBoxItem> _pageItems = new();

    /// <summary>Dex escolhido pelo jogador, ou -1 se nenhum.</summary>
    public int ChosenDex => _selectedDex;

    /// <summary>Ações do tray expostas no menu de engrenagem.</summary>
    public event Action? PetRequested;
    public event Action? PauseResumeRequested;
    public event Action? DiagnosticRequested;
    public event Action? SettingsRequested;
    public event Action? ScreenshotRequested;
    public event Action? SilenceNotificationsToggled;
    public event Action? BlockSpawnsToggled;
    public event Action? QuitRequested;

    // Fire Red palette — brushes frozen para melhor performance de rendering
    private  static readonly SolidColorBrush BrushCardNormal    = Frozen(0x40, 0xF8, 0xF8, 0xF0);
    private  static readonly SolidColorBrush BrushCardActive    = Frozen(0x60, 0xF8, 0xD0, 0x30);
    private  static readonly SolidColorBrush BrushCardSelected  = Frozen(0x80, 0x88, 0xC8, 0xE8);
    internal static readonly SolidColorBrush BrushCardEmpty     = Frozen(0x20, 0x80, 0xC0, 0x60);
    private  static readonly SolidColorBrush BrushBorderNormal  = Frozen(0x60, 0x98, 0xB8, 0x88);
    private  static readonly SolidColorBrush BrushBorderActive  = Frozen(0xFF, 0xE0, 0x40, 0x38);
    private  static readonly SolidColorBrush BrushBorderSelected= Frozen(0xFF, 0x38, 0x90, 0xF8);
    internal static readonly SolidColorBrush BrushBorderEmpty   = Frozen(0x30, 0x98, 0xB8, 0x88);

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public PcBoxWindow(
        IReadOnlyList<int> party,
        int activeDex,
        SpriteCache spriteCache,
        GameplayConfig config,
        int pokeballCount = 0,
        bool isPaused = false,
        bool isSilenceNotifications = false,
        bool isBlockSpawns = false,
        IReadOnlyList<CaptureHistoryEntry>? captureHistory = null,
        UiSfxService? sfx = null)
    {
        InitializeComponent();
        _spriteCache = spriteCache;
        _config = config;
        _activeDex = activeDex;
        _party = party;
        _pokeballCount = pokeballCount;
        _isPaused = isPaused;
        _isSilenceNotifications = isSilenceNotifications;
        _isBlockSpawns = isBlockSpawns;
        _captureHistory = captureHistory ?? Array.Empty<CaptureHistoryEntry>();
        _sfx = sfx;

        ApplyLocale();
        BuildGearMenu();
        ShowPage(0);

        // Timer de animação: avança frames dos sprites na grid a ~7 fps (150 ms/frame)
        _animTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _animTimer.Tick += (_, _) => { foreach (var item in _pageItems) item.AdvanceFrame(); };
        _animTimer.Start();
        Closed += (_, _) => _animTimer.Stop();
    }

    private void ApplyLocale()
    {
        SelectButton.Content = Localizer.Get("pcbox.select").ToUpperInvariant();
        CloseButton.Content = Localizer.Get("pcbox.close").ToUpperInvariant();
        GearButton.ToolTip = Localizer.Get("tray.settings");
    }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_party.Count / (double)PAGE_SIZE));

    /// <summary>
    /// Busca o clip de animação com cache.
    /// Prefere Idle; fallback para WalkFallback, WalkRight.
    /// Retorna null se não há sprites disponíveis.
    /// </summary>
    private AnimationClip? GetClipCached(int dex)
    {
        if (_clipByDex.TryGetValue(dex, out var cached))
            return cached;
        AnimationClip? clip = null;
        try
        {
            var anims = _spriteCache.GetAnimations(dex, "0000", _config);
            clip = anims.Idle ?? anims.WalkFallback ?? anims.WalkRight;
        }
        catch { /* sem sprite */ }
        _clipByDex[dex] = clip;
        return clip;
    }

    private (SolidColorBrush bg, SolidColorBrush border) GetBrushes(int dex)
    {
        if (dex <= 0)                  return (BrushCardEmpty,    BrushBorderEmpty);
        if (dex == _selectedDex)       return (BrushCardSelected, BrushBorderSelected);
        if (dex == _activeDex)         return (BrushCardActive,   BrushBorderActive);
        return (BrushCardNormal, BrushBorderNormal);
    }

    private void ShowPage(int page)
    {
        _currentPage = Math.Clamp(page, 0, TotalPages - 1);
        var start = _currentPage * PAGE_SIZE;
        var end = Math.Min(start + PAGE_SIZE, _party.Count);

        // Update page indicator
        PageText.Text = $"{start + 1:D3}-{end:D3}";
        PrevButton.IsEnabled = _currentPage > 0;
        NextButton.IsEnabled = _currentPage < TotalPages - 1;

        // Build items for this page (sprites via cache)
        var items = new List<PcBoxItem>(PAGE_SIZE);
        for (int i = start; i < end; i++)
        {
            var dex = _party[i];
            var (bg, border) = GetBrushes(dex);
            items.Add(new PcBoxItem
            {
                Dex = dex,
                Clip = GetClipCached(dex),
                Label = $"#{dex:D3}",
                Background = bg,
                BorderColor = border
            });
        }

        // Pad to 30 with empty slots (reuse static brushes)
        while (items.Count < PAGE_SIZE)
            items.Add(PcBoxItem.Empty);

        _pageItems = items;
        PokemonGrid.ItemsSource = _pageItems;
    }

    private void OnPokemonClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not int dex || dex <= 0)
            return;

        _sfx?.PlaySelect();

        var prevDex = _selectedDex;
        _selectedDex = dex;
        SelectButton.IsEnabled = true;

        var label = dex == _activeDex
            ? Localizer.Get("pcbox.active", dex.ToString("D3"))
            : $"#{dex:D3}";
        SelectedText.Text = Localizer.Get("pcbox.selected", label);

        // Atualiza apenas os dois itens afetados — sem recriar a lista inteira
        foreach (var item in _pageItems)
        {
            if (item.Dex == prevDex || item.Dex == dex)
            {
                var (bg, border) = GetBrushes(item.Dex);
                item.Background  = bg;
                item.BorderColor = border;
            }
        }
    }

    private void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            _sfx?.PlaySelect();
            ShowPage(_currentPage - 1);
        }
    }

    private void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (_currentPage < TotalPages - 1)
        {
            _sfx?.PlaySelect();
            ShowPage(_currentPage + 1);
        }
    }

    private void OnWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        _sfx?.PlaySelect();
        RefreshGearMenu();
        _gearMenu.PlacementTarget = GearButton;
        _gearMenu.Placement = PlacementMode.Bottom;
        _gearMenu.IsOpen = true;
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        if (_selectedDex > 0)
        {
            _sfx?.PlayConfirm();
            DialogResult = true;
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _sfx?.PlayCancel();
        DialogResult = false;
        Close();
    }

    private void OnHistoryToggle(object sender, RoutedEventArgs e)
    {
        _sfx?.PlaySelect();
        _showingHistory = !_showingHistory;
        if (_showingHistory)
        {
            ShowHistory();
            HistoryButton.Content = "📦 BOX";
            SelectButton.IsEnabled = false;
            PageText.Text = Localizer.Get("pcbox.history_title");
            PrevButton.IsEnabled = false;
            NextButton.IsEnabled = false;
        }
        else
        {
            ShowPage(_currentPage);
            HistoryButton.Content = $"📜 {Localizer.Get("pcbox.history").ToUpperInvariant()}";
        }
    }

    // Brushes de raridade para o histórico — frozen, criados uma única vez
    private static readonly SolidColorBrush BrushHistShiny      = Frozen(0x60, 0xF8, 0xD0, 0x30);
    private static readonly SolidColorBrush BrushHistNormal      = Frozen(0x40, 0xF8, 0xF8, 0xF0);
    private static readonly SolidColorBrush BrushHistBorderShiny = Frozen(0xFF, 0xF8, 0xD0, 0x30);
    private static readonly SolidColorBrush BrushHistLegendary   = Frozen(0xFF, 0xE0, 0x40, 0x38);
    private static readonly SolidColorBrush BrushHistEpic        = Frozen(0xFF, 0xA0, 0x40, 0xD0);
    private static readonly SolidColorBrush BrushHistRare        = Frozen(0xFF, 0x38, 0x90, 0xF8);
    private static readonly SolidColorBrush BrushHistUncommon    = Frozen(0xFF, 0x40, 0xC8, 0x40);
    private static readonly SolidColorBrush BrushHistCommon      = Frozen(0x60, 0x98, 0xB8, 0x88);

    private void ShowHistory()
    {
        var entries = _captureHistory.Reverse().Take(PAGE_SIZE).ToList();
        var items = new List<PcBoxItem>(PAGE_SIZE);

        foreach (var entry in entries)
        {
            var bg     = entry.IsShiny ? BrushHistShiny : BrushHistNormal;
            var border = entry.IsShiny ? BrushHistBorderShiny : entry.Rarity switch
            {
                "Legendary" => BrushHistLegendary,
                "Epic"      => BrushHistEpic,
                "Rare"      => BrushHistRare,
                "Uncommon"  => BrushHistUncommon,
                _           => BrushHistCommon
            };
            var label = entry.IsShiny ? $"★#{entry.Dex:D3}" : $"#{entry.Dex:D3}";

            items.Add(new PcBoxItem
            {
                Dex        = entry.Dex,
                Clip       = GetClipCached(entry.Dex),
                Label      = label,
                Background = bg,
                BorderColor= border
            });
        }

        while (items.Count < PAGE_SIZE)
            items.Add(PcBoxItem.Empty);

        _pageItems = items;
        PokemonGrid.ItemsSource = _pageItems;
        SelectedText.Text = entries.Count > 0
            ? $"{entries.Count} {Localizer.Get("pcbox.recent_captures")}"
            : Localizer.Get("pcbox.no_captures");
    }

    private void BuildGearMenu()
    {
        _gearMenu.Items.Clear();

        _pokeballMenuItem = new MenuItem { IsEnabled = false };
        _gearMenu.Items.Add(_pokeballMenuItem);
        _gearMenu.Items.Add(new Separator());

        var petItem = new MenuItem { Header = Localizer.Get("tray.pet") };
        petItem.Click += (_, _) => PetRequested?.Invoke();
        _gearMenu.Items.Add(petItem);

        _pauseMenuItem = new MenuItem();
        _pauseMenuItem.Click += (_, _) =>
        {
            PauseResumeRequested?.Invoke();
            _isPaused = !_isPaused;
            RefreshGearMenu();
        };
        _gearMenu.Items.Add(_pauseMenuItem);

        var diagItem = new MenuItem { Header = Localizer.Get("tray.diagnostic") };
        diagItem.Click += (_, _) => DiagnosticRequested?.Invoke();
        _gearMenu.Items.Add(diagItem);

        var pcItem = new MenuItem
        {
            Header = Localizer.Get("tray.pcbox"),
            IsEnabled = false
        };
        _gearMenu.Items.Add(pcItem);

        var settingsItem = new MenuItem { Header = Localizer.Get("tray.settings") };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        _gearMenu.Items.Add(settingsItem);

        var screenshotItem = new MenuItem { Header = Localizer.Get("tray.screenshot") };
        screenshotItem.Click += (_, _) => ScreenshotRequested?.Invoke();
        _gearMenu.Items.Add(screenshotItem);

        _gearMenu.Items.Add(new Separator());

        _silenceMenuItem = new MenuItem
        {
            Header = Localizer.Get("tray.silence"),
            IsCheckable = true
        };
        _silenceMenuItem.Click += (_, _) =>
        {
            SilenceNotificationsToggled?.Invoke();
            _isSilenceNotifications = !_isSilenceNotifications;
            RefreshGearMenu();
        };
        _gearMenu.Items.Add(_silenceMenuItem);

        _blockSpawnsMenuItem = new MenuItem
        {
            Header = Localizer.Get("tray.blockspawns"),
            IsCheckable = true
        };
        _blockSpawnsMenuItem.Click += (_, _) =>
        {
            BlockSpawnsToggled?.Invoke();
            _isBlockSpawns = !_isBlockSpawns;
            RefreshGearMenu();
        };
        _gearMenu.Items.Add(_blockSpawnsMenuItem);

        _gearMenu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = Localizer.Get("tray.quit") };
        quitItem.Click += (_, _) => QuitRequested?.Invoke();
        _gearMenu.Items.Add(quitItem);

        RefreshGearMenu();
    }

    private void RefreshGearMenu()
    {
        if (_pokeballMenuItem != null)
            _pokeballMenuItem.Header = $"Pokeballs: {_pokeballCount}";

        if (_pauseMenuItem != null)
        {
            _pauseMenuItem.Header = _isPaused
                ? Localizer.Get("tray.resume")
                : Localizer.Get("tray.pause");
        }

        if (_silenceMenuItem != null)
            _silenceMenuItem.IsChecked = _isSilenceNotifications;

        if (_blockSpawnsMenuItem != null)
            _blockSpawnsMenuItem.IsChecked = _isBlockSpawns;
    }
}

public class PcBoxItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Dex { get; init; }
    public string Label { get; init; } = string.Empty;

    // Clip de animação: Sprite é o frame atual, avançado pelo timer da janela
    private AnimationClip? _clip;
    private int _clipFrame;

    public AnimationClip? Clip
    {
        get => _clip;
        init => _clip = value;
    }

    /// <summary>Frame atual do clip de animação. Atualiza a binding de Sprite.</summary>
    public BitmapSource? Sprite => _clip?.Frames.Count > 0
        ? _clip.Frames[_clipFrame % _clip.Frames.Count]
        : null;

    /// <summary>Avança um frame da animação e notifica o binding.</summary>
    public void AdvanceFrame()
    {
        if (_clip == null || _clip.Frames.Count <= 1) return;
        _clipFrame = (_clipFrame + 1) % _clip.Frames.Count;
        Notify(nameof(Sprite));
    }

    private SolidColorBrush _background = new();
    public SolidColorBrush Background
    {
        get => _background;
        set { if (!ReferenceEquals(_background, value)) { _background = value; Notify(); } }
    }

    private SolidColorBrush _borderColor = new();
    public SolidColorBrush BorderColor
    {
        get => _borderColor;
        set { if (!ReferenceEquals(_borderColor, value)) { _borderColor = value; Notify(); } }
    }

    /// <summary>Slot vazio pré-alocado (imutável, seguro para reusar).</summary>
    public static readonly PcBoxItem Empty = new()
    {
        Background  = PcBoxWindow.BrushCardEmpty,
        BorderColor = PcBoxWindow.BrushBorderEmpty
    };
}
