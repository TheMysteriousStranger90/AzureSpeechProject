using System.Globalization;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

internal sealed class BoolToPasswordCharMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.FirstOrDefault() is bool showKey)
        {
            return showKey ? '\0' : '●';
        }
        return '●';
    }
}
