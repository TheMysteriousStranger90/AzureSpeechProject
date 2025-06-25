﻿namespace AzureSpeechProject.Models;

public class TranscriptionOptions
{
    public TranscriptFormat OutputFormat { get; set; } = TranscriptFormat.Text;
    public bool IncludeTimestamps { get; set; } = true;
    public bool DetectSpeakers { get; set; } = false;
    public string? CustomModelId { get; set; }
    public int MaxDurationSeconds { get; set; } = 300;
    public string Language { get; set; } = "en-US";
    public float ConfidenceThreshold { get; set; } = 0.5f;
    public bool EnableProfanityFilter { get; set; } = true;
    public bool EnableWordLevelTimestamps { get; set; } = true;
}