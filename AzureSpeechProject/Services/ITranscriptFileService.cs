using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

public interface ITranscriptFileService
{
    Task<string> SaveTranscriptAsync(
        TranscriptionDocument transcript,
        TranscriptFormat format,
        CancellationToken cancellationToken = default,
        string? translatedLanguage = null);
}
