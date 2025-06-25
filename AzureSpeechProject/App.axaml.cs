using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AzureSpeechProject.ViewModels;
using AzureSpeechProject.Views;
using AzureSpeechProject.Logger;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AzureSpeechProject;

public partial class App : Application
{
    private readonly ServiceProvider? _serviceProvider;
    private ILogger? _logger;

    public App(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        try
        {
            _logger = _serviceProvider?.GetService<ILogger>();
            _logger?.Log("App constructor called with ServiceProvider");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in App constructor: {ex.Message}");
        }
    }

    public App()
    {
        Console.WriteLine("App constructor called without ServiceProvider (Design mode)");
    }

    public override void Initialize()
    {
        try
        {
            _logger?.Log("Initializing Avalonia XAML...");
            AvaloniaXamlLoader.Load(this);
            _logger?.Log("Avalonia XAML loaded successfully");
        }
        catch (Exception ex)
        {
            _logger?.Log($"Error loading XAML: {ex.Message}");
            Console.WriteLine($"Error loading XAML: {ex}");
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            _logger?.Log("Framework initialization completed, creating main window...");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (_serviceProvider != null)
                {
                    try
                    {
                        var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                        desktop.MainWindow = new MainWindow
                        {
                            DataContext = mainViewModel
                        };
                        _logger?.Log("Main window created with ViewModel successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Error creating main window with ViewModel: {ex.Message}");
                        Console.WriteLine($"Error creating main window with ViewModel: {ex}");
                        
                        desktop.MainWindow = new MainWindow();
                        _logger?.Log("Created fallback main window without ViewModel");
                    }
                }
                else
                {
                    desktop.MainWindow = new MainWindow();
                    _logger?.Log("Created main window without ServiceProvider (Design mode)");
                }
            }

            base.OnFrameworkInitializationCompleted();
            _logger?.Log("Application initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.Log($"Fatal error during framework initialization: {ex.Message}");
            Console.WriteLine($"Fatal error during framework initialization: {ex}");
            throw;
        }
    }
}