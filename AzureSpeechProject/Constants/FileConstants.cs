using System;
using System.IO;

public static class FileConstants
{
    public static readonly string TranscriptsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Azure Speech Services",
        "Transcripts");
    
    public const string DefaultTranscriptPrefix = "azure_transcript";
    public const string TextExtension = ".txt";
    public const string JsonExtension = ".json";
    public const string SrtExtension = ".srt";
}