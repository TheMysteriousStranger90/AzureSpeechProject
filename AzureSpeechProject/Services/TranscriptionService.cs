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