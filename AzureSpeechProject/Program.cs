using Avalonia;
using Avalonia.ReactiveUI;
using AzureSpeechProject.Interfaces;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using AzureSpeechProject.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSpeechProject;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            using var serviceProvider = BuildServiceProvider();

            BuildAvaloniaApp(serviceProvider)
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            File.WriteAllText("crash.log", ex.ToString());
            throw;
        }
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp(ServiceProvider serviceProvider)
    {
        return AppBuilder.Configure(() => new App(serviceProvider))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<ILogger, FileLogger>();

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IMicrophonePermissionService, MicrophonePermissionService>();

        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<TranslationService>();
        services.AddTransient<ITranscriptFileService, TranscriptFileService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TranscriptionViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
