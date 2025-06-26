using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public class SettingsViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string Region { get; set; } = string.Empty;
    [Reactive] public string Key { get; set; } = string.Empty;
    [Reactive] public bool ShowKey { get; set; } = false;

    [Reactive] public string SelectedSpeechLanguage { get; set; } = "en-US";

    public List<string> AvailableSpeechLanguages { get; } = new List<string>
    {
        "en-US",
    };

    [Reactive] public int SelectedSampleRate { get; set; } = 16000;
    public List<int> SampleRates { get; } = new List<int> { 8000, 16000, 44100, 48000 };

    [Reactive] public int SelectedBitsPerSample { get; set; } = 16;
    public List<int> BitsPerSample { get; } = new List<int> { 8, 16, 24, 32 };

    [Reactive] public int SelectedChannels { get; set; } = 1;
    public List<int> Channels { get; } = new List<int> { 1, 2 };

    [Reactive] public string OutputDirectory { get; set; } = string.Empty;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleShowKeyCommand { get; }

    public SettingsViewModel(ILogger logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        ResetCommand = ReactiveCommand.CreateFromTask(ResetSettingsAsync);
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseForDirectory);
        ToggleShowKeyCommand = ReactiveCommand.Create(() =>
        {
            ShowKey = !ShowKey;
            return Unit.Default;
        });

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Azure Speech Services",
                "Transcripts");
        }

        this.WhenActivated(disposables =>
        {
            _ = LoadSettingsAsync();
            Disposable.Create(() => { }).DisposeWith(disposables);
        });
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();

            Region = settings.Region;
            Key = settings.Key;
            SelectedSpeechLanguage = settings.SpeechLanguage;
            SelectedSampleRate = settings.SampleRate;
            SelectedBitsPerSample = settings.BitsPerSample;
            SelectedChannels = settings.Channels;

            if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                OutputDirectory = settings.OutputDirectory;
            }
            else if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            _logger.Log("Settings loaded in ViewModel");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error loading settings in ViewModel: {ex.Message}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            _logger.Log("SaveSettingsAsync called in ViewModel");
            _logger.Log(
                $"Current settings - Region: {Region}, OutputDirectory: {OutputDirectory}, SpeechLanguage: {SelectedSpeechLanguage}");

            var settings = new AppSettings
            {
                Region = Region,
                Key = Key,
                SpeechLanguage = SelectedSpeechLanguage,
                SampleRate = SelectedSampleRate,
                BitsPerSample = SelectedBitsPerSample,
                Channels = SelectedChannels,
                OutputDirectory = OutputDirectory
            };

            _logger.Log("Created AppSettings object, calling service SaveSettingsAsync");
            await _settingsService.SaveSettingsAsync(settings);
            _logger.Log("Settings saved from ViewModel successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error saving settings from ViewModel: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task ResetSettingsAsync()
    {
        try
        {
            await _settingsService.ResetToDefaultsAsync();
            await LoadSettingsAsync();
            _logger.Log("Settings reset from ViewModel");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error resetting settings from ViewModel: {ex.Message}");
        }
    }

    private async Task BrowseForDirectory()
    {
        try
        {
            var mainWindow = App.Current?.ApplicationLifetime as
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;

            if (mainWindow?.MainWindow != null)
            {
                string initialDirectory = string.IsNullOrWhiteSpace(OutputDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : OutputDirectory;

                if (!Directory.Exists(initialDirectory))
                {
                    initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                var options = new FolderPickerOpenOptions
                {
                    Title = "Select Output Directory",
                    SuggestedStartLocation = await mainWindow.MainWindow.StorageProvider
                        .TryGetFolderFromPathAsync(initialDirectory)
                };

                var result = await mainWindow.MainWindow.StorageProvider.OpenFolderPickerAsync(options);

                if (result.Count > 0)
                {
                    var selectedFolder = result[0];
                    var newPath = selectedFolder.Path.LocalPath;

                    _logger.Log($"User selected new directory: {newPath}");

                    if (OutputDirectory != newPath)
                    {
                        OutputDirectory = newPath;
                        _logger.Log($"OutputDirectory property updated to: {OutputDirectory}");

                        await SaveSettingsAsync();
                        _logger.Log("Settings automatically saved after directory selection");
                    }
                    else
                    {
                        _logger.Log("Selected directory is the same as current, no changes made");
                    }
                }
                else
                {
                    _logger.Log("User cancelled directory selection");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error browsing for directory: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");

                _logger.Log($"Set default output directory: {OutputDirectory}");
            }
        }
    }
}