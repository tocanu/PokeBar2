using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using Pokebar.Core.Localization;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet;

public partial class SettingsWindow : Window
{
    private readonly GameplayConfig _config;
    private readonly AppSettings _appSettings;
    private readonly SaveData _saveData;
    private readonly SpriteCache _spriteCache;
    private readonly List<Achievement> _allAchievements;
    private readonly HashSet<int> _pokedexSeen;
    private readonly HashSet<int> _pokedexCaptured;

    // Valores editados
    private double _editedSpeed;
    private bool _editedSilenceNotifications;
    private bool _editedBlockSpawns;
    private bool _editedMinimal;
    private bool _editedToast;
    private bool _editedTray;
    private bool _editedDebug;
    private string? _editedLanguage;
    private string? _editedProfileId;
    private string _editedIconMode = "off";
    private string _editedIconFreq = "rarely";
    private bool _dirty;

    /// <summary>True se houve mudanças que requerem restart.</summary>
    public bool NeedsRestart { get; private set; }

    /// <summary>Config editado para aplicar.</summary>
    public GameplayConfig? EditedConfig { get; private set; }

    /// <summary>AppSettings editado para salvar.</summary>
    public AppSettings? EditedSettings { get; private set; }

    public SettingsWindow(
        GameplayConfig config,
        AppSettings appSettings,
        SaveData saveData,
        SpriteCache spriteCache,
        int activeDex,
        List<Achievement>? achievements = null,
        HashSet<int>? pokedexSeen = null,
        HashSet<int>? pokedexCaptured = null)
    {
        InitializeComponent();

        _config = config;
        _appSettings = appSettings;
        _saveData = saveData;
        _spriteCache = spriteCache;
        _allAchievements = achievements ?? new List<Achievement>();
        _pokedexSeen = pokedexSeen ?? new HashSet<int>();
        _pokedexCaptured = pokedexCaptured ?? new HashSet<int>();

        LoadPreview(activeDex);
        LoadCurrentValues();
        PopulateProfiles();
        PopulateStats();
        PopulatePokedex();
        PopulateAchievements();
        ApplyLocale();
    }

    private void ApplyLocale()
    {
        HeaderText.Text = Localizer.Get("settings.title");
        TabGeneral.Header = Localizer.Get("settings.tab_general");
        TabBehavior.Header = Localizer.Get("settings.tab_behavior");
        TabStats.Header = Localizer.Get("settings.tab_stats");
        TabPokedex.Header = Localizer.Get("settings.tab_pokedex");
        TabAchievements.Header = Localizer.Get("settings.tab_achievements");
        LblProfile.Text = Localizer.Get("settings.profile");
        LblLanguage.Text = Localizer.Get("settings.language");
        LblSpeed.Text = Localizer.Get("settings.speed");
        ChkSilenceNotifications.Content = Localizer.Get("settings.silence");
        SilenceDescription.Text = Localizer.Get("settings.silence_desc");
        ChkBlockSpawns.Content = Localizer.Get("settings.blockspawns");
        BlockSpawnsDescription.Text = Localizer.Get("settings.blockspawns_desc");
        ChkMinimal.Content = Localizer.Get("settings.minimal");
        MinimalDescription.Text = Localizer.Get("settings.minimal_desc");
        ChkToast.Content = Localizer.Get("settings.toast");
        ChkTray.Content = Localizer.Get("settings.tray");
        ChkDebug.Content = Localizer.Get("settings.debug");
        SaveButton.Content = Localizer.Get("settings.save");
        CancelButton.Content = Localizer.Get("settings.cancel");
    }

    private void LoadPreview(int dex)
    {
        try
        {
            var anims = _spriteCache.GetAnimations(dex, "0000", _config);
            if (anims.Idle?.Frames.Count > 0)
                PreviewImage.Source = anims.Idle.Frames[0];
            else if (anims.WalkRight?.Frames.Count > 0)
                PreviewImage.Source = anims.WalkRight.Frames[0];
        }
        catch { /* sem preview */ }
    }

