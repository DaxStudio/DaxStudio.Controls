using DaxStudio.Controls.Model;
using DaxStudio.Controls.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using DaxStudio.Controls.Services;

namespace DaxStudio.Controls
{


    public class GenericTreeGrid<T> : DataGrid where T : class
    {

        // Cache for the root rows of the hierarchy
        private readonly List<TreeGridRow<T>> _rootRows = new List<TreeGridRow<T>>();
        private readonly Dictionary<T, TreeGridRow<T>> _itemToRowMap = new Dictionary<T, TreeGridRow<T>>();
        private readonly ObservableCollection<TreeGridRow<T>> _flattenedRows = new ObservableCollection<TreeGridRow<T>>();

        // Fields for refresh management
        private bool _isUpdatingFlattenedRows = false;
        private readonly HashSet<TreeGridRow<T>> _visibleRowsSet = new HashSet<TreeGridRow<T>>();
        private readonly Stopwatch _refreshStopwatch = new Stopwatch();
        private readonly object _selectionChangeLock = new object();
        
        // Fields for thread-safe refresh management
        private readonly object _refreshLock = new object();
        private volatile bool _refreshPending = false;
        private DispatcherTimer _refreshTimer;
        private const int REFRESH_DEBOUNCE_MS = 20; // Debounce delay
        
        public ICommand ExecuteCustomDescendantFilter { get; private set; }
        // Add these fields to track bound collections
        private INotifyCollectionChanged _rootItemsCollectionNotifier;
        private readonly Dictionary<T, INotifyCollectionChanged> _childCollectionNotifiers = new Dictionary<T, INotifyCollectionChanged>();

