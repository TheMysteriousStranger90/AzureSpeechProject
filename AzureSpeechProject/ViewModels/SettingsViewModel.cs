using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public class SettingsViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _operationCts;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string Region { get; set; } = string.Empty;
    [Reactive] public string Key { get; set; } = string.Empty;
    [Reactive] public bool ShowKey { get; set; } = false;

    [Reactive] public string SelectedSpeechLanguage { get; set; } = "en-US";

    public IReadOnlyList<string> AvailableSpeechLanguages { get; } = new List<string>
    {
        "en-US",
    };

    [Reactive] public int SelectedSampleRate { get; set; } = 16000;
    public IReadOnlyList<int> SampleRates { get; } = new List<int> { 8000, 16000, 44100, 48000 };

    [Reactive] public int SelectedBitsPerSample { get; set; } = 16;
    public IReadOnlyList<int> BitsPerSample { get; } = new List<int> { 8, 16, 24, 32 };

    [Reactive] public int SelectedChannels { get; set; } = 1;
    public IReadOnlyList<int> Channels { get; } = new List<int> { 1, 2 };

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
            _operationCts = new CancellationTokenSource();

            Observable.FromAsync(() => LoadSettingsAsync(_operationCts.Token))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(
                    _ => _logger.Log("Settings loaded on activation"),
                    ex => _logger.Log($"Error loading settings on activation: {ex.Message}"))
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                _operationCts?.Cancel();
                _operationCts?.Dispose();
                _operationCts = null;
            }).DisposeWith(disposables);
        });
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

            _logger.Log($"Loaded from service - OutputDirectory: '{settings.OutputDirectory}'");
            _logger.Log($"Current ViewModel OutputDirectory before update: '{OutputDirectory}'");

            Region = settings.Region ?? string.Empty;
            Key = settings.Key ?? string.Empty;
            SelectedSpeechLanguage = settings.SpeechLanguage ?? "en-US";
            SelectedSampleRate = settings.SampleRate;
            SelectedBitsPerSample = settings.BitsPerSample;
            SelectedChannels = settings.Channels;

            if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                OutputDirectory = settings.OutputDirectory;
                _logger.Log($"✅ Set OutputDirectory from settings: '{OutputDirectory}'");
            }
            else
            {
                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");

                OutputDirectory = defaultPath;
                _logger.Log($"📁 Set default OutputDirectory: '{OutputDirectory}'");
            }

            _logger.Log($"Final ViewModel OutputDirectory: '{OutputDirectory}'");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings loading was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"❌ Error loading settings in ViewModel: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
                _logger.Log($"📁 Set fallback OutputDirectory: '{OutputDirectory}'");
            }
            throw;
        }
    }

    private async Task SaveSettingsAsync()
    {
        var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

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
            await _settingsService.SaveSettingsAsync(settings, saveCts.Token).ConfigureAwait(false);
            _logger.Log("Settings saved from ViewModel successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings save was cancelled or timed out");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error saving settings from ViewModel: {ex.Message}");
            _logger.Log($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            saveCts.Dispose();
        }
    }

    private async Task ResetSettingsAsync()
    {
        var resetCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await _settingsService.ResetToDefaultsAsync(resetCts.Token).ConfigureAwait(false);
            await LoadSettingsAsync(resetCts.Token).ConfigureAwait(false);
            _logger.Log("Settings reset from ViewModel");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings reset was cancelled or timed out");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error resetting settings from ViewModel: {ex.Message}");
            throw;
        }
        finally
        {
            resetCts.Dispose();
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
                        .TryGetFolderFromPathAsync(initialDirectory).ConfigureAwait(false)
                };

                var result = await mainWindow.MainWindow.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(false);

                if (result.Count > 0)
                {
                    var selectedFolder = result[0];
                    var newPath = selectedFolder.Path.LocalPath;

                    _logger.Log($"User selected new directory: {newPath}");

                    if (OutputDirectory != newPath)
                    {
                        OutputDirectory = newPath;
                        _logger.Log($"OutputDirectory property updated to: {OutputDirectory}");

                        await SaveSettingsAsync().ConfigureAwait(false);
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
            throw;
        }
    }
}
