using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Converters;

namespace DaxStudio.Controls.Converters
{
    public class MultiplyValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double baseValue = 1.0;
            if (values != null && values is Array valuesArray)
            {
                foreach (var val in valuesArray)
                {
                    baseValue *= GetDouble(val);
                }
                return baseValue;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            return (object[])Binding.DoNothing; // ConvertBack is typically not used for multi-value converters  
        }

        private double GetDouble(object value)
        {
            switch (value)
            {
                case DependencyProperty p:
                    return 0.0;
                case int i:
                    return System.Convert.ToDouble(i);
                case double d:
                    return d;
                case string s:
                    double.TryParse(s, out double val);
                    return val;
                default:
                    return 0.0;
            }
        }
    }
}
