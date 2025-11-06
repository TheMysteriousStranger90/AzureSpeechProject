using System.Reactive.Disposables;
using System.Reactive.Linq;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IMicrophonePermissionService _microphonePermissionService;
    private CancellationTokenSource? _initializationCts;

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
        TranscriptionViewModel = transcriptionViewModel ?? throw new ArgumentNullException(nameof(transcriptionViewModel));
        SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _networkStatusService = networkStatusService ?? throw new ArgumentNullException(nameof(networkStatusService));
        _microphonePermissionService = microphonePermissionService ?? throw new ArgumentNullException(nameof(microphonePermissionService));

        try
        {
            this.WhenActivated(disposables =>
            {
                try
                {
                    _initializationCts = new CancellationTokenSource();
                    var token = _initializationCts.Token;

                    Observable.FromAsync(() => CheckMicrophoneAccess(token))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            isAvailable =>
                            {
                                IsMicrophoneAvailable = isAvailable;
                                _logger.Log($"Initial microphone access check: {isAvailable}");

                                if (!isAvailable)
                                {
                                    StatusMessage =
                                        "⚠️ Microphone access denied. Please grant microphone permissions in Windows Settings.";
                                }
                            },
                            ex =>
                            {
                                if (ex is OperationCanceledException)
                                {
                                    _logger.Log("Microphone check was cancelled");
                                }
                                else
                                {
                                    _logger.Log($"Error checking microphone access: {ex.Message}");
                                }
                            }
                        )
                        .DisposeWith(disposables);

                    Observable.FromAsync(() => CheckInternetConnectivity(token))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            isAvailable =>
                            {
                                IsInternetAvailable = isAvailable;
                                _logger.Log($"Initial internet connectivity check: {isAvailable}");

                                if (!isAvailable && IsMicrophoneAvailable)
                                {
                                    StatusMessage =
                                        "No internet connection detected. Azure Speech Services will not work.";
                                }
                                else if (!isAvailable && !IsMicrophoneAvailable)
                                {
                                    StatusMessage =
                                        "No internet connection and microphone access denied. Check both.";
                                }
                            },
                            ex =>
                            {
                                if (ex is OperationCanceledException)
                                {
                                    _logger.Log("Internet check was cancelled");
                                }
                                else
                                {
                                    _logger.Log($"Error checking internet connectivity: {ex.Message}");
                                }
                            }
                        )
                        .DisposeWith(disposables);

                    Observable.FromAsync(async () =>
                        {
                            try
                            {
                                StatusMessage = "Loading settings...";
                                await SettingsViewModel.LoadSettingsAsync(token).ConfigureAwait(false);
                                _logger.Log("Settings preloaded in MainWindowViewModel");
                                StatusMessage = "Settings loaded successfully";

                                await Task.Delay(1000, token).ConfigureAwait(false);

                                UpdateStatusMessage();
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.Log("Settings loading was cancelled");
                            }
                        })
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            _ => { },
                            ex =>
                            {
                                if (ex is not OperationCanceledException)
                                {
                                    _logger.Log($"Error preloading settings in MainWindowViewModel: {ex.Message}");
                                    StatusMessage = "❌ Error loading settings";
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

                    Observable.CombineLatest(
                            SettingsViewModel.WhenAnyValue(x => x.Region),
                            SettingsViewModel.WhenAnyValue(x => x.Key),
                            (region, key) => new { Region = region, Key = key })
                        .Skip(1)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(settings =>
                        {
                            if (string.IsNullOrWhiteSpace(settings.Region) || string.IsNullOrWhiteSpace(settings.Key))
                            {
                                if (!TranscriptionViewModel.IsRecording)
                                {
                                    StatusMessage = "Azure credentials not configured - Check Settings tab";
                                }
                            }
                            else if (!TranscriptionViewModel.IsRecording)
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
                    StatusMessage = "❌ Error during initialization";
                }
            });
        }
        catch (ArgumentNullException ex)
        {
            _logger.Log($"Argument null in MainWindowViewModel constructor: {ex}");
            StatusMessage = "❌ Critical initialization error";
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in MainWindowViewModel constructor: {ex}");
            StatusMessage = "❌ Critical initialization error";
        }
    }

    private void UpdateStatusMessage()
    {
        if (TranscriptionViewModel.IsRecording)
            return;

        if (!IsInternetAvailable && !IsMicrophoneAvailable)
        {
            StatusMessage = "No internet connection and microphone access denied. Check both.";
        }
        else if (!IsInternetAvailable)
        {
            StatusMessage = "No internet connection detected. Azure Speech Services will not work.";
        }
        else if (!IsMicrophoneAvailable)
        {
            StatusMessage = "Microphone access denied. Please grant microphone permissions in Windows Settings.";
        }
        else
        {
            var hasAzureCredentials = !string.IsNullOrWhiteSpace(SettingsViewModel.Region) &&
                                      !string.IsNullOrWhiteSpace(SettingsViewModel.Key);

            if (!hasAzureCredentials)
            {
                StatusMessage = "Azure credentials not configured - Check Settings tab";
            }
            else
            {
                StatusMessage = "Ready to record";
            }
        }
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
            var isAvailable = await _networkStatusService.IsInternetAvailableAsync(cancellationToken).ConfigureAwait(false);
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
            var hasAccess = await _microphonePermissionService.CheckMicrophonePermissionAsync(cancellationToken).ConfigureAwait(false);
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
}
