using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AzureSpeechProject.ViewModels;
using AzureSpeechProject.Views;
using AzureSpeechProject.Logger;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSpeechProject;

internal sealed class App : Application
{
    private readonly ServiceProvider? _serviceProvider;
    private readonly ILogger? _logger;

    public App(ServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider?.GetService<ILogger>();

        if (serviceProvider != null)
        {
            _logger?.Log("App initialized with ServiceProvider");
        }
    }

    public override void Initialize()
    {
        try
        {
            AvaloniaXamlLoader.Load(this);
            _logger?.Log("Avalonia XAML loaded successfully");
        }
        catch (Exception ex)
        {
            _logger?.Log($"Critical error loading XAML: {ex.Message}");
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            _logger?.Log("Framework initialization started");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = CreateMainWindow();

                desktop.ShutdownRequested += OnShutdownRequested;
                desktop.Exit += OnExit;
            }

            base.OnFrameworkInitializationCompleted();
            _logger?.Log("Application initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.Log($"Fatal error during framework initialization: {ex.Message}");
            throw;
        }
    }

    private MainWindow CreateMainWindow()
    {
        if (_serviceProvider == null)
        {
            _logger?.Log("Creating MainWindow without ServiceProvider (Design mode)");
            return new MainWindow();
        }

        try
        {
            var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            var window = new MainWindow
            {
                DataContext = mainViewModel
            };

            _logger?.Log("MainWindow created with ViewModel successfully");
            return window;
        }
        catch (Exception ex)
        {
            _logger?.Log($"Error creating MainWindow with ViewModel: {ex.Message}");
            _logger?.Log("Creating fallback MainWindow without ViewModel");

            return new MainWindow();
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _logger?.Log("Application shutdown requested");
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _logger?.Log($"Application exiting with code: {e.ApplicationExitCode}");
    }
}
