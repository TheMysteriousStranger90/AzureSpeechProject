using AzureSpeechProject.Interfaces;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using AzureSpeechProject.ViewModels;
using AzureSpeechProject.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSpeechProject.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<ILogger, FileLogger>();

        // Core Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        services.AddSingleton<IMicrophonePermissionService, MicrophonePermissionService>();

        // Audio and Speech Services
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<TranslationService>();
        services.AddTransient<ITranscriptFileService, TranscriptFileService>();
    }

    public static void AddCommonViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TranscriptionViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    public static void AddCommonWindows(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindow>();
    }
}
