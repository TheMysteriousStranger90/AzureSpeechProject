namespace AzureSpeechProject.Interfaces;

internal interface IMicrophonePermissionService
{
    Task<bool> CheckMicrophonePermissionAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestMicrophonePermissionAsync(CancellationToken cancellationToken = default);
    Task OpenPrivacySettingsAsync();
}
