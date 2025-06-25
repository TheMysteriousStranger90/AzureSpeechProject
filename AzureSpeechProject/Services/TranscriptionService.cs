using System;
using System.Threading;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AzureSpeechProject.Services;

public class TranscriptionService : IDisposable
{
    private readonly SecretsService _secretsService;
    private readonly AudioCaptureService _audioCapture;
    private readonly ILogger _logger;

    private TranscriptionDocument _transcriptionDocument = new();
    private bool _isTranscribing = false;
    private bool _disposed = false;
    private PushAudioInputStream? _audioInputStream;
    private SpeechRecognizer? _recognizer;
    private SpeechConfig? _speechConfig;
    private AudioConfig? _audioConfig;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler<TranscriptionSegment>? OnTranscriptionUpdated;

    public TranscriptionService(
        SecretsService secretsService,
        AudioCaptureService audioCapture,
        ILogger logger)
    {
        _secretsService = secretsService ?? throw new ArgumentNullException(nameof(secretsService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.Log("TranscriptionService initialized");
    }

    public async Task StartTranscriptionAsync(TranscriptionOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            if (_isTranscribing)
            {
                throw new InvalidOperationException("Transcription is already in progress");
            }

            _logger.Log("Starting Azure Speech transcription...");
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await InitializeSpeechConfigAsync(options);
            await InitializeAudioStreamAsync();
            await InitializeRecognizerAsync(options);

            _transcriptionDocument = new TranscriptionDocument
            {
                StartTime = DateTime.Now,
                Language = _speechConfig!.SpeechRecognitionLanguage
            };

            _audioCapture.AudioCaptured += OnAudioCaptured;

            await _recognizer!.StartContinuousRecognitionAsync().ConfigureAwait(false);
            _isTranscribing = true;

            _audioCapture.StartCapturing(16000, 16, 1);

            _logger.Log("Azure Speech transcription started successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to start transcription: {ex.Message}");
            await CleanupAsync();
            throw new InvalidOperationException($"Transcription startup failed: {ex.Message}", ex);
        }
    }

    private async Task InitializeSpeechConfigAsync(TranscriptionOptions options)
    {
        var (region, key) = _secretsService.GetAzureSpeechCredentials();

        _speechConfig = SpeechConfig.FromSubscription(key, region);
        _speechConfig.SpeechRecognitionLanguage = options.Language;

        _speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "5000");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "1000");
        _speechConfig.SetProfanity(ProfanityOption.Masked);

        if (options.EnableProfanityFilter)
        {
            _speechConfig.SetProfanity(ProfanityOption.Masked);
        }
        else
        {
            _speechConfig.SetProfanity(ProfanityOption.Raw);
        }

        if (options.EnableWordLevelTimestamps)
        {
            _speechConfig.RequestWordLevelTimestamps();
        }

        if (options.DetectSpeakers)
        {
            _speechConfig.SetProperty("DiarizeAudio", "true");
            _speechConfig.SetProperty("MaxSpeakersCount", "10");
        }

