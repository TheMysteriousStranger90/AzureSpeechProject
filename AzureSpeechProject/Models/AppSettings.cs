namespace AzureSpeechProject.Models;

internal sealed class AppSettings
{
    public string Region { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string SpeechLanguage { get; set; } = "en-US";
    public int SampleRate { get; set; } = 16000;
    public int BitsPerSample { get; set; } = 16;
    public int Channels { get; set; } = 1;
    public string OutputDirectory { get; set; } = string.Empty;
}
