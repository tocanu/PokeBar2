using System;
using System.Windows;
using System.Windows.Interop;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Interop;

namespace Pokebar.DesktopPet;

public partial class CaptureBallWindow : Window
{
    private readonly double _ballSizePx;
    private IntPtr _hwnd;

    public CaptureBallWindow(GameplayConfig config)
    {
        InitializeComponent();
        _ballSizePx = config.Capture.BallSizePx;

        // Ajustar tamanho visual do grid para corresponder ao config
        RootGrid.Width = _ballSizePx;
        RootGrid.Height = _ballSizePx;
        var half = _ballSizePx / 2;
        BallClip.Center = new System.Windows.Point(half, half);
        BallClip.RadiusX = half;
        BallClip.RadiusY = half;
        TopHalf.Height = half;

        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Posiciona a pokébola na tela. Coordenadas em DIPs (centro da bola).
    /// </summary>
    public void UpdatePosition(double centerX, double centerY)
    {
        Left = centerX - (_ballSizePx / 2);
        Top = centerY - (_ballSizePx / 2);
    }

    /// <summary>
    /// Garante que a janela fique acima de todas as outras (inclusive player/enemy).
    /// </summary>
    public void EnsureTopmost()
    {
        if (_hwnd != IntPtr.Zero)
            WindowHelper.EnsureTopmost(_hwnd);
    }

    public void SetHidden(bool hidden)
    {
        Opacity = hidden ? 0 : 1;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowHelper.MakeTransparentWindow(this);
        WindowHelper.SetClickThrough(this, true);
    }
}
