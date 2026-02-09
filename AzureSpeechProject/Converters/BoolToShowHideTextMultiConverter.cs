using System.Globalization;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

internal sealed class BoolToShowHideTextMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.FirstOrDefault() is bool showKey)
        {
            return showKey ? "🔓 Hide" : "🔒 Show";
        }
        return "🔒 Show";
    }
}
