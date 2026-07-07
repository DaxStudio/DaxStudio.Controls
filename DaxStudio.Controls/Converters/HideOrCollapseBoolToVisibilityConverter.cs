using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.Controls.Converters
{
    public class HideOrCollapseBoolToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool dontHide = (values[0] as bool?)??false;
            bool dontCollapse = (values[1] as bool?)??false;

            if (!dontCollapse) return Visibility.Collapsed;
            // Collapse (rather than merely hide) the expander for nodes with no children
            // so the reserved space is reclaimed and the icon/text align with the tree line.
            if (!dontHide) return Visibility.Collapsed;
            return Visibility.Visible;

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing }; // Not implemented, as this converter is one-way
        }
    }
}
