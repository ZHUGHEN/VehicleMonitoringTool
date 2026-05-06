using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CarTelemetry.Desktop.Converters;

/// <summary>
/// Formats transmission state for dashboard status labels.
/// </summary>
public class TransmissionStatusConverter : IValueConverter
{
    public static readonly TransmissionStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTransmitting)
        {
            return isTransmitting ? "TRANSMITTING" : "OFFLINE";
        }
        return "OFFLINE";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TransmissionColorConverter : IValueConverter
{
    public static readonly TransmissionColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTransmitting)
        {
            return isTransmitting ? Colors.Green : Colors.Red;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

