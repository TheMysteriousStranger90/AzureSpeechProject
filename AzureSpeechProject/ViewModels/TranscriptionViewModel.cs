using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public class TranscriptionViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly TranscriptionService _transcriptionService;
    private readonly TranslationService _translationService;
    private readonly ITranscriptFileService _fileService;

    [Reactive] public string CurrentTranscript { get; private set; } = string.Empty;
    [Reactive] public string CurrentTranslation { get; private set; } = string.Empty;
    [Reactive] public bool IsRecording { get; set; }
    [Reactive] public bool EnableTranslation { get; set; }
    [Reactive] public string SelectedTargetLanguage { get; set; } = "it";
    [Reactive] public string TranslationHeader { get; set; } = "Translation (Italian)";
    [Reactive] public string Status { get; set; } = "Ready to record";
    [Reactive] public TranscriptFormat SelectedOutputFormat { get; set; } = TranscriptFormat.Text;

    public List<string> AvailableLanguages { get; } = new List<string>
    {
        "es", "fr", "de", "it", "pt", "ja", "ko", "zh-Hans", "ru"
    };

    public List<TranscriptFormat> OutputFormats { get; } = new List<TranscriptFormat>
    {
        TranscriptFormat.Text,
        TranscriptFormat.Json,
        TranscriptFormat.Srt
    };

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleTranslationCommand { get; }

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
        ITranscriptFileService fileService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));

        _logger.Log("TranscriptionViewModel: Initializing commands...");

        var canStart = this.WhenAnyValue(x => x.IsRecording, isRecording => !isRecording);
        StartCommand = ReactiveCommand.CreateFromTask(StartRecordingAsync, canStart);

        var canStop = this.WhenAnyValue(x => x.IsRecording);
        StopCommand = ReactiveCommand.CreateFromTask(StopRecordingAsync, canStop);

        var canSaveOrClear = this.WhenAnyValue(
            x => x.CurrentTranscript,
            x => x.IsRecording,
            (transcript, isRecording) => !string.IsNullOrWhiteSpace(transcript) && !isRecording);

        _canSave = canSaveOrClear.ToProperty(this, x => x.CanSave);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveTranscriptAsync, canSaveOrClear);

        _canClear = canSaveOrClear.ToProperty(this, x => x.CanClear);
        ClearCommand = ReactiveCommand.Create(ClearTranscript, canSaveOrClear);

        ToggleTranslationCommand = ReactiveCommand.Create(() =>
        {
            EnableTranslation = !EnableTranslation;
            _logger.Log($"Translation {(EnableTranslation ? "enabled" : "disabled")}");
            Status = EnableTranslation ? "Translation enabled" : "Translation disabled";
        });

        this.WhenAnyValue(x => x.SelectedTargetLanguage)
            .Subscribe(lang =>
            {
                var languageNames = new Dictionary<string, string>
                {
                    { "es", "Spanish" }, { "fr", "French" }, { "de", "German" }, { "it", "Italian" },
                    { "pt", "Portuguese" }, { "ja", "Japanese" }, { "ko", "Korean" },
                    { "zh-Hans", "Chinese" }, { "ru", "Russian" }
                };
                TranslationHeader = $"Translation ({languageNames.GetValueOrDefault(lang, lang)})";
                _logger.Log($"Language changed to: {lang}");
            });

        this.WhenActivated(disposables =>
        {
            _logger.Log("TranscriptionViewModel activating and subscribing to events.");

            Observable.FromEventPattern<EventHandler<TranscriptionSegment>, TranscriptionSegment>(
                    h => _transcriptionService.OnTranscriptionUpdated += h,
                    h => _transcriptionService.OnTranscriptionUpdated -= h)
                .Select(e => e.EventArgs)
                .ObserveOn(RxApp.MainThreadScheduler) 
                .Subscribe(HandleTranscriptionUpdated, ex => _logger.Log($"Error in transcription subscription: {ex.Message}"))
                .DisposeWith(disposables);

            Observable.FromEventPattern<EventHandler<TranslationResult>, TranslationResult>(
                    h => _translationService.OnTranslationUpdated += h,
                    h => _translationService.OnTranslationUpdated -= h)
                .Select(e => e.EventArgs)
                .ObserveOn(RxApp.MainThreadScheduler) // Marshal to UI thread
                .Subscribe(HandleTranslationUpdated, ex => _logger.Log($"Error in translation subscription: {ex.Message}"))
                .DisposeWith(disposables);

            Disposable.Create(() => _logger.Log("TranscriptionViewModel deactivated.")).DisposeWith(disposables);
        });

        _logger.Log("TranscriptionViewModel constructor completed successfully");
    }

    private void HandleTranscriptionUpdated(TranscriptionSegment segment)
    {
        _logger.Log($"UI thread received transcription: {segment.Text}");
        _document.Segments.Add(segment);
        var newText = $"[{segment.Timestamp:HH:mm:ss}] {segment.Text}{Environment.NewLine}";
        CurrentTranscript += newText;
        Status = $"Transcribing... (Segments: {_document.Segments.Count})";

        if (EnableTranslation)
        {
            _ = Task.Run(() => _translationService.TranslateText(segment.Text, SelectedTargetLanguage));
        }
    }

    private void HandleTranslationUpdated(TranslationResult result)
    {
        _logger.Log($"UI thread received translation: {result.TranslatedText}");
        var newTranslation = $"[{result.Timestamp:HH:mm:ss}] {result.TranslatedText}{Environment.NewLine}";
        CurrentTranslation += newTranslation;
    }

    private async Task StartRecordingAsync()
    {
        _logger.Log("Starting recording process...");
        _document = new TranscriptionDocument();
        IsRecording = true;
        Status = "Starting recording...";

        CurrentTranscript = string.Empty;
        CurrentTranslation = string.Empty;

        var options = new TranscriptionOptions
        {
            Language = "en-US",
            OutputFormat = SelectedOutputFormat,
            IncludeTimestamps = true,
            DetectSpeakers = false,
            MaxDurationSeconds = 3600,
            EnableProfanityFilter = true,
            EnableWordLevelTimestamps = true
        };

        try
        {
            if (EnableTranslation)
            {
                Status = "Initializing translation...";
                await _translationService.InitializeAsync(SelectedTargetLanguage);
            }

            Status = "Recording in progress... Speak now!";
            await _transcriptionService.StartTranscriptionAsync(options);
            _logger.Log("Recording started successfully");
        }
        catch (Exception ex)
        {
            Status = $"Error starting recording: {ex.Message}";
            _logger.Log($"Error starting recording: {ex.Message}");
            IsRecording = false;
        }
    }

    private async Task StopRecordingAsync()
    {
        _logger.Log("Stopping recording...");
        Status = "Stopping recording...";
        try
        {
            await _transcriptionService.StopTranscriptionAsync();
            _document = _transcriptionService.GetTranscriptionDocument();
            IsRecording = false;
            var segmentCount = _document.Segments.Count;
            Status = $"Recording stopped. {segmentCount} segments captured.";
            _logger.Log($"Recording stopped successfully. Segments: {segmentCount}");
        }
        catch (Exception ex)
        {
            Status = $"Error stopping recording: {ex.Message}";
            _logger.Log($"Error stopping recording: {ex.Message}");
            IsRecording = false;
        }
    }

    private async Task SaveTranscriptAsync()
    {
        Status = "Saving transcript...";
        try
        {
            var filePath = await _fileService.SaveTranscriptAsync(_document, SelectedOutputFormat);
            Status = $"Transcript saved to: {filePath}";
            _logger.Log("Save completed successfully");
        }
        catch (Exception ex)
        {
            Status = $"Error saving transcript: {ex.Message}";
            _logger.Log($"Error saving transcript: {ex.Message}");
        }
    }

    private void ClearTranscript()
    {
        CurrentTranscript = string.Empty;
        CurrentTranslation = string.Empty;
        _document = new TranscriptionDocument();
        Status = "Transcript cleared";
        _logger.Log("Transcript cleared");
    }
}