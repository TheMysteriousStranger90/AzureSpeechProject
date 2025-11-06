namespace AzureSpeechProject.Services
{
    public interface IMicrophonePermissionService
    {
        Task<bool> CheckMicrophonePermissionAsync(CancellationToken cancellationToken = default);
        Task<bool> RequestMicrophonePermissionAsync(CancellationToken cancellationToken = default);
    }
}
