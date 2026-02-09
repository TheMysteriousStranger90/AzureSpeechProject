namespace AzureSpeechProject.Interfaces;

internal interface INetworkStatusService
{
    Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default);
    bool IsNetworkConnected();
}
