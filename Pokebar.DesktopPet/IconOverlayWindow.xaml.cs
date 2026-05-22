using System.Windows;
using System.Windows.Interop;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

/// <summary>
/// Janela overlay transparente que simula o ícone do desktop sendo carregado pelo pet.
/// Usada no modo "fake" (não move o ícone real).
/// </summary>
public partial class IconOverlayWindow : Window
{
    public IconOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Transparent, click-through, hidden from Alt-Tab
        WindowHelper.MakeTransparentWindow(this);
        WindowHelper.SetClickThrough(this, true);
    }

    /// <summary>
    /// Show the overlay at the specified screen position.
    /// </summary>
    public void ShowAt(int screenX, int screenY, string iconName = "")
    {
        Left = screenX - 24; // Center the 48px window on icon position
        Top = screenY - 24;

        if (!string.IsNullOrEmpty(iconName))
        {
            IconLabel.Text = iconName;
            IconLabel.Visibility = Visibility.Visible;
        }

        Show();
    }

    /// <summary>
    /// Move the overlay to a new screen position.
    /// </summary>
    public void MoveTo(int screenX, int screenY)
    {
        Left = screenX - 24;
        Top = screenY - 24;
    }

    /// <summary>
    /// Hide and close the overlay.
    /// </summary>
    public void HideOverlay()
    {
        Hide();
    }
}
