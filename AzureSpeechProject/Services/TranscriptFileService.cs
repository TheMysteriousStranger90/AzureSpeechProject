using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AzureSpeechProject.Constants;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;

namespace AzureSpeechProject.Services;

public class TranscriptFileService : ITranscriptFileService
{
    private readonly ILogger _logger;
    
    public TranscriptFileService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<string> SaveTranscriptAsync(
        TranscriptionDocument transcript, 
        TranscriptFormat format, 
        CancellationToken cancellationToken = default,
        string? translatedLanguage = null)
    {
        string extension = format switch
        {
            TranscriptFormat.Json => FileConstants.JsonExtension,
            TranscriptFormat.Srt => FileConstants.SrtExtension,
            _ => FileConstants.TextExtension
        };
        
        string filePath = GenerateTranscriptFilePath(extension, translatedLanguage);
        
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string content = format switch
            {
                TranscriptFormat.Json => JsonSerializer.Serialize(transcript, new JsonSerializerOptions { WriteIndented = true }),
                TranscriptFormat.Srt => transcript.GetSrtTranscript(),
                _ => transcript.GetTextTranscript()
            };

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            
            string logMessage = !string.IsNullOrEmpty(translatedLanguage) 
                ? $"Saved {translatedLanguage} translation to {filePath}" 
                : $"Saved transcript to {filePath}";
                
            _logger.Log(logMessage);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error saving transcript: {ex.Message}");
            throw;
        }
    }
    
    public string GenerateTranscriptFilePath(string extension, string? languageCode = null)
    {
        try
        {
            if (!Directory.Exists(FileConstants.TranscriptsDirectory))
            {
                Directory.CreateDirectory(FileConstants.TranscriptsDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            string filename = string.IsNullOrEmpty(languageCode)
                ? $"{FileConstants.DefaultTranscriptPrefix}_{timestamp}{extension}"
                : $"{FileConstants.DefaultTranscriptPrefix}_{languageCode}_{timestamp}{extension}";
            
            return Path.Combine(FileConstants.TranscriptsDirectory, filename);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error generating transcript path: {ex.Message}");
            
            string tempFilename = string.IsNullOrEmpty(languageCode)
                ? $"{FileConstants.DefaultTranscriptPrefix}_{Guid.NewGuid():N}{extension}"
                : $"{FileConstants.DefaultTranscriptPrefix}_{languageCode}_{Guid.NewGuid():N}{extension}";
                
            var tempPath = Path.Combine(Path.GetTempPath(), tempFilename);
            
            _logger.Log($"Using fallback path: {tempPath}");
            return tempPath;
        }
    }
}