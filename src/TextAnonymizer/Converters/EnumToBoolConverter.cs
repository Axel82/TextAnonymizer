using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TextAnonymizer.Converters;

/// <summary>Lie un RadioButton à une valeur d'énumération (<c>ConverterParameter</c> = nom de la valeur).</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}
