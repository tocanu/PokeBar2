using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

public partial class PetWindow : Window
{
    private double _currentGroundLineY;
    private double _captureScale = 1.0;
    private double _flipScaleX = 1.0;

    public PetWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
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
        WindowHelper.SetClickThrough(this, true);
    }
}
