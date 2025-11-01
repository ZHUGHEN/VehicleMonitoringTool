using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CarTelemetry.Desktop.Converters;

public class RpmToColorConverter : IValueConverter
{
    public static readonly RpmToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double rpm)
        {
            // Clamp RPM to our range
            rpm = Math.Clamp(rpm, 0, 8000);
            
            Color interpolatedColor;
            
            if (rpm <= 3500)
            {
                // Pure green zone (0-3500)
                interpolatedColor = Colors.Green;
            }
            else if (rpm <= 5500)
            {
                // Transition from green to yellow (3500-5500)
                double progress = (rpm - 3500) / (5500 - 3500); // 0.0 to 1.0
                interpolatedColor = InterpolateColor(Colors.Green, Colors.Yellow, progress);
            }
            else
            {
                // Transition from yellow to red (5500-8000)
                double progress = (rpm - 5500) / (8000 - 5500); // 0.0 to 1.0
                interpolatedColor = InterpolateColor(Colors.Yellow, Colors.Red, progress);
            }
            
            return new SolidColorBrush(interpolatedColor);
        }
        return new SolidColorBrush(Colors.Gray);
    }
    
    private static Color InterpolateColor(Color color1, Color color2, double progress)
    {
        // Clamp progress between 0 and 1
        progress = Math.Clamp(progress, 0.0, 1.0);
        
        // Interpolate each color component
        byte r = (byte)(color1.R + (color2.R - color1.R) * progress);
        byte g = (byte)(color1.G + (color2.G - color1.G) * progress);
        byte b = (byte)(color1.B + (color2.B - color1.B) * progress);
        byte a = (byte)(color1.A + (color2.A - color1.A) * progress);
        
        return Color.FromArgb(a, r, g, b);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}