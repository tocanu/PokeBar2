using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using Pokebar.Core.Localization;
using Serilog;
using WinForms = System.Windows.Forms;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Ícone na bandeja do sistema (system tray) com menu de contexto.
/// Usa System.Windows.Forms.NotifyIcon (referência WinForms habilitada no csproj).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ContextMenuStrip _contextMenu;

    // Menu items que precisam ser atualizados dinamicamente
    private WinForms.ToolStripMenuItem _pauseItem = null!;
    private WinForms.ToolStripMenuItem _pokeballItem = null!;
    private WinForms.ToolStripMenuItem _silenceItem = null!;
    private WinForms.ToolStripMenuItem _blockSpawnsItem = null!;
    private WinForms.ToolStripMenuItem _stonesItem = null!;

    /// <summary>Disparado quando o usuário clica em Pausar/Retomar.</summary>
    public event Action? PauseResumeRequested;

    /// <summary>Disparado quando o usuário clica em Diagnóstico.</summary>
    public event Action? DiagnosticRequested;

    /// <summary>Disparado quando o usuário clica em Sair.</summary>
    public event Action? QuitRequested;

    /// <summary>Disparado quando o usuário clica em PC Box.</summary>
    public event Action? PcBoxRequested;

    /// <summary>Disparado quando o usuário clica em Configurações.</summary>
    public event Action? SettingsRequested;

    /// <summary>Disparado quando o usuário clica em Screenshot.</summary>
    public event Action? ScreenshotRequested;

    /// <summary>Disparado quando o usuário alterna Silenciar Notificações.</summary>
    public event Action? SilenceNotificationsToggled;

    /// <summary>Disparado quando o usuário usa uma pedra de evolução. Parâmetro: stone enum.</summary>
    public event Action<int>? UseStoneRequested;

    /// <summary>Disparado quando o usuário alterna Bloquear Spawns.</summary>
    public event Action? BlockSpawnsToggled;

    /// <summary>Disparado quando o usuário clica em Acariciar (FASE 7).</summary>
    public event Action? PetRequested;

    /// <summary>Disparado quando o usuário seleciona um perfil. Parâmetro: profileId.</summary>
#pragma warning disable CS0067
    public event Action<string>? ProfileSwitchRequested;
