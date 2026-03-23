using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.Controls.Converters
{
    public class FallbackValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the source property is set to a non-default value, use it
            if (value != null && !IsDefaultValue(value))
                return value;

            // Otherwise, return UnsetValue to use the style's value
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private bool IsDefaultValue(object value)
        {
            // For brushes, compare with default
            if (value is SolidColorBrush brush)
                return brush.Color.Equals(Colors.Gray);

            return false;
        }
    }
}