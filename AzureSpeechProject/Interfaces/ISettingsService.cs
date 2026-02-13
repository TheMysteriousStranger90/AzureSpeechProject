using AzureSpeechProject.Models;

namespace AzureSpeechProject.Interfaces;

internal interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}
