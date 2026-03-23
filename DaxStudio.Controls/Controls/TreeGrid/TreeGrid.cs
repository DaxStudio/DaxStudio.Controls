using System.Windows;

namespace DaxStudio.Controls
{
    public class TreeGrid : GenericTreeGrid<object>
    {
        static TreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGrid),
                new FrameworkPropertyMetadata(typeof(TreeGrid)));
        }
    }
}
