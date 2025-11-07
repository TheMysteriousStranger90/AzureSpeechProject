using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

public interface ITranscriptFileService
{
    Task<string> SaveTranscriptAsync(
        TranscriptionDocument transcript,
        TranscriptFormat format,
        string? translatedLanguage = null,
        CancellationToken cancellationToken = default);
}
