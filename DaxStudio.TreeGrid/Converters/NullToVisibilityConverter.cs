using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// Converter that converts null values to Visibility.Collapsed and non-null values to Visibility.Visible
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // ConvertBack is typically not used for visibility converters
            return Binding.DoNothing;
        }
    }
}