using DaxStudio.TreeGrid;
using DaxStudio.UI.Controls;
using DaxStudio.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DaxStudio.UI.Views
{
    public partial class HierarchicalDataGridExample : UserControl
    {
        public HierarchicalDataGridExample()
        {
            InitializeComponent();
            // Set the DataContext to the ViewModel
            DataContext = new HierarchicalDataGridExampleViewModel();
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
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is HierarchicalDataGridRow row)
            {
                HierarchicalGrid.ToggleItem(row.Data);
            }
        }
    }
}