using DaxStudio.Controls.Converters;
using DaxStudio.Controls.Model;
using System;
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
    public class TreeGridTreeCell : Control
    {
        static TreeGridTreeCell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridTreeCell),
                new FrameworkPropertyMetadata(typeof(TreeGridTreeCell)));
        }

        /// <summary>
        /// The TreeGridRow data context
        /// </summary>
        public static readonly DependencyProperty RowDataProperty =
            DependencyProperty.Register(nameof(RowData), typeof(TreeGridRow<object>), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(Text), typeof(object), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(TextForeground), typeof(Brush), typeof(TreeGridTreeCell),
                new PropertyMetadata(Brushes.Black));

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        /// <summary>
        /// The line stroke brush for tree lines
        /// </summary>
        public static readonly DependencyProperty LineStrokeProperty =
            DependencyProperty.Register(nameof(LineStroke), typeof(Brush), typeof(TreeGridTreeCell),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))));

        public Brush LineStroke
        {
            get => (Brush)GetValue(LineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line stroke brush for selected tree lines
        /// </summary>
        public static readonly DependencyProperty SelectedLineStrokeProperty =
            DependencyProperty.Register(nameof(SelectedLineStroke), typeof(Brush), typeof(TreeGridTreeCell),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))));

        public Brush SelectedLineStroke
        {
            get => (Brush)GetValue(SelectedLineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        /// <summary>
        /// The line thickness for tree lines
        /// </summary>
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(ShowTreeLines), typeof(bool), typeof(TreeGridTreeCell),
                new PropertyMetadata(true, OnShowTreeLinesChanged));

        public bool ShowTreeLines
        {
            get => (bool)GetValue(ShowTreeLinesProperty);
            set => SetValue(ShowTreeLinesProperty, value);
        }

        /// <summary>
        /// Template for the expander toggle button
        /// </summary>
        public static readonly DependencyProperty ExpanderTemplateProperty =
            DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(ExpanderStyle), typeof(Style), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(TreeGridTreeCell),
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
            DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(TreeGridTreeCell),
                new PropertyMetadata(null));

        public DataTemplate IconTemplate
        {
            get => (DataTemplate)GetValue(IconTemplateProperty);
            set => SetValue(IconTemplateProperty, value);
        }

        private static void OnShowTreeLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGridTreeCell control)
            {
                control.InvalidateVisual();
            }
        }

        /// <summary>
        /// Event fired when the expander is clicked
        /// </summary>
        public static readonly RoutedEvent ExpanderClickEvent = EventManager.RegisterRoutedEvent(
            nameof(ExpanderClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridTreeCell));

        public event RoutedEventHandler ExpanderClick
        {
            add => AddHandler(ExpanderClickEvent, value);
            remove => RemoveHandler(ExpanderClickEvent, value);
        }

        private static void OnRowDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeGridTreeCell control && e.NewValue is TreeGridRow<object> row)
            {
                control.UpdateBindings(row);
            }
        }

        private void UpdateBindings(TreeGridRow<object> row)
        {
            if (row?.Data != null)
            {
                // Set up binding for Text property - only if not already bound from column
                // Handle Text binding - if Text property is set as a Binding, use it; otherwise use default
                if (BindingOperations.GetBinding(this, TextProperty) is Binding textBinding)
                {
                    SetBinding(TextProperty, textBinding);
                }
                else if (Text != null)
                {
                    SetValue(TextProperty, Text);
                }

                // Set up binding for Icon property - only if not already bound from column
                if (BindingOperations.GetBinding(this, IconProperty) is Binding iconBinding)
                {
                    SetBinding(TextProperty, iconBinding);
                }
                else if (Icon != null)
                {
                    SetValue(IconProperty, Icon);
                }

                // Set up binding for TextForeground property
                var foregroundBinding = new Binding("Data.IsVisible") 
                { 
                    Source = row,
                    Converter = new VisibilityToForegroundConverter()
                };
                SetBinding(TextForegroundProperty, foregroundBinding);
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
                expander.PreviewMouseUp += OnExpanderPreviewMouseUp;
            }
        }

        private void OnExpanderPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            //e.Handled = true; // Prevents the click from propagating further
        }

        private void OnExpanderPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //e.Handled = true; // Prevents the click from propagating further
            //RaiseEvent(new RoutedEventArgs(ExpanderClickEvent, this));

        }

        private void OnExpanderButtonClick(object sender, RoutedEventArgs e)
        {
            
            // Raise the ExpanderClick event
            RaiseEvent(new RoutedEventArgs(ExpanderClickEvent, this));
            e.Handled = true;
        }
    }
}