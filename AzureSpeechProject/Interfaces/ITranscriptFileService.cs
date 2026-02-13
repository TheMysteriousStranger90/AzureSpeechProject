using AzureSpeechProject.Models;

namespace AzureSpeechProject.Interfaces;

internal interface ITranscriptFileService
{
    Task<string> SaveTranscriptAsync(
        TranscriptionDocument transcript,
        TranscriptFormat format,
        string? translatedLanguage = null,
        CancellationToken cancellationToken = default);
}