#pragma warning restore CS0067

    public TrayIconService()
    {
        _contextMenu = BuildContextMenu();

        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "Pokebar",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        // Clique esquerdo abre diretamente o PC Box.
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;

        Log.Debug("TrayIconService initialized");
    }

    private void OnNotifyIconMouseUp(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left && e.Clicks == 1)
            PcBoxRequested?.Invoke();
    }

    private WinForms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip();

        // Título
        var titleItem = new WinForms.ToolStripMenuItem("🎮 Pokebar")
        {
            Enabled = false,
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Pokeballs (informativo)
        _pokeballItem = new WinForms.ToolStripMenuItem("Pokeballs: 0") { Enabled = false };
        menu.Items.Add(_pokeballItem);

        // Pedras de evolução (submenu dinâmico)
        _stonesItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.stones"));
        _stonesItem.Visible = false;
        menu.Items.Add(_stonesItem);

        // FASE 7: Acariciar
        var petItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.pet"));
        petItem.Click += (_, _) => PetRequested?.Invoke();
        menu.Items.Add(petItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Pausar / Retomar
        _pauseItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.pause"));
        _pauseItem.Click += (_, _) => PauseResumeRequested?.Invoke();
        menu.Items.Add(_pauseItem);

        // Diagnóstico
        var diagItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.diagnostic"));
        diagItem.Click += (_, _) => DiagnosticRequested?.Invoke();
        menu.Items.Add(diagItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // PC Box
        var pcBoxItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.pcbox"));
        pcBoxItem.Click += (_, _) => PcBoxRequested?.Invoke();
        menu.Items.Add(pcBoxItem);

        // Configurações
        var settingsItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.settings"));
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Items.Add(settingsItem);

        // Screenshot
        var screenshotItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.screenshot"));
        screenshotItem.Click += (_, _) => ScreenshotRequested?.Invoke();
        menu.Items.Add(screenshotItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Silenciar Notificações (toggle)
        _silenceItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.silence"));
        _silenceItem.CheckOnClick = true;
        _silenceItem.Click += (_, _) => SilenceNotificationsToggled?.Invoke();
        menu.Items.Add(_silenceItem);

        // Bloquear Spawns (toggle)
        _blockSpawnsItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.blockspawns"));
        _blockSpawnsItem.CheckOnClick = true;
        _blockSpawnsItem.Click += (_, _) => BlockSpawnsToggled?.Invoke();
        menu.Items.Add(_blockSpawnsItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Sair
        var quitItem = new WinForms.ToolStripMenuItem(Localizer.Get("tray.quit"));
        quitItem.Click += (_, _) => QuitRequested?.Invoke();
        menu.Items.Add(quitItem);

        return menu;
    }

    /// <summary>
    /// Atualiza o estado de pausa exibido no menu.
    /// </summary>
    public void SetPaused(bool paused)
    {
        _pauseItem.Text = paused
            ? Localizer.Get("tray.resume")
            : Localizer.Get("tray.pause");
    }

    /// <summary>
    /// Atualiza o contador de pokeballs exibido no menu.
    /// </summary>
    public void SetPokeballCount(int count)
    {
        _pokeballItem.Text = $"Pokeballs: {count}";
    }

    /// <summary>
    /// Atualiza o checkbox de Silenciar Notificações no menu.
    /// </summary>
    public void SetSilenceNotifications(bool enabled)
    {
        _silenceItem.Checked = enabled;
    }

    /// <summary>
    /// Atualiza o checkbox de Bloquear Spawns no menu.
    /// </summary>
    public void SetBlockSpawns(bool enabled)
    {
        _blockSpawnsItem.Checked = enabled;
    }

    /// <summary>
    /// Atualiza o submenu de pedras de evolução.
    /// </summary>
    public void SetStones(Dictionary<Pokebar.Core.Models.EvolutionStone, int> stones, List<(Pokebar.Core.Models.EvolutionStone Stone, int ToDex)>? available)
    {
        _stonesItem.DropDownItems.Clear();

        if (stones.Count == 0 && (available == null || available.Count == 0))
        {
            _stonesItem.Visible = false;
            return;
        }

        _stonesItem.Visible = true;

        // Listar pedras disponíveis para evolução do Pokémon ativo
        if (available != null && available.Count > 0)
        {
            foreach (var (stone, toDex) in available)
            {
                var count = stones.TryGetValue(stone, out var c) ? c : 0;
                var emoji = GetStoneEmoji(stone);
                var label = $"{emoji} {stone} → #{toDex} ({count}x)";
                var item = new WinForms.ToolStripMenuItem(label);
                item.Enabled = count > 0;
                var capturedStone = (int)stone;
                item.Click += (_, _) => UseStoneRequested?.Invoke(capturedStone);
                _stonesItem.DropDownItems.Add(item);
            }
            _stonesItem.DropDownItems.Add(new WinForms.ToolStripSeparator());
        }

        // Listar inventário completo
        foreach (var (stone, count) in stones.OrderBy(s => (int)s.Key))
        {
            if (count <= 0) continue;
            var emoji = GetStoneEmoji(stone);
            var info = new WinForms.ToolStripMenuItem($"{emoji} {stone}: {count}x") { Enabled = false };
            _stonesItem.DropDownItems.Add(info);
        }
    }

    private static string GetStoneEmoji(Pokebar.Core.Models.EvolutionStone stone) => stone switch
    {
        Pokebar.Core.Models.EvolutionStone.Fire => "🔥",
        Pokebar.Core.Models.EvolutionStone.Water => "💧",
        Pokebar.Core.Models.EvolutionStone.Thunder => "⚡",
        Pokebar.Core.Models.EvolutionStone.Leaf => "🍃",
        Pokebar.Core.Models.EvolutionStone.Moon => "🌙",
        Pokebar.Core.Models.EvolutionStone.Sun => "☀️",
        Pokebar.Core.Models.EvolutionStone.Ice => "❄️",
        Pokebar.Core.Models.EvolutionStone.Dusk => "🌑",
        Pokebar.Core.Models.EvolutionStone.Dawn => "🌅",
        Pokebar.Core.Models.EvolutionStone.Shiny => "✨",
        _ => "💎"
    };

    /// <summary>
    /// Exibe uma notificação balloon tip no tray.
    /// </summary>
    public void ShowNotification(string title, string message, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    /// <summary>
    /// Atualiza o tooltip do ícone.
    /// </summary>
    public void SetTooltip(string text)
    {
        // NotifyIcon.Text tem limite de 128 chars
        _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
    }

    private static Icon LoadTrayIcon()
    {
        // Tenta carregar ícone customizado de pokébola.
        try
        {
            var pokeballIconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "pokeball.ico");
            if (System.IO.File.Exists(pokeballIconPath))
                return new Icon(pokeballIconPath);

            var legacyIconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "pokebar.ico");
            if (System.IO.File.Exists(legacyIconPath))
                return new Icon(legacyIconPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load custom tray icon, using generated Pokeball");
        }

        // Fallback: gera uma pokébola simples em memória.
        try
        {
            return CreatePokeballIcon();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate Pokeball tray icon, using system default");
            return SystemIcons.Application;
        }
    }

    private static Icon CreatePokeballIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var ballRect = new Rectangle(2, 2, 28, 28);
            using var whiteBrush = new SolidBrush(Color.White);
            using var redBrush = new SolidBrush(Color.FromArgb(0xE0, 0x40, 0x38));
            using var borderPen = new Pen(Color.Black, 2f);
            using var linePen = new Pen(Color.Black, 2.4f);

            g.FillEllipse(whiteBrush, ballRect);

            var saved = g.Save();
            g.SetClip(new Rectangle(ballRect.X, ballRect.Y, ballRect.Width, ballRect.Height / 2));
            g.FillEllipse(redBrush, ballRect);
            g.Restore(saved);

            g.DrawEllipse(borderPen, ballRect);

            var middleY = ballRect.Y + (ballRect.Height / 2f);
            g.DrawLine(linePen, ballRect.X + 1, middleY, ballRect.Right - 1, middleY);

            var centerRect = new Rectangle(12, 12, 8, 8);
            g.FillEllipse(Brushes.White, centerRect);
            g.DrawEllipse(borderPen, centerRect);
            g.FillEllipse(Brushes.Black, new Rectangle(14, 14, 4, 4));
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        Log.Debug("TrayIconService disposed");
    }
}
