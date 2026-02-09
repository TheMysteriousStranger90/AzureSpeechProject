namespace AzureSpeechProject.Models;

internal sealed class TranscriptionSegment
{
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public string? SpeakerId { get; set; }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] {(string.IsNullOrEmpty(SpeakerId) ? "" : $"Speaker {SpeakerId}: ")}{Text}";
}
