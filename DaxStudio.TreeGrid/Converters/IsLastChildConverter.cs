using DaxStudio.TreeGrid;
using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// Converter that determines if a HierarchicalDataGridRow is the last child of its parent
    /// </summary>
    public class IsLastChildConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HierarchicalDataGridRow<object> row)
            {
                // If this is a root level item, check if it's the last among root items
                if (row.Parent == null)
                {
                    // For root level items, we need to check if it's the last in the root collection
                    // This would require additional context, so for now we'll return false for root items
                    return false;
                }

                // For non-root items, check if this row is the last child of its parent
                var siblings = row.Parent.Children;
                if (siblings != null && siblings.Count > 0)
                {
                    return siblings[siblings.Count - 1] == row;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}