    private void LoadCurrentValues()
    {
        _editedSpeed = _config.Player.WalkSpeed;
        _editedSilenceNotifications = _config.Windows.SilenceNotifications;
        _editedBlockSpawns = _config.Windows.BlockSpawns;
        _editedMinimal = _config.Windows.MinimalMode;
        _editedToast = _config.Windows.ToastNotificationsEnabled;
        _editedTray = _config.Windows.TrayIconEnabled;
        _editedDebug = _config.Performance.DebugOverlay;
        _editedLanguage = _appSettings.Language;
        _editedProfileId = _appSettings.ActiveProfileId;

        SpeedSlider.Value = _editedSpeed;
        SpeedValue.Text = $"{_editedSpeed:F0}";
        ChkSilenceNotifications.IsChecked = _editedSilenceNotifications;
        ChkBlockSpawns.IsChecked = _editedBlockSpawns;
        ChkMinimal.IsChecked = _editedMinimal;
        ChkToast.IsChecked = _editedToast;
        ChkTray.IsChecked = _editedTray;
        ChkDebug.IsChecked = _editedDebug;

        // Language combo
        for (int i = 0; i < LanguageCombo.Items.Count; i++)
        {
            if (LanguageCombo.Items[i] is ComboBoxItem item && item.Tag is string tag)
            {
                // Detectar cultura ativa
                var activeLang = _appSettings.Language ?? System.Globalization.CultureInfo.CurrentCulture.Name;
                if (tag.Equals(activeLang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Desktop icon mode/frequency combos
        _editedIconMode = _config.DesktopIcons.Mode;
        _editedIconFreq = _config.DesktopIcons.Frequency;
        for (int i = 0; i < IconModeCombo.Items.Count; i++)
        {
            if (IconModeCombo.Items[i] is ComboBoxItem item && item.Tag is string tag &&
                tag.Equals(_editedIconMode, StringComparison.OrdinalIgnoreCase))
            {
                IconModeCombo.SelectedIndex = i;
                break;
            }
        }
        for (int i = 0; i < IconFreqCombo.Items.Count; i++)
        {
            if (IconFreqCombo.Items[i] is ComboBoxItem item && item.Tag is string tag &&
                tag.Equals(_editedIconFreq, StringComparison.OrdinalIgnoreCase))
            {
                IconFreqCombo.SelectedIndex = i;
                break;
            }
        }
    }

    private void PopulateProfiles()
    {
        ProfileCombo.Items.Clear();
        var profiles = ProfileManager.GetProfiles(_appSettings);
        int selectedIdx = 0;
        for (int i = 0; i < profiles.Count; i++)
        {
            var p = profiles[i];
            var name = p.Name.StartsWith("profile.") ? Localizer.Get(p.Name) : p.Name;
            ProfileCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{p.Icon} {name}",
                Tag = p.Id
            });
            if (p.Id == _appSettings.ActiveProfileId)
                selectedIdx = i;
        }
        ProfileCombo.SelectedIndex = selectedIdx;
    }

    private void PopulateStats()
    {
        StatsPanel.Children.Clear();
        var stats = _saveData.Stats;
        AddStatRow("🏆", Localizer.Get("stats.captured"), stats.TotalCaptured.ToString());
        AddStatRow("❌", Localizer.Get("stats.capture_failed"), stats.TotalCaptureFailed.ToString());
        AddStatRow("⚔", Localizer.Get("stats.battles"), stats.TotalBattles.ToString());
        AddStatRow("🥇", Localizer.Get("stats.battles_won"), stats.TotalBattlesWon.ToString());
        AddStatRow("�", Localizer.Get("stats.battles_lost"), stats.TotalBattlesLost.ToString());
        AddStatRow("🔴", Localizer.Get("stats.pokeballs_used"), stats.TotalPokeballsUsed.ToString());
        AddStatRow("✨", Localizer.Get("stats.shiny_captured"), stats.TotalShinyCaptured.ToString());
        AddStatRow("🚶", Localizer.Get("stats.distance_walked"), FormatDistance(stats.TotalDistanceWalked));
        AddStatRow("📅", Localizer.Get("stats.daily_streak"), $"{_saveData.DailyStreak} {Localizer.Get("stats.days")}");
        AddStatRow("📋", Localizer.Get("stats.daily_quests_done"), stats.TotalDailyQuestsCompleted.ToString());
        AddStatRow("⏱", Localizer.Get("stats.playtime"), FormatPlaytime(stats.TotalPlayTimeSeconds));
        AddStatRow("📦", Localizer.Get("stats.party_size"), _saveData.Party.Count.ToString());
        AddStatRow("🎮", Localizer.Get("stats.pokeballs"), _saveData.Pokeballs.ToString());
        AddStatRow("👁", Localizer.Get("stats.pokedex_seen"), _pokedexSeen.Count.ToString());
        AddStatRow("📕", Localizer.Get("stats.pokedex_captured"), _pokedexCaptured.Count.ToString());
    }

    private void AddStatRow(string icon, string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconTb = new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(iconTb, 0);

        var labelTb = new TextBlock { Text = label, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x38, 0x48)), FontSize = 13, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(labelTb, 1);

        var valueTb = new TextBlock { Text = value, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x50, 0x60, 0x50)), FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(valueTb, 2);

        grid.Children.Add(iconTb);
        grid.Children.Add(labelTb);
        grid.Children.Add(valueTb);

        StatsPanel.Children.Add(grid);
    }

