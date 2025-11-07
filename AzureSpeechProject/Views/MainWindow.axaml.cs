using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace AzureSpeechProject.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.Activator.Activate();

                Disposable.Create(() => viewModel.Activator.Deactivate())
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

    public void MinimizeButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        WindowState = WindowState.Minimized;
    }

    public void MaximizeButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        if (this.FindControl<Button>("MaximizeButton") is Button maxButton)
        {
            maxButton.Content = WindowState == WindowState.Maximized ? "🗗" : "🗖";
        }
    }

    public void CloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Close();
    }
}
