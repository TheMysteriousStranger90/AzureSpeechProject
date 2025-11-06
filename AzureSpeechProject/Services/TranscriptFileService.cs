using System.Globalization;
using System.Text.Json;
using AzureSpeechProject.Constants;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

internal sealed class TranscriptFileService : ITranscriptFileService
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TranscriptFileService(ILogger logger, ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task<string> SaveTranscriptAsync(
        TranscriptionDocument transcript,
        TranscriptFormat format,
        string? translatedLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        string extension = format switch
        {
            TranscriptFormat.Json => FileConstants.JsonExtension,
            TranscriptFormat.Srt => FileConstants.SrtExtension,
            _ => FileConstants.TextExtension
        };

        string filePath = await GenerateTranscriptFilePathAsync(extension, translatedLanguage, cancellationToken).ConfigureAwait(false);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.Log($"Created directory: {directory}");
            }

            string content = format switch
            {
                TranscriptFormat.Json => JsonSerializer.Serialize(transcript, JsonOptions),
                TranscriptFormat.Srt => transcript.GetSrtTranscript(),
                _ => transcript.GetTextTranscript()
            };

            await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);

            string logMessage = !string.IsNullOrEmpty(translatedLanguage)
                ? $"Saved {translatedLanguage} translation to {filePath}"
                : $"Saved transcript to {filePath}";

            _logger.Log(logMessage);

            return filePath;
        }
        catch (IOException ex)
        {
            _logger.Log($"IO error saving transcript: {ex.Message}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Access denied saving transcript: {ex.Message}");
            throw;
        }
    }

    private async Task<string> GenerateTranscriptFilePathAsync(
        string extension,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            var outputDirectory = settings.OutputDirectory;

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                _logger.Log($"Created output directory: {outputDirectory}");
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            string filename = string.IsNullOrEmpty(languageCode)
                ? $"{FileConstants.DefaultTranscriptPrefix}_{timestamp}{extension}"
                : $"{FileConstants.DefaultTranscriptPrefix}_{languageCode}_{timestamp}{extension}";

            var filePath = Path.Combine(outputDirectory, filename);
            _logger.Log($"Generated transcript path: {filePath}");

            return filePath;
        }
        catch (IOException ex)
        {
            _logger.Log($"IO error generating transcript path: {ex.Message}");

            string tempFilename = string.IsNullOrEmpty(languageCode)
                ? $"{FileConstants.DefaultTranscriptPrefix}_{Guid.NewGuid():N}{extension}"
                : $"{FileConstants.DefaultTranscriptPrefix}_{languageCode}_{Guid.NewGuid():N}{extension}";

            var tempPath = Path.Combine(Path.GetTempPath(), tempFilename);

            _logger.Log($"Using fallback path: {tempPath}");
            return tempPath;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Access error generating transcript path: {ex.Message}");

            string tempFilename = string.IsNullOrEmpty(languageCode)
                ? $"{FileConstants.DefaultTranscriptPrefix}_{Guid.NewGuid():N}{extension}"
                : $"{FileConstants.DefaultTranscriptPrefix}_{languageCode}_{Guid.NewGuid():N}{extension}";

            var tempPath = Path.Combine(Path.GetTempPath(), tempFilename);

            _logger.Log($"Using fallback path: {tempPath}");
            return tempPath;
        }
    }
}
