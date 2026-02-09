namespace AzureSpeechProject.Models;

internal sealed class TranslationResult
{
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
