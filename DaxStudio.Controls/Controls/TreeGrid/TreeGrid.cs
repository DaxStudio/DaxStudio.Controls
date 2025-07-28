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
using System.Windows.Threading;
using System.Reflection;

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

        // Fields for refresh management
        private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _refreshCancellation;
        private bool _isUpdatingFlattenedRows = false;
        private readonly HashSet<TreeGridRow<object>> _visibleRowsSet = new HashSet<TreeGridRow<object>>();
        private readonly Stopwatch _refreshTimer = new Stopwatch();
        private int _lastRefreshRowCount = 0;
        private readonly object _selectionChangeLock = new object();

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
            
            // Use recycling mode for even better performance
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Item);

            Loaded += OnLoaded;
        }

        #region Dependency Properties

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

        public static readonly DependencyProperty EnableLazyLoadingProperty =
            DependencyProperty.Register(nameof(EnableLazyLoading), typeof(bool), typeof(TreeGrid),
                new PropertyMetadata(false));

        public bool EnableLazyLoading
        {
            get => (bool)GetValue(EnableLazyLoadingProperty);
            set => SetValue(EnableLazyLoadingProperty, value);
        }

        #endregion

        #region Event Handlers for Dependency Properties

        private static void OnShowDefaultContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGrid grid)
            {
                grid.SetupDefaultContextMenu();
            }
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
                if (grid._rootRows.Count > 0)
                {
                    grid.ExpandAll();
                }
            }
        }

        #endregion

        #region Initialization

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
                    
                    if (column is TreeGridTreeColumn treeColumn)
                    {
                        Debug.WriteLine($"OnLoaded: Found TreeGridTreeColumn");
                        Debug.WriteLine($"OnLoaded: SelectedLineStroke={treeColumn.SelectedLineStroke}");
                        Debug.WriteLine($"OnLoaded: ShowTreeLines={treeColumn.ShowTreeLines}");
                        
                        if (treeColumn.SelectedLineStroke is SolidColorBrush selectedBrush)
                        {
                            Debug.WriteLine($"OnLoaded: SelectedLineStroke color={selectedBrush.Color}");
                            
                            if (treeColumn.ShowTreeLines && selectedBrush.Color != Colors.Transparent)
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

        #endregion

        #region Hierarchy Management

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

        private TreeGridRow<object> BuildHierarchy(object item, int level, TreeGridRow<object> parent)
        {
            if (item == null) return null;

            var row = new TreeGridRow<object>
            {
                Data = item,
                Level = level,
                Parent = parent,
                Children = new List<TreeGridRow<object>>(),
                IsExpanded = ExpandOnLoad
            };

            _itemToRowMap[item] = row;
            parent?.AddChild(row);

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

        private IEnumerable GetChildren(object item)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenBindingPath))
                return null;

            var property = item.GetType().GetProperty(ChildrenBindingPath);
            return property?.GetValue(item) as IEnumerable;
        }

        private void UpdateAncestorsForAllRows(List<TreeGridRow<object>> rootRows)
        {
            foreach (var rootRow in rootRows)
            {
                UpdateAncestorsRecursive(rootRow);
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
                UpdateAncestorsRecursive(child);
            }
        }

        #endregion

        #region Refresh and Selection Management

        // Modified RefreshData with more robust selection handling
        private void RefreshData()
        {
            if (_rootRows == null || _rootRows.Count == 0 || _isUpdatingFlattenedRows)
                return;

            // Store the currently selected item before refresh
            var selectedRowBeforeRefresh = SelectedItem as TreeGridRow<object>;
            
            // Store the keyboard focus state
            var focusedElement = Keyboard.FocusedElement;
            var hasFocus = this.IsKeyboardFocusWithin;
            
            // Use a separate flag for the refresh operation
            bool wasUpdatingRows = _isUpdatingFlattenedRows;
            _isUpdatingFlattenedRows = true;
            
            try
            {
                Debug.WriteLine($"RefreshData: Starting with selected item at level {selectedRowBeforeRefresh?.Level ?? -1}");

                // Build the new flattened structure
                var newFlattenedRows = new List<TreeGridRow<object>>();
                _visibleRowsSet.Clear();
                
                foreach (var row in _rootRows)
                {
                    BuildVisibleRowsListOptimized(row, newFlattenedRows);
                }

                Debug.WriteLine($"RefreshData: Collection rebuilt with {newFlattenedRows.Count} rows");

                // Update collection efficiently with minimum changes
                SynchronizeCollections(_flattenedRows, newFlattenedRows);
                
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
                // Ensure flag is always properly reset to its original state
                _isUpdatingFlattenedRows = wasUpdatingRows;
            }
        }

        // Add this helper method for restoring keyboard focus
        private void RestoreKeyboardFocus(bool hadFocus, IInputElement previousFocusedElement)
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
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;
                
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                    return typedChild;
                    
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }

        // Add this new helper method for efficient collection synchronization
        private void SynchronizeCollections(ObservableCollection<TreeGridRow<object>> target, 
                                   List<TreeGridRow<object>> source)
        {
            // Using a direct synchronization approach to minimize UI updates
            
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

        // Improved RestoreSelectionAfterRefresh for more reliable selection restoration
        private void RestoreSelectionAfterRefresh(TreeGridRow<object> originalSelection)
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

        #endregion

        #region Selection Event Handling

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
            foreach (TreeGridRow<object> row in e.RemovedItems)
            {
                ClearSelectedLineRecursive(row);
            }
            
            foreach (TreeGridRow<object> row in e.AddedItems)
            {
                if (!row.IsCollapsing) // Skip if collapsing to avoid visual artifacts
                {
                    SetSelectedLineRecursive(row, row.Level, true);
                }
                else
                {
                    row.IsCollapsing = false; // Reset flag
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

        private static void ClearSelectedLineRecursive(TreeGridRow<object> row)
        {
            if (row.SelectedLineLevels != null)
            {
                for (int i = 0; i < row.SelectedLineLevels.Count; i++)
                {
                    row.SelectedLineLevels[i] = false;
                }
            }

            foreach (TreeGridRow<object> child in row.Children)
            {
                ClearSelectedLineRecursive(child);
            }
        }

        private static void SetSelectedLineRecursive(TreeGridRow<object> row, int level, bool value)
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

            foreach (TreeGridRow<object> child in row.Children)
            {
                SetSelectedLineRecursive(child, level, value);
            }
        }

        #endregion

        #region Public Operations

        public void ToggleItem(object item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                if (row.IsExpanded)
                {
                    row.IsCollapsing = true;
                }

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

        private void ExpandItemRecursively(TreeGridRow<object> row)
        {
            _isUpdatingFlattenedRows = true;
            try
            {
                ExpandRowRecursivelyImpl(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            RefreshData();
        }

        private static void ExpandRowRecursivelyImpl(TreeGridRow<object> row)
        {
            if (row == null)
                return;

            if (row.HasChildren)
            {
                row.IsExpanded = true;
            }

            foreach (var child in row.Children)
            {
                ExpandRowRecursivelyImpl(child);
            }
        }

        private void CollapseItemRecursively(TreeGridRow<object> row)
        {
            _isUpdatingFlattenedRows = true;
            try
            {
                CollapseRowRecursivelyImpl(row);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
            }
            
            RefreshData();
        }

        private static void CollapseRowRecursivelyImpl(TreeGridRow<object> row)
        {
            if (row == null)
                return;

            foreach (var child in row.Children)
            {
                CollapseRowRecursivelyImpl(child);
            }

            if (row.HasChildren)
            {
                row.IsExpanded = false;
            }
        }

        #endregion

        #region Context Menu

        private void SetupDefaultContextMenu()
        {
            if (ShowDefaultContextMenu && ContextMenu == null)
            {
                var contextMenu = new ContextMenu();

                var expandAllItem = new MenuItem { Header = "Expand All" };
                expandAllItem.Click += (s, e) => ExpandAll();
                contextMenu.Items.Add(expandAllItem);

                var collapseAllItem = new MenuItem { Header = "Collapse All" };
                collapseAllItem.Click += (s, e) => CollapseAll();
                contextMenu.Items.Add(collapseAllItem);

                contextMenu.Items.Add(new Separator());

                var expandSelectedItem = new MenuItem { Header = "Expand Selected"};
                expandSelectedItem.Click += (s, e) => {
                    if (SelectedItem is TreeGridRow<object> row && row.HasChildren && !row.IsExpanded)
                    {
                        ExpandItemRecursively(row);
                    }
                };
                contextMenu.Items.Add(expandSelectedItem);

                var collapseSelectedItem = new MenuItem { Header = "Collapse Selected" };
                collapseSelectedItem.Click += (s, e) => {
                    if (SelectedItem is TreeGridRow<object> row && row.HasChildren && row.IsExpanded)
                    {
                        CollapseItemRecursively(row);
                    }
                };
                contextMenu.Items.Add(collapseSelectedItem);

                contextMenu.Opened += (s, e) => {
                    var selectedRow = SelectedItem as TreeGridRow<object>;
                    expandSelectedItem.IsEnabled = selectedRow?.HasChildren == true && !selectedRow.IsExpanded;
                    collapseSelectedItem.IsEnabled = selectedRow?.HasChildren == true && selectedRow.IsExpanded;
                    
                    expandAllItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && !r.IsExpanded);
                    collapseAllItem.IsEnabled = _itemToRowMap.Values.Any(r => r.HasChildren && r.IsExpanded);
                };

                ContextMenu = contextMenu;
            }
        }

        #endregion

        #region Default Template

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

            var expanderFactory = new FrameworkElementFactory(typeof(ToggleButton));
            expanderFactory.SetValue(ToggleButton.WidthProperty, 16.0);
            expanderFactory.SetValue(ToggleButton.HeightProperty, 16.0);
            expanderFactory.SetValue(ToggleButton.StyleProperty, FindResource("ExpanderControlTemplate"));
            expanderFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsExpanded"));
            expanderFactory.SetBinding(ToggleButton.VisibilityProperty, new Binding("HasChildren")
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

        #endregion

        #region Keyboard Handling

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            
            if (SelectedItem is TreeGridRow<object> selectedRow)
            {
                switch (e.Key)
                {
                    case Key.Space:
                        ToggleItem(selectedRow.Data);
                        e.Handled = true;
                        break;
                    case Key.Add:
                    case Key.OemPlus:
                        if (!selectedRow.IsExpanded)
                        {
                            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                            {
                                ExpandItemRecursively(selectedRow);
                            }
                            else
                            {
                                ToggleItem(selectedRow.Data);
                            }
                            e.Handled = true;
                        }
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

        #endregion
    }
}