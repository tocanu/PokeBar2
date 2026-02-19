using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Captura screenshot do pet e copia para a área de transferência.
/// </summary>
public static class ScreenshotService
{
    /// <summary>
    /// Captura o frame atual do pet como PNG e copia para o clipboard.
    /// Retorna o caminho do arquivo salvo, ou null se falhar.
    /// </summary>
    public static string? CaptureToClipboard(BitmapSource? currentFrame, int dex)
    {
        if (currentFrame == null)
        {
            Log.Warning("Screenshot failed: no current frame");
            return null;
        }

        try
        {
            // Copiar para clipboard
            System.Windows.Clipboard.SetImage(currentFrame);

            // Salvar em arquivo também
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pokebar", "Screenshots");
            Directory.CreateDirectory(dir);

            var fileName = $"pokebar_{dex}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(dir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(currentFrame));
            encoder.Save(stream);

            Log.Information("Screenshot saved: {Path}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture screenshot");
            return null;
        }
    }
}
