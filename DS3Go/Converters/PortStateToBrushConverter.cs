using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DS3Go.Models;

namespace DS3Go.Converters;

/// <summary>
/// Converts PortState to a border/indicator Brush.
/// </summary>
public sealed class PortStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EmptyBrush = new(Color.FromRgb(42, 42, 60));
    private static readonly SolidColorBrush AssignedBrush = new(Color.FromRgb(200, 152, 48));
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(60, 200, 120));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PortState state)
        {
            return state switch
            {
                PortState.Empty => EmptyBrush,
                PortState.Assigned => AssignedBrush,
                PortState.Connected => ConnectedBrush,
                _ => EmptyBrush
            };
        }
        return EmptyBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts PortState to a subtle background fill Brush for port cards.
/// </summary>
public sealed class PortStateToBackgroundConverter : IValueConverter
{
    // Subtle, dark tinted backgrounds
    private static readonly SolidColorBrush EmptyBg = new(Color.FromRgb(26, 26, 38));    // #1A1A26  neutral
    private static readonly SolidColorBrush AssignedBg = new(Color.FromRgb(34, 30, 20));  // warm amber tint
    private static readonly SolidColorBrush ConnectedBg = new(Color.FromRgb(20, 32, 28)); // cool green tint

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PortState state)
        {
            return state switch
            {
                PortState.Empty => EmptyBg,
                PortState.Assigned => AssignedBg,
                PortState.Connected => ConnectedBg,
                _ => EmptyBg
            };
        }
        return EmptyBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
