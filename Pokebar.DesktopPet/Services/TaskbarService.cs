using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Pokebar.DesktopPet.Services;

public class TaskbarService
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
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const int ABM_GETTASKBARPOS = 0x00000005;
    private const int ABM_GETSTATE = 0x00000004;
    private const int ABS_AUTOHIDE = 0x0000001;

    private const uint ABE_LEFT = 0;
    private const uint ABE_TOP = 1;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    public class TaskbarInfo
    {
        /// <summary>Retângulo da janela da taskbar em pixels físicos.</summary>
        public Rect BoundsPx { get; set; }

        /// <summary>
        /// Retângulo completo do monitor em pixels físicos.
        /// Usado para calcular os limites de viagem do pet (X mínimo/máximo),
        /// independente de onde a taskbar está posicionada ou qual é o seu tamanho.
        /// </summary>
        public Rect MonitorBoundsPx { get; set; }

        public TaskbarPosition Position { get; set; }
        public bool IsAutoHide { get; set; }
        public double DpiScale { get; set; }
        public int MonitorIndex { get; set; }
        public IntPtr Hwnd { get; set; }
        public IntPtr MonitorHandle { get; set; }
        public bool IsPrimary { get; set; }

        /// <summary>Borda esquerda do monitor — limite de viagem do pet no eixo X.</summary>
        public double TravelLeft  => MonitorBoundsPx.Left;

        /// <summary>Borda direita do monitor — limite de viagem do pet no eixo X.</summary>
        public double TravelRight => MonitorBoundsPx.Right;

        /// <summary>Linha do chão onde o pet caminha (base da taskbar).</summary>
        public double GroundYPx => BoundsPx.Bottom;
    }

    public enum TaskbarPosition
    {
        Left,
        Top,
        Right,
        Bottom,
        Unknown
    }

    public static TaskbarInfo GetPrimaryTaskbar()
    {
        var autoHide = IsAutoHideEnabled();
        var primaryHwnd = FindWindow("Shell_TrayWnd", null);
        if (primaryHwnd != IntPtr.Zero && TryBuildPrimaryTaskbar(primaryHwnd, autoHide, out var primary))
            return primary;

        return BuildFallbackTaskbar(autoHide);
    }

    public static List<TaskbarInfo> GetAllTaskbars()
    {
        var taskbars = new List<TaskbarInfo>();
        var autoHide = IsAutoHideEnabled();
        var primaryHwnd = FindWindow("Shell_TrayWnd", null);

        if (primaryHwnd != IntPtr.Zero && TryBuildPrimaryTaskbar(primaryHwnd, autoHide, out var primary))
        {
            taskbars.Add(primary);
        }
        else
        {
            taskbars.Add(BuildFallbackTaskbar(autoHide));
        }

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsSecondaryTaskbarWindow(hWnd))
                return true;

            if (primaryHwnd != IntPtr.Zero && hWnd == primaryHwnd)
                return true;

            if (TryBuildSecondaryTaskbar(hWnd, autoHide, taskbars.Count, out var secondary))
                taskbars.Add(secondary);

            return true;
        }, IntPtr.Zero);

        // ── Synthetic entries for monitors without a taskbar window ──────────
        // Monitors that have no Shell_SecondaryTrayWnd (e.g. secondary monitors with
        // "Show taskbar on all displays" disabled) won't be in the list yet.
        // We enumerate all physical monitors and add a virtual strip at the bottom
        // of each work area for any monitor not already covered.
        var coveredMonitors = new HashSet<IntPtr>(taskbars.Select(t => t.MonitorHandle));
        var allMonitors = new List<IntPtr>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr data) =>
            {
                allMonitors.Add(hMonitor);
                return true;
            },
            IntPtr.Zero);

        foreach (var hMonitor in allMonitors)
        {
            if (coveredMonitors.Contains(hMonitor))
                continue;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(hMonitor, ref mi))
                continue;

            var monPx  = RectFromRECT(mi.rcMonitor);
            var workPx = RectFromRECT(mi.rcWork);
            var dpi    = GetDpiScaleForMonitor(hMonitor);

            // Virtual strip at the work-area bottom (same height as a typical taskbar)
            const int SyntheticHeight = 40;
            var synPx = new Rect(
                monPx.Left,
                workPx.Bottom - SyntheticHeight,
                monPx.Width,
                SyntheticHeight);

            taskbars.Add(new TaskbarInfo
            {
                BoundsPx        = synPx,
                MonitorBoundsPx = monPx,
                Position        = TaskbarPosition.Bottom,
                IsAutoHide      = false,
                DpiScale        = dpi,
                MonitorIndex    = taskbars.Count,
                Hwnd            = IntPtr.Zero,
                MonitorHandle   = hMonitor,
                IsPrimary       = false
            });
        }

        taskbars.Sort((a, b) => a.BoundsPx.Left.CompareTo(b.BoundsPx.Left));
        return taskbars;
    }

    public static bool IsTaskbarHidden(TaskbarInfo info)
    {
        if (!info.IsAutoHide || info.Hwnd == IntPtr.Zero)
            return false;

        if (!IsWindowVisible(info.Hwnd))
            return true;

        if (!GetWindowRect(info.Hwnd, out var rect))
            return false;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var minThickness = 2;

        return width <= minThickness || height <= minThickness;
    }

    private static bool TryBuildPrimaryTaskbar(IntPtr hWnd, bool autoHide, out TaskbarInfo info)
    {
        info = new TaskbarInfo();
        var data = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
            hWnd = hWnd
        };

        var result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
        if (result == IntPtr.Zero)
            return false;

        var boundsPx        = RectFromRECT(data.rc);
        var dpiScale        = GetDpiScale(hWnd);
        var monitor         = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        var monitorBoundsPx = GetMonitorBoundsPx(monitor);

        info = new TaskbarInfo
        {
            BoundsPx        = boundsPx,
            MonitorBoundsPx = monitorBoundsPx,
            Position = data.uEdge switch
            {
                ABE_LEFT   => TaskbarPosition.Left,
                ABE_TOP    => TaskbarPosition.Top,
                ABE_RIGHT  => TaskbarPosition.Right,
                ABE_BOTTOM => TaskbarPosition.Bottom,
                _          => TaskbarPosition.Unknown
            },
            IsAutoHide   = autoHide,
            DpiScale     = dpiScale,
            MonitorIndex = 0,
            Hwnd         = hWnd,
            MonitorHandle = monitor,
            IsPrimary    = true
        };

        return true;
    }

    private static bool TryBuildSecondaryTaskbar(IntPtr hWnd, bool autoHide, int monitorIndex, out TaskbarInfo info)
    {
        info = new TaskbarInfo();
        if (!GetWindowRect(hWnd, out var rect))
            return false;

        var boundsPx = RectFromRECT(rect);
        var dpiScale = GetDpiScale(hWnd);
        var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        var monitorBoundsPx = GetMonitorBoundsPx(monitor);
        var position = InferPosition(boundsPx, monitorBoundsPx);

        info = new TaskbarInfo
        {
            BoundsPx        = boundsPx,
            MonitorBoundsPx = monitorBoundsPx,
            Position        = position,
            IsAutoHide      = autoHide,
            DpiScale        = dpiScale,
            MonitorIndex    = monitorIndex,
            Hwnd            = hWnd,
            MonitorHandle   = monitor,
            IsPrimary       = false
        };

        return true;
    }

    private static TaskbarInfo BuildFallbackTaskbar(bool autoHide)
    {
        var screenW      = SystemParameters.PrimaryScreenWidth;
        var screenH      = SystemParameters.PrimaryScreenHeight;
        var taskbarHeight = 40;

        return new TaskbarInfo
        {
            BoundsPx        = new Rect(0, screenH - taskbarHeight, screenW, taskbarHeight),
            MonitorBoundsPx = new Rect(0, 0, screenW, screenH),
            Position        = TaskbarPosition.Bottom,
            IsAutoHide      = autoHide,
            DpiScale        = 1.0,
            MonitorIndex    = 0,
            Hwnd            = IntPtr.Zero,
            MonitorHandle   = IntPtr.Zero,
            IsPrimary       = true
        };
    }

    private static bool IsAutoHideEnabled()
    {
        var stateData = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA))
        };
        var state = SHAppBarMessage(ABM_GETSTATE, ref stateData);
        return (state.ToInt32() & ABS_AUTOHIDE) != 0;
    }

    private static Rect RectFromRECT(RECT rect)
    {
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static double GetDpiScale(IntPtr hWnd)
    {
        try
        {
            var dpi = GetDpiForWindow(hWnd);
            if (dpi == 0)
                return 1.0;
            return dpi / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    private static double GetDpiScaleForMonitor(IntPtr hMonitor)
    {
        try
        {
            if (GetDpiForMonitor(hMonitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out _) == 0)
                return dpiX / 96.0;
        }
        catch { }
        return 1.0;
    }

    private static Rect GetMonitorBoundsPx(IntPtr hMonitor)
    {
        var info = new MONITORINFO
        {
            cbSize = Marshal.SizeOf(typeof(MONITORINFO))
        };

        if (!GetMonitorInfo(hMonitor, ref info))
            return Rect.Empty;

        return RectFromRECT(info.rcMonitor);
    }

    private static TaskbarPosition InferPosition(Rect taskbarPx, Rect monitorPx)
    {
        if (monitorPx == Rect.Empty)
            return TaskbarPosition.Unknown;

        var tolerance = 2;
        var horizontal = taskbarPx.Width >= taskbarPx.Height;

        if (horizontal)
        {
            if (Math.Abs(taskbarPx.Top - monitorPx.Top) <= tolerance)
                return TaskbarPosition.Top;
            if (Math.Abs(taskbarPx.Bottom - monitorPx.Bottom) <= tolerance)
                return TaskbarPosition.Bottom;
        }
        else
        {
            if (Math.Abs(taskbarPx.Left - monitorPx.Left) <= tolerance)
                return TaskbarPosition.Left;
            if (Math.Abs(taskbarPx.Right - monitorPx.Right) <= tolerance)
                return TaskbarPosition.Right;
        }

        return TaskbarPosition.Unknown;
    }

    private static bool IsSecondaryTaskbarWindow(IntPtr hWnd)
    {
        var className = GetWindowClassName(hWnd);
        return className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        var len = GetClassName(hWnd, sb, sb.Capacity);
        return len > 0 ? sb.ToString(0, len) : string.Empty;
    }
}
