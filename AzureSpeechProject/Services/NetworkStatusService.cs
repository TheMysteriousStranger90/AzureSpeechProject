using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
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
        catch (Exception ex)
        {
            _logger.Log($"Error checking network connection: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsInternetAvailableAsync()
    {
        try
        {
            _logger.Log("Checking internet connectivity...");
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 5000);
            var isAvailable = reply.Status == IPStatus.Success;
            _logger.Log($"Internet connectivity check: {isAvailable} (Status: {reply.Status})");
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking internet connectivity: {ex.Message}");
            return false;
        }
    }
}