using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AzureSpeechProject.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = this.WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
        
        var button = this.FindControl<Button>("MaximizeButton");
        if (button != null)
        {
            button.Content = this.WindowState == WindowState.Maximized ? "🗗" : "🗖";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == WindowStateProperty)
        {
            var button = this.FindControl<Button>("MaximizeButton");
            if (button != null)
            {
                button.Content = this.WindowState == WindowState.Maximized ? "🗗" : "🗖";
            }
        }
    }
}