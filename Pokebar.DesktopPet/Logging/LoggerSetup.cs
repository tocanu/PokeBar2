using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Pokebar.DesktopPet.Logging;

/// <summary>
/// Configura Serilog com arquivo rolling e debug sink.
/// Logs ficam em %AppData%/Pokebar/Logs
/// </summary>
public static class LoggerSetup
{
    public static void ConfigureLogger()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logPath = Path.Combine(appDataPath, "Pokebar", "Logs", "pokebar-.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Pokebar.DesktopPet")
            .WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB por arquivo
                rollOnFileSizeLimit: true)
            .CreateLogger();

        Log.Information("========== Pokebar Desktop Pet Started ==========");
        Log.Information("Log file: {LogPath}", logPath);
        Log.Information("Operating System: {OS}", Environment.OSVersion);
        Log.Information("CLR Version: {CLRVersion}", Environment.Version);
        Log.Information("Machine Name: {MachineName}", Environment.MachineName);
    }

    public static void CloseLogger()
    {
        Log.Information("========== Pokebar Desktop Pet Shutting Down ==========");
        Log.CloseAndFlush();
    }
}
