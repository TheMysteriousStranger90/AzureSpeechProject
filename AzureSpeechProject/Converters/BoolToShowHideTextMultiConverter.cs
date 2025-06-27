using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

public class BoolToShowHideTextMultiConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.FirstOrDefault() is bool showKey)
        {
            return showKey ? "🔓 Hide" : "🔒 Show";
        }
        return "🔒 Show";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}