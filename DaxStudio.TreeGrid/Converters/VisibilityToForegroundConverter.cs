using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// Converter that converts null values to Visibility.Collapsed and non-null values to Visibility.Visible
    /// </summary>
    public class VisibilityToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((bool)value)
            {
                return SystemColors.ControlTextColor;
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // ConvertBack is typically not used for visibility converters
            return Binding.DoNothing;
        }
    }
}