using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly AudioCaptureService _audioCaptureService;

    [Reactive] public string CurrentTranscript { get; private set; } = string.Empty;
    [Reactive] public string CurrentTranslation { get; private set; } = string.Empty;
    [Reactive] public bool IsRecording { get; set; }
    [Reactive] public bool EnableTranslation { get; set; }
    [Reactive] public string SelectedTargetLanguage { get; set; } = "it";
    [Reactive] public string TranslationHeader { get; set; } = "Translation (Italian)";
    [Reactive] public string Status { get; set; } = "Ready to record";
    [Reactive] public TranscriptFormat SelectedOutputFormat { get; set; } = TranscriptFormat.Text;

    public List<string> AvailableLanguages { get; } = new List<string>
        { "es", "fr", "de", "it", "pt", "ja", "ko", "zh-Hans", "ru" };

    public List<TranscriptFormat> OutputFormats { get; } = new List<TranscriptFormat>
        { TranscriptFormat.Text, TranscriptFormat.Json, TranscriptFormat.Srt };

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
        AudioCaptureService audioCaptureService)
    {
        _logger = logger;
        _transcriptionService = transcriptionService;
        _translationService = translationService;
        _fileService = fileService;
        _audioCaptureService = audioCaptureService;

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
            Observable.FromEventPattern<EventHandler<TranscriptionSegment>, TranscriptionSegment>(
                    h => _transcriptionService.OnTranscriptionUpdated += h,
                    h => _transcriptionService.OnTranscriptionUpdated -= h)
                .Select(e => e.EventArgs).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(HandleTranscriptionUpdated,
                    ex => _logger.Log($"Error in transcription subscription: {ex.Message}"))
                .DisposeWith(disposables);

            Observable.FromEventPattern<EventHandler<TranslationResult>, TranslationResult>(
                    h => _translationService.OnTranslationUpdated += h,
                    h => _translationService.OnTranslationUpdated -= h)
                .Select(e => e.EventArgs).ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(HandleTranslationUpdated,
                    ex => _logger.Log($"Error in translation subscription: {ex.Message}"))
                .DisposeWith(disposables);
        });
    }

    private void HandleTranscriptionUpdated(TranscriptionSegment segment)
    {
        _document.Segments.Add(segment);
        CurrentTranscript += $"[{segment.Timestamp:HH:mm:ss}] {segment.Text}{Environment.NewLine}";
        Status = $"Transcribing... (Segments: {_document.Segments.Count})";
    }

    private void HandleTranslationUpdated(TranslationResult result)
    {
        CurrentTranslation += $"[{result.Timestamp:HH:mm:ss}] {result.TranslatedText}{Environment.NewLine}";
    }

    private async Task StartRecordingAsync()
    {
        _logger.Log("Starting recording process...");
        ClearTranscript();
        IsRecording = true;
        Status = "Initializing...";

        try
        {
            var options = new TranscriptionOptions
                { Language = "en-US", EnableProfanityFilter = true, EnableWordLevelTimestamps = true };

            await _transcriptionService.StartTranscriptionAsync(options);
            if (EnableTranslation)
            {
                await _translationService.StartTranslationAsync("en-US", SelectedTargetLanguage);
            }

            _audioCaptureService.StartCapturing();

            Status = "Recording in progress... Speak now!";
            _logger.Log("All services started successfully.");
        }
        catch (Exception ex)
        {
            Status = $"Error starting recording: {ex.Message}";
            _logger.Log($"Error starting recording: {ex.Message}");
            await StopRecordingAsync();
        }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording) return;

        _logger.Log("Stopping recording process...");
        Status = "Stopping...";

        await _audioCaptureService.StopCapturingAsync();

        await _transcriptionService.StopTranscriptionAsync();
        if (EnableTranslation)
        {
            await _translationService.StopTranslationAsync();
        }

        _document = _transcriptionService.GetTranscriptionDocument();
        IsRecording = false;
        Status = $"Recording stopped. {_document.Segments.Count} segments captured.";
        _logger.Log("All services stopped successfully.");
    }

    private async Task SaveTranscriptAsync()
    {
        Status = "Saving transcript...";
        try
        {
            var filePath = await _fileService.SaveTranscriptAsync(_document, SelectedOutputFormat);
            Status = $"Transcript saved to: {filePath}";

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

                var translationFilePath = await _fileService.SaveTranscriptAsync(
                    translationDocument,
                    SelectedOutputFormat,
                    default,
                    SelectedTargetLanguage);

                Status = $"Transcript and translation saved to: {Path.GetDirectoryName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error saving transcript: {ex.Message}";
            _logger.Log($"Error saving transcript: {ex.Message}");
        }
    }

    private bool TryParseTranslationLine(string line, out DateTime timestamp, out string text)
    {
        timestamp = DateTime.Now;
        text = line;

        try
        {
            if (line.StartsWith("[") && line.Contains("]"))
            {
                var parts = line.Split(']', 2);
                if (parts.Length == 2)
                {
                    var timeString = parts[0].Trim('[', ']');
                    if (TimeSpan.TryParse(timeString, out var timeSpan))
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
}