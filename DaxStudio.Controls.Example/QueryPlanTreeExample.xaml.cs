using DaxStudio.Controls.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DaxStudio.Controls.Example
{
    public partial class QueryPlanTreeExample : UserControl
    {
        public QueryPlanTreeExample()
        {
            InitializeComponent();
            // Set the DataContext to the ViewModel
            DataContext = new QueryPlanTreeExampleViewModel();
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            TreeGrid.ExpandAll();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            TreeGrid.CollapseAll();
        }


        private void TreeGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            
        }
    }
}