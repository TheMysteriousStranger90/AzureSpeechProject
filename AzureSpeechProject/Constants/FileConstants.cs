namespace AzureSpeechProject.Constants;

internal static class FileConstants
{
    public static readonly string TranscriptsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Azio Speech",
        "Transcripts");

    public const string DefaultTranscriptPrefix = "azio_transcript";
    public const string TextExtension = ".txt";
    public const string JsonExtension = ".json";
    public const string SrtExtension = ".srt";
}
