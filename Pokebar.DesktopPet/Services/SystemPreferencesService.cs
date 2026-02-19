using Microsoft.Win32;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Detecta preferências do sistema Windows: high contrast, reduced motion, tema.
/// Emite eventos quando as preferências mudam em tempo real.
/// </summary>
public sealed class SystemPreferencesService : IDisposable
{
    public bool HighContrast { get; private set; }
    public bool ReducedMotion { get; private set; }

    /// <summary>Disparado quando qualquer preferência muda.</summary>
    public event Action? PreferencesChanged;

    public SystemPreferencesService()
    {
        RefreshAll();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Log.Debug("SystemPreferencesService initialized. HighContrast={HC}, ReducedMotion={RM}",
            HighContrast, ReducedMotion);
    }

    private void RefreshAll()
    {
        HighContrast = System.Windows.SystemParameters.HighContrast;
        ReducedMotion = DetectReducedMotion();
    }

    /// <summary>
    /// Windows não tem um flag "Reduced Motion" como macOS/iOS.
    /// Usamos SPI_GETCLIENTAREAANIMATION como proxy — quando o usuário
    /// desabilita "animações no Windows", esse flag fica false.
    /// </summary>
    private static bool DetectReducedMotion()
    {
        try
        {
            const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
            bool animEnabled = true;
            SystemParametersInfoBool(SPI_GETCLIENTAREAANIMATION, 0, ref animEnabled, 0);
            return !animEnabled;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoBool(
        uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Accessibility
            or UserPreferenceCategory.General
            or UserPreferenceCategory.VisualStyle)
        {
            var oldHC = HighContrast;
            var oldRM = ReducedMotion;
            RefreshAll();

            if (oldHC != HighContrast || oldRM != ReducedMotion)
            {
                Log.Information("System preferences changed. HighContrast={HC}, ReducedMotion={RM}",
                    HighContrast, ReducedMotion);
                PreferencesChanged?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
