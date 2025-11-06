namespace AzureSpeechProject.Services
{
    public interface IMicrophonePermissionService
    {
        Task<bool> CheckMicrophonePermissionAsync();
        Task<bool> RequestMicrophonePermissionAsync();
    }
}
