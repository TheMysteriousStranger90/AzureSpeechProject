using System;
using System.Reactive.Disposables;
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
                    TranscriptionViewModel.WhenAnyValue(x => x.Status)
                        .Subscribe(status => 
                        {
                            StatusMessage = status ?? "Ready";
                            _logger.Log($"Status updated: {StatusMessage}");
                        })
                        .DisposeWith(disposables);

                    StatusMessage = "Ready";
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