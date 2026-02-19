using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Pokebar.Core.Localization;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;

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

    // Fire Red palette colors
    private static readonly System.Windows.Media.Color CardNormal = System.Windows.Media.Color.FromArgb(0x40, 0xF8, 0xF8, 0xF0);
    private static readonly System.Windows.Media.Color CardActive = System.Windows.Media.Color.FromArgb(0x60, 0xF8, 0xD0, 0x30);
    private static readonly System.Windows.Media.Color CardSelected = System.Windows.Media.Color.FromArgb(0x80, 0x88, 0xC8, 0xE8);
    private static readonly System.Windows.Media.Color BorderNormal = System.Windows.Media.Color.FromArgb(0x60, 0x98, 0xB8, 0x88);
    private static readonly System.Windows.Media.Color BorderActive = System.Windows.Media.Color.FromRgb(0xE0, 0x40, 0x38);
    private static readonly System.Windows.Media.Color BorderSelected = System.Windows.Media.Color.FromRgb(0x38, 0x90, 0xF8);

    public PcBoxWindow(
        IReadOnlyList<int> party,
        int activeDex,
        SpriteCache spriteCache,
        GameplayConfig config,
        int pokeballCount = 0,
        bool isPaused = false,
        bool isSilenceNotifications = false,
        bool isBlockSpawns = false)
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

        ApplyLocale();
        BuildGearMenu();
        ShowPage(0);
    }

    private void ApplyLocale()
    {
        SelectButton.Content = Localizer.Get("pcbox.select").ToUpperInvariant();
        CloseButton.Content = Localizer.Get("pcbox.close").ToUpperInvariant();
        GearButton.ToolTip = Localizer.Get("tray.settings");
    }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(_party.Count / (double)PAGE_SIZE));

    private void ShowPage(int page)
    {
        _currentPage = Math.Clamp(page, 0, TotalPages - 1);
        var start = _currentPage * PAGE_SIZE;
        var end = Math.Min(start + PAGE_SIZE, _party.Count);

        // Update page indicator
        PageText.Text = $"{start + 1:D3}-{end:D3}";
        PrevButton.IsEnabled = _currentPage > 0;
        NextButton.IsEnabled = _currentPage < TotalPages - 1;

        // Build items for this page
        var items = new List<PcBoxItem>();
        for (int i = start; i < end; i++)
        {
            var dex = _party[i];
            BitmapSource? sprite = null;
            try
            {
                var anims = _spriteCache.GetAnimations(dex, "0000", _config);
                if (anims.Idle?.Frames.Count > 0)
                    sprite = anims.Idle.Frames[0];
                else if (anims.WalkRight?.Frames.Count > 0)
                    sprite = anims.WalkRight.Frames[0];
            }
            catch { /* sem sprite */ }

            var isActive = dex == _activeDex;
            var isSelected = dex == _selectedDex;
            items.Add(new PcBoxItem
            {
                Dex = dex,
                Sprite = sprite,
                Label = $"#{dex:D3}",
                Background = new SolidColorBrush(isSelected ? CardSelected : isActive ? CardActive : CardNormal),
                BorderColor = new SolidColorBrush(isSelected ? BorderSelected : isActive ? BorderActive : BorderNormal)
            });
        }

        // Pad to 30 with empty slots
        while (items.Count < PAGE_SIZE)
        {
            items.Add(new PcBoxItem
            {
                Dex = -1,
                Sprite = null,
                Label = "",
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x80, 0xC0, 0x60)),
                BorderColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x98, 0xB8, 0x88))
            });
        }

        PokemonGrid.ItemsSource = items;
    }

    private void OnPokemonClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is int dex && dex > 0)
        {
            _selectedDex = dex;
            SelectButton.IsEnabled = true;

            var label = dex == _activeDex
                ? Localizer.Get("pcbox.active", dex.ToString("D3"))
                : $"#{dex:D3}";
            SelectedText.Text = Localizer.Get("pcbox.selected", label);

            // Refresh visual selection
            ShowPage(_currentPage);
        }
    }

    private void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
            ShowPage(_currentPage - 1);
    }

    private void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (_currentPage < TotalPages - 1)
            ShowPage(_currentPage + 1);
    }

    private void OnWindowDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        RefreshGearMenu();
        _gearMenu.PlacementTarget = GearButton;
        _gearMenu.Placement = PlacementMode.Bottom;
        _gearMenu.IsOpen = true;
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        if (_selectedDex > 0)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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

public class PcBoxItem
{
    public int Dex { get; set; }
    public BitmapSource? Sprite { get; set; }
    public string Label { get; set; } = string.Empty;
    public SolidColorBrush Background { get; set; } = new SolidColorBrush();
    public SolidColorBrush BorderColor { get; set; } = new SolidColorBrush(System.Windows.Media.Colors.Gray);
}
