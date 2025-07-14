using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using DaxStudio.TreeGrid;


namespace DaxStudio.UI.Converters
{
    public class TreeLineInfoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HierarchicalDataGridRow row)
            {
                var ancestorInfo = new List<bool>();
                var current = row.Parent;

                while (current != null)
                {
                    // Check if this ancestor is the last child of its parent
                    var isLastChild = current.Parent == null || 
                                     current.Parent.Children.LastOrDefault() == current;
                    ancestorInfo.Insert(0, isLastChild);
                    current = current.Parent;
                }

                return ancestorInfo;
            }

            return new List<bool>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsLastChildConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HierarchicalDataGridRow row && row.Parent != null)
            {
                if (row.Parent == null) return true;
                return row.Parent.Children.LastOrDefault() == row;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}