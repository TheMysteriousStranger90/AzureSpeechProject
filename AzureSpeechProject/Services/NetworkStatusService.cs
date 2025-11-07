using System.Net.NetworkInformation;
using AzureSpeechProject.Logger;

namespace AzureSpeechProject.Services;

public class NetworkStatusService : INetworkStatusService
{
    private readonly ILogger _logger;

    public NetworkStatusService(ILogger logger)
    {
        _logger = logger;
    }

    public bool IsNetworkConnected()
    {
        try
        {
            var isConnected = NetworkInterface.GetIsNetworkAvailable();
            _logger.Log($"Network connection check: {isConnected}");
            return isConnected;
        }
        catch (NetworkInformationException ex)
        {
            _logger.Log($"Error checking network connection: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Unexpected error checking network connection: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ping = new Ping();

            var reply = await ping.SendPingAsync("8.8.8.8", 2000).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var isAvailable = reply.Status == IPStatus.Success;
            _logger.Log($"Internet connectivity check: {isAvailable} (Status: {reply.Status})");
            return isAvailable;
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Internet connectivity check was cancelled");
            throw;
        }
        catch (PingException ex)
        {
            _logger.Log($"Ping failed - no internet connection: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Log($"Unexpected error checking internet connectivity: {ex.Message}");
            throw;
        }
    }
}
