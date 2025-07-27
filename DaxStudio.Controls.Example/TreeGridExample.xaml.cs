using DaxStudio.Controls.Model;
using DaxStudio.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DaxStudio.UI.Views
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
    }
}