using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AzureSpeechProject.Converters;

public class BoolToIconConverter : IValueConverter
{
    public string TrueIcon { get; set; } = "Stop";
    public string FalseIcon { get; set; } = "Play";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (boolValue && TrueIcon == "Stop")
            {
                return "M18,18H6V6H18V18Z";
            }
            else if (!boolValue && FalseIcon == "Microphone")
            {
                return "M12,2A3,3 0 0,1 15,5V11A3,3 0 0,1 12,14A3,3 0 0,1 9,11V5A3,3 0 0,1 12,2M19,11C19,14.53 16.39,17.44 13,17.93V21H11V17.93C7.61,17.44 5,14.53 5,11H7A5,5 0 0,0 12,16A5,5 0 0,0 17,11H19Z"; // Microphone icon
            }
            
            return boolValue ? TrueIcon : FalseIcon;
        }
        return FalseIcon;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}