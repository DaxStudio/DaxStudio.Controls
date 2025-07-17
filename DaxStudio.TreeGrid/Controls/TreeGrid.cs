using DaxStudio.Controls.Model;
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
using System.Windows.Media;

namespace DaxStudio.Controls
{
    public class TreeGrid : DataGrid
    {
        private const string ExpanderColumnName = "TreeColumn";
        private static Stopwatch stopwatch = new Stopwatch();

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
            System.Diagnostics.Debug.WriteLine($"Selection changed: {e.AddedItems.Count} items added, {e.RemovedItems.Count} items removed");
            foreach (TreeGridRow<object> row in e.AddedItems)
            {
                foreach (TreeGridRow<object> child in row.Children)
                {
                    SetSelectedLineLevelRecursive(row, row.Level, true);
                }
            }

            foreach (TreeGridRow<object> row in e.RemovedItems)
            {
                foreach (TreeGridRow<object> child in row.Children)
                {
                    SetSelectedLineLevelRecursive(row, row.Level, false);
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

        public static readonly DependencyProperty ExpanderTemplateProperty =
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(DataTemplate), typeof(TreeGrid),
                new PropertyMetadata(null));

        public DataTemplate ExpanderTemplate
        {
            get => (DataTemplate)GetValue(ExpanderTemplateProperty);
            set => SetValue(ExpanderTemplateProperty, value);
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
                Children = new List<TreeGridRow<object>>()
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

        private void RefreshData()
        {
            stopwatch.Restart();
            if (_rootRows == null || _rootRows.Count == 0)
                return;

            // Build the new flattened structure  
            var newFlattenedRows = new List<TreeGridRow<object>>();
            foreach (var row in _rootRows)
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

        /// <summary>
        /// Toggles the expansion state of the specified item
        /// </summary>
        public void ToggleItem(object item)
        {
            if (_itemToRowMap.TryGetValue(item, out var row))
            {
                row.IsExpanded = !row.IsExpanded;
                RefreshData();
                SetSelectedLineLevelRecursive((TreeGridRow)row, row.Level, row.IsExpanded);

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
    }
}