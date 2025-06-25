namespace AzureSpeechProject.Models;

public class TranscriptionOptions
{
    public TranscriptFormat OutputFormat { get; set; } = TranscriptFormat.Text;
    public bool IncludeTimestamps { get; set; } = false;
    public bool DetectSpeakers { get; set; } = false;
    public string? CustomModelId { get; set; }
    public int MaxDurationSeconds { get; set; } = 300;
}