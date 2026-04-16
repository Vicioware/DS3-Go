using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DS3Go.Models;

namespace DS3Go.Converters;

public sealed class PortStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EmptyBrush = new(Color.FromRgb(58, 58, 80));
    private static readonly SolidColorBrush AssignedBrush = new(Color.FromRgb(232, 160, 48));
    private static readonly SolidColorBrush ConnectedBrush = new(Color.FromRgb(64, 208, 128));

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
