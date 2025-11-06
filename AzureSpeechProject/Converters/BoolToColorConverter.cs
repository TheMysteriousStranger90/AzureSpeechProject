using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AzureSpeechProject.Converters;

internal sealed class BoolToColorConverter : IValueConverter
{
    public IBrush TrueColor { get; set; } = new SolidColorBrush(Colors.Red);
    public IBrush FalseColor { get; set; } = new SolidColorBrush(Colors.Blue);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }
        return FalseColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
