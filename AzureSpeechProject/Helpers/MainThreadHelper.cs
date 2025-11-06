using Avalonia.Threading;

namespace AzureSpeechProject.Helpers;

internal static class MainThreadHelper
{
    public static void InvokeOnMainThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    public static async Task InvokeAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
