using Avalonia;
using Avalonia.ReactiveUI;
using AzureSpeechProject.Extensions;
using AzureSpeechProject.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSpeechProject;

internal static class Program
{
    private static SingleInstanceService? _singleInstance;
    private static ServiceProvider? _serviceProvider;

    [STAThread]
    public static int Main(string[] args)
    {
        _singleInstance = new SingleInstanceService();

        if (!_singleInstance.TryAcquire())
        {
            SingleInstanceService.BringExistingInstanceToFront();
            _singleInstance.Dispose();
            return 1;
        }

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            File.WriteAllText("crash.log", ex.ToString());
            throw;
        }
        finally
        {
            _serviceProvider?.Dispose();
            _singleInstance.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddCommonServices();

        services.AddCommonViewModels();

        services.AddCommonWindows();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App(_serviceProvider))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
