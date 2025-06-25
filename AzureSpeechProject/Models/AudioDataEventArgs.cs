using System;

namespace AzureSpeechProject.Models;

public class AudioDataEventArgs : EventArgs
{
    public byte[] Data { get; }
    public int BytesRecorded { get; }
    public DateTime Timestamp { get; }
    
    public AudioDataEventArgs(byte[] data, int bytesRecorded)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        BytesRecorded = bytesRecorded;
        Timestamp = DateTime.Now;
    }
}