using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;

namespace AzureSpeechProject.Views;

internal sealed partial class TranscriptionView : ReactiveUserControl<TranscriptionViewModel>
{
    public TranscriptionView()
    {
        InitializeComponent();
    }
}
