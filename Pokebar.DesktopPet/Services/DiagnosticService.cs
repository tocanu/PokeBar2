using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gera um pacote de diagnóstico (.zip) com logs, config sanitizada, save e info do sistema.
/// Usado para suporte: 1 clique → zip pronto para enviar.
/// </summary>
public static class DiagnosticService
{
    /// <summary>
    /// Gera o zip de diagnóstico no Desktop do usuário.
    /// Retorna o caminho do arquivo ou null se falhar.
    /// </summary>
    public static string? GenerateDiagnosticZip()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipPath = Path.Combine(desktop, $"pokebar_diagnostic_{timestamp}.zip");

            Log.Information("Generating diagnostic zip: {Path}", zipPath);

            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

            AddSystemInfo(archive);
            AddLogs(archive);
            AddConfig(archive);
            AddSaveData(archive);

            Log.Information("Diagnostic zip created successfully: {Path}", zipPath);
            return zipPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate diagnostic zip");
            return null;
        }
    }

    private static void AddSystemInfo(ZipArchive archive)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Pokebar Diagnostic Info ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("--- System ---");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"CLR: {Environment.Version}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
        sb.AppendLine();
        sb.AppendLine("--- Process ---");

        try
        {
            using var proc = Process.GetCurrentProcess();
            sb.AppendLine($"Process: {proc.ProcessName}");
            sb.AppendLine($"PID: {proc.Id}");
            sb.AppendLine($"Private Memory: {proc.PrivateMemorySize64 / 1024 / 1024} MB");
            sb.AppendLine($"Uptime: {(DateTime.Now - proc.StartTime):hh\\:mm\\:ss}");
        }
        catch
        {
            sb.AppendLine("(could not read process info)");
        }

        sb.AppendLine();
        sb.AppendLine("--- Paths ---");
        sb.AppendLine($"AppData: {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}");
        sb.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");

        try
        {
            // Listar monitores via taskbar info
            sb.AppendLine();
            sb.AppendLine("--- Monitors ---");
            var taskbars = TaskbarService.GetAllTaskbars();
            foreach (var tb in taskbars)
            {
                sb.AppendLine($"  Monitor {tb.MonitorIndex}: {tb.BoundsPx.Width}x{tb.BoundsPx.Height} @ DPI {tb.DpiScale:F2} (Primary: {tb.IsPrimary})");
            }
        }
        catch
        {
            sb.AppendLine("(could not read monitor info)");
        }

        WriteEntry(archive, "system_info.txt", sb.ToString());
    }

    private static void AddLogs(ZipArchive archive)
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pokebar", "Logs");

        if (!Directory.Exists(logsDir))
        {
            WriteEntry(archive, "logs/no_logs_found.txt", "Log directory does not exist.");
            return;
        }

        // Flush Serilog para garantir que os logs mais recentes estejam no disco
        Log.CloseAndFlush();

        var logFiles = Directory.GetFiles(logsDir, "pokebar*.txt", SearchOption.TopDirectoryOnly);
        foreach (var logFile in logFiles)
        {
            try
            {
                var fileName = Path.GetFileName(logFile);
                // Ler com compartilhamento (o Serilog pode ter lock)
                using var reader = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var entry = archive.CreateEntry($"logs/{fileName}", CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                reader.CopyTo(entryStream);
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        // Re-inicializar logger (foi fechado para flush)
        Pokebar.DesktopPet.Logging.LoggerSetup.ConfigureLogger();
    }

    private static void AddConfig(ZipArchive archive)
    {
        var configPath = GameplayConfigLoader.GetDefaultConfigPath();
        if (File.Exists(configPath))
        {
            try
            {
                var content = File.ReadAllText(configPath);
                WriteEntry(archive, "gameplay.json", content);
            }
            catch
            {
                WriteEntry(archive, "gameplay.json.error", "Could not read config file.");
            }
        }
        else
        {
            WriteEntry(archive, "gameplay.json.missing", "Config file not found.");
        }
    }

    private static void AddSaveData(ZipArchive archive)
    {
        var savePath = SaveManager.GetSavePath();
        if (File.Exists(savePath))
        {
            try
            {
                var content = File.ReadAllText(savePath);
                WriteEntry(archive, "save.json", content);
            }
            catch
            {
                WriteEntry(archive, "save.json.error", "Could not read save file.");
            }
        }
        else
        {
            WriteEntry(archive, "save.json.missing", "Save file not found.");
        }
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
