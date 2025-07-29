using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.TreeGrid.Example
{
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
        }

        private void TreeGridExampleButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new window to host the TreeGridExample UserControl
            var window = new Window
            {
                Title = "TreeGrid Example",
                Content = new DaxStudio.Controls.Example.TreeGridExample(),
                //Width = 800,
                //Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            
            window.Show();
            
            // Close the startup window
            this.Close();
        }

        private void QueryPlanTreeExampleButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new window to host the QueryPlanTreeExample UserControl
            var window = new Window
            {
                Title = "Query Plan Tree Example",
                Content = new DaxStudio.Controls.Example.QueryPlanTreeExample(),
                //Width = 800,
                //Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            
            window.Show();
            
            // Close the startup window
            this.Close();
        }
    }
}