using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
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
    private readonly SemaphoreSlim _transcriptionLock = new(1, 1);
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
        
        await _transcriptionLock.WaitAsync(cancellationToken);
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
            
            await _recognizer!.StartContinuousRecognitionAsync().ConfigureAwait(false);
            _isTranscribing = true;
            
            _transcriptionDocument = new TranscriptionDocument
            {
                StartTime = DateTime.Now,
                Language = _speechConfig!.SpeechRecognitionLanguage
            };
            
            _audioCapture.AudioCaptured += OnAudioCaptured;
            
            await _audioCapture.StartCapturingAsync(16000, 16, 1, _cancellationTokenSource.Token);
            
            _logger.Log("Azure Speech transcription started successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to start transcription: {ex.Message}");
            await CleanupAsync();
            throw new InvalidOperationException($"Transcription startup failed: {ex.Message}", ex);
        }
        finally
        {
            _transcriptionLock.Release();
        }
    }
    
    private async Task InitializeSpeechConfigAsync(TranscriptionOptions options)
    {
        var (region, key) = _secretsService.GetAzureSpeechCredentials();
        
        _speechConfig = SpeechConfig.FromSubscription(key, region);
        _speechConfig.SpeechRecognitionLanguage = options.Language;
        
        _speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "5000");
        _speechConfig.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000");
        _speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "500");
        
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
        
        if (!string.IsNullOrEmpty(options.CustomModelId))
        {
            _speechConfig.EndpointId = options.CustomModelId;
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
        
        _recognizer.Recognizing += OnRecognizing;
        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _recognizer.SessionStopped += OnSessionStopped;
        _recognizer.SessionStarted += OnSessionStarted;
        
        _logger.Log("Speech recognizer initialized with event handlers");
        await Task.CompletedTask;
    }
    
    private void OnRecognizing(object? sender, SpeechRecognitionEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Result.Text))
        {
            _logger.Log($"Recognizing: {e.Result.Text}");
        }
    }
    
    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            ProcessRecognizedSpeech(e.Result);
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            _logger.Log("No speech could be recognized");
        }
    }
    
    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        _logger.Log($"Recognition canceled: {e.Reason}");
        
        if (e.Reason == CancellationReason.Error)
        {
            _logger.Log($"Error details: {e.ErrorCode} - {e.ErrorDetails}");
            
            switch (e.ErrorCode)
            {
                case CancellationErrorCode.AuthenticationFailure:
                    _logger.Log("Authentication failed. Check your Speech Service credentials.");
                    break;
                case CancellationErrorCode.ConnectionFailure:
                    _logger.Log("Connection to Azure Speech Service failed. Check your network connection.");
                    break;
                case CancellationErrorCode.ServiceTimeout:
                    _logger.Log("Azure Speech Service request timed out.");
                    break;
                case CancellationErrorCode.ServiceUnavailable:
                    _logger.Log("Azure Speech Service is temporarily unavailable.");
                    break;
                default:
                    _logger.Log($"Unknown error occurred: {e.ErrorCode}");
                    break;
            }
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
    
    private void OnAudioCaptured(object? sender, AudioDataEventArgs e)
    {
        if (_isTranscribing && _audioInputStream != null && !_disposed)
        {
            try
            {
                _audioInputStream.Write(e.Data);
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
            float? confidence = null;
            string? jsonDetails = null;
            
            try
            {
                jsonDetails = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult, "{}");
                
                if (!string.IsNullOrEmpty(jsonDetails) && jsonDetails != "{}")
                {
                    using var document = JsonDocument.Parse(jsonDetails);
                    if (document.RootElement.TryGetProperty("NBest", out var nBestElement) &&
                        nBestElement.ValueKind == JsonValueKind.Array &&
                        nBestElement.GetArrayLength() > 0)
                    {
                        var firstResult = nBestElement[0];
                        if (firstResult.TryGetProperty("Confidence", out var confidenceElement))
                        {
                            confidence = (float)confidenceElement.GetDouble();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing JSON response: {ex.Message}");
            }
            
            var segment = new TranscriptionSegment
            {
                Text = result.Text,
                Timestamp = DateTime.Now,
                Duration = result.Duration,
                Confidence = confidence,
                JsonDetails = jsonDetails
            };
        
            _transcriptionDocument.Segments.Add(segment);
            _logger.Log($"Transcribed: {segment.Text} (Confidence: {confidence:F2})");
            
            OnTranscriptionUpdated?.Invoke(this, segment);
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
            
        await _transcriptionLock.WaitAsync();
        try
        {
            _logger.Log("Stopping Azure Speech transcription...");
            
            if (_recognizer != null)
            {
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            
            await _audioCapture.StopCapturingAsync();
            
            _transcriptionDocument.EndTime = DateTime.Now;
            _isTranscribing = false;
            
            var duration = (_transcriptionDocument.EndTime - _transcriptionDocument.StartTime)?.TotalSeconds ?? 0;
            _logger.Log($"Transcription stopped. Duration: {duration:F1} seconds, Segments: {_transcriptionDocument.Segments.Count}");
        }
        finally
        {
            _transcriptionLock.Release();
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
                    _recognizer.Recognizing -= OnRecognizing;
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
                
                _transcriptionLock?.Dispose();
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