using DaxStudio.Controls.Model;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.Controls
{
    /// <summary>
    /// A tree grid that displays data in a tree-like structure with expandable/collapsible nodes
    /// A tree grid that displays data in a tree-like structure with expandable/collapsible nodes
    /// </summary>
    public class TreeGrid : DataGrid
    {
        private const string ExpanderColumnName = "TreeColumn";
        private static Stopwatch stopwatch = new Stopwatch();
        static TreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGrid),
                new FrameworkPropertyMetadata(typeof(TreeGrid)));
        }

        public TreeGrid()
        {
            this.AutoGenerateColumns = false;
            this.CanUserAddRows = false;
            this.CanUserDeleteRows = false;
            this.SelectionMode = DataGridSelectionMode.Single;
            this.SelectionUnit = DataGridSelectionUnit.FullRow;
            this.HeadersVisibility = DataGridHeadersVisibility.Column;
            this.GridLinesVisibility = DataGridGridLinesVisibility.None;
            this.AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));

            Loaded += OnLoaded;
        }

        /// <summary>
        /// The property path for child items in the hierarchy
        /// </summary>
        public static readonly DependencyProperty ChildrenBindingPathProperty =
            DependencyProperty.Register(nameof(ChildrenBindingPath), typeof(string), typeof(TreeGrid),
                new PropertyMetadata(string.Empty, OnChildrenBindingPathChanged));

        public string ChildrenBindingPath
        {
            get => (string)GetValue(ChildrenBindingPathProperty);
            set => SetValue(ChildrenBindingPathProperty, value);
        }

        /// <summary>
        /// Template for the expander column
        /// </summary>
        public static readonly DependencyProperty ExpanderTemplateProperty =
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(DataTemplate), typeof(TreeGrid),
                new PropertyMetadata(null));

        public DataTemplate ExpanderTemplate
        {
            get => (DataTemplate)GetValue(ExpanderTemplateProperty);
            set => SetValue(ExpanderTemplateProperty, value);
        }

        /// <summary>
        /// Indent width for each level of hierarchy
        /// </summary>
        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeGrid),
                new PropertyMetadata(20.0));

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        /// <summary>
        /// The root items collection for the hierarchy
        /// </summary>
        public static readonly DependencyProperty RootItemsProperty =
            DependencyProperty.Register(nameof(RootItems), typeof(IEnumerable), typeof(TreeGrid),
                new PropertyMetadata(null, OnRootItemsChanged));

        public IEnumerable RootItems
        {
            get => (IEnumerable)GetValue(RootItemsProperty);
            set => SetValue(RootItemsProperty, value);
        }

        private readonly Dictionary<object, TreeGridRow<object>> _itemToRowMap = new Dictionary<object, TreeGridRow<object>>();
        //private readonly List<HierarchicalDataGridRow> _flattenedRows = new List<HierarchicalDataGridRow>();
        private readonly ObservableCollection<TreeGridRow<object>> _flattenedRows = new ObservableCollection<TreeGridRow<object>>();

        private static void OnChildrenBindingPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.RefreshData(true);
            }
        }

        private static void OnRootItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.RefreshData(true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Columns.Count == 0)
            {
                CreateDefaultExpanderColumn();
            }
            //RefreshData();
            ItemsSource = _flattenedRows;
        }

        /// <summary>
        /// Expands the specified item
        /// </summary>
        public void ExpandItem(object item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                row.IsExpanded = true;
                RefreshData();
            }
        }

        /// <summary>
        /// Collapses the specified item
        /// </summary>
        public void CollapseItem(object item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                row.IsExpanded = false;
                RefreshData();
            }
        }

        /// <summary>
        /// Toggles the expansion state of the specified item
        /// </summary>
        public void ToggleItem(object item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                row.IsExpanded = !row.IsExpanded;
                RefreshData();
            }
        }

        /// <summary>
        /// Expands all items in the hierarchy
        /// </summary>
        public void ExpandAll()
        {
            foreach (var row in _itemToRowMap.Values)
            {
                row.IsExpanded = true;
            }
            RefreshData();
        }

        /// <summary>
        /// Collapses all items in the hierarchy
        /// </summary>
        public void CollapseAll()
        {
            foreach (var row in _itemToRowMap.Values)
            {
                row.IsExpanded = false;
            }
            RefreshData();
        }

        private void CreateDefaultExpanderColumn()
        {
            var expanderColumn = new DataGridTemplateColumn
            {
                Header = "",
                Width = new DataGridLength(200),
                CellTemplate = CreateDefaultExpanderTemplate()
            };

            Columns.Insert(0, expanderColumn);
        }

        private DataTemplate CreateDefaultExpanderTemplate()
        {
            if (ExpanderTemplate != null)
                return ExpanderTemplate;

            var template = new DataTemplate();

            // Create StackPanel for horizontal layout
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Create margin binding for indentation
            //var marginBinding = new Binding("Level")
            //{
            //    Converter = new Converters.IndentConverter(),
            //    ConverterParameter = IndentWidth
            //};
            //stackPanelFactory.SetBinding(StackPanel.MarginProperty, marginBinding);

            // Create Expander
            var expanderFactory = new FrameworkElementFactory(typeof(ToggleButton));
            expanderFactory.SetValue(ToggleButton.WidthProperty, 16.0);
            expanderFactory.SetValue(ToggleButton.HeightProperty, 16.0);
            expanderFactory.SetValue(ToggleButton.StyleProperty, FindResource("ExpanderToggleStyle"));
            expanderFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsExpanded"));
            expanderFactory.SetBinding(ToggleButton.VisibilityProperty, new Binding("HasChildren")
            {
                Converter = new BooleanToVisibilityConverter()
            });

            expanderFactory.AddHandler(ToggleButton.ClickEvent, new RoutedEventHandler(OnExpanderClick));

            // Create Content TextBlock
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.MarginProperty, new Thickness(4, 0, 0, 0));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("Data.Name"));

            stackPanelFactory.AppendChild(expanderFactory);
            stackPanelFactory.AppendChild(textBlockFactory);

            template.VisualTree = stackPanelFactory;

            return template;
        }

        private void OnExpanderClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is TreeGridRow<object> row)
            {
                row.IsExpanded = toggleButton.IsChecked ?? false;
                RefreshData();
            }
        }

        private void RefreshData(bool refreshItemMap = false)
        {
            stopwatch.Restart();
            if (RootItems == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return;

            if (refreshItemMap) _itemToRowMap.Clear();

            // Build hierarchy (this part remains the same as we need the complete structure)  
            var rootRows = new List<TreeGridRow<object>>();
            foreach (var rootItem in RootItems)
            {
                BuildHierarchy(rootItem, 0, null, refreshItemMap);
                if (_itemToRowMap.TryGetValue(rootItem, out var rootRow))
                {
                    rootRows.Add(rootRow);
                }
            }
            System.Diagnostics.Debug.WriteLine($"Hierarchy built : {stopwatch.ElapsedMilliseconds}ms");
            // After building the complete hierarchy, update the ancestors for all rows
            UpdateAncestorsForAllRows(rootRows);
            System.Diagnostics.Debug.WriteLine($"Ancestors updated : {stopwatch.ElapsedMilliseconds}ms");
            // Build the new flattened structure  
            var newFlattenedRows = new List<TreeGridRow<object>>();
            foreach (var row in rootRows)
            {
                BuildVisibleRowsList(row, newFlattenedRows);
            }
            System.Diagnostics.Debug.WriteLine($"Visible rows built : {stopwatch.ElapsedMilliseconds}ms");
            // Perform incremental updates to _flattenedRows  
            UpdateFlattenedRowsCollection(newFlattenedRows);
            System.Diagnostics.Debug.WriteLine($"Visible rows updated : {stopwatch.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine("====");
            stopwatch.Stop();
        }

        private void BuildHierarchy(object item, int level, HierarchicalDataGridRow parent, bool rebuildItemMap)
        {
            var row = new HierarchicalDataGridRow ()
            {
                Data = item,
                Level = level,
                Parent = parent
            };

            if (rebuildItemMap) _itemToRowMap[item] = row;

            if (parent != null)
            {
                parent.Children.Add(row);
            }

            // Get children using reflection or property path
            var children = GetChildren(item);

            if (children != null)
            {
                foreach (var child in children)
                {
                    BuildHierarchy(child, level + 1, row, rebuildItemMap);
                }
            }
        }

        private void UpdateAncestorsForAllRows(List<TreeGridRow<object>> rootRows)
        {
            foreach (var rootRow in rootRows)
            {
                UpdateAncestorsRecursive(rootRow);
            }
        }

        private void UpdateAncestorsRecursive(TreeGridRow<object> row)
        {
            // Update ancestors for this row based on its position among siblings
            if (row.Parent != null)
            {
                var siblings = row.Parent.Children;
                var isLastChild = siblings.LastOrDefault() == row;

                // Copy parent's ancestors and add this row's position
                row.Ancestors = new List<bool>(row.Parent.Ancestors);
                row.Ancestors.Add(isLastChild);
            }
            else
            {
                // Root level - no ancestors
                row.Ancestors = new List<bool>();
            }

            // Recursively update children
            foreach (var child in row.Children)
            {
                UpdateAncestorsRecursive(child);
            }
        }

        private IEnumerable GetChildren(object item)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return null;

            var property = item.GetType().GetProperty(ChildrenBindingPath);
            return property?.GetValue(item) as IEnumerable;
        }

        private void BuildVisibleRowsList(TreeGridRow<object> row, List<TreeGridRow<object>> visibleRows)
        {
            visibleRows.Add(row);

            if (row.IsExpanded && row.HasChildren)
            {
                foreach (var child in row.Children)
                {
                    BuildVisibleRowsList(child, visibleRows);
                }
            }
        }

        

void UpdateFlattenedRowsCollection(List<TreeGridRow<object>> newRows)
        {
            // Convert current collection to list for easier manipulation
            var currentRows = _flattenedRows.ToList();

            // Find rows to remove (exist in current but not in new)
            for (int i = currentRows.Count - 1; i >= 0; i--)
            {
                if (!newRows.Contains(currentRows[i]))
                {
                    _flattenedRows.RemoveAt(i);
                }
            }

            // Find rows to add and their correct positions
            for (int newIndex = 0; newIndex < newRows.Count; newIndex++)
            {
                var newRow = newRows[newIndex];
                var currentIndex = _flattenedRows.IndexOf(newRow);

                if (currentIndex == -1)
                {
                    // Row doesn't exist, insert it at the correct position
                    _flattenedRows.Insert(newIndex, newRow);
                }
                else if (currentIndex != newIndex)
                {
                    // Row exists but in wrong position, move it
                    _flattenedRows.Move(currentIndex, newIndex);
                }
            }
        }
    }

}