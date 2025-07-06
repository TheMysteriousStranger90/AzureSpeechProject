using System.Threading.Tasks;
using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task ResetToDefaultsAsync();
}