using DaxStudio.Controls.Model;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.Controls
{
    /// <summary>
    /// A custom DataGridTemplateColumn that uses TreeGridNameCell for display
    /// </summary>
    public class TreeGridTreeColumn : DataGridTemplateColumn
    {
        /// <summary>
        /// The indent width for each level
        /// </summary>
        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeGridTreeColumn),
                new PropertyMetadata(16.0));

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        /// <summary>
        /// The line stroke brush for tree lines
        /// </summary>
        public static readonly DependencyProperty LineStrokeProperty =
            DependencyProperty.Register(nameof(LineStroke), typeof(System.Windows.Media.Brush), typeof(TreeGridTreeColumn),
                new PropertyMetadata(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA))));

        public System.Windows.Media.Brush LineStroke
        {
            get => (System.Windows.Media.Brush)GetValue(LineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line stroke brush for tree lines
        /// </summary>
        public static readonly DependencyProperty SelectedLineStrokeProperty =
            DependencyProperty.Register(nameof(SelectedLineStroke), typeof(System.Windows.Media.Brush), typeof(TreeGridTreeColumn),
                new PropertyMetadata(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA))));

        public System.Windows.Media.Brush SelectedLineStroke
        {
            get => (System.Windows.Media.Brush)GetValue(SelectedLineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line thickness for tree lines
        /// </summary>
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(TreeGridTreeColumn),
                new PropertyMetadata(1.0));

        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        /// <summary>
        /// Controls the visibility of tree lines
        /// </summary>
        public static readonly DependencyProperty ShowTreeLinesProperty =
            DependencyProperty.Register(nameof(ShowTreeLines), typeof(bool), typeof(TreeGridTreeColumn),
                new PropertyMetadata(true, OnShowTreeLinesChanged));

        public bool ShowTreeLines
        {
            get => (bool)GetValue(ShowTreeLinesProperty);
            set => SetValue(ShowTreeLinesProperty, value);
        }

        /// <summary>
        /// The text content to display in the tree cell
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// The icon content to display in the tree cell
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>
        /// Template for displaying text content in the tree cell
        /// </summary>
        public static readonly DependencyProperty TextTemplateProperty =
            DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public DataTemplate TextTemplate
        {
            get => (DataTemplate)GetValue(TextTemplateProperty);
            set => SetValue(TextTemplateProperty, value);
        }

        /// <summary>
        /// Template for displaying icon content in the tree cell
        /// </summary>
        public static readonly DependencyProperty IconTemplateProperty =
            DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public DataTemplate IconTemplate
        {
            get => (DataTemplate)GetValue(IconTemplateProperty);
            set => SetValue(IconTemplateProperty, value);
        }

        /// <summary>
        /// Template for the expander button in the tree cell
        /// </summary>
        public static readonly DependencyProperty ExpanderTemplateProperty =
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public ControlTemplate ExpanderTemplate
        {
            get => (ControlTemplate)GetValue(ExpanderTemplateProperty);
            set => SetValue(ExpanderTemplateProperty, value);
        }

        /// <summary>
        /// The path to the property for text content, used when the Text property is not set
        /// </summary>
        public static readonly DependencyProperty TextPathProperty =
            DependencyProperty.Register(nameof(TextPath), typeof(string), typeof(TreeGridTreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public string TextPath
        {
            get => (string)GetValue(TextPathProperty);
            set => SetValue(TextPathProperty, value);
        }

        private static void OnShowTreeLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGridTreeColumn column)
            {
                // Recreate the cell template when the property changes
                column.CellTemplate = column.CreateCellTemplate();
            }
        }

        private static void OnTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGridTreeColumn column)
            {
                // Recreate the cell template when any template property changes
                column.CellTemplate = column.CreateCellTemplate();
            }
        }

        public TreeGridTreeColumn()
        {
            // Set the default cell template
            CellTemplate = CreateCellTemplate();
        }

        private DataTemplate CreateCellTemplate()
        {
            var template = new DataTemplate();

            // Create the TreeGridNameCell element
            var cellFactory = new FrameworkElementFactory(typeof(TreeGridTreeCell));
            
            // Bind the RowData property to the current data context (which should be a TreeGridRow)
            cellFactory.SetBinding(TreeGridTreeCell.RowDataProperty, new Binding("."));
            
            // Bind the properties from the column to the cell
            cellFactory.SetBinding(TreeGridTreeCell.IndentWidthProperty, new Binding(nameof(IndentWidth)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.LineStrokeProperty, new Binding(nameof(LineStroke)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.SelectedLineStrokeProperty, new Binding(nameof(SelectedLineStroke)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.LineThicknessProperty, new Binding(nameof(LineThickness)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.ShowTreeLinesProperty, new Binding(nameof(ShowTreeLines)) { Source = this });
            
            // Handle Text binding - if Text property is set as a Binding, use it; otherwise use default
            if (BindingOperations.GetBinding(this, TextProperty) is Binding textBinding)
            {
                cellFactory.SetBinding(TreeGridTreeCell.TextProperty, textBinding);
            }
            else if (!string.IsNullOrEmpty(TextPath))
            {
                cellFactory.SetBinding(TreeGridTreeCell.TextProperty, new Binding(TextPath));
            }
            else if (Text != null)
            {
                cellFactory.SetValue(TreeGridTreeCell.TextProperty, Text);
            }
            
            // Handle Icon binding - if Icon property is set as a Binding, use it; otherwise use default
            if (BindingOperations.GetBinding(this, IconProperty) is Binding iconBinding)
            {
                cellFactory.SetBinding(TreeGridTreeCell.IconProperty, iconBinding);
            }
            else if (Icon != null)
            {
                cellFactory.SetValue(TreeGridTreeCell.IconProperty, Icon);
            }
            
            // Bind the template properties
            cellFactory.SetBinding(TreeGridTreeCell.TextTemplateProperty, new Binding(nameof(TextTemplate)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.IconTemplateProperty, new Binding(nameof(IconTemplate)) { Source = this });
            cellFactory.SetBinding(TreeGridTreeCell.ExpanderTemplateProperty, new Binding(nameof(ExpanderTemplate)) { Source = this });

            // Handle the expander click event
            cellFactory.AddHandler(TreeGridTreeCell.ExpanderClickEvent, new RoutedEventHandler(OnExpanderClick));
            cellFactory.AddHandler(TreeGridTreeCell.ExpanderPreviewMouseDownEvent, new RoutedEventHandler(OnExpanderPreviewMouseDownEvent));

            template.VisualTree = cellFactory;
            return template;
        }

        private void OnExpanderPreviewMouseDownEvent(object sender, RoutedEventArgs e)
        {
            if (e.Source is TreeGridTreeCell cell)
            {
                var context = cell.DataContext as TreeGridRow<object>;
                if (context.IsExpanded) context.IsCollapsing = true;
            }
        }

        private void OnExpanderClick(object sender, RoutedEventArgs e)
        {
            if (sender is TreeGridTreeCell cell && cell.RowData is TreeGridRow<object> row)
            {
                // Find the parent TreeGrid and toggle the item
                var treeGrid = FindParentTreeGrid(cell);
                treeGrid?.ToggleItem(row.Data);
            }
        }

        private TreeGrid FindParentTreeGrid(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is TreeGrid treeGrid)
                    return treeGrid;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}