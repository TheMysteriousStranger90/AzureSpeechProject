using Avalonia.Input;
using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace AzureSpeechProject.Views;

internal sealed partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (ViewModel != null)
            {
                ViewModel.Activator.Activate();

                Disposable.Create(() => ViewModel.Activator.Deactivate())
                    .DisposeWith(disposables);

                this.Bind(ViewModel,
                        vm => vm.CurrentWindowState,
                        v => v.WindowState)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                        vm => vm.MaximizeButtonIcon,
                        v => v.MaximizeButton.Content)
                    .DisposeWith(disposables);
            }
        });
    }

    public void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    public void CloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Close();
    }
}
