using System;
using AzureSpeechProject.Logger;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

public class AudioCaptureService : IDisposable
{
    private readonly ILogger _logger;
    private WaveInEvent? _waveIn;
    private bool _isCapturing = false;
    
    public AudioCaptureService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public void StartCapturing(int sampleRate = 16000, int bitsPerSample = 16, int channels = 1)
    {
        if (_isCapturing)
        {
            _logger.Log("Audio capture is already in progress");
            return;
        }
        
        try
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, bitsPerSample, channels),
                BufferMilliseconds = 50
            };
            
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;
            
            _waveIn.StartRecording();
            _isCapturing = true;
            
            _logger.Log($"Started audio capture: {sampleRate}Hz, {bitsPerSample}-bit, {channels} channel(s)");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to start audio capture: {ex.Message}");
            throw;
        }
    }
    
    public void StopCapturing()
    {
        if (!_isCapturing || _waveIn == null)
        {
            return;
        }
        
        try
        {
            _waveIn.StopRecording();
        }
        catch (Exception ex)
        {
            _logger.Log($"Error stopping audio capture: {ex.Message}");
        }
        
        _isCapturing = false;
    }
    
    public event EventHandler<byte[]>? AudioCaptured;
    
    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {

        var audioData = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, audioData, e.BytesRecorded);

        AudioCaptured?.Invoke(this, audioData);
    }
    
    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;
        _logger.Log("Audio capture stopped");
        
        if (e.Exception != null)
        {
            _logger.Log($"Recording stopped due to error: {e.Exception.Message}");
        }
    }
    
    public void Dispose()
    {
        if (_waveIn != null)
        {
            StopCapturing();
            _waveIn.Dispose();
            _waveIn = null;
        }
    }
}