using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Pokebar.Core.Localization;
using Pokebar.DesktopPet.Logging;
using Serilog;

namespace Pokebar.DesktopPet;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configurar logging antes de qualquer outra coisa
        LoggerSetup.ConfigureLogger();

        // Inicializar localização (detecta idioma do sistema ou salvo)
        _ = Localizer.Instance;
        Log.Information("Locale: {Culture}", Localizer.Instance.Culture);

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
            System.Windows.MessageBox.Show(
                Localizer.Get("error.fatal.message", exception?.Message ?? "Unknown"),
                Localizer.Get("error.fatal.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception in UI thread");

        System.Windows.MessageBox.Show(
            Localizer.Get("error.ui.message", e.Exception.Message),
            Localizer.Get("error.ui.title"),
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

