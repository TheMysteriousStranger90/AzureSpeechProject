namespace AzureSpeechProject.Constants;

internal static class TimeoutConstants
{
    public static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan SaveTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MicrophoneTestTimeout = TimeSpan.FromSeconds(3);
    public const int PingTimeoutMs = 2000;
}
