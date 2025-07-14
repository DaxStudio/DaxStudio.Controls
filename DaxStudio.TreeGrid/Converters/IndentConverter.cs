using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// Converter for calculating indentation based on hierarchy level
    /// </summary>
    public class IndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                double indentWidth = 20.0; // Default indent width

                // Try to get indent width from parameter
                if (parameter != null)
                {
                    if (parameter is double paramDouble)
                    {
                        indentWidth = paramDouble;
                    }
                    else if (double.TryParse(parameter.ToString(), out double parsedWidth))
                    {
                        indentWidth = parsedWidth;
                    }
                }

                // Return a Thickness with left margin based on level
                return level * indentWidth; //  new Thickness(level * indentWidth, 0, 0, 0);
            }

            return 0.0;//  new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}