using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using AzureSpeechProject.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;

namespace AzureSpeechProject.Views
{
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

        public void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        public void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        public void MaximizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            
            if (this.FindControl<Button>("MaximizeButton") is Button maxButton)
            {
                maxButton.Content = WindowState == WindowState.Maximized ? "🗗" : "🗖";
            }
        }

        public void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}