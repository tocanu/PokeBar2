using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

public static class FullscreenService
{
    // BUG FIX #3: Cache pid → nome do processo para evitar Process.GetProcessById()
    // em cada janela a cada varredura de fullscreen (era O(n) handle opens por frame).
    private static readonly Dictionary<uint, (string Name, long ExpiryTick)> _pidCache = new();
    private static readonly long CACHE_TTL_TICKS = TimeSpan.FromSeconds(10).Ticks;
    private static long _lastCacheCleanup;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Retorna monitores que estão em fullscreen, respeitando whitelist/blacklist.
    /// </summary>
    public static HashSet<IntPtr> GetFullscreenMonitors(
        IEnumerable<IntPtr> ignoreWindows,
        FullscreenConfig? config = null)
    {
        var mode = config?.Mode ?? "hide";

        // Se modo é "show", nunca bloqueia nenhum monitor
        if (mode.Equals("show", StringComparison.OrdinalIgnoreCase))
            return new HashSet<IntPtr>();

        var ignore = new HashSet<IntPtr>(ignoreWindows);
        var monitors = new HashSet<IntPtr>();

        // Pré-computar listas para comparação case-insensitive
        var whitelist = config?.Whitelist
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet() ?? new HashSet<string>();

        var blacklist = config?.Blacklist
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet() ?? new HashSet<string>();

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

            if (!IsFullscreenRect(rect, monitor))
                return true;

            // Janela está em fullscreen — aplicar regras de whitelist/blacklist
            var processName = GetProcessNameForWindow(hWnd);

            switch (mode.ToLowerInvariant())
            {
                case "whitelist":
                    // Só bloqueia se o processo NÃO está na whitelist
                    if (!whitelist.Contains(processName))
                        monitors.Add(monitor);
                    break;

                case "blacklist":
                    // Só bloqueia se o processo ESTÁ na blacklist
                    if (blacklist.Contains(processName))
                        monitors.Add(monitor);
                    break;

                case "hide":
                default:
                    // Bloqueia sempre em fullscreen
                    monitors.Add(monitor);
                    break;
            }

            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    /// <summary>
    /// Obtém o nome do processo (sem extensão, lowercase) de uma janela.
    /// Resultado cacheado por 10 s para evitar abrir handle de processo a cada frame.
    /// </summary>
    private static string GetProcessNameForWindow(IntPtr hWnd)
    {
        try
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0) return string.Empty;

            var now = DateTime.UtcNow.Ticks;

            if (_pidCache.TryGetValue(pid, out var cached) && cached.ExpiryTick > now)
                return cached.Name;

            var name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
            _pidCache[pid] = (name, now + CACHE_TTL_TICKS);

            // Limpeza periódica para evitar crescimento ilimitado (a cada ~60 s)
            if (now - _lastCacheCleanup > TimeSpan.FromSeconds(60).Ticks)
            {
                _lastCacheCleanup = now;
                var expired = _pidCache.Where(kv => kv.Value.ExpiryTick <= now)
                                       .Select(kv => kv.Key).ToList();
                foreach (var key in expired)
                    _pidCache.Remove(key);
            }

            return name;
        }
        catch
        {
            return string.Empty;
        }
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
