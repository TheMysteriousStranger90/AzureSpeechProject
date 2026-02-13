using System.Text;

namespace AzureSpeechProject.Constants;

internal static class SecurityConstants
{
    private const string EntropyString = "AzureSpeechProject_v1.0_Entropy_Azure";

    public static byte[] GetEntropy() =>
        Encoding.UTF8.GetBytes(EntropyString);
}
