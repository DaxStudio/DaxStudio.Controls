using DaxStudio.Controls.Model;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;


namespace DaxStudio.Controls.Example
{
    public partial class TreeGridExample : UserControl
    {
        public TreeGridExample()
        {
            InitializeComponent();
            // Set the DataContext to the ViewModel
            DataContext = new TreeGridExampleViewModel();
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            HierarchicalGrid.ExpandAll();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            HierarchicalGrid.CollapseAll();
        }

        private void Expander_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is TreeGridRow<object> row)
            {
                HierarchicalGrid.ToggleItem(row.Data);
            }
        }

        private void PruneTree_Click(object sender, RoutedEventArgs e)
        {
            ((TreeGridExampleViewModel)HierarchicalGrid.DataContext).RootItems.RemoveAt(1);
        }

        private void ResetTree_Click(object sender, RoutedEventArgs e)
        {
            ((TreeGridExampleViewModel)HierarchicalGrid.DataContext).Reset();
        }

        private void ClearTree_Click(object sender, RoutedEventArgs e)
        {
            ((TreeGridExampleViewModel)HierarchicalGrid.DataContext).Clear();
        }

        private void ToggleBrush_Click(object sender, RoutedEventArgs e)
        {
            var oldBrush = Application.Current.Resources["TextBrush"] as SolidColorBrush;
            System.Diagnostics.Debug.WriteLine($"Old brush color: {oldBrush?.Color}");
            
            if (oldBrush?.Color == Colors.Green)
            {
                Application.Current.Resources["TextBrush"] = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                Application.Current.Resources["TextBrush"] = new SolidColorBrush(Colors.Green);
            }
            
            var newBrush = Application.Current.Resources["TextBrush"] as SolidColorBrush;
            System.Diagnostics.Debug.WriteLine($"New brush color: {newBrush?.Color}");
            
            // Force update on the column
            var column = HierarchicalGrid.Columns.OfType<TreeColumn>().FirstOrDefault();
            if (column != null)
            {
                var currentForeground = column.TextForeground as SolidColorBrush;
                System.Diagnostics.Debug.WriteLine($"Column TextForeground after resource change: {currentForeground?.Color}");
                
                // Force the column to re-evaluate its dynamic resource
                column.InvalidateProperty(TreeColumn.TextForegroundProperty);
            }
        }
    }
}