        _logger.Log($"Speech config initialized for language: {_speechConfig.SpeechRecognitionLanguage}");
        await Task.CompletedTask;
    }

    private async Task InitializeAudioStreamAsync()
    {
        var format = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        _audioInputStream = AudioInputStream.CreatePushStream(format);

        _logger.Log("Audio stream initialized with Azure-optimized format");
        await Task.CompletedTask;
    }

    private async Task InitializeRecognizerAsync(TranscriptionOptions options)
    {
        _audioConfig = AudioConfig.FromStreamInput(_audioInputStream);
        _recognizer = new SpeechRecognizer(_speechConfig!, _audioConfig);

        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _recognizer.SessionStopped += OnSessionStopped;
        _recognizer.SessionStarted += OnSessionStarted;

        _logger.Log("Speech recognizer initialized with event handlers");
        await Task.CompletedTask;
    }

    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        _logger.Log($"Recognition event: Reason={e.Result.Reason}, Text='{e.Result.Text}'");

        if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            _logger.Log($"Processing recognized speech: {e.Result.Text}");
            ProcessRecognizedSpeech(e.Result);
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            _logger.Log("No speech could be recognized - check microphone and speech clarity");
        }
        else
        {
            _logger.Log($"Recognition result: {e.Result.Reason}");
        }
    }

    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        _logger.Log($"Recognition canceled: {e.Reason}");

        if (e.Reason == CancellationReason.Error)
        {
            _logger.Log($"Error details: {e.ErrorCode} - {e.ErrorDetails}");
        }

        _isTranscribing = false;
    }

    private void OnSessionStopped(object? sender, SessionEventArgs e)
    {
        _logger.Log($"Session stopped: {e.SessionId}");
        _isTranscribing = false;
    }

    private void OnSessionStarted(object? sender, SessionEventArgs e)
    {
        _logger.Log($"Session started: {e.SessionId}");
    }

    private void OnAudioCaptured(object? sender, byte[] audioData)
    {
        if (_isTranscribing && _audioInputStream != null && !_disposed)
        {
            try
            {
                _audioInputStream.Write(audioData);
                
                if (DateTime.Now.Millisecond % 1000 < 50)
                {
                    _logger.Log($"Audio data written: {audioData.Length} bytes");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error writing audio data: {ex.Message}");
            }
        }
    }

    private void ProcessRecognizedSpeech(SpeechRecognitionResult result)
    {
        try
        {
            var segment = new TranscriptionSegment
            {
                Text = result.Text,
                Timestamp = DateTime.Now,
                Duration = result.Duration,
            };

            _transcriptionDocument.Segments.Add(segment);
            _logger.Log($"Transcribed: {segment.Text}");

            _logger.Log("Firing OnTranscriptionUpdated event...");
            OnTranscriptionUpdated?.Invoke(this, segment);
            _logger.Log("OnTranscriptionUpdated event fired");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error processing recognized speech: {ex.Message}");
        }
    }

    public async Task StopTranscriptionAsync()
    {
        if (!_isTranscribing)
            return;

        try
        {
            _logger.Log("Stopping Azure Speech transcription...");

            if (_recognizer != null)
            {
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }

            _audioCapture.StopCapturing();

            _transcriptionDocument.EndTime = DateTime.Now;
            _isTranscribing = false;

            var duration = (_transcriptionDocument.EndTime - _transcriptionDocument.StartTime)?.TotalSeconds ?? 0;
            _logger.Log($"Transcription stopped. Duration: {duration:F1} seconds, Segments: {_transcriptionDocument.Segments.Count}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error stopping transcription: {ex.Message}");
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            _audioCapture.AudioCaptured -= OnAudioCaptured;
            _cancellationTokenSource?.Cancel();
            _isTranscribing = false;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error during cleanup: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public TranscriptionDocument GetTranscriptionDocument()
    {
        return _transcriptionDocument;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TranscriptionService));
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
                StopTranscriptionAsync().Wait(TimeSpan.FromSeconds(10));

                if (_recognizer != null)
                {
                    _recognizer.Recognized -= OnRecognized;
                    _recognizer.Canceled -= OnCanceled;
                    _recognizer.SessionStopped -= OnSessionStopped;
                    _recognizer.SessionStarted -= OnSessionStarted;
                    _recognizer.Dispose();
                    _recognizer = null;
                }

                _audioInputStream?.Dispose();
                _audioConfig?.Dispose();
                _speechConfig = null;

                _cancellationTokenSource?.Dispose();

                _logger.Log("TranscriptionService disposed");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error during TranscriptionService disposal: {ex.Message}");
            }

            _disposed = true;
        }
    }
}



