using DaxStudio.Controls.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DaxStudio.Controls
{
    /// <summary>
    /// A custom control that displays the name cell for a TreeGrid with tree lines, expander, and text
    /// </summary>
    public class TreeCell : DataGridCell
    {

        static TreeCell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeCell),
                new FrameworkPropertyMetadata(typeof(TreeCell)));
        }

        /// <summary>
        /// The TreeGridRow data context
        /// </summary>
        public static readonly DependencyProperty RowDataProperty =
            DependencyProperty.Register(nameof(RowData), typeof(TreeGridRow<object>), typeof(TreeCell),
                new PropertyMetadata(null, OnRowDataChanged));

        public TreeGridRow<object> RowData
        {
            get => (TreeGridRow<object>)GetValue(RowDataProperty);
            set => SetValue(RowDataProperty, value);
        }

        /// <summary>
        /// The indent width for each level
        /// </summary>
        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeCell),
                new PropertyMetadata(16.0));

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        /// <summary>
        /// The text content to display
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(object), typeof(TreeCell),
                new PropertyMetadata(null));

        public object Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// The icon content to display
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(TreeCell),
                new PropertyMetadata(null));

        public ImageSource Icon
        {
            get => (ImageSource)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>
        /// The foreground brush for the text
        /// </summary>
        public static readonly DependencyProperty TextForegroundProperty =
            DependencyProperty.Register(
                nameof(TextForeground), 
                typeof(Brush), 
                typeof(TreeCell),
                new FrameworkPropertyMetadata(
                    new SolidColorBrush(SystemColors.ControlTextColor),
                    FrameworkPropertyMetadataOptions.AffectsRender | 
                    FrameworkPropertyMetadataOptions.Inherits |
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextForegroundChanged));

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        private static void OnTextForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeCell cell)
            {
                System.Diagnostics.Debug.WriteLine($"Cell TextForeground changed: {e.OldValue} -> {e.NewValue}");
                // Force visual update
                cell.InvalidateVisual();
            }
        }

        /// <summary>
        /// The line stroke brush for tree lines
        /// </summary>
        public static readonly DependencyProperty LineStrokeProperty =
            DependencyProperty.Register(nameof(LineStroke), typeof(Brush), typeof(TreeCell),
                new PropertyMetadata(null));

        public Brush LineStroke
        {
            get => (Brush)GetValue(LineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line stroke brush for selected tree lines
        /// </summary>
        public static readonly DependencyProperty SelectedLineStrokeProperty =
            DependencyProperty.Register(nameof(SelectedLineStroke), typeof(Brush), typeof(TreeCell),
                new PropertyMetadata(Brushes.Red));

        public Brush SelectedLineStroke
        {
            get => (Brush)GetValue(SelectedLineStrokeProperty);
            set => SetValue(SelectedLineStrokeProperty, value); 
        }


        /// <summary>
        /// The line thickness for tree lines
        /// </summary>
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(TreeCell),
                new PropertyMetadata(1.0));

        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        /// <summary>
        /// Controls whether tree lines are visible
        /// </summary>
        public static readonly DependencyProperty ShowTreeLinesProperty =
            DependencyProperty.Register(nameof(ShowTreeLines), typeof(bool), typeof(TreeCell),
                new PropertyMetadata(true, RedrawCell));

        public bool ShowTreeLines
        {
            get => (bool)GetValue(ShowTreeLinesProperty);
            set => SetValue(ShowTreeLinesProperty, value);
        }

        /// <summary>
        /// Controls the visibility of the expander
        /// </summary>
        public static readonly DependencyProperty ShowExpanderProperty =
            DependencyProperty.Register(nameof(ShowExpander), typeof(bool), typeof(TreeCell),
                new PropertyMetadata(true, RedrawCell));

        public bool ShowExpander
        {
            get => (bool)GetValue(ShowExpanderProperty);
            set => SetValue(ShowExpanderProperty, value);
        }

        public static readonly DependencyProperty IsExpandedProperty =
    DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TreeCell),
        new PropertyMetadata(true, RedrawCell));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        /// <summary>
        /// Template for the expander toggle button
        /// </summary>
        public static readonly DependencyProperty ExpanderTemplateProperty =
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(TreeCell),
                new PropertyMetadata(null));

        public ControlTemplate ExpanderTemplate
        {
            get => (ControlTemplate)GetValue(ExpanderTemplateProperty);
            set => SetValue(ExpanderTemplateProperty, value);
        }

        /// <summary>
        /// Style for the expander toggle button
        /// </summary>
        public static readonly DependencyProperty ExpanderStyleProperty =
            DependencyProperty.Register(nameof(ExpanderStyle), typeof(Style), typeof(TreeCell),
                new PropertyMetadata(null));

        public Style ExpanderStyle
        {
            get => (Style)GetValue(ExpanderStyleProperty);
            set => SetValue(ExpanderStyleProperty, value);
        }

        /// <summary>
        /// Template for the text content
        /// </summary>
        public static readonly DependencyProperty TextTemplateProperty =
            DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(TreeCell),
                new PropertyMetadata(null));

        public DataTemplate TextTemplate
        {
            get => (DataTemplate)GetValue(TextTemplateProperty);
            set => SetValue(TextTemplateProperty, value);
        }

        /// <summary>
        /// Template for the icon content
        /// </summary>
        public static readonly DependencyProperty IconTemplateProperty =
            DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(TreeCell),
                new PropertyMetadata(null));

        public DataTemplate IconTemplate
        {
            get => (DataTemplate)GetValue(IconTemplateProperty);
            set => SetValue(IconTemplateProperty, value);
        }

        /// <summary>
        /// The style for the tree lines
        /// </summary>
        public static readonly DependencyProperty TreeLineStyleProperty =
            DependencyProperty.Register(
                nameof(TreeLineStyle),
                typeof(Style),
                typeof(TreeCell),
                new PropertyMetadata(null));

        public Style TreeLineStyle
        {
            get => (Style)GetValue(TreeLineStyleProperty);
            set => SetValue(TreeLineStyleProperty, value);
        }

        private static void RedrawCell(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeCell control)
            {
                control.InvalidateVisual();
            }
        }

        /// <summary>
        /// Event fired when the expander is clicked
        /// </summary>
        public static readonly RoutedEvent ExpanderClickEvent = EventManager.RegisterRoutedEvent(
            nameof(ExpanderClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeCell));

        public event RoutedEventHandler ExpanderClick
        {
            add => AddHandler(ExpanderClickEvent, value);
            remove => RemoveHandler(ExpanderClickEvent, value);
        }



        public static readonly RoutedEvent ExpanderPreviewMouseDownEvent = EventManager.RegisterRoutedEvent(
            nameof(ExpanderPreviewMouseDown), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeCell));

        public event RoutedEventHandler ExpanderPreviewMouseDown
        {
            add => AddHandler(ExpanderPreviewMouseDownEvent, value);
            remove => RemoveHandler(ExpanderPreviewMouseDownEvent, value);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == DataContextProperty)
            {
                RowData = DataContext as TreeGridRow<object>;
            }
        }

        private static void OnRowDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeCell control && e.NewValue is TreeGridRow<object> row)
            {
                control.UpdateBindings(row);
            }
        }

        private void UpdateBindings(TreeGridRow<object> row)
        {
            if (row?.Data != null)
            {
                // Set up binding for Text property - only if not already bound from column
                if (BindingOperations.GetBinding(this, TextProperty) is Binding textBinding)
                {
                    SetBinding(TextProperty, textBinding);
                }
                else if (Text != null)
                {
                    SetValue(TextProperty, Text);
                }

                // Set up binding for Icon property
                if (BindingOperations.GetBinding(this, IconProperty) is Binding iconBinding)
                {
                    SetBinding(IconProperty, iconBinding);
                }
                else if (Icon != null)
                {
                    SetValue(IconProperty, Icon);
                }
                
                // IMPORTANT: DON'T override TextForeground here with direct SetValue
                // as it would break dynamic resource updates
            }
        }
        
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Find the expander button in the template and wire up the click event
            if (GetTemplateChild("PART_Expander") is ToggleButton expander)
            {
                expander.Click += OnExpanderButtonClick;
                expander.PreviewMouseDown += OnExpanderPreviewMouseDown;
            }
        }

        private void OnExpanderPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ExpanderPreviewMouseDownEvent, this));
        }

        private void OnExpanderButtonClick(object sender, RoutedEventArgs e)
        {
            
            // Raise the ExpanderClick event
            RaiseEvent(new RoutedEventArgs(ExpanderClickEvent, this));

        }
    }
}