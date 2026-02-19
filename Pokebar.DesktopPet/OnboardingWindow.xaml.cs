using System.Windows;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Pokebar.Core.Localization;
using Pokebar.DesktopPet.Animation;
using Pokebar.Core.Models;

namespace Pokebar.DesktopPet;

public partial class OnboardingWindow : Window
{
    private int _currentPage;

    public OnboardingWindow(SpriteCache? spriteCache = null, GameplayConfig? config = null)
    {
        InitializeComponent();
        ApplyLocale();

        // Tentar carregar preview do Pikachu
        if (spriteCache != null && config != null)
        {
            try
            {
                var anims = spriteCache.GetAnimations(25, "0000", config);
                if (anims.Idle?.Frames.Count > 0)
                    PreviewImage.Source = anims.Idle.Frames[0];
                else if (anims.WalkRight?.Frames.Count > 0)
                    PreviewImage.Source = anims.WalkRight.Frames[0];
            }
            catch { /* sem preview, tudo bem */ }
        }
    }

    private void ApplyLocale()
    {
        SubtitleText.Text = Localizer.Get("onboarding.subtitle");
        WelcomeText.Text = Localizer.Get("onboarding.welcome");
        ControlsTitle.Text = Localizer.Get("onboarding.controls_title");
        CtrlPause.Text = Localizer.Get("onboarding.ctrl_pause");
        CtrlDiag.Text = Localizer.Get("onboarding.ctrl_diagnostic");
        CtrlScreenshot.Text = Localizer.Get("onboarding.ctrl_screenshot");
        CtrlBox.Text = Localizer.Get("onboarding.ctrl_pcbox");
        CtrlTray.Text = Localizer.Get("onboarding.ctrl_tray");
        ReadyText.Text = Localizer.Get("onboarding.ready");
        SkipButton.Content = Localizer.Get("onboarding.skip");
        NextButton.Content = Localizer.Get("onboarding.next");
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        _currentPage++;
        if (_currentPage > 2)
        {
            DialogResult = true;
            Close();
            return;
        }
        UpdatePage();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void UpdatePage()
    {
        Page0.Visibility = _currentPage == 0 ? Visibility.Visible : Visibility.Collapsed;
        Page1.Visibility = _currentPage == 1 ? Visibility.Visible : Visibility.Collapsed;
        Page2.Visibility = _currentPage == 2 ? Visibility.Visible : Visibility.Collapsed;

        var active = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x40, 0x38));   // FR_AccentRed
        var inactive = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD8, 0xE8, 0xD0)); // FR_PanelBg
        Dot0.Background = _currentPage == 0 ? active : inactive;
        Dot1.Background = _currentPage == 1 ? active : inactive;
        Dot2.Background = _currentPage == 2 ? active : inactive;

        if (_currentPage == 2)
        {
            NextButton.Content = Localizer.Get("onboarding.start");
            SkipButton.Visibility = Visibility.Collapsed;
        }
    }
}