    private static string FormatPlaytime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
            : $"{ts.Minutes}m {ts.Seconds}s";
    }

    private static string FormatDistance(double pixels)
    {
        if (pixels >= 100000)
            return $"{pixels / 1000.0:F1}k px";
        return $"{pixels:F0} px";
    }

    private void PopulatePokedex()
    {
        PokedexPanel.Children.Clear();

        // Header summary
        var summaryGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var seenTb = new TextBlock
        {
            Text = $"👁 {Localizer.Get("pokedex.seen")}: {_pokedexSeen.Count}",
            FontSize = 14,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x38, 0x48)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(seenTb, 0);

        var capturedTb = new TextBlock
        {
            Text = $"📕 {Localizer.Get("pokedex.captured")}: {_pokedexCaptured.Count}",
            FontSize = 14,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x38, 0x48)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(capturedTb, 1);

        summaryGrid.Children.Add(seenTb);
        summaryGrid.Children.Add(capturedTb);
        PokedexPanel.Children.Add(summaryGrid);

        // Grid de dex entries (mini sprites)
        var wrapPanel = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };

        // Mostrar todos os dex vistos ou capturados, ordenados
        var allDex = new SortedSet<int>(_pokedexSeen);
        foreach (var d in _pokedexCaptured) allDex.Add(d);

        foreach (var dex in allDex)
        {
            var isCaptured = _pokedexCaptured.Contains(dex);
            var isSeen = _pokedexSeen.Contains(dex);

            BitmapSource? sprite = null;
            if (isCaptured)
            {
                try
                {
                    var anims = _spriteCache.GetAnimations(dex, "0000", _config);
                    sprite = anims.Idle?.Frames.Count > 0 ? anims.Idle.Frames[0]
                           : anims.WalkRight?.Frames.Count > 0 ? anims.WalkRight.Frames[0]
                           : null;
                }
                catch { /* sem sprite */ }
            }

            var bgColor = isCaptured
                ? System.Windows.Media.Color.FromArgb(0x60, 0xF8, 0xD0, 0x30)  // gold
                : System.Windows.Media.Color.FromArgb(0x40, 0xA0, 0xA0, 0xA0); // gray

            var borderColor = isCaptured
                ? System.Windows.Media.Color.FromRgb(0xE0, 0x40, 0x38)
                : System.Windows.Media.Color.FromRgb(0x80, 0x90, 0x80);

            var border = new Border
            {
                Width = 42,
                Height = 42,
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                SnapsToDevicePixels = true,
                ToolTip = isCaptured ? $"#{dex:D3} ✓" : $"#{dex:D3} ?"
            };

            var grid = new Grid();

            if (sprite != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = sprite,
                    Width = 32,
                    Height = 32,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(img, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                grid.Children.Add(img);
            }
            else
            {
                // Silhouette/question mark for seen-only
                var qm = new TextBlock
                {
                    Text = "?",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0x70, 0x60)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                grid.Children.Add(qm);
            }

            // Dex number label
            var label = new TextBlock
            {
                Text = $"{dex:D3}",
                FontSize = 7,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x38, 0x48)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xB0, 0xD8, 0xE8, 0xD0))
            };
            grid.Children.Add(label);

            border.Child = grid;
            wrapPanel.Children.Add(border);
        }

        if (allDex.Count == 0)
        {
            var emptyTb = new TextBlock
            {
                Text = Localizer.Get("pokedex.empty"),
                FontSize = 13,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x90, 0x80)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            PokedexPanel.Children.Add(emptyTb);
        }
        else
        {
            PokedexPanel.Children.Add(wrapPanel);
        }
    }

    private void PopulateAchievements()
    {
        var items = new List<AchievementDisplayItem>();
        var unlocked = _saveData.Achievements ?? new List<string>();

        foreach (var a in _allAchievements)
        {
            var isUnlocked = unlocked.Contains(a.Id);
            items.Add(new AchievementDisplayItem
            {
                Icon = isUnlocked ? a.Icon : "🔒",
                Title = Localizer.Get(a.TitleKey),
                Description = Localizer.Get(a.DescriptionKey),
                Background = isUnlocked
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xD0, 0x30))  // gold
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xD8, 0xC0))  // muted green
            });
        }

        AchievementsList.ItemsSource = items;
    }

    // Event handlers
    private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            _editedProfileId = id;
            _dirty = true;
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _editedLanguage = lang;
            _dirty = true;
        }
    }

    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _editedSpeed = Math.Round(SpeedSlider.Value);
        if (SpeedValue != null)
            SpeedValue.Text = $"{_editedSpeed:F0}";
        _dirty = true;
    }

    private void OnSilenceNotificationsChanged(object sender, RoutedEventArgs e)
    {
        _editedSilenceNotifications = ChkSilenceNotifications.IsChecked == true;
        _dirty = true;
    }

    private void OnBlockSpawnsChanged(object sender, RoutedEventArgs e)
    {
        _editedBlockSpawns = ChkBlockSpawns.IsChecked == true;
        _dirty = true;
    }

    private void OnMinimalChanged(object sender, RoutedEventArgs e)
    {
        _editedMinimal = ChkMinimal.IsChecked == true;
        _dirty = true;
    }

    private void OnToastChanged(object sender, RoutedEventArgs e)
    {
        _editedToast = ChkToast.IsChecked == true;
        _dirty = true;
    }

    private void OnTrayChanged(object sender, RoutedEventArgs e)
    {
        _editedTray = ChkTray.IsChecked == true;
        _dirty = true;
    }

    private void OnDebugChanged(object sender, RoutedEventArgs e)
    {
        _editedDebug = ChkDebug.IsChecked == true;
        _dirty = true;
    }

    private void OnIconModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IconModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _editedIconMode = tag;
            _dirty = true;
        }
    }

    private void OnIconFreqChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IconFreqCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _editedIconFreq = tag;
            _dirty = true;
        }
    }

    /// <summary>Fires when user clicks Restore Layout button.</summary>
    public event Action? RestoreLayoutRequested;

    private void OnRestoreLayoutClick(object sender, RoutedEventArgs e)
    {
        RestoreLayoutRequested?.Invoke();
    }

    /// <summary>Show auto-arrange warning and restore button when applicable.</summary>
    public void SetIconInteractionState(bool autoArrangeDetected, bool hasLayout)
    {
        if (autoArrangeDetected)
            IconAutoArrangeWarning.Visibility = Visibility.Visible;
        if (hasLayout)
            BtnRestoreLayout.Visibility = Visibility.Visible;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!_dirty)
        {
            DialogResult = false;
            Close();
            return;
        }

        EditedConfig = _config with
        {
            Player = _config.Player with { WalkSpeed = _editedSpeed },
            Performance = _config.Performance with { DebugOverlay = _editedDebug },
            Windows = _config.Windows with
            {
                SilenceNotifications = _editedSilenceNotifications,
                BlockSpawns = _editedBlockSpawns,
                MinimalMode = _editedMinimal,
                ToastNotificationsEnabled = _editedToast,
                TrayIconEnabled = _editedTray
            },
            DesktopIcons = _config.DesktopIcons with
            {
                Mode = _editedIconMode,
                Frequency = _editedIconFreq
            }
        };

        EditedSettings = _appSettings with
        {
            Language = _editedLanguage,
            ActiveProfileId = _editedProfileId ?? _appSettings.ActiveProfileId
        };

        // Só requer restart se perfil ou idioma realmente mudaram
        var profileChanged = (_editedProfileId ?? _appSettings.ActiveProfileId) != _appSettings.ActiveProfileId;
        var langChanged = _editedLanguage != null && _editedLanguage != _appSettings.Language;
        NeedsRestart = profileChanged || langChanged;

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnWindowDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }
}

public class AchievementDisplayItem
{
    public string Icon { get; set; } = "⭐";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SolidColorBrush Background { get; set; } = new SolidColorBrush();
}
