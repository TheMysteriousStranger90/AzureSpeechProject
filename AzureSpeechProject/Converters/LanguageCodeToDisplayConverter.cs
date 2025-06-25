using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

public class LanguageCodeToDisplayConverter : IValueConverter
{
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        { "es", "Spanish" },
        { "fr", "French" },
        { "de", "German" },
        { "it", "Italian" },
        { "pt", "Portuguese" },
        { "ja", "Japanese" },
        { "ko", "Korean" },
        { "zh-Hans", "Chinese" },
        { "ru", "Russian" }
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string languageCode)
        {
            return LanguageNames.GetValueOrDefault(languageCode, languageCode);
        }
        return value ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}