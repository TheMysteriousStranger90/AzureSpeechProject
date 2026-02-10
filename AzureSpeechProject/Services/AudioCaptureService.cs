using AzureSpeechProject.Constants;
using AzureSpeechProject.Interfaces;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models.Events;
using NAudio.Wave;

namespace AzureSpeechProject.Services;

internal sealed class AudioCaptureService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private WaveInEvent? _waveIn;
    private bool _isCapturing;
    private bool _disposed;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private CancellationTokenSource? _captureCts;
    private long _totalBytesProcessed;

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
                BufferMilliseconds = AudioConstants.BufferMilliseconds
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;

            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _totalBytesProcessed = 0;

            _logger.Log("🎤 Attempting to start microphone...");

            try
            {
                _waveIn.StartRecording();
                _isCapturing = true;
                _logger.Log(
                    $"✅ Audio capture started: {settings.SampleRate}Hz, {settings.BitsPerSample}-bit, {settings.Channels} channel(s)");
            }
            catch (NAudio.MmException mmEx)
            {
                _logger.Log($"❌ NAudio microphone error: {mmEx.Message}");
                _logger.Log($"   Error result: {mmEx.Result}");
                _logger.Log("   Possible causes:");
                _logger.Log("   1. Microphone is being used by another application (Skype, Teams, Discord, etc.)");
                _logger.Log("   2. Microphone permissions denied in Windows Settings → Privacy → Microphone");
                _logger.Log("   3. Microphone driver issue - try updating audio drivers");
                _logger.Log("   4. Microphone hardware not properly connected");

                CleanupCapture();
                throw new InvalidOperationException(
                    "Cannot access microphone. Please check: 1) No other app is using it, 2) Windows microphone permissions are enabled, 3) Microphone is properly connected.",
                    mmEx);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Audio capture start was cancelled");
            CleanupCapture();
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"❌ Microphone access denied: {ex.Message}");
            _logger.Log("   Check Windows Settings → Privacy & Security → Microphone");
            CleanupCapture();
            throw;
        }
        catch (InvalidOperationException)
        {
            CleanupCapture();
            throw;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger.Log($"❌ COM error accessing microphone: {ex.Message} (HResult: {ex.HResult:X})");
            CleanupCapture();
            throw new InvalidOperationException($"Audio device COM error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.Log($"❌ Unexpected error starting audio capture: {ex.GetType().Name} - {ex.Message}");
            CleanupCapture();
            throw;
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
            await (_captureCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);

            if (_waveIn != null)
            {
                await Task.Run(() => _waveIn.StopRecording(), cancellationToken).ConfigureAwait(false);
                _logger.Log("Audio capture stop requested");
            }

            CleanupCapture();

            _logger.Log($"✅ Audio capture stopped. Total bytes processed: {_totalBytesProcessed}");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Audio capture stop was cancelled");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation stopping audio capture: {ex.Message}");
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

            Interlocked.Add(ref _totalBytesProcessed, e.BytesRecorded);

            AudioCaptured?.Invoke(this, new AudioCapturedEventArgs(audioData));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.Log($"Audio processing stopped due to disposal: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.Log($"Invalid argument in audio processing: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation in WaveIn_DataAvailable: {ex.Message}");
        }
    }

    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;
        _logger.Log("Audio capture stopped");

        if (e.Exception != null)
        {
            _logger.Log($"❌ Recording stopped due to error: {e.Exception.Message}");
        }
    }

    private void CleanupCapture()
    {
        _isCapturing = false;

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= WaveIn_DataAvailable;
            _waveIn.RecordingStopped -= WaveIn_RecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _captureCts?.Dispose();
        _captureCts = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _captureCts?.Cancel();
                CleanupCapture();
                _captureLock.Dispose();
                _logger.Log("AudioCaptureService disposed");
            }
            catch (ObjectDisposedException ex)
            {
                _logger.Log($"Object already disposed during cleanup: {ex.Message}");
            }

            _disposed = true;
        }
    }
}
