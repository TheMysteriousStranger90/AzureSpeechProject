using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public sealed class TranscriptionViewModel : ReactiveObject, IActivatableViewModel, IDisposable
{
    private readonly ILogger _logger;
    private readonly TranscriptionService _transcriptionService;
    private readonly TranslationService _translationService;
    private readonly ITranscriptFileService _fileService;
    private readonly AudioCaptureService _audioCaptureService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _recordingCts;
    private bool _disposed;

    [Reactive] public string CurrentTranscript { get; private set; } = string.Empty;
    [Reactive] public string CurrentTranslation { get; private set; } = string.Empty;
    [Reactive] public bool IsRecording { get; set; }
    [Reactive] public bool EnableTranslation { get; set; }
    [Reactive] public string SelectedTargetLanguage { get; set; } = "it";
    [Reactive] public string TranslationHeader { get; set; } = "Translation (Italian)";
    [Reactive] public string Status { get; set; } = "Ready to record";
    [Reactive] public TranscriptFormat SelectedOutputFormat { get; set; } = TranscriptFormat.Text;

    public IReadOnlyList<string> AvailableLanguages { get; } =
        ["es", "fr", "de", "it", "pt", "ja", "ko", "zh-Hans", "ru"];

    public IReadOnlyList<TranscriptFormat> OutputFormats { get; } =
        [TranscriptFormat.Text, TranscriptFormat.Json, TranscriptFormat.Srt];

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    public ViewModelActivator Activator { get; } = new ViewModelActivator();
    private readonly ObservableAsPropertyHelper<bool> _canSave;
    public bool CanSave => _canSave.Value;
    private readonly ObservableAsPropertyHelper<bool> _canClear;
    public bool CanClear => _canClear.Value;
    private TranscriptionDocument _document = new();

    public TranscriptionViewModel(
        ILogger logger,
        TranscriptionService transcriptionService,
        TranslationService translationService,
        ITranscriptFileService fileService,
        AudioCaptureService audioCaptureService,
        INetworkStatusService networkStatusService,
        IMicrophonePermissionService microphonePermissionService,
        ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        var canStart = this.WhenAnyValue(x => x.IsRecording, isRecording => !isRecording);
        StartCommand = ReactiveCommand.CreateFromTask(StartRecordingAsync, canStart);

        var canStop = this.WhenAnyValue(x => x.IsRecording);
        StopCommand = ReactiveCommand.CreateFromTask(StopRecordingAsync, canStop);

        var canSaveOrClear = this.WhenAnyValue(x => x.CurrentTranscript, x => x.IsRecording,
            (t, r) => !string.IsNullOrWhiteSpace(t) && !r);
        _canSave = canSaveOrClear.ToProperty(this, x => x.CanSave);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveTranscriptAsync, canSaveOrClear);
        _canClear = canSaveOrClear.ToProperty(this, x => x.CanClear);
        ClearCommand = ReactiveCommand.Create(ClearTranscript, canSaveOrClear);

        this.WhenAnyValue(x => x.SelectedTargetLanguage).Subscribe(lang =>
        {
            var languageNames = new Dictionary<string, string>
            {
                { "es", "Spanish" }, { "fr", "French" }, { "de", "German" }, { "it", "Italian" },
                { "pt", "Portuguese" }, { "ja", "Japanese" }, { "ko", "Korean" }, { "zh-Hans", "Chinese" },
                { "ru", "Russian" }
            };
            TranslationHeader = $"Translation ({languageNames.GetValueOrDefault(lang, lang)})";
        });

        this.WhenActivated(disposables =>
        {
            Observable.FromEventPattern<EventHandler<TranscriptionSegmentEventArgs>, TranscriptionSegmentEventArgs>(
                    h => _transcriptionService.OnTranscriptionUpdated += h,
                    h => _transcriptionService.OnTranscriptionUpdated -= h)
                .Select(e => e.EventArgs).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(HandleTranscriptionUpdated,
                    ex => _logger.Log($"Error in transcription subscription: {ex.Message}"))
                .DisposeWith(disposables);

            Observable.FromEventPattern<EventHandler<TranslationResultEventArgs>, TranslationResultEventArgs>(
                    h => _translationService.OnTranslationUpdated += h,
                    h => _translationService.OnTranslationUpdated -= h)
                .Select(e => e.EventArgs).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(HandleTranslationUpdated,
                    ex => _logger.Log($"Error in translation subscription: {ex.Message}"))
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                _recordingCts?.Cancel();
                _recordingCts?.Dispose();
            }).DisposeWith(disposables);
        });
    }

    private void HandleTranscriptionUpdated(TranscriptionSegmentEventArgs e)
    {
        _document.Segments.Add(e.Segment);
        CurrentTranscript += $"[{e.Segment.Timestamp:HH:mm:ss}] {e.Segment.Text}{Environment.NewLine}";
        Status = $"Transcribing... (Segments: {_document.Segments.Count})";
    }

    private void HandleTranslationUpdated(TranslationResultEventArgs e)
    {
        CurrentTranslation += $"[{e.Result.Timestamp:HH:mm:ss}] {e.Result.TranslatedText}{Environment.NewLine}";
    }

    private async Task StartRecordingAsync()
    {
        _logger.Log("Starting recording process...");
        ClearTranscript();

        await (_recordingCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        _recordingCts?.Dispose();
        _recordingCts = new CancellationTokenSource();

        IsRecording = true;
        Status = "Initializing services...";

        try
        {
            var settings = await _settingsService.LoadSettingsAsync(_recordingCts.Token).ConfigureAwait(false);
            var options = new TranscriptionOptions
            {
                Language = settings.SpeechLanguage,
                EnableProfanityFilter = true,
                EnableWordLevelTimestamps = true
            };

            await _transcriptionService.StartTranscriptionAsync(options, _recordingCts.Token).ConfigureAwait(false);

            if (EnableTranslation)
            {
                await _translationService.StartTranslationAsync(
                    options.Language,
                    SelectedTargetLanguage,
                    _recordingCts.Token).ConfigureAwait(false);
            }

            await _audioCaptureService.StartCapturingAsync(_recordingCts.Token).ConfigureAwait(false);

            Status = "🎙️ Recording in progress... Speak now!";
            _logger.Log("All services started successfully.");
        }
        catch (OperationCanceledException)
        {
            Status = "Recording was cancelled";
            _logger.Log("Recording start was cancelled");
            await StopRecordingAsync().ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            Status = "❌ Microphone access denied. Please grant microphone permissions in Windows Settings.";
            _logger.Log($"Microphone permission error: {ex.Message}");
            await StopRecordingAsync().ConfigureAwait(false);
        }
        catch (System.Net.WebException ex)
        {
            Status = "❌ Network error. Please check your internet connection and try again.";
            _logger.Log($"Network error starting recording: {ex.Message}");
            await StopRecordingAsync().ConfigureAwait(false);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            Status = "❌ Unable to connect to Azure Speech Services. Check your internet connection.";
            _logger.Log($"HTTP error starting recording: {ex.Message}");
            await StopRecordingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Status = $"❌ Error starting recording: {ex.Message}";
            _logger.Log($"Error starting recording: {ex.Message}");
            await StopRecordingAsync().ConfigureAwait(false);
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording) return;

        _logger.Log("Stopping recording process...");
        Status = "Stopping...";

        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await (_recordingCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);

            await _audioCaptureService.StopCapturingAsync(stopCts.Token).ConfigureAwait(false);
            await _transcriptionService.StopTranscriptionAsync(stopCts.Token).ConfigureAwait(false);

            if (EnableTranslation)
            {
                await _translationService.StopTranslationAsync(stopCts.Token).ConfigureAwait(false);
            }

            _document = _transcriptionService.TranscriptionDocument;
            IsRecording = false;

            if (_document.Segments.Count > 0)
            {
                Status = $"✅ Recording stopped. {_document.Segments.Count} segments captured.";
            }
            else
            {
                Status = "⚠️ Recording stopped. No audio was captured.";
            }

            _logger.Log("All services stopped successfully.");
        }
        catch (OperationCanceledException)
        {
            Status = "⚠️ Stop operation timed out or was cancelled";
            _logger.Log("Stop recording was cancelled or timed out");
            IsRecording = false;
        }
        catch (Exception ex)
        {
            Status = $"❌ Error stopping recording: {ex.Message}";
            _logger.Log($"Error stopping recording: {ex.Message}");
            IsRecording = false;
        }
        finally
        {
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task SaveTranscriptAsync()
    {
        Status = "Saving transcript...";

        using var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var filePath = await _fileService.SaveTranscriptAsync(
                _document,
                SelectedOutputFormat,
                null,
                saveCts.Token).ConfigureAwait(false);

            Status = $"✅ Transcript saved to: {filePath}";

            if (EnableTranslation && !string.IsNullOrWhiteSpace(CurrentTranslation))
            {
                var translationDocument = new TranscriptionDocument
                {
                    Language = SelectedTargetLanguage,
                    StartTime = _document.StartTime,
                    EndTime = _document.EndTime
                };

                var lines = CurrentTranslation.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (TryParseTranslationLine(line, out var timestamp, out var text))
                    {
                        translationDocument.Segments.Add(new TranscriptionSegment
                        {
                            Text = text,
                            Timestamp = timestamp,
                            Duration = TimeSpan.FromSeconds(2)
                        });
                    }
                }

                await _fileService.SaveTranscriptAsync(
                    translationDocument,
                    SelectedOutputFormat,
                    SelectedTargetLanguage,
                    saveCts.Token).ConfigureAwait(false);

                Status = $"✅ Transcript and translation saved to: {Path.GetDirectoryName(filePath)}";
            }
        }
        catch (OperationCanceledException)
        {
            Status = "⚠️ Save operation was cancelled or timed out";
            _logger.Log("Save transcript was cancelled");
        }
        catch (Exception ex)
        {
            Status = $"❌ Error saving transcript: {ex.Message}";
            _logger.Log($"Error saving transcript: {ex.Message}");
        }
    }

    private static bool TryParseTranslationLine(string line, out DateTime timestamp, out string text)
    {
        timestamp = DateTime.Now;
        text = line;

        try
        {
            if (line.StartsWith('[') && line.Contains(']', StringComparison.Ordinal))
            {
                var parts = line.Split(']', 2);
                if (parts.Length == 2)
                {
                    var timeString = parts[0].Trim('[', ']');
                    if (TimeSpan.TryParse(timeString, CultureInfo.InvariantCulture, out var timeSpan))
                    {
                        timestamp = DateTime.Today.Add(timeSpan);
                        text = parts[1].Trim();
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void ClearTranscript()
    {
        CurrentTranscript = string.Empty;
        CurrentTranslation = string.Empty;
        _document = new TranscriptionDocument();
        Status = "Ready to record";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _canSave?.Dispose();
        _canClear?.Dispose();
        _recordingCts?.Cancel();
        _recordingCts?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
