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
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        var serviceProvider = services.BuildServiceProvider();

        BuildAvaloniaApp(serviceProvider)
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(ServiceProvider serviceProvider)
    {
        return AppBuilder.Configure<App>(() => new App(serviceProvider))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register configurations
        services.AddSingleton<IEnvironmentConfiguration, EnvironmentConfiguration>();
        
        // Register services
        services.AddSingleton<ILogger, FileLogger>();
        services.AddSingleton<SecretsService>();
        services.AddSingleton<ITranscriptFileService, TranscriptFileService>();
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<TranslationService>();
        
        // Register ViewModels
        services.AddSingleton<ViewModelBase>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<TranscriptionViewModel>();
        services.AddTransient<SettingsViewModel>();
        
        // Register Views
        services.AddTransient<Views.MainWindow>();
        services.AddTransient<Views.TranscriptionView>();
        services.AddTransient<Views.SettingsView>();
    }
}