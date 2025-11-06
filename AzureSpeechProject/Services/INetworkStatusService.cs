namespace AzureSpeechProject.Services
{
    public interface INetworkStatusService
    {
        Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default);
        bool IsNetworkConnected();
    }
}
