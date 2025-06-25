using System;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;

namespace AzureSpeechProject.Services;

public class TranslationService
{
    private readonly SecretsService _secretsService;
    private readonly ILogger _logger;
    private SpeechTranslationConfig? _translationConfig;

    public event EventHandler<TranslationResult>? OnTranslationUpdated;

    public TranslationService(SecretsService secretsService, ILogger logger)
    {
        _secretsService = secretsService;
        _logger = logger;
    }

    public async Task InitializeAsync(string targetLanguage)
    {
        try
        {
            var (region, key) = _secretsService.GetAzureSpeechCredentials();
            _translationConfig = SpeechTranslationConfig.FromSubscription(key, region);
            _translationConfig.SpeechRecognitionLanguage = "en-US";
            _translationConfig.AddTargetLanguage(targetLanguage);

            _logger.Log($"Translation service initialized with target language: {targetLanguage}");

            await TestTranslationServiceAsync(targetLanguage);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error initializing translation service: {ex.Message}");
            throw;
        }
    }

    private async Task TestTranslationServiceAsync(string targetLanguage)
    {
        if (_translationConfig == null)
        {
            throw new InvalidOperationException("Translation config is not initialized");
        }

        try
        {
            using var recognizer = new TranslationRecognizer(_translationConfig);
            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.TranslatedSpeech)
            {
                _logger.Log("Translation service test succeeded");
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Warning: Translation service test failed: {ex.Message}");
        }
    }

    public async Task TranslateText(string text, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            if (_translationConfig == null)
            {
                await InitializeAsync(targetLanguage);
            }

            if (_translationConfig == null)
            {
                throw new InvalidOperationException("Failed to initialize translation configuration");
            }

            using var speechTranslator = new TranslationRecognizer(_translationConfig);

            // Here we're "cheating" a bit by using the recognizer for text translation
            // In a production app, you'd use the Text Translation API directly
            var result = await speechTranslator.RecognizeOnceAsync();

            if (result.Reason == ResultReason.TranslatedSpeech)
            {
                var translatedText = result.Translations[targetLanguage];

                var translationResult = new TranslationResult
                {
                    OriginalText = text,
                    TranslatedText = translatedText,
                    TargetLanguage = targetLanguage,
                    Timestamp = DateTime.Now
                };

                OnTranslationUpdated?.Invoke(this, translationResult);
            }
            else
            {
                _logger.Log($"Translation failed: {result.Reason}");

                var fallbackTranslation = $"[Translation pending for: {text}]";
                var fallbackResult = new TranslationResult
                {
                    OriginalText = text,
                    TranslatedText = fallbackTranslation,
                    TargetLanguage = targetLanguage,
                    Timestamp = DateTime.Now
                };

                OnTranslationUpdated?.Invoke(this, fallbackResult);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in translation: {ex.Message}");

            var errorResult = new TranslationResult
            {
                OriginalText = text,
                TranslatedText = $"[Translation error: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}...]",
                TargetLanguage = targetLanguage,
                Timestamp = DateTime.Now
            };

            OnTranslationUpdated?.Invoke(this, errorResult);
        }
    }
}