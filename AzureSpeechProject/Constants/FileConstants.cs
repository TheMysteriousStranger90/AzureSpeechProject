namespace AzureSpeechToTextApp.Constants;

public static class FileConstants
{
    public static readonly string TranscriptsDirectory = Path.Combine(
        Directory.GetCurrentDirectory(), "Transcripts");
    
    public const string DefaultTranscriptPrefix = "azure_transcript";
    public const string TextExtension = ".txt";
    public const string JsonExtension = ".json";
    public const string SrtExtension = ".srt";
}