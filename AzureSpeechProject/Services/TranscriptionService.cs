using System;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AzureSpeechProject.Services;

public class TranscriptionService
{
    private readonly SecretsService _secretsService;
    private readonly AudioCaptureService _audioCapture;
    private readonly ILogger _logger;
    
    private TranscriptionDocument _transcriptionDocument = new();
    private bool _isTranscribing = false;
    private PushAudioInputStream? _audioInputStream;
    private SpeechRecognizer? _recognizer;
    private SpeechConfig? _speechConfig;
    
    public event EventHandler<TranscriptionSegment>? OnTranscriptionUpdated;
    
    public TranscriptionService(
        SecretsService secretsService,
        AudioCaptureService audioCapture,
        ILogger logger)
    {
        _secretsService = secretsService;
        _audioCapture = audioCapture;
        _logger = logger;
    }
    
    public async Task StartTranscriptionAsync(TranscriptionOptions options)
    {
        try
        {
            if (_isTranscribing)
            {
                throw new InvalidOperationException("Transcription is already in progress");
            }
            
            _logger.Log("Starting real-time transcription...");
            
            var (region, key) = _secretsService.GetAzureSpeechCredentials();
            
            _speechConfig = SpeechConfig.FromSubscription(key, region);
            _speechConfig.SpeechRecognitionLanguage = "en-US";
            
            ConfigureSpeechService(options);
            
            _audioInputStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
            using var audioConfig = AudioConfig.FromStreamInput(_audioInputStream);
            
            _recognizer = new SpeechRecognizer(_speechConfig, audioConfig);
            
            _recognizer.Recognized += (s, e) => 
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    ProcessRecognizedSpeech(e.Result);
                }
            };
            
            _recognizer.Canceled += (s, e) => 
            {
                if (e.Reason == CancellationReason.Error)
                {
                    _logger.Log($"Speech recognition error: {e.ErrorCode} - {e.ErrorDetails}");
                }
                _isTranscribing = false;
            };
            
            _recognizer.SessionStopped += (s, e) => 
            {
                _logger.Log("Speech recognition session stopped");
                _isTranscribing = false;
            };
            
            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            _isTranscribing = true;
            
            _transcriptionDocument = new TranscriptionDocument
            {
                StartTime = DateTime.Now,
                Language = _speechConfig.SpeechRecognitionLanguage
            };
            
            _audioCapture.AudioCaptured += (s, audioData) => 
            {
                if (_isTranscribing && _audioInputStream != null)
                {
                    _audioInputStream.Write(audioData);
                }
            };
            
            _audioCapture.StartCapturing();
        }
        catch (Exception ex)
        {
            _logger.Log($"Transcription error: {ex.Message}");
            throw;
        }
    }
    
    private void ProcessRecognizedSpeech(SpeechRecognitionResult result)
    {
        var segment = new TranscriptionSegment
        {
            Text = result.Text,
            Timestamp = DateTime.Now,
            Duration = result.Duration
        };
    
        _transcriptionDocument.Segments.Add(segment);
        _logger.Log($"Transcribed: {segment.Text}");
        
        OnTranscriptionUpdated?.Invoke(this, segment);
    }
    
    private void ConfigureSpeechService(TranscriptionOptions options)
    {
        if (_speechConfig == null)
        {
            throw new InvalidOperationException("Speech config is not initialized");
        }
        
        if (options.DetectSpeakers)
        {
            _speechConfig.EnableAudioLogging();
            _speechConfig.SetProperty("DiarizeAudio", "true");
        }
        
        if (!string.IsNullOrEmpty(options.CustomModelId))
        {
            _speechConfig.EndpointId = options.CustomModelId;
        }
        
        _speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "5000");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "1000");
        _speechConfig.SetProfanity(ProfanityOption.Masked);
        
        _logger.Log("Speech service configured for real-time transcription");
    }
    
    public async Task StopTranscriptionAsync()
    {
        if (_recognizer != null)
        {
            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
        
        _audioCapture.StopCapturing();
        _isTranscribing = false;
        
        _transcriptionDocument.EndTime = DateTime.Now;
        _logger.Log($"Transcription stopped. Total duration: {(_transcriptionDocument.EndTime - _transcriptionDocument.StartTime).Value.TotalSeconds:F1} seconds");
    }
    
    public TranscriptionDocument GetTranscriptionDocument()
    {
        return _transcriptionDocument;
    }
}