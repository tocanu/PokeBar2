using System.Runtime.InteropServices;
using System.Windows.Interop;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Registra hotkeys globais do sistema via RegisterHotKey/UnregisterHotKey.
/// Processa WM_HOTKEY via HwndSource hook.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    // IDs de hotkey (devem ser únicos por janela)
    private const int HOTKEY_PAUSE_RESUME = 9001;
    private const int HOTKEY_DIAGNOSTIC = 9002;
    private const int HOTKEY_SCREENSHOT = 9003;
    private const int HOTKEY_PCBOX = 9004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private readonly List<int> _registeredIds = new();

    /// <summary>Disparado ao pressionar hotkey de Pause/Resume.</summary>
    public event Action? PauseResumePressed;

    /// <summary>Disparado ao pressionar hotkey de Diagnostic.</summary>
    public event Action? DiagnosticPressed;

    /// <summary>Disparado ao pressionar hotkey de Screenshot.</summary>
    public event Action? ScreenshotPressed;

    /// <summary>Disparado ao pressionar hotkey de PC Box.</summary>
    public event Action? PcBoxPressed;

    /// <summary>
    /// Inicializa o serviço e registra as hotkeys configuradas.
    /// Deve ser chamado após SourceInitialized (quando o HWND já existe).
    /// </summary>
    public void Initialize(IntPtr hwnd, HotkeyConfig config, string? screenshotHotkey = null, string? pcBoxHotkey = null)
    {
        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        RegisterFromString(HOTKEY_PAUSE_RESUME, config.PauseResume, "PauseResume");
        RegisterFromString(HOTKEY_DIAGNOSTIC, config.Diagnostic, "Diagnostic");

        if (!string.IsNullOrWhiteSpace(screenshotHotkey))
            RegisterFromString(HOTKEY_SCREENSHOT, screenshotHotkey, "Screenshot");

        if (!string.IsNullOrWhiteSpace(pcBoxHotkey))
            RegisterFromString(HOTKEY_PCBOX, pcBoxHotkey, "PcBox");

        Log.Information("HotkeyService initialized. PauseResume={Pause}, Diagnostic={Diag}, Screenshot={Screen}, PcBox={Box}",
            config.PauseResume, config.Diagnostic, screenshotHotkey ?? "none", pcBoxHotkey ?? "none");
    }

    private void RegisterFromString(int id, string hotkeyString, string name)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return;

        if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk))
        {
            Log.Warning("Failed to parse hotkey '{Hotkey}' for {Name}", hotkeyString, name);
            return;
        }

        var result = RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk);
        if (result)
        {
            _registeredIds.Add(id);
            Log.Debug("Hotkey registered: {Name} = {Hotkey} (mod=0x{Mod:X}, vk=0x{Vk:X})",
                name, hotkeyString, modifiers, vk);
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            Log.Warning("Failed to register hotkey {Name} = {Hotkey}. Win32 error: {Error}",
                name, hotkeyString, error);
        }
    }

    /// <summary>
    /// Parseia uma string como "Ctrl+Shift+P" em modifiers e virtual key code.
    /// </summary>
    private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    return false;
            }
        }

        // Último elemento é a tecla
        var keyStr = parts[^1].ToUpperInvariant();
        vk = KeyNameToVk(keyStr);

        return vk != 0;
    }

    private static uint KeyNameToVk(string key)
    {
        // Letras A-Z
        if (key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z')
            return (uint)key[0]; // VK_A = 0x41

        // Números 0-9
        if (key.Length == 1 && key[0] >= '0' && key[0] <= '9')
            return (uint)key[0]; // VK_0 = 0x30

        // Teclas especiais
        return key switch
        {
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "TAB" => 0x09,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "PAUSE" => 0x13,
            _ => 0
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
            return IntPtr.Zero;

        var id = wParam.ToInt32();
        switch (id)
        {
            case HOTKEY_PAUSE_RESUME:
                Log.Debug("Hotkey pressed: PauseResume");
                PauseResumePressed?.Invoke();
                handled = true;
                break;
            case HOTKEY_DIAGNOSTIC:
                Log.Debug("Hotkey pressed: Diagnostic");
                DiagnosticPressed?.Invoke();
                handled = true;
                break;
            case HOTKEY_SCREENSHOT:
                Log.Debug("Hotkey pressed: Screenshot");
                ScreenshotPressed?.Invoke();
                handled = true;
                break;
            case HOTKEY_PCBOX:
                Log.Debug("Hotkey pressed: PcBox");
                PcBoxPressed?.Invoke();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _hwndSource?.RemoveHook(WndProc);

        foreach (var id in _registeredIds)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _registeredIds.Clear();

        Log.Debug("HotkeyService disposed, hotkeys unregistered");
    }
}
