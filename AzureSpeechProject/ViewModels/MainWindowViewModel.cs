using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AzureSpeechProject.Logger;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AzureSpeechProject.ViewModels;

public class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly ILogger _logger;

    public ViewModelActivator Activator { get; } = new ViewModelActivator();

    [Reactive] public string StatusMessage { get; set; } = "Initializing...";
    [Reactive] public TranscriptionViewModel TranscriptionViewModel { get; set; }
    [Reactive] public SettingsViewModel SettingsViewModel { get; set; }

    public MainWindowViewModel(
        ILogger logger,
        TranscriptionViewModel transcriptionViewModel,
        SettingsViewModel settingsViewModel)
    {
        try
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            TranscriptionViewModel = transcriptionViewModel ?? throw new ArgumentNullException(nameof(transcriptionViewModel));
            SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));

            _logger.Log("MainWindowViewModel constructor completed successfully");

            this.WhenActivated(disposables =>
            {
                try
                {
                    // Загружаем настройки при активации
                    Observable.FromAsync(async () =>
                    {
                        StatusMessage = "Loading settings...";
                        await SettingsViewModel.LoadSettingsAsync();
                        _logger.Log("Settings preloaded in MainWindowViewModel");
                        StatusMessage = "Settings loaded successfully";
                        
                        // Небольшая задержка, чтобы пользователь увидел сообщение
                        await System.Threading.Tasks.Task.Delay(1000);
                        StatusMessage = "Ready";
                    })
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(
                        _ => { },
                        ex =>
                        {
                            _logger.Log($"Error preloading settings in MainWindowViewModel: {ex.Message}");
                            StatusMessage = "Error loading settings";
                        })
                    .DisposeWith(disposables);

                    // Реактивно отслеживаем изменения статуса транскрипции
                    TranscriptionViewModel.WhenAnyValue(x => x.Status)
                        .Where(status => !string.IsNullOrWhiteSpace(status))
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(status => 
                        {
                            StatusMessage = status;
                            _logger.Log($"Status updated from TranscriptionViewModel: {StatusMessage}");
                        })
                        .DisposeWith(disposables);

                    // Отслеживаем состояние записи для обновления статуса
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
                            }
                        })
                        .DisposeWith(disposables);

                    // Отслеживаем ошибки в настройках
                    Observable.CombineLatest(
                        SettingsViewModel.WhenAnyValue(x => x.Region),
                        SettingsViewModel.WhenAnyValue(x => x.Key),
                        (region, key) => new { Region = region, Key = key })
                        .Skip(1) // Пропускаем первоначальные значения
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
                                StatusMessage = "Ready to record";
                            }
                        })
                        .DisposeWith(disposables);

                    _logger.Log("MainWindowViewModel activated successfully");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error during MainWindowViewModel activation: {ex.Message}");
                    StatusMessage = "Error during initialization";
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in MainWindowViewModel constructor: {ex}");
            throw;
        }
    }
}