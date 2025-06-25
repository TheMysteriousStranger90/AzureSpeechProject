using System;

namespace AzureSpeechProject.Models;

public class TranscriptionSegment
{
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public string? SpeakerId { get; set; }
    public float? Confidence { get; set; } = null;
    public string? JsonDetails { get; set; }
    
    public override string ToString() => 
        $"[{Timestamp:HH:mm:ss}] {(string.IsNullOrEmpty(SpeakerId) ? "" : $"Speaker {SpeakerId}: ")}{Text}";
}