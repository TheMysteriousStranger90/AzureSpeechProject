using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AzureSpeechProject.Constants;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public class SettingsViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly SecretsService _secretsService;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string Region { get; set; } = string.Empty;
    [Reactive] public string Key { get; set; } = string.Empty;
    [Reactive] public bool ShowKey { get; set; } = false;

    [Reactive] public string SelectedSpeechLanguage { get; set; } = "en-US";

    public List<string> AvailableSpeechLanguages { get; } = new List<string>
    {
        "en-US", "en-GB", "es-ES", "fr-FR", "de-DE", "it-IT",
        "pt-BR", "ja-JP", "ko-KR", "zh-CN", "ru-RU"
    };

    [Reactive] public int SelectedSampleRate { get; set; } = 16000;
    public List<int> SampleRates { get; } = new List<int> { 8000, 16000, 44100, 48000 };

    [Reactive] public int SelectedBitsPerSample { get; set; } = 16;
    public List<int> BitsPerSample { get; } = new List<int> { 8, 16, 24, 32 };

    [Reactive] public int SelectedChannels { get; set; } = 1;
    public List<int> Channels { get; } = new List<int> { 1, 2 };

    [Reactive] public string OutputDirectory { get; set; } = FileConstants.TranscriptsDirectory;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleShowKeyCommand { get; }

    public SettingsViewModel(ILogger logger, SecretsService secretsService)
    {
        _logger = logger;
        _secretsService = secretsService;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        ResetCommand = ReactiveCommand.Create(ResetSettings);
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseForDirectory);
        ToggleShowKeyCommand = ReactiveCommand.Create(ToggleShowKey);

        this.WhenActivated(disposables =>
        {
            LoadSettings();

            Disposable.Create(() => { }).DisposeWith(disposables);
        });
        
        ToggleShowKeyCommand = ReactiveCommand.Create(() => 
        {
            ShowKey = !ShowKey;
        });
    }

    private void ToggleShowKey()
    {
        ShowKey = !ShowKey;
    }


    private void LoadSettings()
    {
        try
        {
            var (region, key) = _secretsService.GetAzureSpeechCredentials();
            Region = region;
            Key = key;

            if (Directory.Exists(FileConstants.TranscriptsDirectory))
            {
                OutputDirectory = FileConstants.TranscriptsDirectory;
            }

            _logger.Log("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error loading settings: {ex.Message}");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            Environment.SetEnvironmentVariable(SecretConstants.AzureSpeechRegion, Region,
                EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(SecretConstants.AzureSpeechKey, Key, EnvironmentVariableTarget.User);

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            // Update FileConstants.TranscriptsDirectory (in a real app, you would save this to app settings)
            // This is simplified for this example

            _logger.Log("Settings saved successfully");

            // Optional: Update .env file
            await UpdateEnvFileAsync();
        }
        catch (Exception ex)
        {
            _logger.Log($"Error saving settings: {ex.Message}");
        }
    }

    private async Task UpdateEnvFileAsync()
    {
        try
        {
            string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            string content = $"{SecretConstants.AzureSpeechRegion}={Region}\n{SecretConstants.AzureSpeechKey}={Key}";

            await File.WriteAllTextAsync(envPath, content);
            _logger.Log(".env file updated successfully");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error updating .env file: {ex.Message}");
        }
    }

    private void ResetSettings()
    {
        Region = "westeurope";
        Key = string.Empty;
        SelectedSpeechLanguage = "en-US";
        SelectedSampleRate = 16000;
        SelectedBitsPerSample = 16;
        SelectedChannels = 1;
        OutputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Transcripts");

        _logger.Log("Settings reset to defaults");
    }

    private async Task BrowseForDirectory()
    {
        try
        {
            // In a real app, you would use Avalonia's folder picker
            // This is a simplified version for this example
            var mainWindow =
                App.Current?.ApplicationLifetime as
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            if (mainWindow?.MainWindow != null)
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select Output Directory",
                    Directory = OutputDirectory
                };

                var result = await dialog.ShowAsync(mainWindow.MainWindow);
                if (!string.IsNullOrEmpty(result))
                {
                    OutputDirectory = result;
                    _logger.Log($"Selected directory: {OutputDirectory}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error browsing for directory: {ex.Message}");
        }
    }
}