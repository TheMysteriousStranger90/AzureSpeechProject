using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

public class AudioCaptureService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private WaveInEvent? _waveIn;
    private bool _isCapturing;
    private bool _disposed;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private CancellationTokenSource? _captureCts;

    public AudioCaptureService(ILogger logger, ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger.Log("AudioCaptureService initialized");
    }

    public async Task StartCapturingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_isCapturing)
        {
            _logger.Log("Audio capture is already in progress");
            return;
        }

        await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(settings.SampleRate, settings.BitsPerSample, settings.Channels),
                BufferMilliseconds = 50
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;

            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _waveIn.StartRecording();
            _isCapturing = true;

            _logger.Log(
                $"Started audio capture: {settings.SampleRate}Hz, {settings.BitsPerSample}-bit, {settings.Channels} channel(s)");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Audio capture start was cancelled");
            CleanupCapture();
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to start audio capture: {ex.Message}");
            CleanupCapture();
            throw new InvalidOperationException($"Audio capture initialization failed: {ex.Message}", ex);
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public async Task StopCapturingAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || (!_isCapturing && _waveIn == null))
        {
            return;
        }

        await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _captureCts?.CancelAsync();

            if (_waveIn != null)
            {
                await Task.Run(() => _waveIn.StopRecording(), cancellationToken).ConfigureAwait(false);
                _logger.Log("Audio capture stop requested");
            }

            CleanupCapture();
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Audio capture stop was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error stopping audio capture: {ex.Message}");
            throw;
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public event EventHandler<AudioCapturedEventArgs>? AudioCaptured;

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || !_isCapturing || _captureCts?.Token.IsCancellationRequested == true)
            return;

        try
        {
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);

            AudioCaptured?.Invoke(this, new AudioCapturedEventArgs(audioData));

            if (DateTime.Now.Millisecond % 1000 < 50)
            {
                _logger.Log($"Audio data captured: {e.BytesRecorded} bytes");
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.Log("Audio processing stopped due to disposal");
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

    private void CleanupCapture()
    {
        _isCapturing = false;
        _captureCts?.Dispose();
        _captureCts = null;
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
                _captureCts?.Cancel();
                _captureCts?.Dispose();

                if (_waveIn != null)
                {
                    _waveIn.StopRecording();
                    _waveIn.DataAvailable -= WaveIn_DataAvailable;
                    _waveIn.RecordingStopped -= WaveIn_RecordingStopped;
                    _waveIn.Dispose();
                    _waveIn = null;
                }

                _captureLock?.Dispose();
                _logger.Log("AudioCaptureService disposed");
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            _disposed = true;
        }
    }
}
