namespace AzureSpeechProject.Models;

public sealed class TranscriptionSegmentEventArgs : EventArgs
{
    public TranscriptionSegment Segment { get; }

    public TranscriptionSegmentEventArgs(TranscriptionSegment segment)
    {
        Segment = segment ?? throw new ArgumentNullException(nameof(segment));
    }
}
