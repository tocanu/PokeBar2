using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Pokebar.DesktopPet.Logging;
using Serilog;

namespace Pokebar.DesktopPet;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configurar logging antes de qualquer outra coisa
        LoggerSetup.ConfigureLogger();

        // Captura global de exceções não tratadas
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Log.Information("Application startup complete");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting with code {ExitCode}", e.ApplicationExitCode);
        LoggerSetup.CloseLogger();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Fatal(exception, "Unhandled exception in AppDomain. IsTerminating: {IsTerminating}", e.IsTerminating);

        if (e.IsTerminating)
        {
            MessageBox.Show(
                $"Erro fatal não tratado:\n{exception?.Message}\n\nO aplicativo será encerrado.\nVerifique os logs em %AppData%\\Pokebar\\Logs",
                "Pokebar - Erro Fatal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception in UI thread");

        MessageBox.Show(
            $"Erro não tratado na interface:\n{e.Exception.Message}\n\nVerifique os logs em %AppData%\\Pokebar\\Logs",
            "Pokebar - Erro",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true; // Impede que o app feche
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Marca como observada para não crashar o app
    }
}

