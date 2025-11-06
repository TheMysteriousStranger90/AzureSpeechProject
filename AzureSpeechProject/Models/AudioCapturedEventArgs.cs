namespace AzureSpeechProject.Models;

public sealed class AudioCapturedEventArgs : EventArgs
{
    private readonly byte[] _audioData;

    public IReadOnlyList<byte> AudioData => _audioData;

    public AudioCapturedEventArgs(byte[] audioData)
    {
        _audioData = audioData ?? throw new ArgumentNullException(nameof(audioData));
    }

    internal byte[] GetAudioDataArray() => _audioData;
}
