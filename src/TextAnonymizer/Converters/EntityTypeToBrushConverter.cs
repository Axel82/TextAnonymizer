using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TextAnonymizer.Models;

namespace TextAnonymizer.Converters;

[ValueConversion(typeof(EntityType), typeof(Brush))]
public sealed class EntityTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is EntityType type ? EntityColors.BrushFor(type) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
