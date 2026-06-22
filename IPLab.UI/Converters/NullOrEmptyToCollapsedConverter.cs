using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IPLab.UI.Converters;

/// <summary>Returns Collapsed when the value is null or an empty/whitespace string; Visible otherwise.</summary>
public class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s))
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
