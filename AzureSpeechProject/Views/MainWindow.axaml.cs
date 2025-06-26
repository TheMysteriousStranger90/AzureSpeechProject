using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace AzureSpeechProject.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Это позволяет перетаскивать окно при нажатии на заголовок
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
            
            // Обновление значка кнопки в зависимости от состояния окна
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