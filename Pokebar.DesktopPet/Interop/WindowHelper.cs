using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Pokebar.DesktopPet.Interop;

/// <summary>
/// Helper para configurar janela transparente, topmost, click-through e esconder do Alt-Tab
/// </summary>
public static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TOPMOST = 0x00000008;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>
    /// Configura a janela como transparente, topmost e escondida do Alt-Tab
    /// </summary>
    public static void MakeTransparentWindow(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        
        // Adiciona WS_EX_LAYERED (necessário para transparência)
        // Adiciona WS_EX_TOOLWINDOW (esconde do Alt-Tab)
        exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Ativa ou desativa o modo click-through (WS_EX_TRANSPARENT)
    /// Quando ativo, cliques passam através da janela para o que está atrás
    /// </summary>
    public static void SetClickThrough(Window window, bool enabled)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        
        if (enabled)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;
        
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Força a janela a ficar sempre no topo (topmost)
    /// </summary>
    public static void EnsureTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, 
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}