        static GenericTreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GenericTreeGrid<T>),
                new FrameworkPropertyMetadata(typeof(GenericTreeGrid<T>))
            );
        }

        public GenericTreeGrid()
        {
            this.AutoGenerateColumns = false;
            this.CanUserAddRows = false;
            this.CanUserDeleteRows = false;
            this.SelectionMode = DataGridSelectionMode.Single;
            this.SelectionUnit = DataGridSelectionUnit.FullRow;
            this.HeadersVisibility = DataGridHeadersVisibility.Column;
            this.GridLinesVisibility = DataGridGridLinesVisibility.None;
            this.IsReadOnly = true;
            // Use recycling mode for even better performance
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Item);
            VirtualizingPanel.SetIsVirtualizing(this, true);
            VirtualizingPanel.SetCacheLengthUnit(this, VirtualizationCacheLengthUnit.Page);
            VirtualizingPanel.SetCacheLength(this, new VirtualizationCacheLength(1, 1));
            EnableRowVirtualization = true;

            // Optimize layout performance
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            ExecuteCustomDescendantFilter = new RelayCommand(ExecuteCustomDescendantsFilterAction);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public IDebounceService DebounceService { get; set; } = new DebounceService();

        public static readonly DependencyProperty ChildrenBindingPathProperty =
            DependencyProperty.Register(nameof(ChildrenBindingPath), typeof(string), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(string.Empty, OnChildrenBindingPathChanged));

        public string ChildrenBindingPath
        {
            get => (string)GetValue(ChildrenBindingPathProperty);
            set => SetValue(ChildrenBindingPathProperty, value);
        }

        public static readonly DependencyProperty RootItemsProperty =
            DependencyProperty.Register(nameof(RootItems), typeof(IEnumerable<T>), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(null, OnRootItemsChanged));

        public IEnumerable<T> RootItems
        {
            get => (IEnumerable<T>)GetValue(RootItemsProperty);
            set => SetValue(RootItemsProperty, value);
        }

        public static readonly DependencyProperty ExpandOnLoadProperty =
            DependencyProperty.Register(nameof(ExpandOnLoad), typeof(bool), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(false, OnExpandOnLoadChanged));

        public bool ExpandOnLoad
        {
            get => (bool)GetValue(ExpandOnLoadProperty);
            set => SetValue(ExpandOnLoadProperty, value);
        }

        public static readonly DependencyProperty AddCustomMenusAtBottomProperty =
            DependencyProperty.Register(nameof(AddCustomMenusAtBottom), typeof(bool), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(true));

        public bool AddCustomMenusAtBottom
        {
            get => (bool)GetValue(AddCustomMenusAtBottomProperty);
            set => SetValue(AddCustomMenusAtBottomProperty, value);
        }

        // Add these dependency properties for context menu configuration
        public static readonly DependencyProperty ShowDefaultContextMenuProperty =
            DependencyProperty.Register(nameof(ShowDefaultContextMenu), typeof(bool), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(true, OnShowDefaultContextMenuChanged));

        public bool ShowDefaultContextMenu
        {
            get => (bool)GetValue(ShowDefaultContextMenuProperty);
            set => SetValue(ShowDefaultContextMenuProperty, value);
        }

        private static void OnShowDefaultContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GenericTreeGrid<T> grid)
            {
                grid.SetupDefaultContextMenu();
            }
        }

        private static void OnChildrenBindingPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GenericTreeGrid<T> grid)
            {
                grid.RebuildHierarchy();
                grid.RefreshData();
            }
        }

        private static void OnExpandOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GenericTreeGrid<T> grid && (bool)e.NewValue)
            {
                if (grid._rootRows.Count > 0)
                {
                    grid.ExpandAll();
                }
            }
        }

        // Modify the OnRootItemsChanged method to handle collection changes
        private static void OnRootItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GenericTreeGrid<T> grid)
            {
                // Unsubscribe from old collection's change notifications
                if (e.OldValue is INotifyCollectionChanged oldNotifier && grid._rootItemsCollectionNotifier != null)
                {
                    oldNotifier.CollectionChanged -= grid.OnRootItemsCollectionChanged;
                    grid._rootItemsCollectionNotifier = null;
                }

                // Subscribe to new collection's change notifications
                if (e.NewValue is INotifyCollectionChanged newNotifier)
                {
                    grid._rootItemsCollectionNotifier = newNotifier;
                    newNotifier.CollectionChanged += grid.OnRootItemsCollectionChanged;
                }

                // Rebuild hierarchy and refresh
                grid.RebuildHierarchy();
                grid.RefreshData();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"OnLoaded: Entry - Columns.Count={Columns.Count}");
            
            if (Columns.Count == 0)
            {
                Debug.WriteLine($"OnLoaded: Creating default expander column");
                CreateDefaultExpanderColumn();
            }
            else
            {
                bool handlerAttached = false;
                foreach (var column in Columns)
                {
                    Debug.WriteLine($"OnLoaded: Checking column of type {column.GetType().Name}");
                    
                    if (column is TreeColumn treeColumn)
                    {
                        Debug.WriteLine($"OnLoaded: Found TreeColumn");
                        Debug.WriteLine($"OnLoaded: SelectedLineStroke={treeColumn.SelectedLineStroke}");
                        Debug.WriteLine($"OnLoaded: ShowTreeLines={treeColumn.ShowTreeLines}");
                        
                        if (treeColumn.SelectedLineStroke is SolidColorBrush selectedBrush)
                        {
                            Debug.WriteLine($"OnLoaded: SelectedLineStroke color={selectedBrush.Color}");
                            
                            if (treeColumn.ShowTreeLines) // && selectedBrush.Color != Colors.Transparent)
                            {
                                Debug.WriteLine($"OnLoaded: Attaching OnSelectionChanged handler");
                                SelectionChanged += this.OnSelectionChanged;
                                handlerAttached = true;
                                break; // Only attach once
                            }
                        }
                    }
                }
                Debug.WriteLine($"OnLoaded: Selection handler attached: {handlerAttached}");
            }
            
            Debug.WriteLine($"OnLoaded: Setting ItemsSource");
            ItemsSource = _flattenedRows;
            SetupDefaultContextMenu();
            Debug.WriteLine($"OnLoaded: Complete");
        }

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

        private TreeGridRow<T> BuildHierarchy(T item, int level, TreeGridRow<T> parent)
        {
            if (item == null) return null;

            // **CIRCULAR REFERENCE PROTECTION**
            // Check if this item has already been processed to prevent infinite recursion
            if (_itemToRowMap.ContainsKey(item))
            {
                // Return the existing row instead of creating a new one
                return _itemToRowMap[item];
            }

            var row = new TreeGridRow<T>
            {
                Data = item,
                Level = level,
                Parent = parent,
                Children = new List<TreeGridRow<T>>(),
                IsExpanded = ExpandOnLoad,
                OnRowIsExpandedChanged = OnRowIsExpandedChanged

            };

            _itemToRowMap[item] = row;
            parent?.AddChild(row);

            // Subscribe to child collection changes if available
            if (!IsReadOnly)
            {
                SubscribeToChildCollectionChanges(item, row);
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

        // Add this method to handle child collection changes
        private void SubscribeToChildCollectionChanges(T item, TreeGridRow<T> row)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return;

            try
            {
                var property = item.GetType().GetProperty(ChildrenBindingPath);

                if (property?.GetValue(item) is INotifyCollectionChanged childCollection && !_childCollectionNotifiers.ContainsKey(item))
                {
                    //childCollection.CollectionChanged += OnChildCollectionChanged;
                    _childCollectionNotifiers[item] = childCollection;

                    // Store the parent item as a tag on the event subscription
                    // so we know which item to update when children change
                    childCollection.CollectionChanged += (s, e) => OnChildCollectionChanged(item, e);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error subscribing to child collection changes: {ex.Message}");

            }
        }

        private IEnumerable<T> GetChildren(T item)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return null;

            var property = item.GetType().GetProperty(ChildrenBindingPath);
            return property?.GetValue(item) as IEnumerable<T>;
        }

        private static void UpdateAncestorsForAllRows(List<TreeGridRow<T>> rootRows)
        {
            foreach (var rootRow in rootRows)
            {
                UpdateAncestorsRecursive(rootRow);
            }
        }

        private static void UpdateAncestorsRecursive(TreeGridRow<T> row)
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

        // Add a flag to prevent re-entry during bulk operations
        private bool _isInBulkOperation = false;

        // Optimized version of ExpandItemRecursively with proper hierarchical sorting
        private void ExpandItemRecursively(TreeGridRow<T> row)
        {
            if (row == null) return;
            
            using (new OverrideCursor(Cursors.Wait))
            {
                _isInBulkOperation = true;
                _isUpdatingFlattenedRows = true;
                
                try
                {
                    using (new TreeGridFocusManager<T>(this))
                    {
                        Debug.WriteLine($"ExpandItemRecursively: Expanding row at level {row.Level}");

                        // Skip if already expanded
                        if (row.IsExpanded) return;

                        // Set the expanded state directly to avoid triggering change notifications
                        row.IsExpanded = true;

                        // If the row has no children, nothing to expand
                        if (!row.HasChildren) return;
                        
                        // Find the index of the selected row in the flattened list
                        int insertIndex = _flattenedRows.IndexOf(row) + 1;
                        if (insertIndex <= 0) return;

                        // Get all descendants in the correct hierarchical order
                        var rowsToAdd = new List<TreeGridRow<T>>();
                        
                        // Recursively expand all nodes and collect them
                        ExpandAllDescendantsRecursively(row, rowsToAdd);
                        
                        // Process the flattened view in a single batch operation
                        if (rowsToAdd.Count > 0)
                        {
                            // Now batch insert the rows in proper hierarchical order
                            using (new DeferRefresh<T>(_flattenedRows))
                            {
                                foreach (var rowToAdd in rowsToAdd)
                                {
                                    // Only add if not already in the flattened view
                                    if (!_visibleRowsSet.Contains(rowToAdd))
                                    {
                                        _flattenedRows.Insert(insertIndex++, rowToAdd);
                                        _visibleRowsSet.Add(rowToAdd);
                                    }
                                }
                            }
                        }

                    // Force UI update
                    Items.Refresh();

                    // Update selection lines
                    SetSelectedLineRecursive(row, row.Level, true);
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                    _isInBulkOperation = false;
                }
            }
        }

        // New method to expand all descendants recursively
        private void ExpandAllDescendantsRecursively(TreeGridRow<T> parent, List<TreeGridRow<T>> result)
        {
            if (parent == null || !parent.HasChildren)
                return;
    
            // Process each child and its descendants in order
            foreach (var child in parent.Children)
            {
                // Add the child to the result list
                result.Add(child);
        
                // Mark the child as expanded
                child.IsExpanded = true;
        
                // Recursively process this child's descendants
                if (child.HasChildren)
                {
                    ExpandAllDescendantsRecursively(child, result);
                }
            }
        }

        // Optimized version of CollapseItemRecursively with batched updates
        private void CollapseItemRecursively(TreeGridRow<T> row)
        {
            if (row == null) return;
            
            using (new OverrideCursor(Cursors.Wait))
            {
                _isInBulkOperation = true;
                _isUpdatingFlattenedRows = true;
                
                try
                {
                    using (new TreeGridFocusManager<T>(this))
                    {
                        // First collect all rows that will be affected
                        var rowsToCollapse = new HashSet<TreeGridRow<T>>();
                        CollectRowsToCollapseRecursively(row, rowsToCollapse);

                        // Now collect all visible descendants that need to be removed from flattened rows
                        var rowsToRemove = new List<TreeGridRow<T>>();

                        // Collect all visible descendants
                        foreach (var rowToCollapse in rowsToCollapse)
                        {
                            // Only process visible rows
                            if (_visibleRowsSet.Contains(rowToCollapse) && rowToCollapse != row)
                            {
                                rowsToRemove.Add(rowToCollapse);
                            }

                            // Set the collapsed state directly to avoid triggering change notifications
                            rowToCollapse.IsExpanded = false;
                        }

                        // Process the flattened view in a single batch operation
                        if (rowsToRemove.Count > 0)
                        {
                            using (new DeferRefresh<T>(_flattenedRows))
                            {
                                foreach (var rowToRemove in rowsToRemove)
                                {
                                    _flattenedRows.Remove(rowToRemove);
                                    _visibleRowsSet.Remove(rowToRemove);
                                }
                            }
                        }

                        // Force UI update
                        Items.Refresh();
                    }
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                    _isInBulkOperation = false;
                }
            }
        }

        // Helper method to collect rows to collapse
        private void CollectRowsToCollapseRecursively(TreeGridRow<T> row, HashSet<TreeGridRow<T>> rowsToCollapse)
        {
            if (row == null) return;
            
            // Process children first (depth-first)
            if (row.HasChildren)
            {
                foreach (var child in row.Children)
                {
                    CollectRowsToCollapseRecursively(child, rowsToCollapse);
                }
            }
            
            // Add this row after processing children (ensures bottom-up processing)
            rowsToCollapse.Add(row);
        }

        // Modify the OnRowIsExpandedChanged method to handle recursive operations
        private void OnRowIsExpandedChanged(TreeGridRow<T> row)
        {

            
            // Skip during bulk operations to avoid triggering multiple refreshes
            if (_isInBulkOperation)
                return;

            Debug.WriteLine($"OnRowIsExpandedChanged: Row at level {row.Level} changed to expanded={row.IsExpanded}");

            // Refresh the data to update UI
            RefreshData();
            
            if (row.IsExpanded)
            {
                SetSelectedLineRecursive(row, row.Level, true);
            }
        }

        private void RefreshData()
        {
            // Prevent excessive calls with debouncing
           DebounceService.Debounce(DoRefreshData, TimeSpan.FromMilliseconds(REFRESH_DEBOUNCE_MS));
        }

        // Add an object pooling system for list reuse
        private class ListPool<TItem>
        {
            private readonly Stack<List<TItem>> _pool = new Stack<List<TItem>>();
            
            public List<TItem> Get()
            {
                if (_pool.Count > 0)
                {
                    return _pool.Pop();
                }
                return new List<TItem>();
            }
            
            public void Return(List<TItem> list)
            {
                list.Clear();
                _pool.Push(list);
            }
        }

        // Add a pool for list reuse
        private readonly ListPool<TreeGridRow<T>> _listPool = new ListPool<TreeGridRow<T>>();

        private void DoRefreshData()
        {
            // Ensure we're on the UI thread
            //if (!Dispatcher.CheckAccess())
            //{
            //    Dispatcher.BeginInvoke(new Action(DoRefreshData), DispatcherPriority.Normal);
            //    return;
            //}

            // Thread-safe check for pending refresh
            lock (_refreshLock)
            {
                if (_refreshPending)
                {
                    Debug.WriteLine("RefreshData: Skipping - refresh already pending");
                    return;
                }
                _refreshPending = true;
            }

            try
            {
                _refreshStopwatch.Reset();
                _refreshStopwatch.Start();
                Debug.WriteLine(">> RefreshData Started");
                
                if (_rootRows == null || _rootRows.Count == 0 || _isUpdatingFlattenedRows)
                {
                    Debug.WriteLine("RefreshData: Early exit - no data or updating");
                    return;
                }

                // Store the currently selected item before refresh
                var selectedRowBeforeRefresh = SelectedItem as TreeGridRow<T>;
                
                // Store the keyboard focus state
                var focusedElement = Keyboard.FocusedElement;
                var hasFocus = this.IsKeyboardFocusWithin;
                
                // Use a separate flag for the refresh operation
                bool wasUpdatingRows = _isUpdatingFlattenedRows;
                _isUpdatingFlattenedRows = true;
                
                try
                {
                    Debug.WriteLine($"RefreshData: Starting with selected item at level {selectedRowBeforeRefresh?.Level ?? -1}");

                    // Get a list from the pool instead of creating a new one
                    var newFlattenedRows = _listPool.Get();
                    try
                    {
                        _visibleRowsSet.Clear();
                        
                        // Create a snapshot of root rows to avoid collection modification issues
                        List<TreeGridRow<T>> rootRowsSnapshot;
                        lock (_refreshLock)
                        {
                            rootRowsSnapshot = new List<TreeGridRow<T>>(_rootRows);
                        }
                        
                        foreach (var row in rootRowsSnapshot)
                        {
                            BuildVisibleRowsListOptimized(row, newFlattenedRows);
                        }

                        Debug.WriteLine($"RefreshData: Collection rebuilt with {newFlattenedRows.Count} rows ({_refreshStopwatch.ElapsedMilliseconds}ms)");

                        // Update collection efficiently with minimum changes
                        SynchronizeCollections(_flattenedRows, newFlattenedRows);
                        Debug.WriteLine($"RefreshData collections synchronized ({_refreshStopwatch.ElapsedMilliseconds}ms)");
                        
                        // Refresh items on UI thread
                        Items.Refresh();
                        
                        // Restore selection immediately
                        if (selectedRowBeforeRefresh != null)
                        {
                            RestoreSelectionAfterRefresh(selectedRowBeforeRefresh);
                        }
                        
                        // Restore keyboard focus
                        RestoreKeyboardFocus(hasFocus, focusedElement);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in RefreshData: {ex.Message}");
                    }
                    finally
                    {
                        // Return the list to the pool
                        _listPool.Return(newFlattenedRows);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in RefreshData: {ex.Message}");
                }
                finally
                {
                    // Ensure flag is always properly reset to its original state
                    _isUpdatingFlattenedRows = wasUpdatingRows;
                    Debug.WriteLine($">> RefreshData Stopped {_refreshStopwatch.ElapsedMilliseconds}ms");
                    _refreshStopwatch.Stop();
                }
            }
            finally
            {
                // Always reset the pending flag
                lock (_refreshLock)
                {
                    _refreshPending = false;
                }
            }
        }

        // Add this helper method for restoring keyboard focus
        internal void RestoreKeyboardFocus(bool hadFocus, IInputElement previousFocusedElement)
        {
            if (!hadFocus)
                return;
                
            try
            {
                
                if (SelectedItem != null)
                {
                    // First try to focus the DataGridCell if we can find it
                    Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // Find the DataGridCell corresponding to the selected row and focused column
                            var row = (DataGridRow)ItemContainerGenerator.ContainerFromItem(SelectedItem);
                            if (row != null)
                            {
                                row.Focus();
                                
                                // Try to find which column was previously focused
                                if (CurrentCell.Column != null)
                                {
                                    // Get the cell and focus it
                                    var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                                    if (presenter != null)
                                    {
                                        var cell = presenter.ItemContainerGenerator.ContainerFromIndex(CurrentCell.Column.DisplayIndex) as DataGridCell;
                                        if (cell != null)
                                        {
                                            cell.Focus();
                                            return;
                                        }
                                    }
                                }
                            }
                            
                            // If we couldn't find the specific cell, focus the DataGrid itself
                            this.Focus();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error restoring focus: {ex.Message}");
                            this.Focus();  // Fallback to just focusing the grid
                        }
                    }, DispatcherPriority.Input);  // Higher priority for UI responsiveness
                }
                else if (previousFocusedElement != null)
                {
                    // If we had a focused element before and it's still in visual tree, try to restore focus to it
                    Dispatcher.InvokeAsync(() => previousFocusedElement.Focus(), DispatcherPriority.Input);
                }
                else
                {
                    // Last resort, just focus the grid itself
                    Dispatcher.InvokeAsync(() => this.Focus(), DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RestoreKeyboardFocus: {ex.Message}");
            }
        }

        // Helper method to find a visual child of a given type
        private static T2 FindVisualChild<T2>(DependencyObject parent) where T2 : DependencyObject
        {
            if (parent == null)
                return null;
                
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T2 typedChild)
                    return typedChild;

                var childOfChild = FindVisualChild<T2>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }

        // Add this new helper method for efficient collection synchronization
        private static void SynchronizeCollections(ObservableCollection<TreeGridRow<T>> target, 
                                   List<TreeGridRow<T>> source)
        {
            // Using a direct synchronization approach to minimize UI updates
            using (new DeferRefresh<T>(target))
            {
                int commonLength = Math.Min(target.Count, source.Count);

                // Step 1: Update existing items that match positions
                for (int i = 0; i < commonLength; i++)
                {
                    if (!ReferenceEquals(target[i], source[i]))
                    {
                        target[i] = source[i];
                    }
                }

                // Step 2: Remove extra items from end of target
                if (target.Count > source.Count)
                {
                    for (int i = target.Count - 1; i >= source.Count; i--)
                    {
                        target.RemoveAt(i);
                    }
                }

                // Step 3: Add missing items to target
                if (source.Count > target.Count)
                {
                    for (int i = target.Count; i < source.Count; i++)
                    {
                        target.Add(source[i]);
                    }
                }
            }
        }

        internal void RestoreSelectionAfterRefresh(TreeGridRow<T> originalSelection)
        {
            Debug.WriteLine($"RestoreSelectionAfterRefresh: Restoring selection for row at level {originalSelection.Level}");
            
            try
            {
                // Look for the exact same row instance
                var restoredRow = _flattenedRows.FirstOrDefault(r => ReferenceEquals(r, originalSelection));
                
                if (restoredRow != null)
                {
                    // First call focus on the grid to ensure UI state is updated
                    Focus();
                    
                    // Then set both properties to ensure consistent selection state
                    SelectedItem = restoredRow;
                    SelectedIndex = _flattenedRows.IndexOf(restoredRow);
                    
                    // Scroll after setting selection to ensure it's visible
                    Dispatcher.InvokeAsync(() => ScrollIntoView(restoredRow), DispatcherPriority.Background);
                    
                    Debug.WriteLine($"RestoreSelectionAfterRefresh: Restored exact row at index {SelectedIndex}");
                }
                else
                {
                    // Try to find an ancestor that's visible
                    Debug.WriteLine($"RestoreSelectionAfterRefresh: No exact match, looking for ancestors");
                    var ancestor = originalSelection.Parent;
                    while (ancestor != null && !_flattenedRows.Contains(ancestor))
                    {
                        ancestor = ancestor.Parent;
                    }
                    
                    if (ancestor != null)
                    {
                        Focus();
                        SelectedItem = ancestor;
                        SelectedIndex = _flattenedRows.IndexOf(ancestor);
                        Dispatcher.InvokeAsync(() => ScrollIntoView(ancestor), DispatcherPriority.Background);
                        
                        Debug.WriteLine($"RestoreSelectionAfterRefresh: Selected ancestor at level {ancestor.Level}");
                    }
                    else
                    {
                        // Clear selection if no suitable row was found
                        SelectedIndex = -1;
                        SelectedItem = null;
                        Debug.WriteLine($"RestoreSelectionAfterRefresh: No row to restore, clearing selection");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestoreSelectionAfterRefresh failed: {ex.Message}");
            }
        }

        // Add a helper method to get the hierarchy path as a list of objects
        private List<object> GetHierarchyPath(TreeGridRow<T> row)
        {
            var path = new List<object>();
            while (row != null)
            {
                path.Add(row.Data);
                row = row.Parent;
            }
            path.Reverse();
            return path;
        }

        private void BuildVisibleRowsListOptimized(TreeGridRow<T> row, List<TreeGridRow<T>> visibleRows)
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

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != DependencyProperty.UnsetValue)
            {
                base.OnSelectionChanged(e);
            }
        }

        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            try
            {

                if (e.OriginalSource != null)
                {
                    //base.OnContextMenuOpening(e);

                    // Ensure the context menu is only opened if the source is a valid row
                    //if (!(e.OriginalSource is DataGridCell) && !(e.OriginalSource is DataGridRow))
                    //{
                    //    e.Handled = true; // Prevent opening context menu if not on a valid row
                    //    Debug.WriteLine("OnContextMenuOpening: Prevented opening due to invalid source");
                    //    return;
                    //}
                    var grid = e.Source as GenericTreeGrid<T>;
                    var treeColumn = grid.Columns.OfType<TreeColumn>().FirstOrDefault();
                    var selectedRow = SelectedValue as TreeGridRow<T>;

                    // Find our menu items by tag
                    foreach (var item in ContextMenu.Items)
                    {
                        if (item is MenuItem menuItem && menuItem.Tag as string == "TreeGridDefaultItem")
                        {
                            switch (menuItem.Header.ToString())
                            {
                                case "Expand Selected":
                                    menuItem.IsEnabled = selectedRow?.HasChildren == true && (treeColumn?.ShowExpander??false) ;// && !selectedRow.IsExpanded;
                                    break;
                                case "Collapse Selected":
                                    menuItem.IsEnabled = selectedRow?.HasChildren == true && (treeColumn?.ShowExpander ?? false);// && selectedRow.IsExpanded;
                                    break;
                                case "Expand All":
                                    menuItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && !r.IsExpanded) && (treeColumn?.ShowExpander ?? false);
                                    break;
                                case "Collapse All":
                                    menuItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && r.IsExpanded) && (treeColumn?.ShowExpander ?? false);
                                    break;
                            }
                        }
                    }

                }
                else
                {
                    e.Handled = true; // Prevent context menu from opening if OriginalSource is null
                    Debug.WriteLine("OnContextMenuOpening: Prevented opening due to null OriginalSource");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContextMenu crash: {ex.Message}");
                //e.Handled = true; // Prevent further propagation
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine($"OnSelectionChanged: Entry - Added={e.AddedItems.Count}, Removed={e.RemovedItems.Count}");
            Debug.WriteLine($"OnSelectionChanged: _isUpdatingFlattenedRows={_isUpdatingFlattenedRows}");
    
            // Skip if called during collection update
            if (_isUpdatingFlattenedRows)
            {
                Debug.WriteLine("OnSelectionChanged: Skipping during collection update");
                return;
            }
    
            // Use a lock to prevent concurrent execution
            lock (_selectionChangeLock)
            {
                try
                {
                    // Update line selection visuals
                    foreach (TreeGridRow<T> row in e.RemovedItems)
                    {
                        ClearSelectedLineRecursive(row);
                    }
            
                    foreach (TreeGridRow<T> row in e.AddedItems)
                    {
                        if (!row._isCollapsing) // Skip if collapsing to avoid visual artifacts
                        {
                            SetSelectedLineRecursive(row, row.Level, true);
                        }
                        else
                        {
                            row._isCollapsing = false; // Reset flag
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnSelectionChanged: {ex.Message}");
                }
            }
            Debug.WriteLine($"OnSelectionChanged: Exit");
        }

        private static void ClearSelectedLineRecursive(TreeGridRow<T> row)
        {
            if (row.SelectedLineLevels != null)
            {
                for (int i = 0; i < row.SelectedLineLevels.Count; i++)
                {
                    row.SelectedLineLevels[i] = false;
                }
            }

            foreach (TreeGridRow<T> child in row.Children)
            {
                ClearSelectedLineRecursive(child);
            }
        }

        private static void SetSelectedLineRecursive(TreeGridRow<T> row, int level, bool value)
        {
            if (row.SelectedLineLevels != null)
            {
                while (row.SelectedLineLevels.Count <= level)
                {
                    row.SelectedLineLevels.Add(false);
                }
                
                if (level < row.SelectedLineLevels.Count)
                {
                    row.SelectedLineLevels[level] = value;
                }
            }

            foreach (TreeGridRow<T> child in row.Children)
            {
                SetSelectedLineRecursive(child, level, value);
            }
        }

        internal void ToggleItem(T item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                row._isCollapsing = row.IsExpanded;
                row.IsExpanded = !row.IsExpanded;
                RefreshData();

                if (row.IsExpanded)
                {
                    SetSelectedLineRecursive(row, row.Level, true);
                }
            }
        }

        public void ExpandAll()
        {
            using (new OverrideCursor(Cursors.Wait))
            {
                _isInBulkOperation = true;
                _isUpdatingFlattenedRows = true;
                
                try
                {
                    // First mark all rows as expanded
                    foreach (var row in _itemToRowMap.Values)
                    {
                        row.IsExpanded = true;
                    }
                    
                    // Now rebuild the flattened rows in one operation
                    var allRows = new List<TreeGridRow<T>>();
                    foreach (var rootRow in _rootRows)
                    {
                        allRows.Add(rootRow);
                        CollectAllDescendants(rootRow, allRows);
                    }
                    
                    // Update visible rows set
                    _visibleRowsSet.Clear();
                    foreach (var row in allRows)
                    {
                        _visibleRowsSet.Add(row);
                    }
                    
                    // Batch update the flattened rows
                    using (new DeferRefresh<T>(_flattenedRows))
                    {
                        _flattenedRows.Clear();
                        foreach (var row in allRows)
                        {
                            _flattenedRows.Add(row);
                        }
                    }
                    
                    // Force UI update
                    Items.Refresh();
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                    _isInBulkOperation = false;
                }
            }
        }

        public void CollapseAll()
        {
            using (new OverrideCursor(Cursors.Wait))
            {
                _isInBulkOperation = true;
                _isUpdatingFlattenedRows = true;
                
                try
                {
                    // Mark all rows as collapsed
                    foreach (var row in _itemToRowMap.Values)
                    {
                        row.IsExpanded = false;
                    }
                    
                    // Just show root rows
                    _visibleRowsSet.Clear();
                    foreach (var rootRow in _rootRows)
                    {
                        _visibleRowsSet.Add(rootRow);
                    }
                    
                    // Batch update the flattened rows
                    using (new DeferRefresh<T>(_flattenedRows))
                    {
                        _flattenedRows.Clear();
                        foreach (var rootRow in _rootRows)
                        {
                            _flattenedRows.Add(rootRow);
                        }
                    }
                    
                    // Force UI update
                    Items.Refresh();
                }
                finally
                {
                    _isUpdatingFlattenedRows = false;
                    _isInBulkOperation = false;
                }
            }
        }

        // Helper method to collect all descendants
        private void CollectAllDescendants(TreeGridRow<T> parent, List<TreeGridRow<T>> result)
        {
            foreach (var child in parent.Children)
            {
                result.Add(child);
                CollectAllDescendants(child, result);
            }
        }

        // Add a method to optimize expand/collapse menu actions
        private void SetupDefaultContextMenu()
        {
            if (ShowDefaultContextMenu)
            {
                ContextMenu menu = null;

                // If there's already a context menu, we'll merge with it
                if (ContextMenu != null)
                {
                    menu = ContextMenu;

                    // Check if we've already added our default items
                    bool hasDefaultItems = menu.Items.OfType<MenuItem>()
                        .Any(item => item.Header.ToString() == "Expand All" && item.Tag as string == "TreeGridDefaultItem");

                    if (hasDefaultItems)
                        return; // Default items already present

                    // Add separator if there are existing items
                    if (menu.Items.Count > 0 && !AddCustomMenusAtBottom)
                    {
                        menu.Items.Add(new Separator());
                    }
                }
                else
                {
                    menu = new ContextMenu();
                    ContextMenu = menu;
                }

                // Add default menu items
                var expandAllItem = new MenuItem { Header = "Expand All", Tag = "TreeGridDefaultItem" };
                expandAllItem.Click += (s, e) => ExpandAll();
                AddMenuItem(0, expandAllItem);

                var collapseAllItem = new MenuItem { Header = "Collapse All", Tag = "TreeGridDefaultItem" };
                collapseAllItem.Click += (s, e) => CollapseAll();
                AddMenuItem(1,collapseAllItem);

                AddSeparator(2);

                var expandSelectedItem = new MenuItem { Header = "Expand Selected", Tag = "TreeGridDefaultItem" };
                expandSelectedItem.Click += (s, e) =>
                {
                    if (SelectedItem is TreeGridRow<T> row && row.HasChildren)
                    {
                        // Use optimized version
                        ExpandItemRecursively(row);
                    }
                };
                AddMenuItem(3, expandSelectedItem);

                var collapseSelectedItem = new MenuItem { Header = "Collapse Selected", Tag = "TreeGridDefaultItem" };
                collapseSelectedItem.Click += (s, e) =>
                {
                    if (SelectedItem is TreeGridRow<T> row && row.HasChildren)
                    {
                        // Use optimized version
                        CollapseItemRecursively(row);
                    }
                };
                AddMenuItem(4, collapseSelectedItem);

                //ContextMenu.Opened += (s, e) =>
                //{
                    
                //};
            }
        }

        private void AddMenuItem(int position, MenuItem menuItem)
        {
            if (AddCustomMenusAtBottom)
            {
                ContextMenu.Items.Insert(position, menuItem);
            }
            else
            {
                ContextMenu.Items.Add(menuItem);
            }
        }

        private void AddSeparator(int position)
        {
            if (AddCustomMenusAtBottom)
            {
                ContextMenu.Items.Insert(position, new Separator());
            }
            else
            {
                ContextMenu.Items.Add(new Separator());
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
            var template = new DataTemplate();

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var expanderFactory = new FrameworkElementFactory(typeof(CheckBox));
            expanderFactory.SetValue(CheckBox.WidthProperty, 16.0);
            expanderFactory.SetValue(CheckBox.HeightProperty, 16.0);
            if (FindResource("PlusMinusExpanderTemplate") is ControlTemplate expanderTemplate)
                expanderFactory.SetValue(CheckBox.TemplateProperty, expanderTemplate);
            expanderFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsExpanded"));
            expanderFactory.SetBinding(CheckBox.VisibilityProperty, new Binding("HasChildren")
            {
                Converter = new BooleanToVisibilityConverter()
            });
            expanderFactory.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(Expander_PreviewMouseDown));

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.MarginProperty, new Thickness(4, 0, 0, 0));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("Data.Name"));

            stackPanelFactory.AppendChild(expanderFactory);
            stackPanelFactory.AppendChild(textBlockFactory);

            template.VisualTree = stackPanelFactory;
            return template;
        }

        private void Expander_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            
            if (SelectedItem is TreeGridRow<T> selectedRow)
            {
                switch (e.Key)
                {
                    case Key.Space:
                        ToggleItem(selectedRow.Data);
                        e.Handled = true;
                        break;
                    case Key.Add:
                    case Key.OemPlus:

                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        {
                            ExecuteCustomDescendantFilter.Execute(null);
                        }
                        else
                        {
                            if (!selectedRow.IsExpanded)
                            {
                                ToggleItem(selectedRow.Data);
                            }
                        }
                        e.Handled = true;

                        break;
                    case Key.Subtract:
                    case Key.OemMinus:
                        if (selectedRow.IsExpanded)
                        {
                            ToggleItem(selectedRow.Data);
                            e.Handled = true;
                        }
                        break;
                    case Key.Multiply:
                        ExpandItemRecursively(selectedRow);
                        e.Handled = true;
                        break;
                    case Key.Divide:
                        CollapseItemRecursively(selectedRow);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void OnRootItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine($"Root collection changed: {e.Action}");

            // For UI thread safety - use BeginInvoke for better responsiveness
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => OnRootItemsCollectionChanged(sender, e)));
                return;
            }

            // Prevent concurrent modifications
            lock (_refreshLock)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        // Only add new root items without rebuilding everything
                        if (e.NewItems != null)
                        {
                            foreach (T newItem in e.NewItems)
                            {
                                if (!_itemToRowMap.ContainsKey(newItem))
                                {
                                    var newRootRow = BuildHierarchy(newItem, 0, null);
                                    if (newRootRow != null)
                                    {
                                        _rootRows.Add(newRootRow);
                                        // Update ancestors for the new branch only
                                        UpdateAncestorsRecursive(newRootRow);
                                    }
                                }
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        // Remove specific root items
                        if (e.OldItems != null)
                        {
                            foreach (T removedItem in e.OldItems)
                            {
                                if (_itemToRowMap.TryGetValue(removedItem, out var rowToRemove))
                                {
                                    _rootRows.Remove(rowToRemove);
                                    RemoveItemAndDescendants(removedItem);
                                }
                            }
                            // Update ancestors for remaining items
                            UpdateAncestorsForAllRows(_rootRows);
                        }
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        // Remove old and add new
                        if (e.OldItems != null)
                        {
                            foreach (T oldItem in e.OldItems)
                            {
                                if (_itemToRowMap.TryGetValue(oldItem, out var rowToRemove))
                                {
                                    _rootRows.Remove(rowToRemove);
                                    RemoveItemAndDescendants(oldItem);
                                }
                            }
                        }
                        if (e.NewItems != null)
                        {
                            foreach (T newItem in e.NewItems)
                            {
                                if (!_itemToRowMap.ContainsKey(newItem))
                                {
                                    var newRootRow = BuildHierarchy(newItem, 0, null);
                                    if (newRootRow != null)
                                    {
                                        _rootRows.Add(newRootRow);
                                    }
                                }
                            }
                        }
                        UpdateAncestorsForAllRows(_rootRows);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        // Only for Reset do we need a full rebuild since the collection was cleared
                        RebuildHierarchyPreservingState();
                        break;

                    case NotifyCollectionChangedAction.Move:
                        // For move operations, just update the order in _rootRows
                        var currentRootItems = RootItems?.ToList();
                        if (currentRootItems != null)
                        {
                            // Reorder _rootRows to match the new order in RootItems
                            var reorderedRootRows = new List<TreeGridRow<T>>();
                            foreach (var item in currentRootItems)
                            {
                                if (_itemToRowMap.TryGetValue(item, out var row))
                                {
                                    reorderedRootRows.Add(row);
                                }
                            }
                            _rootRows.Clear();
                            _rootRows.AddRange(reorderedRootRows);
                            UpdateAncestorsForAllRows(_rootRows);
                        }
                        break;
                }
            }

            // Use debounced refresh
            RefreshData();
        }

        // Add this new method that preserves expanded state during full rebuilds
        private void RebuildHierarchyPreservingState()
        {
            // Save the current expanded state
            var expandedStates = new Dictionary<T, bool>();
            foreach (var kvp in _itemToRowMap)
            {
                expandedStates[kvp.Key] = kvp.Value.IsExpanded;
            }

            // Clear and rebuild
            _itemToRowMap.Clear();
            _rootRows.Clear();

            if (RootItems == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return;

            // Rebuild hierarchy
            foreach (var rootItem in RootItems)
            {
                var rootRow = BuildHierarchyPreservingState(rootItem, 0, null, expandedStates);
                if (rootRow != null)
                    _rootRows.Add(rootRow);
            }

            UpdateAncestorsForAllRows(_rootRows);
        }

        // Modified version of BuildHierarchy that preserves expanded state
        private TreeGridRow<T> BuildHierarchyPreservingState(T item, int level, TreeGridRow<T> parent, Dictionary<T, bool> expandedStates)
        {
            if (item == null) return null;

            // **CIRCULAR REFERENCE PROTECTION**
            if (_itemToRowMap.ContainsKey(item))
            {
                return _itemToRowMap[item];
            }

            var row = new TreeGridRow<T>
            {
                Data = item,
                Level = level,
                Parent = parent,
                Children = new List<TreeGridRow<T>>(),
                // Preserve the previous expanded state, or use ExpandOnLoad as default
                IsExpanded = expandedStates.TryGetValue(item, out var wasExpanded) ? wasExpanded : ExpandOnLoad,
                OnRowIsExpandedChanged = OnRowIsExpandedChanged
            };

            _itemToRowMap[item] = row;
            parent?.AddChild(row);

            // Subscribe to child collection changes if available
            if (!IsReadOnly)
            {
                SubscribeToChildCollectionChanges(item, row);
            }

            var children = GetChildren(item);
            if (children != null)
            {
                foreach (var child in children)
                {
                    BuildHierarchyPreservingState(child, level + 1, row, expandedStates);
                }
            }

            return row;
        }

        private void RemoveItemAndDescendants(T item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                // Recursively unsubscribe from children collections
                foreach (var child in row.Children)
                {
                    RemoveItemAndDescendants(child.Data);
                }

                // Unsubscribe from any child collection change notifications
                if (_childCollectionNotifiers.TryGetValue(item, out var notifier))
                {
                    notifier.CollectionChanged -= OnChildCollectionChanged;
                    _childCollectionNotifiers.Remove(item);
                }

                row.Children.Clear();
                // Finally remove from the map
                _itemToRowMap.Remove(item);
            }
        }

        private void OnChildCollectionChanged(object parentItem, NotifyCollectionChangedEventArgs e)
        {
            lock (_refreshLock)
            {
                if (_itemToRowMap.TryGetValue((T)parentItem, out var parentRow))
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            // Only add new items
                            if (e.NewItems != null)
                            {
                                foreach (T newItem in e.NewItems)
                                {
                                    // Check if item already exists to avoid duplicates
                                    if (!_itemToRowMap.ContainsKey(newItem))
                                    {
                                        BuildHierarchy(newItem, parentRow.Level + 1, parentRow);
                                    }
                                }
                            }
                            break;

                        case NotifyCollectionChangedAction.Remove:
                            // Only remove specific items
                            if (e.OldItems != null)
                            {
                                foreach (T removedItem in e.OldItems)
                                {
                                    RemoveItemAndDescendants(removedItem);
                                    // Also remove from parent's children list
                                    var childToRemove = parentRow.Children.FirstOrDefault(c => ReferenceEquals(c.Data, removedItem));
                                    if (childToRemove != null)
                                    {
                                        parentRow.Children.Remove(childToRemove);
                                    }
                                }
                            }
                            break;

                        case NotifyCollectionChangedAction.Replace:
                            // Remove old and add new
                            if (e.OldItems != null)
                            {
                                foreach (T oldItem in e.OldItems)
                                {
                                    RemoveItemAndDescendants(oldItem);
                                    var childToRemove = parentRow.Children.FirstOrDefault(c => ReferenceEquals(c.Data, oldItem));
                                    if (childToRemove != null)
                                    {
                                        parentRow.Children.Remove(childToRemove);
                                    }
                                }
                            }
                            if (e.NewItems != null)
                            {
                                foreach (T newItem in e.NewItems)
                                {
                                    if (!_itemToRowMap.ContainsKey(newItem))
                                    {
                                        BuildHierarchy(newItem, parentRow.Level + 1, parentRow);
                                    }
                                }
                            }
                            break;

                        case NotifyCollectionChangedAction.Reset:
                            // Collection was cleared or drastically changed - full rebuild needed
                            foreach (var child in parentRow.Children.ToList())
                            {
                                RemoveItemAndDescendants(child.Data);
                            }
                            parentRow.Children.Clear();

                            // Rebuild from current state
                            var children = GetChildren(parentRow.Data);
                            if (children != null)
                            {
                                foreach (var child in children)
                                {
                                    BuildHierarchy(child, parentRow.Level + 1, parentRow);
                                }
                            }
                            break;

                        case NotifyCollectionChangedAction.Move:
                            // For move operations, we might need to update the order
                            // For now, do a simple rebuild of this branch
                            goto case NotifyCollectionChangedAction.Reset;
                    }

                    // Update ancestors only if there were actual changes
                    UpdateAncestorsRecursive(parentRow);
                }
            }
            
            // Use debounced refresh
            RefreshData();
        }

        public void Cleanup()
        {
            // Stop and dispose refresh timer
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }

            // Unsubscribe from root collection changes
            if (_rootItemsCollectionNotifier != null)
            {
                _rootItemsCollectionNotifier.CollectionChanged -= OnRootItemsCollectionChanged;
                _rootItemsCollectionNotifier = null;
            }
            
            // Unsubscribe from all child collection changes
            foreach (var kvp in _childCollectionNotifiers)
            {
                kvp.Value.CollectionChanged -= OnChildCollectionChanged;
            }
            _childCollectionNotifiers.Clear();
        }

        // Override OnUnloaded to call cleanup
        protected void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        public void CustomTreeFilter(Func<TreeGridRow<T>, TreeGridRow<T>, bool> filterPredicate, TreeGridRow<T> parameter)
        {
            if (filterPredicate == null)
                return;

            using (new OverrideCursor(Cursors.Wait))
            {
                // Store the currently selected item before filtering
                if (!(SelectedItem is TreeGridRow<T> selectedRowBeforeFilter))
                    return; // Nothing selected to filter against

                // Apply filter to all rows (starting from the selected node)
                ApplyCustomFilterRecursively(parameter, selectedRowBeforeFilter, filterPredicate);

                // Rebuild display to reflect filter changes
                RefreshData();

                // Try to restore selection if possible
                if (_flattenedRows.Contains(selectedRowBeforeFilter))
                {
                    SelectedItem = selectedRowBeforeFilter;
                    ScrollIntoView(selectedRowBeforeFilter);
                }
            }
        }

        // Revised filter application method
        private static bool ApplyCustomFilterRecursively(TreeGridRow<T> selectedItem, TreeGridRow<T> currentItem, Func<TreeGridRow<T>, TreeGridRow<T>, bool> filterPredicate)
        {
            // Check if the current item should be visible based on the filter
            bool currentItemVisible = filterPredicate(selectedItem, currentItem);

            // Process children
            bool anyChildVisible = false;
            foreach (var childRow in currentItem.Children)
            {
                bool childVisible = ApplyCustomFilterRecursively(selectedItem, childRow, filterPredicate);
                anyChildVisible = anyChildVisible || childVisible;

                // If child is visible, we need to expand current item
                currentItem.IsExpanded = childVisible;
            }

            // An item should be expanded if any of its children are visible
            currentItem.IsExpanded = anyChildVisible;

            // Return whether this node or any of its descendants should be visible
            return currentItemVisible || anyChildVisible;
        }

        // Add a dependency property to allow binding a filter command
        public static readonly DependencyProperty CustomDescendantFilterProperty =
            DependencyProperty.Register(nameof(CustomDescendantFilter), typeof(Func<object,object,bool>), typeof(GenericTreeGrid<T>),
                new PropertyMetadata(null));

        public Func<TreeGridRow<T>,TreeGridRow<T>,bool> CustomDescendantFilter
        {
            get => (Func<TreeGridRow<T>,TreeGridRow<T>,bool>)GetValue(CustomDescendantFilterProperty);
            set => SetValue(CustomDescendantFilterProperty, value);
        }

        private void ExecuteCustomDescendantsFilterAction(object param)
        {
            if (CustomDescendantFilter != null && SelectedItem is TreeGridRow<T> selectedRow)
            {
                CustomTreeFilter(CustomDescendantFilter, selectedRow);
            }
        }

        // This fixes a datagrid timing issue where the selected item does not get set
        // before the context menu shows. Because some of the context menu items depend
        // on the Selected item being set we are forcing this in this override.
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);

            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null)
            {
                this.SelectedItem = row.Item;
                row.IsSelected = true;
            }
        }

        private T3 FindVisualParent<T3>(DependencyObject child) where T3 : DependencyObject
        {
            while (child != null)
            {
                if (child is T3 parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // Override MeasureOverride for better performance
        protected override Size MeasureOverride(Size constraint)
        {
            // Skip unnecessary measurement during toggle operations
            if (_refreshPending)
            {
                return new Size(constraint.Width, constraint.Height);
            }
    
            return base.MeasureOverride(constraint);
        }

        // Override ArrangeOverride for better performance
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            // Skip unnecessary arrangement during toggle operations
            if (_refreshPending)
            {
                return arrangeSize;
            }
    
            return base.ArrangeOverride(arrangeSize);
        }

        // Add container cache
        private Dictionary<object, DataGridRow> _containerCache = new Dictionary<object, DataGridRow>();

        // Override PrepareContainerForItemOverride to cache containers
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
    
            if (element is DataGridRow container && item != null)
            {
                _containerCache[item] = container;
            }
        }

        // Override ClearContainerForItemOverride to remove from cache
        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
    
            if (item != null && _containerCache.ContainsKey(item))
            {
                _containerCache.Remove(item);
            }
        }

        // Add a helper method to get container without triggering container generation
        private DataGridRow GetCachedContainer(object item)
        {
            if (item == null)
                return null;
        
            if (_containerCache.TryGetValue(item, out var container))
                return container;
        
            return null;
        }
    }
}