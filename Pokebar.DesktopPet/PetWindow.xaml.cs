using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

public partial class PetWindow : Window
{
    private double _currentGroundLineY;
    private double _captureScale = 1.0;
    private double _flipScaleX = 1.0;

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
        var scale = dpiScale > 0 ? dpiScale : 1.0;
        var windowX = (xPx / scale) - (RootCanvas.Width / 2);
        var groundLine = _currentGroundLineY > 0 ? _currentGroundLineY : RootCanvas.Height;
        var windowY = (yPx / scale) - groundLine;

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