/*

using System;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AzureSpeechProject.Services;

public class TranscriptionService : IDisposable
{
    private readonly SecretsService _secretsService;
    private readonly AudioCaptureService _audioCapture;
    private readonly ILogger _logger;

    private TranscriptionDocument _transcriptionDocument = new();
    private bool _isTranscribing = false;
    private PushAudioInputStream? _audioInputStream;
    private SpeechRecognizer? _recognizer;

    public event EventHandler<TranscriptionSegment>? OnTranscriptionUpdated;

    public TranscriptionService(
        SecretsService secretsService,
        AudioCaptureService audioCapture,
        ILogger logger)
    {
        _secretsService = secretsService ?? throw new ArgumentNullException(nameof(secretsService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.Log("TranscriptionService initialized");
    }

    public async Task StartTranscriptionAsync(TranscriptionOptions options)
    {
        if (_isTranscribing)
        {
            _logger.Log("Transcription is already in progress.");
            return;
        }

        _logger.Log("Starting Azure Speech transcription...");
        _transcriptionDocument = new TranscriptionDocument { StartTime = DateTime.Now, Language = options.Language };

        var (region, key) = _secretsService.GetAzureSpeechCredentials();
        var speechConfig = SpeechConfig.FromSubscription(key, region);
        speechConfig.SpeechRecognitionLanguage = options.Language;
        speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
        speechConfig.SetProfanity(options.EnableProfanityFilter ? ProfanityOption.Masked : ProfanityOption.Raw);
        if (options.EnableWordLevelTimestamps)
        {
            speechConfig.RequestWordLevelTimestamps();
        }

        _audioInputStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        var audioConfig = AudioConfig.FromStreamInput(_audioInputStream);
        _recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _recognizer.SessionStarted += (s, e) => _logger.Log($"Transcription Session started: {e.SessionId}");
        _recognizer.SessionStopped += (s, e) => _logger.Log($"Transcription Session stopped: {e.SessionId}");

        _audioCapture.AudioCaptured += OnAudioCaptured;
        await _recognizer.StartContinuousRecognitionAsync();
        _isTranscribing = true;
        _logger.Log("Transcription service is now listening for audio data.");
    }

    private void OnAudioCaptured(object? sender, byte[] audioData)
    {
        if (_isTranscribing && _audioInputStream != null)
        {
            _audioInputStream.Write(audioData);
        }
    }

    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            var segment = new TranscriptionSegment
            {
                Text = e.Result.Text,
                Timestamp = DateTime.Now,
                Duration = e.Result.Duration,
            };
            _transcriptionDocument.Segments.Add(segment);
            OnTranscriptionUpdated?.Invoke(this, segment);
            _logger.Log($"Transcribed: {segment.Text}");
        }
    }

    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        _logger.Log($"Transcription CANCELED: Reason={e.Reason}");
        if (e.Reason == CancellationReason.Error)
        {
            _logger.Log($"CANCELED: ErrorCode={e.ErrorCode}, ErrorDetails={e.ErrorDetails}");
        }
    }

    public async Task StopTranscriptionAsync()
    {
        if (!_isTranscribing) return;

        _logger.Log("Stopping transcription service...");
        _audioCapture.AudioCaptured -= OnAudioCaptured;

        if (_recognizer != null)
        {
            await _recognizer.StopContinuousRecognitionAsync();
            _recognizer.Dispose();
            _recognizer = null;
        }
        if (_audioInputStream != null)
        {
            _audioInputStream.Close();
            _audioInputStream = null;
        }
        _isTranscribing = false;
        _transcriptionDocument.EndTime = DateTime.Now;
        _logger.Log("Transcription service stopped.");
    }

    public TranscriptionDocument GetTranscriptionDocument() => _transcriptionDocument;

    public void Dispose()
    {
        if (_recognizer != null)
        {
            _recognizer.Recognized -= OnRecognized;
            _recognizer.Canceled -= OnCanceled;
            _recognizer.Dispose();
        }
        _audioInputStream?.Dispose();
    }
}

*/