using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

public partial class PetWindow : Window
{
    private double _currentGroundLineY;
    private double _captureScale = 1.0;
    private double _flipScaleX = 1.0;
    private double _hitFlashTimer;
    private double _tintPulseTimer;
    private string? _currentTintColor;

    /// <summary>Fired when the user clicks on the enemy sprite.</summary>
    public event Action? EnemyClicked;

    public PetWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public bool ShowDebugOverlay
    {
        get => DebugPanel.Visibility == Visibility.Visible;
        set => DebugPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetDebugText(string? text)
    {
        if (!ShowDebugOverlay)
            return;
        DebugText.Text = text ?? string.Empty;
    }

    public void SetDebugHitbox(double frameWidth, double frameHeight, double hitboxX, double hitboxY, double hitboxWidth, double hitboxHeight, bool flipped)
    {
        if (!ShowDebugOverlay)
            return;

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

    public void UpdateFrame(BitmapSource frame, double groundLineY, double scaleX)
    {
        PokemonImage.Source = frame;
        _currentGroundLineY = groundLineY;
        _flipScaleX = scaleX;
        ApplyScale();

        if (RootCanvas.Width != frame.PixelWidth || RootCanvas.Height != frame.PixelHeight)
        {
            RootCanvas.Width = frame.PixelWidth;
            RootCanvas.Height = frame.PixelHeight;
        }
    }

    public void UpdatePosition(double xPx, double yPx, double dpiScale)
    {
        // BUG FIX #1: usar TransformFromDevice para converter pixels físicos → lógicos WPF
        // corretamente em setups com DPI diferente por monitor (PerMonitorV2).
        var source = PresentationSource.FromVisual(this);
        var groundLine = _currentGroundLineY > 0 ? _currentGroundLineY : RootCanvas.Height;

        double windowX, windowY;
        if (source?.CompositionTarget != null)
        {
            var logical = source.CompositionTarget.TransformFromDevice
                .Transform(new System.Windows.Point(xPx, yPx));
            windowX = logical.X - (RootCanvas.Width / 2);
            windowY = logical.Y - groundLine;
        }
        else
        {
            var scale = dpiScale > 0 ? dpiScale : 1.0;
            windowX = (xPx / scale) - (RootCanvas.Width / 2);
            windowY = (yPx / scale) - groundLine;
        }

        Left = windowX;
        Top = windowY;
    }

    public void SetHidden(bool hidden)
    {
        Opacity = hidden ? 0 : 1;
    }

    /// <summary>
    /// Retorna o centro visual da janela em coordenadas de tela (DIPs).
    /// </summary>
    public (double X, double Y) GetScreenCenter()
    {
        return (Left + RootCanvas.Width / 2, Top + RootCanvas.Height / 2);
    }

    /// <summary>
    /// Retorna a posição Y do chão em coordenadas de tela (DIPs).
    /// </summary>
    public double GetScreenGroundY()
    {
        var groundLine = _currentGroundLineY > 0 ? _currentGroundLineY : RootCanvas.Height;
        return Top + groundLine;
    }

    public void SetCaptureScale(double scale)
    {
        _captureScale = Math.Clamp(scale, 0, 1);
        ApplyScale();
    }

    // ── Status overlay ──

    /// <summary>
    /// Define o texto de overlay de status (ex: "💤", "⚡", "☠").
    /// Passa null para esconder.
    /// </summary>
    public void SetStatusOverlay(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            StatusOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        StatusOverlay.Text = text;
        StatusOverlay.Visibility = Visibility.Visible;

        // Posicionar acima do sprite (centro-topo)
        var x = (RootCanvas.Width / 2) - 10;
        Canvas.SetLeft(StatusOverlay, x);
        Canvas.SetTop(StatusOverlay, -4);
    }

    // ── Visual effects ──

    /// <summary>
    /// Dispara um flash branco rápido (hit effect). Duração padrão 0.15s.
    /// </summary>
    public void TriggerHitFlash()
    {
        _hitFlashTimer = 0.15;
        HitFlashRect.Opacity = 0.6;
        SyncOverlaySize();
    }

    /// <summary>
    /// Define a cor de tint para status effects (ou null para remover).
    /// Purple = poison, Yellow = paralysis, Blue = sleep.
    /// </summary>
    public void SetTintColor(string? hexColor)
    {
        _currentTintColor = hexColor;
        if (hexColor == null)
        {
            TintRect.Opacity = 0;
            _tintPulseTimer = 0;
            return;
        }

        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
            TintRect.Fill = new SolidColorBrush(color);
            _tintPulseTimer = 0;
        }
        catch
        {
            TintRect.Opacity = 0;
        }
    }

    /// <summary>
    /// Atualiza efeitos visuais baseados em tempo (flash decay, tint pulse).
    /// Chamar a cada tick do game loop.
    /// </summary>
    public void UpdateEffects(double deltaTime)
    {
        // Hit flash decay
        if (_hitFlashTimer > 0)
        {
            _hitFlashTimer -= deltaTime;
            HitFlashRect.Opacity = Math.Max(0, _hitFlashTimer / 0.15 * 0.6);
        }

        // Tint pulse (suave pulsação de opacidade)
        if (_currentTintColor != null)
        {
            _tintPulseTimer += deltaTime;
            TintRect.Opacity = 0.15 + 0.1 * Math.Sin(_tintPulseTimer * 3.0);
            SyncOverlaySize();
        }
    }

    /// <summary>
    /// Sincroniza tamanho dos overlays com o sprite atual.
    /// </summary>
    private void SyncOverlaySize()
    {
        var w = PokemonImage.ActualWidth;
        var h = PokemonImage.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var left = Canvas.GetLeft(PokemonImage);
        var top = Canvas.GetTop(PokemonImage);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        HitFlashRect.Width = w;
        HitFlashRect.Height = h;
        Canvas.SetLeft(HitFlashRect, left);
        Canvas.SetTop(HitFlashRect, top);

        TintRect.Width = w;
        TintRect.Height = h;
        Canvas.SetLeft(TintRect, left);
        Canvas.SetTop(TintRect, top);
    }

    private void ApplyScale()
    {
        FlipTransform.ScaleX = _flipScaleX * _captureScale;
        FlipTransform.ScaleY = _captureScale;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowHelper.MakeTransparentWindow(this);
        // Use hit-test hook instead of click-through so enemy sprites are clickable
        var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        src?.AddHook(HitTestHook);
    }

    private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;
        const int HTCLIENT = 1;

        if (msg == WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            var point = PointFromScreen(new System.Windows.Point(x, y));

            if (IsPointOnSprite(point))
            {
                handled = true;
                return new IntPtr(HTCLIENT);
            }

            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }
        return IntPtr.Zero;
    }

    private bool IsPointOnSprite(System.Windows.Point point)
    {
        var imgLeft = Canvas.GetLeft(PokemonImage);
        var imgTop = Canvas.GetTop(PokemonImage);
        if (double.IsNaN(imgLeft)) imgLeft = 0;
        if (double.IsNaN(imgTop)) imgTop = 0;
        var imgWidth = PokemonImage.ActualWidth;
        var imgHeight = PokemonImage.ActualHeight;
        if (imgWidth <= 0 || imgHeight <= 0) return false;
        return point.X >= imgLeft && point.X <= imgLeft + imgWidth &&
               point.Y >= imgTop && point.Y <= imgTop + imgHeight;
    }

    private void OnSpriteMouseClick(object sender, MouseButtonEventArgs e)
    {
        EnemyClicked?.Invoke();
        e.Handled = true;
    }
}
