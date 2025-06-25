using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;

namespace AzureSpeechProject.Views;

public partial class TranscriptionView : ReactiveUserControl<TranscriptionViewModel>
{
    public TranscriptionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}