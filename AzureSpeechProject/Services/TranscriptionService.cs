using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AzureSpeechProject.Services;

public sealed class TranscriptionService : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly AudioCaptureService _audioCapture;
    private readonly ILogger _logger;

    private TranscriptionDocument _transcriptionDocument = new();
    private bool _isTranscribing;
    private PushAudioInputStream? _audioInputStream;
    private SpeechRecognizer? _recognizer;
    private bool _disposed;
    private EventHandler<SessionEventArgs>? _sessionStartedHandler;
    private EventHandler<SessionEventArgs>? _sessionStoppedHandler;

    public event EventHandler<TranscriptionSegment>? OnTranscriptionUpdated;

    public TranscriptionService(
        ISettingsService settingsService,
        AudioCaptureService audioCapture,
        ILogger logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.Log("TranscriptionService initialized");
    }

    public async Task StartTranscriptionAsync(TranscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_isTranscribing)
        {
            _logger.Log("Transcription is already in progress.");
            return;
        }

        _logger.Log("Starting Azure Speech transcription...");
        _transcriptionDocument = new TranscriptionDocument { StartTime = DateTime.Now, Language = options.Language };

        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);

        if (string.IsNullOrEmpty(settings.Region) || string.IsNullOrEmpty(settings.Key))
        {
            throw new InvalidOperationException("Azure Speech credentials are not configured in settings");
        }

        var speechConfig = SpeechConfig.FromSubscription(settings.Key, settings.Region);
        speechConfig.SpeechRecognitionLanguage = settings.SpeechLanguage;
        speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
        speechConfig.SetProfanity(options.EnableProfanityFilter ? ProfanityOption.Masked : ProfanityOption.Raw);

        _logger.Log($"Speech config - Language: {speechConfig.SpeechRecognitionLanguage}, Region: {settings.Region}");

        if (options.EnableWordLevelTimestamps)
        {
            speechConfig.RequestWordLevelTimestamps();
        }

        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(
            (uint)settings.SampleRate,
            (byte)settings.BitsPerSample,
            (byte)settings.Channels);

        _logger.Log(
            $"Audio format - SampleRate: {settings.SampleRate}, BitsPerSample: {settings.BitsPerSample}, Channels: {settings.Channels}");

        _audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
        var audioConfig = AudioConfig.FromStreamInput(_audioInputStream);
        _recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        _recognizer.Recognizing += OnRecognizing;
        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _sessionStartedHandler = (s, e) => _logger.Log($"Transcription Session started: {e.SessionId}");
        _recognizer.SessionStarted += _sessionStartedHandler;
        _sessionStoppedHandler = (s, e) => _logger.Log($"Transcription Session stopped: {e.SessionId}");
        _recognizer.SessionStopped += _sessionStoppedHandler;

        _audioCapture.AudioCaptured += OnAudioCaptured;
        await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
        _isTranscribing = true;
        _logger.Log("Transcription service is now listening for audio data.");
    }

    private void OnAudioCaptured(object? sender, AudioCaptureService.AudioCapturedEventArgs e)
    {
        if (_isTranscribing && _audioInputStream != null)
        {
            _audioInputStream.Write(e.AudioData);
        }
    }

    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        _logger.Log(
            $"Recognition result received - Reason: {e.Result.Reason}, Text: '{e.Result.Text}', Duration: {e.Result.Duration}");

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
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            _logger.Log("No speech was recognized in the audio segment");
        }
        else
        {
            _logger.Log($"Recognition failed with reason: {e.Result.Reason}");
        }
    }

    private void OnRecognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Result.Text))
        {
            _logger.Log($"Recognizing: {e.Result.Text}");
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
            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            try
            {
                if (_recognizer != null)
                {
                    _recognizer.Recognizing -= OnRecognizing;
                    _recognizer.Recognized -= OnRecognized;
                    _recognizer.Canceled -= OnCanceled;
                    if (_sessionStartedHandler != null) _recognizer.SessionStarted -= _sessionStartedHandler;
                    if (_sessionStoppedHandler != null) _recognizer.SessionStopped -= _sessionStoppedHandler;
                    _recognizer.Dispose();
                    _recognizer = null;
                }

                _audioInputStream?.Dispose();
                _audioInputStream = null;

                _logger.Log("TranscriptionService disposed");
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        _disposed = true;
    }
}
