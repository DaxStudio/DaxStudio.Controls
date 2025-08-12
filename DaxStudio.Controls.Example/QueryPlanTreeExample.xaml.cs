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

        private void DrillIn_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Custom Command");
            //((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).TestContextMenuCommand()
            var selectedRow = ((TreeGridRow<object>)TreeGrid.SelectedValue).GetDataAs<QPTreeItem>();
            ((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).RootItems.Clear();
            ((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).RootItems.Add(selectedRow);
        }

        private void DrillOut_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Custom Command");
            //((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).TestContextMenuCommand()
            var selectedRow = ((TreeGridRow<object>)TreeGrid.SelectedValue).GetDataAs<QPTreeItem>();
            ((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).ResetTree();
            //((QueryPlanTreeExampleViewModel)TreeGrid.DataContext).RootItems.Add(selectedRow);
        }
    }
}