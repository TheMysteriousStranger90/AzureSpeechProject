using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using AzureSpeechProject.Interfaces;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

internal sealed class SettingsViewModel : ReactiveObject, IActivatableViewModel, IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _operationCts;
    private bool _disposed;
    private bool _isLoadingSettings;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string Region { get; set; } = string.Empty;
    [Reactive] public string Key { get; set; } = string.Empty;
    [Reactive] public bool ShowKey { get; set; }

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

    [Reactive] public bool AreCredentialsSaved { get; private set; }

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
                    _ => { _logger.Log("Settings loaded on activation"); },
                    ex => _logger.Log($"Error loading settings on activation: {ex.Message}"))
                .DisposeWith(disposables);

            Observable.CombineLatest(
                    this.WhenAnyValue(x => x.Region),
                    this.WhenAnyValue(x => x.Key),
                    (region, key) => new { Region = region, Key = key })
                .Skip(1)
                .Where(_ => !_isLoadingSettings)
                .Subscribe(_ =>
                {
                    UpdateCredentialsSavedState();
                    _logger.Log($"Settings changed - AreCredentialsSaved: {AreCredentialsSaved}");
                })
                .DisposeWith(disposables);

            Disposable.Create(() =>
            {
                _operationCts?.Cancel();
                _operationCts?.Dispose();
                _operationCts = null;
            }).DisposeWith(disposables);
        });
    }

    private void UpdateCredentialsSavedState()
    {
        var hasCredentials = !string.IsNullOrWhiteSpace(Region) && !string.IsNullOrWhiteSpace(Key);

        if (!hasCredentials)
        {
            AreCredentialsSaved = false;
            _logger.Log("Credentials not configured");
            return;
        }

        try
        {
            var savedSettings = _settingsService.LoadSettingsAsync(CancellationToken.None).GetAwaiter().GetResult();
            var credentialsMatch = savedSettings.Region == Region && savedSettings.Key == Key;
            AreCredentialsSaved = credentialsMatch;
            _logger.Log($"Credentials match saved state: {credentialsMatch}");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking saved credentials: {ex.Message}");
            AreCredentialsSaved = false;
        }
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        _isLoadingSettings = true;

        try
        {
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);

            _logger.Log($"Loaded from service - OutputDirectory: '{settings.OutputDirectory}'");

            Region = settings.Region ?? string.Empty;
            Key = settings.Key ?? string.Empty;
            SelectedSpeechLanguage = settings.SpeechLanguage ?? "en-US";
            SelectedSampleRate = settings.SampleRate;
            SelectedBitsPerSample = settings.BitsPerSample;
            SelectedChannels = settings.Channels;

            if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                OutputDirectory = settings.OutputDirectory;
            }
            else
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            var hasCredentials = !string.IsNullOrWhiteSpace(Region) && !string.IsNullOrWhiteSpace(Key);
            AreCredentialsSaved = hasCredentials;
            _logger.Log($"Credentials loaded - AreCredentialsSaved: {AreCredentialsSaved}");
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings loading was cancelled");
            throw;
        }
        catch (IOException ex)
        {
            _logger.Log($"Error loading settings in ViewModel: {ex.Message}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            throw;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.Log($"JSON error loading settings: {ex.Message}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            throw;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        using var saveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            _logger.Log(
                $"Saving - Region: {Region}, OutputDirectory: {OutputDirectory}, SpeechLanguage: {SelectedSpeechLanguage}");

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

            await _settingsService.SaveSettingsAsync(settings, saveCts.Token).ConfigureAwait(false);

            var hasCredentials = !string.IsNullOrWhiteSpace(Region) && !string.IsNullOrWhiteSpace(Key);
            AreCredentialsSaved = hasCredentials;

            _logger.Log($"Settings saved - AreCredentialsSaved: {AreCredentialsSaved}");

            if (_logger is FileLogger fileLogger)
            {
                fileLogger.UpdateLogPathFromSettings(OutputDirectory);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Settings save was cancelled or timed out");
            throw;
        }
        catch (IOException ex)
        {
            _logger.Log($"Error saving settings from ViewModel: {ex.Message}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Access denied saving settings: {ex.Message}");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation saving settings: {ex.Message}");
            throw;
        }
    }

    private async Task ResetSettingsAsync()
    {
        using var resetCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

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
        catch (IOException ex)
        {
            _logger.Log($"Error resetting settings from ViewModel: {ex.Message}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Access denied resetting settings: {ex.Message}");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Invalid operation resetting settings: {ex.Message}");
            throw;
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

                var result = await mainWindow.MainWindow.StorageProvider.OpenFolderPickerAsync(options)
                    .ConfigureAwait(false);

                if (result.Count > 0)
                {
                    var selectedFolder = result[0];
                    var newPath = selectedFolder.Path.LocalPath;

                    _logger.Log($"User selected new directory: {newPath}");

                    if (OutputDirectory != newPath)
                    {
                        OutputDirectory = newPath;
                        await SaveSettingsAsync().ConfigureAwait(false);
                        _logger.Log("Settings automatically saved after directory selection");
                    }
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.Log($"Error browsing for directory: {ex.Message}");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Azure Speech Services",
                    "Transcripts");
            }

            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Log($"Access denied browsing for directory: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
