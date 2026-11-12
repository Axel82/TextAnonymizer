using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TextAnonymizer.Converters;

/// <summary><c>true</c> → Visible ; avec <c>ConverterParameter=Invert</c>, <c>false</c> → Visible.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (parameter is "Invert")
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
