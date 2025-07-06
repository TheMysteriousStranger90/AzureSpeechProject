using System.Threading.Tasks;

namespace AzureSpeechProject.Services;

public interface INetworkStatusService
{
    Task<bool> IsInternetAvailableAsync();
    bool IsNetworkConnected();
}