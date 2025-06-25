using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AzureSpeechProject.Helpers;
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

    [Reactive] public bool IsRecording { get; set; }
    [Reactive] public string CurrentTranscript { get; set; } = string.Empty;
    [Reactive] public string CurrentTranslation { get; set; } = string.Empty;
    [Reactive] public bool EnableTranslation { get; set; }
    [Reactive] public string SelectedTargetLanguage { get; set; } = "es";
    [Reactive] public string TranslationHeader { get; set; } = "Translation (Spanish)";
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
        try
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcriptionService =
                transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
            _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));

            _logger.Log("TranscriptionViewModel: Initializing commands...");

            var canStart = this.WhenAnyValue(x => x.IsRecording)
                .Select(isRecording => !isRecording);

            StartCommand = ReactiveCommand.CreateFromTask(StartRecordingAsync, canStart);

            var canStop = this.WhenAnyValue(x => x.IsRecording);
            StopCommand = ReactiveCommand.CreateFromTask(StopRecordingAsync, canStop);

            var canSave = this.WhenAnyValue(
                x => x.CurrentTranscript,
                x => x.IsRecording,
                (transcript, isRecording) => !string.IsNullOrWhiteSpace(transcript) && !isRecording);
            _canSave = canSave.ToProperty(this, x => x.CanSave);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveTranscriptAsync, canSave);

            var canClear = this.WhenAnyValue(
                x => x.CurrentTranscript,
                x => x.IsRecording,
                (transcript, isRecording) => !string.IsNullOrWhiteSpace(transcript) && !isRecording);
            _canClear = canClear.ToProperty(this, x => x.CanClear);
            ClearCommand = ReactiveCommand.Create(ClearTranscript, canClear);

            this.WhenAnyValue(x => x.SelectedTargetLanguage)
                .Subscribe(lang =>
                {
                    try
                    {
                        var languageNames = new Dictionary<string, string>
                        {
                            { "es", "Spanish" },
                            { "fr", "French" },
                            { "de", "German" },
                            { "it", "Italian" },
                            { "pt", "Portuguese" },
                            { "ja", "Japanese" },
                            { "ko", "Korean" },
                            { "zh-Hans", "Chinese" },
                            { "ru", "Russian" }
                        };

                        TranslationHeader = $"Translation ({languageNames.GetValueOrDefault(lang, lang)})";
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error updating translation header: {ex.Message}");
                    }
                });

            this.WhenActivated(disposables =>
            {
                try
                {
                    _transcriptionService.OnTranscriptionUpdated += HandleTranscriptionUpdated;
                    _translationService.OnTranslationUpdated += HandleTranslationUpdated;

                    Disposable.Create(() =>
                    {
                        try
                        {
                            _transcriptionService.OnTranscriptionUpdated -= HandleTranscriptionUpdated;
                            _translationService.OnTranslationUpdated -= HandleTranslationUpdated;
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Error during cleanup: {ex.Message}");
                        }
                    }).DisposeWith(disposables);

                    _logger.Log("TranscriptionViewModel activated successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error during TranscriptionViewModel activation: {ex.Message}");
                }
            });

            ToggleTranslationCommand = ReactiveCommand.Create(() =>
            {
                try
                {
                    EnableTranslation = !EnableTranslation;
                    _logger.Log($"Translation {(EnableTranslation ? "enabled" : "disabled")}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error toggling translation: {ex.Message}");
                }
            });

            _logger.Log("TranscriptionViewModel constructor completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in TranscriptionViewModel constructor: {ex}");
            throw;
        }
    }

    private void HandleTranscriptionUpdated(object? sender, TranscriptionSegment segment)
    {
        MainThreadHelper.InvokeOnMainThread(() =>
        {
            CurrentTranscript += $"[{segment.Timestamp:HH:mm:ss}] {segment.Text}{Environment.NewLine}";

            if (EnableTranslation)
            {
                _translationService.TranslateText(segment.Text, SelectedTargetLanguage);
            }
        });
    }

    private void HandleTranslationUpdated(object? sender, TranslationResult result)
    {
        MainThreadHelper.InvokeOnMainThread(() =>
        {
            CurrentTranslation += $"[{result.Timestamp:HH:mm:ss}] {result.TranslatedText}{Environment.NewLine}";
        });
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            _document = new TranscriptionDocument();
            IsRecording = true;
            Status = "Recording in progress...";

            var options = new TranscriptionOptions
            {
                OutputFormat = SelectedOutputFormat,
                IncludeTimestamps = true,
                DetectSpeakers = false,
                MaxDurationSeconds = 3600
            };

            if (EnableTranslation)
            {
                await _translationService.InitializeAsync(SelectedTargetLanguage);
            }

            await _transcriptionService.StartTranscriptionAsync(options);
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
        try
        {
            await _transcriptionService.StopTranscriptionAsync();
            _document = _transcriptionService.GetTranscriptionDocument();
            IsRecording = false;
            Status = "Recording stopped. You can save the transcript now.";
        }
        catch (Exception ex)
        {
            Status = $"Error stopping recording: {ex.Message}";
            _logger.Log($"Error stopping recording: {ex.Message}");
        }
    }

    private async Task SaveTranscriptAsync()
    {
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
                    if (line.Length > 10 && line[0] == '[' && line[9] == ']')
                    {
                        var timestamp = DateTime.ParseExact(line.Substring(1, 8), "HH:mm:ss", null);
                        var text = line.Substring(11);

                        translationDocument.Segments.Add(new TranscriptionSegment
                        {
                            Text = text,
                            Timestamp = timestamp,
                            Duration = TimeSpan.FromSeconds(2)
                        });
                    }
                }

                var translationPath = await _fileService.SaveTranscriptAsync(
                    translationDocument,
                    SelectedOutputFormat,
                    translatedLanguage: SelectedTargetLanguage);

                Status += $" Translation saved to: {translationPath}";
            }
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
    }
}