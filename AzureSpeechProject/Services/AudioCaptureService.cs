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
    
    public async Task StartCapturingAsync(int sampleRate = 16000, int bitsPerSample = 16, int channels = 1, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _captureLock.WaitAsync(cancellationToken);
        try
        {
            if (_isCapturing)
            {
                _logger.Log("Audio capture is already in progress");
                return;
            }
            
            if (sampleRate != 16000 && sampleRate != 8000)
            {
                _logger.Log($"Warning: Azure Speech Services recommends 16kHz or 8kHz. Current: {sampleRate}Hz");
            }
            
            if (bitsPerSample != 16)
            {
                _logger.Log($"Warning: Azure Speech Services recommends 16-bit audio. Current: {bitsPerSample}-bit");
            }
            
            if (channels != 1)
            {
                _logger.Log($"Warning: Azure Speech Services recommends mono audio. Current: {channels} channel(s)");
            }
            
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
        finally
        {
            _captureLock.Release();
        }
    }
    
    public async Task StopCapturingAsync()
    {
        if (_disposed || (!_isCapturing && _waveIn == null))
        {
            return;
        }
        
        await _captureLock.WaitAsync();
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
        finally
        {
            _captureLock.Release();
        }
    }
    
    public event EventHandler<AudioDataEventArgs>? AudioCaptured;
    
    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || !_isCapturing)
            return;
            
        try
        {
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);

            AudioCaptured?.Invoke(this, new AudioDataEventArgs(audioData, e.BytesRecorded));
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
                StopCapturingAsync().Wait(TimeSpan.FromSeconds(5));
                
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