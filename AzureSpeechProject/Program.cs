using Avalonia;
using Avalonia.ReactiveUI;
using System;
using AzureSpeechProject.Configurations;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using AzureSpeechProject.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using FileLogger = AzureSpeechProject.Logger.FileLogger;

namespace AzureSpeechProject;

class Program
{
    private static ServiceProvider? _serviceProvider;
    
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        _serviceProvider = services.BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>(() => new App(_serviceProvider!))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register configurations
        services.AddSingleton<IEnvironmentConfiguration, EnvironmentConfiguration>();
        
        // Register core services
        services.AddSingleton<ILogger, FileLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<SecretsService>();
        services.AddSingleton<ITranscriptFileService, TranscriptFileService>();
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<TranslationService>();
        
        // Register ViewModels
        services.AddTransient<TranscriptionViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        
        // Register Views
        services.AddTransient<Views.MainWindow>();
        services.AddTransient<Views.TranscriptionView>();
        services.AddTransient<Views.SettingsView>();
    }
}