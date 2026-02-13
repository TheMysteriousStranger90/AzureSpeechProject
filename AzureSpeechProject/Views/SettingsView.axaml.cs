using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;

namespace AzureSpeechProject.Views;

internal sealed partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
