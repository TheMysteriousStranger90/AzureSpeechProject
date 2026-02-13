namespace AzureSpeechProject.Constants;

internal static class PathConstants
{
    public static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AzureSpeechProject");

    public static readonly string DefaultOutputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "AzioSpeechRecognitionAndTranslation",
        "Transcripts");
}
