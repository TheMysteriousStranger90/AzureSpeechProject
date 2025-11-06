using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;

namespace AzureSpeechProject.Services;

public sealed class TranslationService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly AudioCaptureService _audioCaptureService;
    private TranslationRecognizer? _recognizer;
    private PushAudioInputStream? _audioStream;
    private bool _isTranslating;
    private CancellationTokenSource? _translationCts;

    public event EventHandler<TranslationResult>? OnTranslationUpdated;

    public TranslationService(ILogger logger, ISettingsService settingsService, AudioCaptureService audioCaptureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
        _logger.Log("TranslationService initialized");
    }

    public async Task StartTranslationAsync(
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isTranslating)
        {
            _logger.Log("Translation is already in progress.");
            return;
        }

        _logger.Log($"Starting translation from {sourceLanguage} to {targetLanguage}");

        try
        {
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(settings.Region) || string.IsNullOrEmpty(settings.Key))
            {
                throw new InvalidOperationException("Azure Speech credentials are not configured in settings");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var config = SpeechTranslationConfig.FromSubscription(settings.Key, settings.Region);
            config.SpeechRecognitionLanguage = sourceLanguage;
            config.AddTargetLanguage(targetLanguage);

            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(
                (uint)settings.SampleRate,
                (byte)settings.BitsPerSample,
                (byte)settings.Channels);

            _audioStream = AudioInputStream.CreatePushStream(audioFormat);
            var audioConfig = AudioConfig.FromStreamInput(_audioStream);
            _recognizer = new TranslationRecognizer(config, audioConfig);

            _recognizer.Recognized += (s, e) =>
            {
                if (_translationCts?.Token.IsCancellationRequested == true)
                    return;

                if (e.Result.Reason == ResultReason.TranslatedSpeech && e.Result.Translations.ContainsKey(targetLanguage))
                {
                    var translatedText = e.Result.Translations[targetLanguage];
                    _logger.Log($"Translated ({targetLanguage}): {translatedText}");
                    OnTranslationUpdated?.Invoke(this, new TranslationResult
                    {
                        OriginalText = e.Result.Text,
                        TranslatedText = translatedText,
                        TargetLanguage = targetLanguage,
                        Timestamp = DateTime.Now
                    });
                }
            };

            _recognizer.Canceled += (s, e) => _logger.Log($"Translation CANCELED: Reason={e.Reason}");
            _recognizer.SessionStarted += (s, e) => _logger.Log($"Translation Session started: {e.SessionId}");
            _recognizer.SessionStopped += (s, e) => _logger.Log($"Translation Session stopped: {e.SessionId}");

            _audioCaptureService.AudioCaptured += OnAudioCaptured;

            _translationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            _isTranslating = true;
            _logger.Log("Translation service is now listening for audio data.");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Translation start was cancelled");
            await CleanupTranslationAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error starting translation: {ex.Message}");
            await CleanupTranslationAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void OnAudioCaptured(object? sender, AudioCaptureService.AudioCapturedEventArgs e)
    {
        if (_isTranslating && _audioStream != null && _translationCts?.Token.IsCancellationRequested != true)
        {
            _audioStream.Write(e.AudioData, e.AudioData.Length);
        }
    }

    public async Task StopTranslationAsync(CancellationToken cancellationToken = default)
    {
        if (!_isTranslating) return;

        _logger.Log("Stopping translation service...");

        try
        {
            _translationCts?.CancelAsync();
            await CleanupTranslationAsync(cancellationToken).ConfigureAwait(false);

            _logger.Log("Translation service stopped.");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Translation stop was cancelled");
            throw;
        }
    }

    private async Task CleanupTranslationAsync(CancellationToken cancellationToken = default)
    {
        _audioCaptureService.AudioCaptured -= OnAudioCaptured;

        if (_recognizer != null)
        {
            try
            {
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error stopping translation recognizer: {ex.Message}");
            }

            _recognizer.Dispose();
            _recognizer = null;
        }

        if (_audioStream != null)
        {
            _audioStream.Close();
            _audioStream = null;
        }

        _translationCts?.Dispose();
        _translationCts = null;
        _isTranslating = false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _translationCts?.Cancel();
            _translationCts?.Dispose();
            _recognizer?.Dispose();
            _audioStream?.Dispose();
        }
    }
}
