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
    private readonly ISettingsService _settingsService;
    private readonly AudioCaptureService _audioCaptureService;
    private TranslationRecognizer? _recognizer;
    private PushAudioInputStream? _audioStream;
    private bool _isTranslating = false;

    public event EventHandler<TranslationResult>? OnTranslationUpdated;

    public TranslationService(ILogger logger, ISettingsService settingsService, AudioCaptureService audioCaptureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
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
        
        var settings = await _settingsService.LoadSettingsAsync();
        
        if (string.IsNullOrEmpty(settings.Region) || string.IsNullOrEmpty(settings.Key))
        {
            throw new InvalidOperationException("Azure Speech credentials are not configured in settings");
        }

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