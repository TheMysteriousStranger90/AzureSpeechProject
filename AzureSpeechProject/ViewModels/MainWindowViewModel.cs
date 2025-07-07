using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AzureSpeechProject.Logger;
using AzureSpeechProject.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Threading.Tasks;

namespace AzureSpeechProject.ViewModels;

public class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly ILogger _logger;
    private readonly INetworkStatusService _networkStatusService;
    private readonly IMicrophonePermissionService _microphonePermissionService;

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
        try
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            TranscriptionViewModel =
                transcriptionViewModel ?? throw new ArgumentNullException(nameof(transcriptionViewModel));
            SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
            _networkStatusService =
                networkStatusService ?? throw new ArgumentNullException(nameof(networkStatusService));
            _microphonePermissionService =
                microphonePermissionService ?? throw new ArgumentNullException(nameof(microphonePermissionService));
            
            this.WhenActivated(disposables =>
            {
                try
                {
                    Observable.FromAsync(CheckMicrophoneAccess)
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
                            ex => _logger.Log($"Error checking microphone access: {ex.Message}")
                        )
                        .DisposeWith(disposables);
/*
                    Observable.Interval(TimeSpan.FromMinutes(1))
                        .SelectMany(_ => Observable.FromAsync(CheckMicrophoneAccess))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            isAvailable =>
                            {
                                if (isAvailable != IsMicrophoneAvailable)
                                {
                                    IsMicrophoneAvailable = isAvailable;
                                    _logger.Log($"Microphone access changed: {isAvailable}");

                                    if (!isAvailable && !TranscriptionViewModel.IsRecording)
                                    {
                                        StatusMessage =
                                            "⚠️ Microphone access denied. Please grant microphone permissions in Windows Settings.";
                                    }
                                    else if (isAvailable && !TranscriptionViewModel.IsRecording && IsInternetAvailable)
                                    {
                                        StatusMessage = "✅ Microphone access granted. Ready to record.";
                                    }
                                }
                            },
                            ex => _logger.Log($"Error in periodic microphone check: {ex.Message}")
                        )
                        .DisposeWith(disposables);
*/
                    Observable.FromAsync(CheckInternetConnectivity)
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
                            ex => _logger.Log($"Error checking internet connectivity: {ex.Message}")
                        )
                        .DisposeWith(disposables);
/*
                    Observable.Interval(TimeSpan.FromSeconds(30))
                        .SelectMany(_ => Observable.FromAsync(CheckInternetConnectivity))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            isAvailable =>
                            {
                                if (isAvailable != IsInternetAvailable)
                                {
                                    IsInternetAvailable = isAvailable;
                                    _logger.Log($"Internet connectivity changed: {isAvailable}");

                                    if (!isAvailable && !TranscriptionViewModel.IsRecording)
                                    {
                                        if (!IsMicrophoneAvailable)
                                        {
                                            StatusMessage =
                                                "⚠️ No internet connection and microphone access denied. Check both.";
                                        }
                                        else
                                        {
                                            StatusMessage =
                                                "⚠️ Internet connection lost. Azure Speech Services will not work.";
                                        }
                                    }
                                    else if (isAvailable && !TranscriptionViewModel.IsRecording)
                                    {
                                        if (!IsMicrophoneAvailable)
                                        {
                                            StatusMessage =
                                                "⚠️ Microphone access denied. Please grant microphone permissions.";
                                        }
                                        else
                                        {
                                            StatusMessage = "✅ Internet connection restored. Ready to record.";
                                        }
                                    }
                                }
                            },
                            ex => _logger.Log($"Error in periodic internet check: {ex.Message}")
                        )
                        .DisposeWith(disposables);
*/
                    Observable.FromAsync(async () =>
                        {
                            StatusMessage = "Loading settings...";
                            await SettingsViewModel.LoadSettingsAsync();
                            _logger.Log("Settings preloaded in MainWindowViewModel");
                            StatusMessage = "Settings loaded successfully";

                            await Task.Delay(1000);

                            UpdateStatusMessage();
                        })
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(
                            _ => { },
                            ex =>
                            {
                                _logger.Log($"Error preloading settings in MainWindowViewModel: {ex.Message}");
                                StatusMessage = "❌ Error loading settings";
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

                    _logger.Log("MainWindowViewModel activated successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error during MainWindowViewModel activation: {ex.Message}");
                    StatusMessage = "❌ Error during initialization";
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in MainWindowViewModel constructor: {ex}");
            throw;
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

    private async Task<bool> CheckInternetConnectivity()
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
            var isAvailable = await _networkStatusService.IsInternetAvailableAsync();
            _logger.Log($"Internet connection available: {isAvailable}");
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking internet connectivity: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckMicrophoneAccess()
    {
        try
        {
            _logger.Log("Checking microphone access...");
            var hasAccess = await _microphonePermissionService.CheckMicrophonePermissionAsync();
            _logger.Log($"Microphone access: {hasAccess}");
            return hasAccess;
        }
        catch (Exception ex)
        {
            _logger.Log($"Error checking microphone access: {ex.Message}");
            return false;
        }
    }
}