using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace Pokebar.DesktopPet.Interop;

/// <summary>
/// Win32 interop para manipular ícones do Desktop Windows.
/// Acessa o SysListView32 do Explorer para enumerar, posicionar e restaurar ícones.
/// </summary>
public static class DesktopIconInterop
{
    // ── ListView messages ──
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const int LVM_GETITEMPOSITION = LVM_FIRST + 16;
    private const int LVM_SETITEMPOSITION32 = LVM_FIRST + 49;
    private const int LVM_GETITEMTEXTW = LVM_FIRST + 115;
    private const int LVM_GETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 55;

    // ListView extended styles
    private const int LVS_EX_SNAPTOGRID = 0x00080000;

    // ListView styles (window style)
    private const int GWL_STYLE = -16;
    private const int LVS_AUTOARRANGE = 0x0100;

    // Process access
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LVITEMW
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
    }

    private const uint LVIF_TEXT = 0x0001;

    // ── P/Invoke declarations ──

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Represents a desktop icon with its index, name, and position.
    /// </summary>
    public record DesktopIcon(int Index, string Name, int X, int Y);

    /// <summary>
    /// Find the SysListView32 handle for the desktop icons.
    /// Works on Windows 10 and 11.
    /// </summary>
    public static IntPtr GetDesktopListViewHandle()
    {
        // Standard path: Progman → SHELLDLL_DefView → SysListView32
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                var listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
                if (listView != IntPtr.Zero) return listView;
            }
        }

        // Alternative path (when desktop slideshow is active):
        // WorkerW → SHELLDLL_DefView → SysListView32
        IntPtr workerW = IntPtr.Zero;
        while (true)
        {
            workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
            if (workerW == IntPtr.Zero) break;

            var shellView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                var listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
                if (listView != IntPtr.Zero) return listView;
            }
        }

        Log.Warning("DesktopIconInterop: Could not find SysListView32 handle");
        return IntPtr.Zero;
    }

    /// <summary>
    /// Get the number of icons on the desktop.
    /// </summary>
    public static int GetIconCount(IntPtr listViewHandle)
    {
        if (listViewHandle == IntPtr.Zero) return 0;
        return (int)SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Check if "Auto-arrange icons" is enabled on the desktop.
    /// </summary>
    public static bool IsAutoArrangeEnabled(IntPtr listViewHandle)
    {
        if (listViewHandle == IntPtr.Zero) return false;
        var style = GetWindowLong(listViewHandle, GWL_STYLE);
        return (style & LVS_AUTOARRANGE) != 0;
    }

    /// <summary>
    /// Check if "Snap to grid" is enabled.
    /// </summary>
    public static bool IsSnapToGridEnabled(IntPtr listViewHandle)
    {
        if (listViewHandle == IntPtr.Zero) return false;
        var exStyle = (int)SendMessage(listViewHandle, LVM_GETEXTENDEDLISTVIEWSTYLE, IntPtr.Zero, IntPtr.Zero);
        return (exStyle & LVS_EX_SNAPTOGRID) != 0;
    }

    /// <summary>
    /// Get all desktop icons with their names and positions.
    /// Uses cross-process memory to read from Explorer.
    /// </summary>
    public static List<DesktopIcon> GetAllIcons(IntPtr listViewHandle)
    {
        var result = new List<DesktopIcon>();
        if (listViewHandle == IntPtr.Zero) return result;

        int count = GetIconCount(listViewHandle);
        if (count == 0) return result;

        GetWindowThreadProcessId(listViewHandle, out uint processId);
        IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            Log.Warning("DesktopIconInterop: Failed to open Explorer process (PID={Pid})", processId);
            return result;
        }

        try
        {
            // Allocate memory in the remote process for POINT + LVITEMW + text buffer
            uint allocSize = (uint)(Marshal.SizeOf<POINT>()
                + Marshal.SizeOf<LVITEMW>()
                + 520); // 260 wchars for text
            IntPtr pRemoteMem = VirtualAllocEx(hProcess, IntPtr.Zero, allocSize, MEM_COMMIT, PAGE_READWRITE);
            if (pRemoteMem == IntPtr.Zero)
            {
                Log.Warning("DesktopIconInterop: VirtualAllocEx failed");
                return result;
            }

            try
            {
                IntPtr pPoint = pRemoteMem;
                IntPtr pItem = pRemoteMem + Marshal.SizeOf<POINT>();
                IntPtr pText = pItem + Marshal.SizeOf<LVITEMW>();

                for (int i = 0; i < count; i++)
                {
                    // Get position
                    var pos = GetIconPosition(hProcess, listViewHandle, i, pPoint);

                    // Get text (name)
                    var name = GetIconText(hProcess, listViewHandle, i, pItem, pText);

                    if (name != null)
                        result.Add(new DesktopIcon(i, name, pos.X, pos.Y));
                }
            }
            finally
            {
                VirtualFreeEx(hProcess, pRemoteMem, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }

        return result;
    }

    /// <summary>
    /// Get a single icon's position.
    /// </summary>
    public static (int X, int Y) GetIconPosition(IntPtr listViewHandle, int index)
    {
        if (listViewHandle == IntPtr.Zero) return (0, 0);

        GetWindowThreadProcessId(listViewHandle, out uint processId);
        IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ, false, processId);
        if (hProcess == IntPtr.Zero) return (0, 0);

        try
        {
            uint allocSize = (uint)Marshal.SizeOf<POINT>();
            IntPtr pRemote = VirtualAllocEx(hProcess, IntPtr.Zero, allocSize, MEM_COMMIT, PAGE_READWRITE);
            if (pRemote == IntPtr.Zero) return (0, 0);

            try
            {
                return GetIconPosition(hProcess, listViewHandle, index, pRemote);
            }
            finally
            {
                VirtualFreeEx(hProcess, pRemote, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Set a desktop icon's position (Mode B — real drag).
    /// </summary>
    public static bool SetIconPosition(IntPtr listViewHandle, int index, int x, int y)
    {
        if (listViewHandle == IntPtr.Zero) return false;

        GetWindowThreadProcessId(listViewHandle, out uint processId);
        IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_WRITE, false, processId);
        if (hProcess == IntPtr.Zero) return false;

        try
        {
            // MAKELPARAM(x, y) via POINT struct written to remote memory
            uint allocSize = (uint)Marshal.SizeOf<POINT>();
            IntPtr pRemote = VirtualAllocEx(hProcess, IntPtr.Zero, allocSize, MEM_COMMIT, PAGE_READWRITE);
            if (pRemote == IntPtr.Zero) return false;

            try
            {
                var pt = new POINT { X = x, Y = y };
                byte[] buffer = new byte[Marshal.SizeOf<POINT>()];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    Marshal.StructureToPtr(pt, handle.AddrOfPinnedObject(), false);
                }
                finally
                {
                    handle.Free();
                }

                WriteProcessMemory(hProcess, pRemote, buffer, (uint)buffer.Length, out _);
                SendMessage(listViewHandle, LVM_SETITEMPOSITION32, new IntPtr(index), pRemote);
                return true;
            }
            finally
            {
                VirtualFreeEx(hProcess, pRemote, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Save the current icon layout (all positions). Used for restore functionality.
    /// </summary>
    public static Dictionary<string, (int X, int Y)> SaveLayout(IntPtr listViewHandle)
    {
        var layout = new Dictionary<string, (int X, int Y)>();
        var icons = GetAllIcons(listViewHandle);
        foreach (var icon in icons)
            layout[icon.Name] = (icon.X, icon.Y);
        return layout;
    }

    /// <summary>
    /// Restore a previously saved icon layout.
    /// </summary>
    public static int RestoreLayout(IntPtr listViewHandle, Dictionary<string, (int X, int Y)> layout)
    {
        if (listViewHandle == IntPtr.Zero || layout.Count == 0) return 0;

        var currentIcons = GetAllIcons(listViewHandle);
        int restored = 0;

        foreach (var icon in currentIcons)
        {
            if (layout.TryGetValue(icon.Name, out var savedPos))
            {
                if (icon.X != savedPos.X || icon.Y != savedPos.Y)
                {
                    if (SetIconPosition(listViewHandle, icon.Index, savedPos.X, savedPos.Y))
                        restored++;
                }
            }
        }

        Log.Information("DesktopIconInterop: Restored {Count}/{Total} icon positions", restored, layout.Count);
        return restored;
    }

    // ── Private helpers ──

    private static (int X, int Y) GetIconPosition(IntPtr hProcess, IntPtr listViewHandle, int index, IntPtr pRemotePoint)
    {
        SendMessage(listViewHandle, LVM_GETITEMPOSITION, new IntPtr(index), pRemotePoint);

        byte[] buffer = new byte[Marshal.SizeOf<POINT>()];
        ReadProcessMemory(hProcess, pRemotePoint, buffer, (uint)buffer.Length, out _);

        int x = BitConverter.ToInt32(buffer, 0);
        int y = BitConverter.ToInt32(buffer, 4);
        return (x, y);
    }

    private static string? GetIconText(IntPtr hProcess, IntPtr listViewHandle, int index, IntPtr pRemoteItem, IntPtr pRemoteText)
    {
        // Set up LVITEMW structure to request text
        var item = new LVITEMW
        {
            mask = LVIF_TEXT,
            iItem = index,
            iSubItem = 0,
            pszText = pRemoteText,
            cchTextMax = 260
        };

        byte[] itemBuffer = new byte[Marshal.SizeOf<LVITEMW>()];
        var handle = GCHandle.Alloc(itemBuffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(item, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        // Write LVITEMW to remote memory
        WriteProcessMemory(hProcess, pRemoteItem, itemBuffer, (uint)itemBuffer.Length, out _);

        // Send LVM_GETITEMTEXT
        SendMessage(listViewHandle, LVM_GETITEMTEXTW, new IntPtr(index), pRemoteItem);

        // Read text back
        byte[] textBuffer = new byte[520];
        ReadProcessMemory(hProcess, pRemoteText, textBuffer, (uint)textBuffer.Length, out _);

        string text = Encoding.Unicode.GetString(textBuffer);
        int nullIdx = text.IndexOf('\0');
        if (nullIdx >= 0) text = text[..nullIdx];

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
