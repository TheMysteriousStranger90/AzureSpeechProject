using System.Reactive.Disposables;
using System.Reactive.Linq;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IActivatableViewModel, IDisposable
{
    private readonly ILogger _logger;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IMicrophonePermissionService _microphonePermissionService;
    private CancellationTokenSource? _initializationCts;
    private bool _disposed;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string StatusMessage { get; set; } = "Initializing...";
    [Reactive] public TranscriptionViewModel TranscriptionViewModel { get; set; }
    [Reactive] public SettingsViewModel SettingsViewModel { get; set; }
    [Reactive] public bool IsInternetAvailable { get; private set; } = true;
    [Reactive] public bool IsMicrophoneAvailable { get; private set; } = true;

    public MainWindowViewModel(
        ILogger logger,
        TranscriptionViewModel transcriptionViewModel,
        SettingsViewModel settingsViewModel,
        INetworkStatusService networkStatusService,
        IMicrophonePermissionService microphonePermissionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        TranscriptionViewModel =
            transcriptionViewModel ?? throw new ArgumentNullException(nameof(transcriptionViewModel));
        SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _networkStatusService = networkStatusService ?? throw new ArgumentNullException(nameof(networkStatusService));
        _microphonePermissionService = microphonePermissionService ??
                                       throw new ArgumentNullException(nameof(microphonePermissionService));

        try
        {
            this.WhenActivated(disposables =>
            {
                try
                {
                    _initializationCts = new CancellationTokenSource();
                    var token = _initializationCts.Token;

                    Observable.FromAsync(async () =>
                        {
                            try
                            {
                                StatusMessage = "Loading settings...";
                                await SettingsViewModel.LoadSettingsAsync(token).ConfigureAwait(false);
                                _logger.Log("Settings preloaded in MainWindowViewModel FIRST");
                                return true;
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.Log("Settings loading was cancelled");
                                return false;
                            }
                        })
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .SelectMany(settingsLoaded =>
                        {
                            if (!settingsLoaded)
                            {
                                StatusMessage = "Error loading settings";
                                return Observable.Return(false);
                            }

                            return Observable.CombineLatest(
                                Observable.FromAsync(() => CheckMicrophoneAccess(token))
                                    .Catch<bool, Exception>(ex =>
                                    {
                                        _logger.Log($"Error checking microphone: {ex.Message}");
                                        return Observable.Return(false);
                                    }),
                                Observable.FromAsync(() => CheckInternetConnectivity(token))
                                    .Catch<bool, Exception>(ex =>
                                    {
                                        _logger.Log($"Error checking internet: {ex.Message}");
                                        return Observable.Return(false);
                                    }),
                                (mic, internet) => new { Mic = mic, Internet = internet })
                            .Select(result =>
                            {
                                IsMicrophoneAvailable = result.Mic;
                                IsInternetAvailable = result.Internet;
                                _logger.Log($"Checks complete - Mic: {result.Mic}, Internet: {result.Internet}");
                                return true;
                            });
                        })
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            _ =>
                            {
                                UpdateStatusMessage();
                                _logger.Log($"Initial status set to: {StatusMessage}");
                            },
                            ex =>
                            {
                                if (ex is not OperationCanceledException)
                                {
                                    _logger.Log($"Error during initialization: {ex.Message}");
                                    StatusMessage = "Error during initialization";
                                }
                            })
                        .DisposeWith(disposables);

                    TranscriptionViewModel.WhenAnyValue(x => x.Status)
                        .Where(status => !string.IsNullOrWhiteSpace(status))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(status =>
                        {
                            StatusMessage = status;
                            _logger.Log($"Status updated from TranscriptionViewModel: {StatusMessage}");
                        })
                        .DisposeWith(disposables);

                    TranscriptionViewModel.WhenAnyValue(x => x.IsRecording)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(isRecording =>
                        {
                            if (isRecording)
                            {
                                _logger.Log("Recording state changed to: Recording");
                            }
                            else
                            {
                                _logger.Log("Recording state changed to: Stopped");
                                UpdateStatusMessage();
                            }
                        })
                        .DisposeWith(disposables);

                    SettingsViewModel.WhenAnyValue(x => x.AreCredentialsSaved)
                        .Skip(1)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(saved =>
                        {
                            _logger.Log($"Credentials saved state changed: {saved}");
                            if (!TranscriptionViewModel.IsRecording)
                            {
                                UpdateStatusMessage();
                            }
                        })
                        .DisposeWith(disposables);

                    Disposable.Create(() =>
                    {
                        _initializationCts?.Cancel();
                        _initializationCts?.Dispose();
                        _initializationCts = null;
                    }).DisposeWith(disposables);

                    _logger.Log("MainWindowViewModel activated successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error during MainWindowViewModel activation: {ex.Message}");
                    StatusMessage = "Error during initialization";
                }
            });
        }
        catch (ArgumentNullException ex)
        {
            _logger.Log($"Argument null in MainWindowViewModel constructor: {ex}");
            StatusMessage = "Critical initialization error";
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in MainWindowViewModel constructor: {ex}");
            StatusMessage = "Critical initialization error";
        }
    }

    private void UpdateStatusMessage()
    {
        if (TranscriptionViewModel.IsRecording)
            return;

        if (!IsInternetAvailable && !IsMicrophoneAvailable)
        {
            StatusMessage = "No internet connection and microphone access denied";
        }
        else if (!IsInternetAvailable)
        {
            StatusMessage = "No internet connection detected";
        }
        else if (!IsMicrophoneAvailable)
        {
            StatusMessage = "Microphone access denied";
        }
        else if (!SettingsViewModel.AreCredentialsSaved)
        {
            StatusMessage = "Azure credentials not configured or not saved - Check Settings tab";
        }
        else
        {
            StatusMessage = "Ready to record";
        }

        _logger.Log($"Status message updated to: {StatusMessage}");
    }

    private async Task<bool> CheckInternetConnectivity(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Log("Checking network connection...");
            if (!_networkStatusService.IsNetworkConnected())
            {
                _logger.Log("No network connection detected");
                return false;
            }

            _logger.Log("Checking internet connectivity...");
            var isAvailable = await _networkStatusService.IsInternetAvailableAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.Log($"Internet connection available: {isAvailable}");
            return isAvailable;
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Internet connectivity check was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking internet connectivity: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckMicrophoneAccess(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Log("Checking microphone access...");
            var hasAccess = await _microphonePermissionService.CheckMicrophonePermissionAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.Log($"Microphone access: {hasAccess}");
            return hasAccess;
        }
        catch (OperationCanceledException)
        {
            _logger.Log("Microphone access check was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking microphone access: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _initializationCts?.Cancel();
        _initializationCts?.Dispose();
        _initializationCts = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
