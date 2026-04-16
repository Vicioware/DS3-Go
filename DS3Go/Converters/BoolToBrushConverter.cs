using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DS3Go.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public SolidColorBrush TrueBrush { get; set; } = new(Colors.White);
    public SolidColorBrush FalseBrush { get; set; } = new(Color.FromRgb(40, 40, 64));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
