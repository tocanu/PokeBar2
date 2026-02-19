using Pokebar.Core.Localization;
using Serilog;
using WinForms = System.Windows.Forms;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Serviço de notificações toast via balloon tip do tray icon.
/// Centraliza todas as notificações do app e respeita a preferência do usuário.
/// </summary>
public sealed class NotificationService
{
    private readonly TrayIconService? _trayIcon;
    private bool _enabled = true;

    public NotificationService(TrayIconService? trayIcon)
    {
        _trayIcon = trayIcon;
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Notifica captura bem-sucedida.</summary>
    public void NotifyCaptureSuccess(int dex)
    {
        if (!_enabled || _trayIcon == null) return;

        _trayIcon.ShowNotification(
            Localizer.Get("toast.capture.title"),
            Localizer.Get("toast.capture.success", $"#{dex}"),
            WinForms.ToolTipIcon.Info);

        Log.Debug("Toast: Capture success Dex={Dex}", dex);
    }

    /// <summary>Notifica falha na captura.</summary>
    public void NotifyCaptureFailed(int dex)
    {
        if (!_enabled || _trayIcon == null) return;

        _trayIcon.ShowNotification(
            Localizer.Get("toast.capture.title"),
            Localizer.Get("toast.capture.failed", $"#{dex}"),
            WinForms.ToolTipIcon.Warning);
    }

    /// <summary>Notifica resultado de batalha.</summary>
    public void NotifyBattleResult(bool playerWon)
    {
        if (!_enabled || _trayIcon == null) return;

        var msg = playerWon
            ? Localizer.Get("toast.battle.won")
            : Localizer.Get("toast.battle.lost");

        _trayIcon.ShowNotification(
            Localizer.Get("toast.battle.title"),
            msg,
            playerWon ? WinForms.ToolTipIcon.Info : WinForms.ToolTipIcon.Warning);
    }

    /// <summary>Notifica que o app foi pausado/retomado.</summary>
    public void NotifyPauseState(bool paused)
    {
        if (!_enabled || _trayIcon == null) return;

        _trayIcon.ShowNotification(
            "Pokebar",
            paused ? Localizer.Get("toast.paused") : Localizer.Get("toast.resumed"),
            WinForms.ToolTipIcon.None);
    }

    /// <summary>Notificação genérica com título e mensagem customizados.</summary>
    public void Notify(string title, string message, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        if (!_enabled || _trayIcon == null) return;
        _trayIcon.ShowNotification(title, message, icon);
    }
}
