using DaxStudio.Controls.Model;
using DaxStudio.Controls.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
            SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Batch process selection changes
            _isUpdatingFlattenedRows = true;
            try
            {
                foreach (TreeGridRow<object> row in e.RemovedItems)
                {
                    if (!row.IsCollapsing)
                    {
                        SetSelectedLineLevelRecursive(row, row.Level, false);
                    }
                }

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
            ItemsSource = _flattenedRows;
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

        // Optimized RefreshData method
        private void RefreshData()
        {
            if (_rootRows == null || _rootRows.Count == 0 || _isUpdatingFlattenedRows)
                return;

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

                // Perform batch updates to _flattenedRows
                UpdateFlattenedRowsCollectionOptimized(newFlattenedRows);
            }
            finally
            {
                _isUpdatingFlattenedRows = false;
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
        private void UpdateFlattenedRowsCollectionOptimized(List<TreeGridRow<object>> newRows)
        {
            // Suspend collection change notifications during bulk updates
            using (var deferRefresh = new DeferRefresh(_flattenedRows))
            {
                // Remove items not in new set
                for (int i = _flattenedRows.Count - 1; i >= 0; i--)
                {
                    if (!_visibleRowsSet.Contains(_flattenedRows[i]))
                    {
                        _flattenedRows.RemoveAt(i);
                    }
                }

                // Add/move items efficiently
                for (int newIndex = 0; newIndex < newRows.Count; newIndex++)
                {
                    var newRow = newRows[newIndex];
                    var currentIndex = _flattenedRows.IndexOf(newRow);

                    if (currentIndex == -1)
                    {
                        _flattenedRows.Insert(newIndex, newRow);
                    }
                    else if (currentIndex != newIndex)
                    {
                        _flattenedRows.Move(currentIndex, newIndex);
                    }
                }
            }
        }

        // Optimized toggle with minimal refresh
        public void ToggleItem(object item)
        {
            using (new OverrideCursor(Cursors.Wait))
            {
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
            }
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

        void UpdateFlattenedRowsCollection(List<TreeGridRow<object>> newRows)
        {
            var currentRows = _flattenedRows.ToList();

            for (int i = currentRows.Count - 1; i >= 0; i--)
            {
                if (!newRows.Contains(currentRows[i]))
                {
                    _flattenedRows.RemoveAt(i);
                }
            }

            for (int newIndex = 0; newIndex < newRows.Count; newIndex++)
            {
                var newRow = newRows[newIndex];
                var currentIndex = _flattenedRows.IndexOf(newRow);

                if (currentIndex == -1)
                {
                    _flattenedRows.Insert(newIndex, newRow);
                }
                else if (currentIndex != newIndex)
                {
                    _flattenedRows.Move(currentIndex, newIndex);
                }
            }
        }





        private void Expander_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevents the event from bubbling to the DataGridRow
        }


    }

    // Helper class for deferring ObservableCollection notifications
    public class DeferRefresh : IDisposable
    {
        private readonly ObservableCollection<TreeGridRow<object>> _collection;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private readonly NotifyCollectionChangedEventHandler _collectionChangedHandler;

        public DeferRefresh(ObservableCollection<TreeGridRow<object>> collection)
        {
            
            _collection = collection;
            // Store original handlers and temporarily remove them
            var collectionChangedField = typeof(ObservableCollection<TreeGridRow<object>>)
                .GetField("CollectionChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var propertyChangedField = typeof(ObservableCollection<TreeGridRow<object>>)
                .GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            _collectionChangedHandler = (NotifyCollectionChangedEventHandler)collectionChangedField?.GetValue(_collection);
            _propertyChangedHandler = (PropertyChangedEventHandler)propertyChangedField?.GetValue(_collection);

            // Temporarily clear the handlers
            collectionChangedField?.SetValue(_collection, null);
            propertyChangedField?.SetValue(_collection, null);
        }

        public void Dispose()
        {
            // Restore handlers and fire a reset notification
            var collectionChangedField = typeof(ObservableCollection<TreeGridRow<object>>)
                .GetField("CollectionChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var propertyChangedField = typeof(ObservableCollection<TreeGridRow<object>>)
                .GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            collectionChangedField?.SetValue(_collection, _collectionChangedHandler);
            propertyChangedField?.SetValue(_collection, _propertyChangedHandler);

            // Fire a reset notification
            _collectionChangedHandler?.Invoke(_collection, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}