using System.Runtime.InteropServices;
using System.Text;

namespace Pokebar.DesktopPet.Services;

public static class FullscreenService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTONULL = 0;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    public static HashSet<IntPtr> GetFullscreenMonitors(IEnumerable<IntPtr> ignoreWindows)
    {
        var ignore = new HashSet<IntPtr>(ignoreWindows);
        var monitors = new HashSet<IntPtr>();

        EnumWindows((hWnd, lParam) =>
        {
            if (ignore.Contains(hWnd))
                return true;

            if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
                return true;

            if (IsIgnoredWindow(hWnd))
                return true;

            if (!GetWindowRect(hWnd, out var rect))
                return true;

            var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONULL);
            if (monitor == IntPtr.Zero)
                return true;

            if (IsFullscreenRect(rect, monitor))
                monitors.Add(monitor);

            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    private static bool IsIgnoredWindow(IntPtr hWnd)
    {
        var className = GetWindowClassName(hWnd);
        if (className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
            return true;
        if (className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
            return true;
        if (className.Equals("Progman", StringComparison.OrdinalIgnoreCase))
            return true;
        if (className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase))
            return true;

        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TOOLWINDOW) != 0;
    }

    private static bool IsFullscreenRect(RECT rect, IntPtr monitor)
    {
        var info = new MONITORINFO
        {
            cbSize = Marshal.SizeOf(typeof(MONITORINFO))
        };

        if (!GetMonitorInfo(monitor, ref info))
            return false;

        var tol = 2;
        return rect.Left <= info.rcMonitor.Left + tol
            && rect.Top <= info.rcMonitor.Top + tol
            && rect.Right >= info.rcMonitor.Right - tol
            && rect.Bottom >= info.rcMonitor.Bottom - tol;
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        var len = GetClassName(hWnd, sb, sb.Capacity);
        return len > 0 ? sb.ToString(0, len) : string.Empty;
    }
}
