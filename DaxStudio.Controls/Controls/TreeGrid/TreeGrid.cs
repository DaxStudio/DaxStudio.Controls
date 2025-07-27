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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Specialized;

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
        private readonly List<TreeGridRow<object>> _rootRows = new List<TreeGridRow<object>>();

        private readonly Dictionary<object, TreeGridRow<object>> _itemToRowMap = new Dictionary<object, TreeGridRow<object>>();
        private readonly ObservableCollection<TreeGridRow<object>> _flattenedRows = new ObservableCollection<TreeGridRow<object>>();

        // Add these missing fields for async RefreshData
        private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _refreshCancellation;
        private DispatcherTimer _rebuildDebounceTimer;

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

            // Enable virtualization for better performance with large datasets
            this.EnableRowVirtualization = true;
            this.EnableColumnVirtualization = true;
            
            // Use recycling mode for even better performance - Fixed syntax
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Item);

            Loaded += OnLoaded;
            //SelectionChanged += OnSelectionChanged;

            // ... existing code ...
            _rebuildDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // Adjust as needed
            };
            _rebuildDebounceTimer.Tick += (s, e) =>
            {
                _rebuildDebounceTimer.Stop();
                RebuildHierarchy();
                RefreshData();
            };
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
            System.Diagnostics.Debug.WriteLine("ExpandItemRecursively _isUpdatingFlattenedRows = true;");
            try
            {
                // Expand the specified item and all its descendants
                TreeGrid.ExpandRowRecursively(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            // Refresh the UI to show the expanded items
            RefreshData();
        }

        private static void ExpandRowRecursively(TreeGridRow<object> row)
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
                TreeGrid.ExpandRowRecursively(child);
            }
        }

        private void CollapseItemRecursively(object data)
        {
            if (data == null || !_itemToRowMap.TryGetValue(data, out var row))
                return;

            // Use batch operation for performance
            _isUpdatingFlattenedRows = true;
            System.Diagnostics.Debug.WriteLine("CollapseItemRecursively _isUpdatingFlattenedRows = true;");
            try
            {
                // Collapse the specified item and all its descendants
                TreeGrid.CollapseRowRecursively(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            // Refresh the UI to show the collapsed items
            RefreshData();
        }

        private static void CollapseRowRecursively(TreeGridRow<object> row)
        {
            if (row == null)
                return;

            // Recursively collapse all child rows first
            foreach (var child in row.Children)
            {
                TreeGrid.CollapseRowRecursively(child);
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
                // Prevent recursive calls during updates
                if (_isUpdatingFlattenedRows)
                    return;

                _isUpdatingFlattenedRows = true;
                System.Diagnostics.Debug.WriteLine("OnSelectionChanged _isUpdatingFlattenedRows = true;");
                try
                {
                    // ALWAYS clear previous selections - remove IsCollapsing check
                    foreach (TreeGridRow<object> row in e.RemovedItems)
                    {
                        TreeGrid.ClearSelectedLineRecursive(row);
                    }

                    // Set new selections
                    foreach (TreeGridRow<object> row in e.AddedItems)
                    {
                        // Skip updating selection lines if row is collapsing
                        if (row.IsCollapsing)
                        {
                            Debug.WriteLine($"Skipping selection update for collapsing row at level {row.Level}");
                            // Reset IsCollapsing flag but don't update selection lines
                            row.IsCollapsing = false;
                        }
                        else
                        {
                            // Only update selection lines when NOT collapsing
                            TreeGrid.SetSelectedLineRecursive(row, row.Level, true);
                        }
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                }
            }
        }

        // Add this new method for complete cleanup
        private static void ClearSelectedLineRecursive(TreeGridRow<object> row)
        {            
            // Clear all selection levels for this row's path
            if (row.SelectedLineLevels != null)
            {
                for (int i = 0; i < row.SelectedLineLevels.Count; i++)
                {
                    row.SelectedLineLevels[i] = false;
                }
            }
            // Clear selection for all descendants
            foreach (TreeGridRow<object> child in row.Children)
            {   
                ClearSelectedLineRecursive(child);
            }
        }

        // Improve the existing method
        private static void SetSelectedLineRecursive(TreeGridRow<object> row, int level, bool value)
        {
                      
            if (row.SelectedLineLevels != null)
            {
                // Ensure the collection is large enough
                while (row.SelectedLineLevels.Count <= level)
                {
                    row.SelectedLineLevels.Add(false);
                }
                
                // Only set the specific level, don't clear others unless explicitly needed
                if (level < row.SelectedLineLevels.Count)
                {
                    row.SelectedLineLevels[level] = value;
                }
            }

            // Propagate to children for the same level (ancestor line)
            foreach (TreeGridRow<object> child in row.Children)
            {
                SetSelectedLineRecursive(child, level, value);
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
            DependencyProperty.Register(nameof(RootItems), typeof(INotifyCollectionChanged), typeof(TreeGrid),
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

        // Add this property
        public static readonly DependencyProperty EnableLazyLoadingProperty =
            DependencyProperty.Register(nameof(EnableLazyLoading), typeof(bool), typeof(TreeGrid),
                new PropertyMetadata(false));

        public bool EnableLazyLoading
        {
            get => (bool)GetValue(EnableLazyLoadingProperty);
            set => SetValue(EnableLazyLoadingProperty, value);
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
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                    oldCollection.CollectionChanged -= grid.RootItems_CollectionChanged;

                if (e.NewValue is INotifyCollectionChanged newCollection)
                    newCollection.CollectionChanged += grid.RootItems_CollectionChanged;

                grid.RebuildHierarchy();
                grid.RefreshData();
            }
        }

        private void RootItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Restart the debounce timer
            _rebuildDebounceTimer.Stop();
            _rebuildDebounceTimer.Start();
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
            parent?.AddChild(row); // Use the optimized AddChild method

            // Only build children if not using lazy loading or if expanded
            if (!EnableLazyLoading || row.IsExpanded)
            {
                var children = GetChildren(item);
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        BuildHierarchy(child, level + 1, row);
                    }
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
        private async void RefreshData()
        {
            if (_rootRows?.Count ==0 )
            {
                _flattenedRows.Clear();
                return;
            }

            if (_rootRows == null || _rootRows.Count == 0 || _isUpdatingFlattenedRows)
                return;

            // Cancel any pending refresh
            _refreshCancellation?.Cancel();
            _refreshCancellation = new CancellationTokenSource();
            var cancellationToken = _refreshCancellation.Token;

            if (!await _refreshSemaphore.WaitAsync(0)) // Don't wait, just skip if busy
                return;

            try
            {
                _refreshTimer.Restart();
                _isUpdatingFlattenedRows = true;
                System.Diagnostics.Debug.WriteLine("RefreshData _isUpdatingFlattenedRows = true;");
                // Build the new flattened structure more efficiently
                var newFlattenedRows = new List<TreeGridRow<object>>();
                _visibleRowsSet.Clear();

                // Process in batches for large datasets
                const int batchSize = 1000;
                int processed = 0;

                foreach (var row in _rootRows)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    BuildVisibleRowsListOptimized(row, newFlattenedRows);
                    
                    // Yield control every batch to keep UI responsive
                    if (++processed % batchSize == 0)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                }

                if (cancellationToken.IsCancellationRequested) return;

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

                // Perform batch updates to _flattenedRows on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        UpdateFlattenedRowsCollectionOptimized(newFlattenedRows);
                    }
                }, DispatcherPriority.Normal);

                Debug.WriteLine($"TreeGrid Flattened Rows updated at: {_refreshTimer.ElapsedMilliseconds} ms");
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
                _refreshSemaphore.Release();
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
                var operations = TreeGrid.CalculateMinimalOperations(_flattenedRows, newRows);
                ApplyOperations(operations);
            }
        }

        private struct CollectionOperation
        {
            public enum OperationType { Insert, Remove, Move }
            public OperationType Type;
            public int Index;
            //public int TargetIndex; // For moves
            public TreeGridRow<object> Item; // For inserts
        }

        private static List<CollectionOperation> CalculateMinimalOperations(
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

            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                // Mark row as collapsing if it's being collapsed
                if (row.IsExpanded)
                {
                    row.IsCollapsing = true;
                    //MarkDescendantsAsCollapsing(row);
                }

                row.IsExpanded = !row.IsExpanded;
                RefreshData();

                // Update selection lines more efficiently
                if (row.IsExpanded)
                {
                    SetSelectedLineRecursive(row, row.Level, true);
                }
            }
        }

        // Optimized ExpandAll/CollapseAll with batch operations
        public void ExpandAll()
        {
            using (new OverrideCursor(Cursors.Wait))
            {
                _isUpdatingFlattenedRows = true;
                System.Diagnostics.Debug.WriteLine("ExpandAll _isUpdatingFlattenedRows = true;");
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
                System.Diagnostics.Debug.WriteLine("CollapseAll _isUpdatingFlattenedRows = true;");
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
                TreeGrid.UpdateAncestorsRecursive(rootRow);
            }
        }

        private static void UpdateAncestorsRecursive(TreeGridRow<object> row)
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
                TreeGrid.UpdateAncestorsRecursive(child);
            }
        }

        private IEnumerable GetChildren(object item)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return null;

            var property = item.GetType().GetProperty(ChildrenBindingPath);
            return property?.GetValue(item) as IEnumerable;
        }

        private static void BuildVisibleRowsList(TreeGridRow<object> row, List<TreeGridRow<object>> visibleRows)
        {
            visibleRows.Add(row);

            if (row.IsExpanded && row.HasChildren)
            {
                foreach (var child in row.Children)
                {
                    TreeGrid.BuildVisibleRowsList(child, visibleRows);
                }
            }
        }

        private void Expander_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevents the event from bubbling to the DataGridRow
        }



    }

}