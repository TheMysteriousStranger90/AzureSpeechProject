using System.Collections.ObjectModel;
using System.Globalization;

namespace AzureSpeechProject.Models;

internal sealed class TranscriptionDocument
{
    public Collection<TranscriptionSegment> Segments { get; } = [];
    public string Language { get; set; } = "en-US";
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public bool IncludeTimestamps { get; set; } = true;

    public string GetTextTranscript()
    {
        return string.Join(Environment.NewLine, Segments.Select(s => s.ToFormattedString(IncludeTimestamps)));
    }

    public string GetSrtTranscript()
    {
        var srtBuilder = new System.Text.StringBuilder();
        TimeSpan startOffset = Segments.FirstOrDefault()?.Timestamp.TimeOfDay ?? TimeSpan.Zero;

        for (int i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            var startTime = segment.Timestamp.TimeOfDay - startOffset;
            var endTime = startTime + segment.Duration;

            srtBuilder.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            string timeLine = string.Format(CultureInfo.InvariantCulture, "{0} --> {1}",
                FormatSrtTime(startTime), FormatSrtTime(endTime));
            srtBuilder.AppendLine(timeLine);

            var speakerPrefix = !string.IsNullOrEmpty(segment.SpeakerId)
                ? $"Speaker {segment.SpeakerId}: "
                : "";
#pragma warning disable CA1305
            srtBuilder.AppendLine($"{speakerPrefix}{segment.Text}");
#pragma warning restore CA1305
            srtBuilder.AppendLine();
        }

        return srtBuilder.ToString();
    }

    private static string FormatSrtTime(TimeSpan time)
    {
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }
}
