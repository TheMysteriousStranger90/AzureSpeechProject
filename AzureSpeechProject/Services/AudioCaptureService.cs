using System;
using System.Threading;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

public class AudioCaptureService : IDisposable
{
    private readonly ILogger _logger;
    private WaveInEvent? _waveIn;
    private bool _isCapturing = false;
    private bool _disposed = false;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    
    public AudioCaptureService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.Log("AudioCaptureService initialized");
    }
    
    public void StartCapturing(int sampleRate = 16000, int bitsPerSample = 16, int channels = 1)
    {
        ThrowIfDisposed();
        
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
            throw new InvalidOperationException($"Audio capture initialization failed: {ex.Message}", ex);
        }
    }
    
    public async Task StartCapturingAsync(int sampleRate = 16000, int bitsPerSample = 16, int channels = 1, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => StartCapturing(sampleRate, bitsPerSample, channels), cancellationToken);
    }
    
    public void StopCapturing()
    {
        if (_disposed || (!_isCapturing && _waveIn == null))
        {
            return;
        }
        
        try
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _logger.Log("Audio capture stop requested");
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error stopping audio capture: {ex.Message}");
        }
    }
    
    public async Task StopCapturingAsync()
    {
        await Task.Run(() => StopCapturing());
    }
    
    public event EventHandler<byte[]>? AudioCaptured;
    
    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || !_isCapturing)
            return;
            
        try
        {
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);

            AudioCaptured?.Invoke(this, audioData);
            
            if (DateTime.Now.Millisecond % 1000 < 50)
            {
                _logger.Log($"Audio data captured: {e.BytesRecorded} bytes");
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error processing audio data: {ex.Message}");
        }
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
    
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioCaptureService));
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                StopCapturing();
                
                if (_waveIn != null)
                {
                    _waveIn.DataAvailable -= WaveIn_DataAvailable;
                    _waveIn.RecordingStopped -= WaveIn_RecordingStopped;
                    _waveIn.Dispose();
                    _waveIn = null;
                }
                
                _captureLock?.Dispose();
                _logger.Log("AudioCaptureService disposed");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error during AudioCaptureService disposal: {ex.Message}");
            }
            
            _disposed = true;
        }
    }
}