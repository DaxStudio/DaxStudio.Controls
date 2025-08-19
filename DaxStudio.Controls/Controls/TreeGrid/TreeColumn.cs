using DaxStudio.Controls.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;


namespace DaxStudio.Controls
{
    /// <summary>
    /// A custom DataGridTemplateColumn that uses TreeGridNameCell for display
    /// </summary>
    public class TreeColumn : DataGridColumn
    {
        /// <summary>
        /// The indent width for each level
        /// </summary>
        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(LineStroke), typeof(System.Windows.Media.Brush), typeof(TreeColumn),
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA))));

        public Brush LineStroke
        {
            get => (Brush)GetValue(LineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line stroke brush for tree lines
        /// </summary>
        public static readonly DependencyProperty SelectedLineStrokeProperty =
            DependencyProperty.Register(nameof(SelectedLineStroke), typeof(System.Windows.Media.Brush), typeof(TreeColumn),
                new PropertyMetadata(Brushes.Red));

        public Brush SelectedLineStroke
        {
            get => (Brush)GetValue(SelectedLineStrokeProperty);
            set => SetValue(SelectedLineStrokeProperty, value); // Fixed: was setting LineStrokeProperty
        }

        /// <summary>
        /// The line thickness for tree lines
        /// </summary>
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(ShowTreeLines), typeof(bool), typeof(TreeColumn),
                new PropertyMetadata(true));

        public bool ShowTreeLines
        {
            get => (bool)GetValue(ShowTreeLinesProperty);
            set => SetValue(ShowTreeLinesProperty, value);
        }

        /// <summary>
        /// The text content to display in the tree cell
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// The foreground color for text
        /// </summary>
        public static readonly DependencyProperty TextForegroundProperty =
            DependencyProperty.Register(
                nameof(TextForeground), 
                typeof(Brush), 
                typeof(TreeColumn),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(SystemColors.ControlTextColor), 
                    FrameworkPropertyMetadataOptions.AffectsRender | 
                    FrameworkPropertyMetadataOptions.Inherits |
                    FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender,
                    OnTextForegroundChanged,
                    CoerceTextForegroundValue));

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        private static void OnTextForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeColumn column)
            {
                System.Diagnostics.Debug.WriteLine($"TextForeground changed: {e.OldValue} -> {e.NewValue}");

                // Find and refresh the DataGrid
                var grid = column.DataGridOwner;
                grid?.UpdateLayout();
                grid?.Items.Refresh();
            }
        }

        private static object CoerceTextForegroundValue(DependencyObject d, object baseValue)
        {
            // This coercion callback helps ensure dynamic resources are properly resolved
            return baseValue;
        }


        /// <summary>
        /// The icon content to display in the tree cell
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(TreeColumn),
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
            DependencyProperty.Register(nameof(TextPath), typeof(string), typeof(TreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public string TextPath
        {
            get => (string)GetValue(TextPathProperty);
            set => SetValue(TextPathProperty, value);
        }

        /// <summary>
        /// Controls the visibility of the expander
        /// </summary>
        public static readonly DependencyProperty ShowExpanderProperty =
            DependencyProperty.Register(nameof(ShowExpander), typeof(bool), typeof(TreeColumn),
                new PropertyMetadata(true));

        public bool ShowExpander
        {
            get => (bool)GetValue(ShowExpanderProperty);
            set => SetValue(ShowExpanderProperty, value);
        }

        private static void OnTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeColumn column)
            {
                // Check if this is a Foreground property change
                bool isForegroundChange = e.Property == TextForegroundProperty;
                
                if (isForegroundChange)
                {
                    // Log the change to debug
                    System.Diagnostics.Debug.WriteLine($"Foreground changed: {e.OldValue} -> {e.NewValue}");
                }

                column.DataGridOwner?.Dispatcher.Invoke(() =>
                {
                    column.DataGridOwner?.InvalidateVisual();
                }, DispatcherPriority.Render);
                
            }
        }

        private DataGrid FindDataGridInWindow(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DataGrid dataGrid && dataGrid.Columns.Contains(this))
                {
                    return dataGrid;
                }
                
                var result = FindDataGridInWindow(child);
                if (result != null)
                    return result;
            }
            return null;
        }
       

        private void OnExpanderPreviewMouseDownEvent(object sender, RoutedEventArgs e)
        {
            if (e.Source is TreeCell cell)
            {
                if (!(cell.DataContext is TreeGridRow<object> context))
                {
                    return;
                }
                if (context.IsExpanded) context._isCollapsing = true;
            }
            e.Handled = true;
        }



        /// <summary>
        /// The style for the tree lines
        /// </summary>
        public static readonly DependencyProperty TreeLineStyleProperty =
            DependencyProperty.Register(
                nameof(TreeLineStyle),
                typeof(Style),
                typeof(TreeColumn),
                new PropertyMetadata(null, OnTemplateChanged));

        public Style TreeLineStyle
        {
            get => (Style)GetValue(TreeLineStyleProperty);
            set => SetValue(TreeLineStyleProperty, value);
        }


        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var treeCell = new TreeCell();

            // Set DataContext and RowData
            //treeCell.DataContext = dataItem;
            //treeCell.RowData = dataItem as TreeGridRow<object>;

            // Bind properties from column to cell
            MapColumnPropertyToCell(treeCell, SelectedLineStrokeProperty, TreeCell.SelectedLineStrokeProperty, SelectedLineStroke);
            MapColumnPropertyToCell(treeCell, LineStrokeProperty, TreeCell.LineStrokeProperty, LineStroke);
            MapColumnPropertyToCell(treeCell, ShowTreeLinesProperty, TreeCell.ShowTreeLinesProperty, ShowTreeLines);
            MapColumnPropertyToCell(treeCell, ShowExpanderProperty, TreeCell.ShowExpanderProperty, ShowExpander);
            MapColumnPropertyToCell(treeCell, IndentWidthProperty, TreeCell.IndentWidthProperty, IndentWidth);
            MapColumnPropertyToCell(treeCell, LineThicknessProperty, TreeCell.LineThicknessProperty, LineThickness);
            MapColumnPropertyToCell(treeCell, TextForegroundProperty, TreeCell.TextForegroundProperty, TextForeground);
            MapColumnPropertyToCell(treeCell, IconProperty, TreeCell.IconProperty, Icon);

            // Text binding
            if (BindingOperations.GetBinding(this, TextProperty) is Binding textBinding)
            {
                treeCell.SetBinding(TreeCell.TextProperty, textBinding);
            }
            else if (!string.IsNullOrEmpty(TextPath))
            {
                treeCell.SetBinding(TreeCell.TextProperty, new Binding(TextPath));
            }
            else if (Text != null)
            {
                treeCell.SetValue(TreeCell.TextProperty, Text);
            }

            // Templates
            treeCell.SetBinding(TreeCell.TextTemplateProperty, new Binding(nameof(TextTemplate)) { Source = this });
            treeCell.SetBinding(TreeCell.IconTemplateProperty, new Binding(nameof(IconTemplate)) { Source = this });
            treeCell.SetBinding(TreeCell.ExpanderTemplateProperty, new Binding(nameof(ExpanderTemplate)) { Source = this });

            // TreeLineStyle
            if (TreeLineStyle != null)
            {
                treeCell.SetValue(TreeCell.StyleProperty, TreeLineStyle);
            }
            
            // Attach event handlers if needed
            //treeCell.AddHandler(TreeCell.ExpanderClickEvent, new RoutedEventHandler(OnExpanderClick));
            treeCell.AddHandler(TreeCell.ExpanderPreviewMouseDownEvent, new RoutedEventHandler(OnExpanderPreviewMouseDownEvent));

            return treeCell;
        }

        private void MapColumnPropertyToCell(TreeCell treeCell, DependencyProperty columnProperty, DependencyProperty cellProperty, object value)
        {
            if (BindingOperations.GetBinding(this, columnProperty) is Binding existingBinding)
            {
                treeCell.SetBinding(cellProperty, existingBinding);
            }
            else if (value != null)
            {
                var defaultValue = columnProperty.DefaultMetadata.DefaultValue;
                var currentValue = this.GetValue(columnProperty);

                bool isDefault = Equals(currentValue, defaultValue);
                if (!isDefault)
                    treeCell.SetValue(cellProperty, value);
            }
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            // Use the same as display element
            return GenerateElement(cell, dataItem);
        }
    }
}