using Avalonia;
using Avalonia.ReactiveUI;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using AzureSpeechProject.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using FileLogger = AzureSpeechProject.Logger.FileLogger;

namespace AzureSpeechProject;

internal static class Program
{
    private static ServiceProvider? _serviceProvider;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _serviceProvider?.Dispose();
        }
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
        // Services - Singleton
        services.AddSingleton<ILogger, FileLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IMicrophonePermissionService, MicrophonePermissionService>();

        // Services - Transient
        services.AddTransient<ITranscriptFileService, TranscriptFileService>();

        // Audio Services
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<TranslationService>();

        // ViewModels
        services.AddTransient<TranscriptionViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Views
        services.AddTransient<Views.MainWindow>();
        services.AddTransient<Views.TranscriptionView>();
        services.AddTransient<Views.SettingsView>();
    }
}
