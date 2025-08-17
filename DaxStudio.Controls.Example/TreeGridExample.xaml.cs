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
            var model = (TreeGridExampleViewModel)DataContext;
            if (model.RootItems[0].Children[0].Children.Count > 5)
            {
                model.RootItems[0].Children[0].Children.RemoveAt(4);
            }
            else
            {
                model.RootItems[0].Children[0].Children.Add(new TreeItem { Name = "Level 2", Type = "Level", Description = "sub-level" });
            }

        }
    }
}