using System;
using System.Windows;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

public partial class CaptureBallWindow : Window
{
    private readonly double _ballSizePx;

    public CaptureBallWindow(GameplayConfig config)
    {
        InitializeComponent();
        _ballSizePx = config.Capture.BallSizePx;
        SourceInitialized += OnSourceInitialized;
    }

    public void UpdatePosition(double xPx, double yPx, double dpiScale)
    {
        var scale = dpiScale > 0 ? dpiScale : 1.0;
        var windowX = (xPx / scale) - (_ballSizePx / 2);
        var windowY = (yPx / scale) - (_ballSizePx / 2);
        Left = windowX;
        Top = windowY;
    }

    public void SetHidden(bool hidden)
    {
        Opacity = hidden ? 0 : 1;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowHelper.MakeTransparentWindow(this);
        WindowHelper.SetClickThrough(this, true);
    }
}
