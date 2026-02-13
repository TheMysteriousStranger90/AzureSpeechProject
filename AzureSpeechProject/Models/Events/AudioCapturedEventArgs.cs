namespace AzureSpeechProject.Models.Events;

internal sealed class AudioCapturedEventArgs : EventArgs
{
    private readonly byte[] _audioData;

    public AudioCapturedEventArgs(byte[] audioData)
    {
        _audioData = audioData ?? throw new ArgumentNullException(nameof(audioData));
    }

    public byte[] GetAudioDataArray()
    {
        var copy = new byte[_audioData.Length];
        Array.Copy(_audioData, copy, _audioData.Length);
        return copy;
    }

    public int Length => _audioData.Length;
}
