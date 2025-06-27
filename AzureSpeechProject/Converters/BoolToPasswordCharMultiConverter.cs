using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

public class BoolToPasswordCharMultiConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values?.FirstOrDefault() is bool showKey)
        {
            return showKey ? '\0' : '●';
        }
        return '●';
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}