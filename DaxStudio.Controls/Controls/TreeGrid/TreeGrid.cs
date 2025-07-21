using DaxStudio.Controls.Model;
using DaxStudio.Controls.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DaxStudio.Controls
{
    public class TreeGrid : DataGrid
    {
        // Add these dependency properties for context menu configuration
        public static readonly DependencyProperty ShowDefaultContextMenuProperty =
            DependencyProperty.Register(nameof(ShowDefaultContextMenu), typeof(bool), typeof(TreeGrid),
                new PropertyMetadata(true, OnShowDefaultContextMenuChanged));

        public bool ShowDefaultContextMenu
        {
            get => (bool)GetValue(ShowDefaultContextMenuProperty);
            set => SetValue(ShowDefaultContextMenuProperty, value);
        }

        // Cache for the root rows of the hierarchy
        private List<TreeGridRow<object>> _rootRows = new List<TreeGridRow<object>>();

        private readonly Dictionary<object, TreeGridRow<object>> _itemToRowMap = new Dictionary<object, TreeGridRow<object>>();
        private readonly ObservableCollection<TreeGridRow<object>> _flattenedRows = new ObservableCollection<TreeGridRow<object>>();

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
            //SelectionChanged += OnSelectionChanged;
        }

        private static void OnShowDefaultContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.SetupDefaultContextMenu();
            }
        }

        private void SetupDefaultContextMenu()
        {
            if (ShowDefaultContextMenu && ContextMenu == null)
            {
                var contextMenu = new ContextMenu();

                // Expand All
                var expandAllItem = new MenuItem { Header = "Expand All" };
                expandAllItem.Click += (s, e) => ExpandAll();
                contextMenu.Items.Add(expandAllItem);

                // Collapse All
                var collapseAllItem = new MenuItem { Header = "Collapse All" };
                collapseAllItem.Click += (s, e) => CollapseAll();
                contextMenu.Items.Add(collapseAllItem);

                // Separator
                contextMenu.Items.Add(new Separator());

                // Expand Selected
                var expandSelectedItem = new MenuItem { Header = "Expand Selected"};
                expandSelectedItem.Click += (s, e) => {
                    if (SelectedItem is TreeGridRow<object> row && row.HasChildren && !row.IsExpanded)
                    {
                        ExpandItemRecursively(row.Data);
                    }
                };
                contextMenu.Items.Add(expandSelectedItem);

                // Collapse Selected (uncommented and fixed)
                var collapseSelectedItem = new MenuItem { Header = "Collapse Selected" };
                collapseSelectedItem.Click += (s, e) => {
                    if (SelectedItem is TreeGridRow<object> row && row.HasChildren && row.IsExpanded)
                    {
                        CollapseItemRecursively(row.Data);
                    }
                };
                contextMenu.Items.Add(collapseSelectedItem);

                // Update menu states when opened
                contextMenu.Opened += (s, e) => {
                    var selectedRow = SelectedItem as TreeGridRow<object>;
                    expandSelectedItem.IsEnabled = selectedRow?.HasChildren == true && !selectedRow.IsExpanded;
                    collapseSelectedItem.IsEnabled = selectedRow?.HasChildren == true && selectedRow.IsExpanded;
                    
                    expandAllItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && !r.IsExpanded);
                    collapseAllItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && r.IsExpanded);
                };

                ContextMenu = contextMenu;
            }
            else if (!ShowDefaultContextMenu && ContextMenu != null)
            {
                // Only remove if it's our default menu (simple check)
                if (ContextMenu.Items.Count >= 2 && 
                    ContextMenu.Items[0] is MenuItem firstItem && 
                    firstItem.Header.ToString() == "Expand All")
                {
                    ContextMenu = null;
                }
            }
        }

        private void ExpandItemRecursively(object data)
        {
            if (data == null || !_itemToRowMap.TryGetValue(data, out var row))
                return;

            // Use batch operation for performance
            _isUpdatingFlattenedRows = true;
            try
            {
                // Expand the specified item and all its descendants
                ExpandRowRecursively(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            // Refresh the UI to show the expanded items
            RefreshData();
        }

        private void ExpandRowRecursively(TreeGridRow<object> row)
        {
            if (row == null)
                return;

            // Expand this row if it has children
            if (row.HasChildren)
            {
                row.IsExpanded = true;
            }

            // Recursively expand all child rows
            foreach (var child in row.Children)
            {
                ExpandRowRecursively(child);
            }
        }

        private void CollapseItemRecursively(object data)
        {
            if (data == null || !_itemToRowMap.TryGetValue(data, out var row))
                return;

            // Use batch operation for performance
            _isUpdatingFlattenedRows = true;
            try
            {
                // Collapse the specified item and all its descendants
                CollapseRowRecursively(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            // Refresh the UI to show the collapsed items
            RefreshData();
        }

        private void CollapseRowRecursively(TreeGridRow<object> row)
        {
            if (row == null)
                return;

            // Recursively collapse all child rows first
            foreach (var child in row.Children)
            {
                CollapseRowRecursively(child);
            }

            // Then collapse this row if it has children
            if (row.HasChildren)
            {
                row.IsExpanded = false;
            }
        }

        private readonly object _selectionChangeLock = new object();

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ensure thread safety with lock
            lock (_selectionChangeLock)
            {
                // Batch process selection changes
                _isUpdatingFlattenedRows = true;
                try
                {
                    // Process removed items first to avoid conflicts
                    foreach (TreeGridRow<object> row in e.RemovedItems)
                    {
                        if (!row.IsCollapsing)
                        {
                            SetSelectedLineLevelRecursive(row, row.Level, false);
                        }
                    }

                    // Then process added items
                    foreach (TreeGridRow<object> row in e.AddedItems)
                    {
                        if (row.IsCollapsing)
                        {
                            row.IsCollapsing = false;
                            continue;
                        }
                        SetSelectedLineLevelRecursive(row, row.Level, true);
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                }
            }
        }

        private void SetSelectedLineLevelRecursive(TreeGridRow<object> row, int level, bool value)
        {
            if (row.SelectedLineLevels != null && level < row.SelectedLineLevels.Count)
            {
                row.SelectedLineLevels[level] = value;
            }

            foreach (TreeGridRow<object> child in row.Children)
            {
                SetSelectedLineLevelRecursive(child, level, value);
            }
        }

        public static readonly DependencyProperty ChildrenBindingPathProperty =
            DependencyProperty.Register(nameof(ChildrenBindingPath), typeof(string), typeof(TreeGrid),
                new PropertyMetadata(string.Empty, OnChildrenBindingPathChanged));

        public string ChildrenBindingPath
        {
            get => (string)GetValue(ChildrenBindingPathProperty);
            set => SetValue(ChildrenBindingPathProperty, value);
        }

        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeGrid),
                new PropertyMetadata(20.0));

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        public static readonly DependencyProperty RootItemsProperty =
            DependencyProperty.Register(nameof(RootItems), typeof(IEnumerable), typeof(TreeGrid),
                new PropertyMetadata(null, OnRootItemsChanged));

        public IEnumerable RootItems
        {
            get => (IEnumerable)GetValue(RootItemsProperty);
            set => SetValue(RootItemsProperty, value);
        }

        public static readonly DependencyProperty ExpandOnLoadProperty =
            DependencyProperty.Register(nameof(ExpandOnLoad), typeof(bool), typeof(TreeGrid),
                new PropertyMetadata(false, OnExpandOnLoadChanged));

        public bool ExpandOnLoad
        {
            get => (bool)GetValue(ExpandOnLoadProperty);
            set => SetValue(ExpandOnLoadProperty, value);
        }

        private static void OnChildrenBindingPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.RebuildHierarchy();
                grid.RefreshData();
            }
        }

        private static void OnRootItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.RebuildHierarchy();
                grid.RefreshData();
            }
        }

        private static void OnExpandOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid && (bool)e.NewValue)
            {
                // If setting to true after hierarchy is already built, expand all nodes
                if (grid._rootRows.Count > 0)
                {
                    grid.ExpandAll();
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Columns.Count == 0)
            {
                CreateDefaultExpanderColumn();
            }
            else
            {
                foreach (var column in Columns)
                {
                    if (!(column is TreeGridTreeColumn treeColumn))
                        continue;
                    
                    if (!(treeColumn.SelectedLineStroke is SolidColorBrush selectedBrush) || !treeColumn.ShowTreeLines)
                        continue;
                    
                    if (selectedBrush.Color == Colors.Transparent)
                        continue;
                    
                    // If the column is a TreeGridTreeColumn and the SelectedLineStroke property is set, set up selection change handling
                    SelectionChanged += this.OnSelectionChanged;
                }
            }
                ItemsSource = _flattenedRows;
                
                // Setup default context menu after everything is loaded
                SetupDefaultContextMenu();
        }

        // Rebuilds the hierarchy and caches it in _rootRows
        private void RebuildHierarchy()
        {
            _itemToRowMap.Clear();
            _rootRows.Clear();

            if (RootItems == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return;

            foreach (var rootItem in RootItems)
            {
                var rootRow = BuildHierarchy(rootItem, 0, null);
                if (rootRow != null)
                    _rootRows.Add(rootRow);
            }

            UpdateAncestorsForAllRows(_rootRows);
        }

        // Builds the hierarchy and returns the root row for the given item
        private TreeGridRow<object> BuildHierarchy(object item, int level, TreeGridRow<object> parent)
        {
            if (item == null) return null;

            var row = new TreeGridRow<object>
            {
                Data = item,
                Level = level,
                Parent = parent,
                Children = new List<TreeGridRow<object>>(),
                IsExpanded = ExpandOnLoad // Set initial expansion state based on property
            };

            _itemToRowMap[item] = row;

            if (parent != null)
            {
                parent.Children.Add(row);
            }

            var children = GetChildren(item);
            if (children != null)
            {
                foreach (var child in children)
                {
                    BuildHierarchy(child, level + 1, row);
                }
            }

            return row;
        }

        // Add these performance-related properties
        private bool _isUpdatingFlattenedRows = false;
        private readonly HashSet<TreeGridRow<object>> _visibleRowsSet = new HashSet<TreeGridRow<object>>();
        private readonly Stopwatch _refreshTimer = new Stopwatch();
        // Add this field for better performance tracking
        private int _lastRefreshRowCount = 0;

        // Optimized RefreshData method
        private void RefreshData()
        {
            if (_rootRows == null || _rootRows.Count == 0 || _isUpdatingFlattenedRows)
                return;

            _refreshTimer.Restart();
            _isUpdatingFlattenedRows = true;
            try
            {
                // Build the new flattened structure more efficiently
                var newFlattenedRows = new List<TreeGridRow<object>>();
                _visibleRowsSet.Clear();

                foreach (var row in _rootRows)
                {
                    BuildVisibleRowsListOptimized(row, newFlattenedRows);
                }

                // Early exit if no changes
                if (newFlattenedRows.Count == _lastRefreshRowCount && 
                    _flattenedRows.Count == _lastRefreshRowCount &&
                    newFlattenedRows.SequenceEqual(_flattenedRows))
                {
                    Debug.WriteLine("TreeGrid RefreshData: No changes detected, skipping update");
                    return;
                }

                _lastRefreshRowCount = newFlattenedRows.Count;
                Debug.WriteLine($"TreeGrid Visible Rows built: {newFlattenedRows.Count} rows at {_refreshTimer.ElapsedMilliseconds} ms");

                // Perform batch updates to _flattenedRows
                UpdateFlattenedRowsCollectionOptimized(newFlattenedRows);
                Debug.WriteLine($"TreeGrid Flattened Rows updated at: {_refreshTimer.ElapsedMilliseconds} ms");
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
                Debug.WriteLine($"TreeGrid RefreshData completed in: {_refreshTimer.ElapsedMilliseconds} ms");
                Debug.WriteLine("====");
            }
        }

        // Optimized visible rows building
        private void BuildVisibleRowsListOptimized(TreeGridRow<object> row, List<TreeGridRow<object>> visibleRows)
        {
            visibleRows.Add(row);
            _visibleRowsSet.Add(row);

            if (row.IsExpanded && row.HasChildren)
            {
                foreach (var child in row.Children)
                {
                    BuildVisibleRowsListOptimized(child, visibleRows);
                }
            }
        }

        // Optimized collection update with batch operations
        // Alternative high-performance implementation
        private void UpdateFlattenedRowsCollectionOptimized(List<TreeGridRow<object>> newRows)
        {
            using (var deferRefresh = new DeferRefresh(_flattenedRows))
            {
                // Quick check for complete replacement scenario
                if (_flattenedRows.Count == 0)
                {
                    // Simple add all
                    foreach (var row in newRows)
                    {
                        _flattenedRows.Add(row);
                    }
                    return;
                }

                if (newRows.Count == 0)
                {
                    // Simple clear all
                    _flattenedRows.Clear();
                    return;
                }

                // Use a more efficient diff algorithm
                var operations = CalculateMinimalOperations(_flattenedRows, newRows);
                ApplyOperations(operations);
            }
        }

        private struct CollectionOperation
        {
            public enum OperationType { Insert, Remove, Move }
            public OperationType Type;
            public int Index;
            public int TargetIndex; // For moves
            public TreeGridRow<object> Item; // For inserts
        }

        private List<CollectionOperation> CalculateMinimalOperations(
            ObservableCollection<TreeGridRow<object>> current, 
            List<TreeGridRow<object>> target)
        {
            var operations = new List<CollectionOperation>();
            
            // Create lookup for fast existence checks
            var targetSet = new HashSet<TreeGridRow<object>>(target);
            var currentList = current.ToList();
            
            // Find items to remove
            for (int i = currentList.Count - 1; i >= 0; i--)
            {
                if (!targetSet.Contains(currentList[i]))
                {
                    operations.Add(new CollectionOperation 
                    { 
                        Type = CollectionOperation.OperationType.Remove, 
                        Index = i 
                    });
                }
            }
            
            // Find items to add and their positions
            var currentSet = new HashSet<TreeGridRow<object>>(currentList);
            for (int i = 0; i < target.Count; i++)
            {
                if (!currentSet.Contains(target[i]))
                {
                    operations.Add(new CollectionOperation 
                    { 
                        Type = CollectionOperation.OperationType.Insert, 
                        Index = i, 
                        Item = target[i] 
                    });
                }
            }
            
            return operations;
        }

        private void ApplyOperations(List<CollectionOperation> operations)
        {
            // Apply removals first (in reverse order)
            var removals = operations.Where(op => op.Type == CollectionOperation.OperationType.Remove)
                            .OrderByDescending(op => op.Index);
            foreach (var removal in removals)
            {
                _flattenedRows.RemoveAt(removal.Index);
            }
            
            // Apply insertions
            var insertions = operations.Where(op => op.Type == CollectionOperation.OperationType.Insert)
                              .OrderBy(op => op.Index);
            foreach (var insertion in insertions)
            {
                int safeIndex = Math.Min(insertion.Index, _flattenedRows.Count);
                _flattenedRows.Insert(safeIndex, insertion.Item);
            }
        }

        // Optimized toggle with minimal refresh
        public void ToggleItem(object item)
        {
            //using (new OverrideCursor(Cursors.Wait))
            //{
                if (_itemToRowMap.TryGetValue(item, out var row))
                {
                    // Mark row as collapsing if it's being collapsed
                    if (row.IsExpanded)
                    {
                        row.IsCollapsing = true;
                        MarkDescendantsAsCollapsing(row);
                    }

                    row.IsExpanded = !row.IsExpanded;
                    RefreshData();

                    // Update selection lines more efficiently
                    if (row.IsExpanded)
                    {
                        SetSelectedLineLevelRecursive(row, row.Level, true);
                    }
                }
            //}
        }

        // Helper method to mark descendants as collapsing
        private void MarkDescendantsAsCollapsing(TreeGridRow<object> row)
        {
            foreach (var child in row.Children)
            {
                child.IsCollapsing = true;
                MarkDescendantsAsCollapsing(child);
            }
        }

        // Optimized ExpandAll/CollapseAll with batch operations
        public void ExpandAll()
        {
            using (new OverrideCursor(Cursors.Wait))
            {
                _isUpdatingFlattenedRows = true;
                try
                {
                    foreach (var row in _itemToRowMap.Values)
                    {
                        row.IsExpanded = true;
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                }
                RefreshData();
            }
        }

        public void CollapseAll()
        {
            using (new OverrideCursor(Cursors.Wait))
            {
                _isUpdatingFlattenedRows = true;
                try
                {
                    foreach (var row in _itemToRowMap.Values)
                    {
                        row.IsExpanded = false;
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                }
                RefreshData();
            }
        }

        private void CreateDefaultExpanderColumn()
        {
            var expanderColumn = new DataGridTemplateColumn
            {
                Header = "",
                Width = new DataGridLength(200),
                CellTemplate = CreateDefaultCellTemplate()
            };

            Columns.Insert(0, expanderColumn);
        }

        private DataTemplate CreateDefaultCellTemplate()
        {
            //if (ExpanderTemplate != null)
            //    return ExpanderTemplate;

            var template = new DataTemplate();

            // Create StackPanel for horizontal layout
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Create Expander
            var expanderFactory = new FrameworkElementFactory(typeof(ToggleButton));
            expanderFactory.SetValue(ToggleButton.WidthProperty, 16.0);
            expanderFactory.SetValue(ToggleButton.HeightProperty, 16.0);

            // Use a style without event setter
            expanderFactory.SetValue(ToggleButton.StyleProperty, FindResource("ExpanderControlTemplate"));
            expanderFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsExpanded"));
            expanderFactory.SetBinding(ToggleButton.VisibilityProperty, new Binding("HasChildren")
            {
                Converter = new BooleanToVisibilityConverter()
            });

            // Add event handler programmatically with name
            expanderFactory.AddHandler(
                UIElement.PreviewMouseDownEvent,
                new MouseButtonEventHandler(Expander_PreviewMouseDown));

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

        private void UpdateAncestorsForAllRows(List<TreeGridRow<object>> rootRows)
        {
            foreach (var rootRow in rootRows)
            {
                UpdateAncestorsRecursive(rootRow);
            }
        }

        private void UpdateAncestorsRecursive(TreeGridRow<object> row)
        {
            if (row.Parent != null)
            {
                var siblings = row.Parent.Children;
                var isLastChild = siblings.LastOrDefault() == row;

                row.Ancestors = new List<bool>(row.Parent.Ancestors);
                row.Ancestors.Add(isLastChild);
                row.SelectedLineLevels = new ObservableCollection<bool>(row.Parent.SelectedLineLevels);
                row.SelectedLineLevels.Add(false);
            }
            else
            {
                row.Ancestors = new List<bool>();
                row.SelectedLineLevels = new ObservableCollection<bool>();
            }

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

        private void Expander_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevents the event from bubbling to the DataGridRow
        }


    }

}