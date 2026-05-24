using IPLab.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IPLab.UI.Converters;

public class OperatorStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (OperatorStatus)value switch
        {
            OperatorStatus.Running  => new SolidColorBrush(Color.FromRgb(0x00, 0x98, 0xFF)), // bright blue
            OperatorStatus.Success  => new SolidColorBrush(Color.FromRgb(0x3C, 0x9A, 0x3C)), // green
            OperatorStatus.Failed   => new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33)), // red
            OperatorStatus.Disabled => new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x3F)), // dim gray
            _                       => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), // gray (NotRun)
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
