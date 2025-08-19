using DaxStudio.Controls;
using DaxStudio.Controls.Example;
using System.Windows;

namespace DaxStudio.TreeGrid.Example
{
    public class QueryPlanTreeGrid: GenericTreeGrid<QPTreeItem>
    {
        static QueryPlanTreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QueryPlanTreeGrid),
                new FrameworkPropertyMetadata(typeof(QueryPlanTreeGrid)));
        }
        
    }
}
