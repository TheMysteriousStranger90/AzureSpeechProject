namespace AzureSpeechToTextApp.Models;

public class TranscriptionDocument
{
    public List<TranscriptionSegment> Segments { get; set; } = new();
    public string Language { get; set; } = "en-US";
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    
    public string GetTextTranscript()
    {
        return string.Join(Environment.NewLine, Segments.Select(s => s.ToString()));
    }
    
    public string GetSrtTranscript()
    {
        var srtBuilder = new System.Text.StringBuilder();
        TimeSpan startOffset = Segments.FirstOrDefault()?.Timestamp.TimeOfDay ?? TimeSpan.Zero;
        
        for (int i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            var startTime = (segment.Timestamp.TimeOfDay - startOffset);
            var endTime = startTime + segment.Duration;
            
            srtBuilder.AppendLine((i + 1).ToString());
            srtBuilder.AppendLine($"{FormatSrtTime(startTime)} --> {FormatSrtTime(endTime)}");
            srtBuilder.AppendLine(segment.Text);
            srtBuilder.AppendLine();
        }
        
        return srtBuilder.ToString();
    }
    
    private string FormatSrtTime(TimeSpan time)
    {
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }
}