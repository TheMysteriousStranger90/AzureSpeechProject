using System;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;

namespace AzureSpeechProject.Services;

public class TranslationService : IDisposable
{
    private readonly ILogger _logger;
    private readonly SecretsService _secretsService;
    private readonly AudioCaptureService _audioCaptureService;
    private TranslationRecognizer? _recognizer;
    private PushAudioInputStream? _audioStream;
    private bool _isTranslating = false;

    public event EventHandler<TranslationResult>? OnTranslationUpdated;

    public TranslationService(ILogger logger, SecretsService secretsService, AudioCaptureService audioCaptureService)
    {
        _logger = logger;
        _secretsService = secretsService;
        _audioCaptureService = audioCaptureService;
        _logger.Log("TranslationService initialized");
    }

    public async Task StartTranslationAsync(string sourceLanguage, string targetLanguage)
    {
        if (_isTranslating)
        {
            _logger.Log("Translation is already in progress.");
            return;
        }
        
        _logger.Log($"Starting translation from {sourceLanguage} to {targetLanguage}");
        var (region, key) = _secretsService.GetAzureSpeechCredentials();
        var config = SpeechTranslationConfig.FromSubscription(key, region);
        config.SpeechRecognitionLanguage = sourceLanguage;
        config.AddTargetLanguage(targetLanguage);

        _audioStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        var audioConfig = AudioConfig.FromStreamInput(_audioStream);
        _recognizer = new TranslationRecognizer(config, audioConfig);

        _recognizer.Recognized += (s, e) =>
        {
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
        await _recognizer.StartContinuousRecognitionAsync();
        _isTranslating = true;
        _logger.Log("Translation service is now listening for audio data.");
    }

    private void OnAudioCaptured(object? sender, byte[] audioData)
    {
        if (_isTranslating && _audioStream != null)
        {
            _audioStream.Write(audioData, audioData.Length);
        }
    }

    public async Task StopTranslationAsync()
    {
        if (!_isTranslating) return;
        
        _logger.Log("Stopping translation service...");
        _audioCaptureService.AudioCaptured -= OnAudioCaptured;

        if (_recognizer != null)
        {
            await _recognizer.StopContinuousRecognitionAsync();
            _recognizer.Dispose();
            _recognizer = null;
        }
        if (_audioStream != null)
        {
            _audioStream.Close();
            _audioStream = null;
        }
        _isTranslating = false;
        _logger.Log("Translation service stopped.");
    }

    public void Dispose()
    {
        _recognizer?.Dispose();
        _audioStream?.Dispose();
    }
}