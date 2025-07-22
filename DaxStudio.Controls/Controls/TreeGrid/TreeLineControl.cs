using DaxStudio.Controls.Model;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DaxStudio.Controls
{
    public class TreeLineControl : Control
    {
        static TreeLineControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeLineControl), 
                new FrameworkPropertyMetadata(typeof(TreeLineControl)));
        }

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(Level), typeof(int), typeof(TreeLineControl),
                new PropertyMetadata(0, OnLevelChanged));

        public int Level
        {
            get => (int)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public static readonly DependencyProperty IndentWidthProperty =
            DependencyProperty.Register(nameof(IndentWidth), typeof(double), typeof(TreeLineControl),
                new PropertyMetadata(20.0, OnIndentWidthChanged));

        public double IndentWidth
        {
            get => (double)GetValue(IndentWidthProperty);
            set => SetValue(IndentWidthProperty, value);
        }

        public static readonly DependencyProperty IsLastChildProperty =
            DependencyProperty.Register(nameof(IsLastChild), typeof(bool), typeof(TreeLineControl),
                new PropertyMetadata(false, OnIsLastChildChanged));

        public bool IsLastChild
        {
            get => (bool)GetValue(IsLastChildProperty);
            set => SetValue(IsLastChildProperty, value);
        }

        public static readonly DependencyProperty HasChildrenProperty =
            DependencyProperty.Register(nameof(HasChildren), typeof(bool), typeof(TreeLineControl),
                new PropertyMetadata(false, OnHasChildrenChanged));

        public bool HasChildren
        {
            get => (bool)GetValue(HasChildrenProperty);
            set => SetValue(HasChildrenProperty, value);
        }

        public static readonly DependencyProperty AncestorLevelsProperty =
            DependencyProperty.Register(nameof(AncestorLevels), typeof(IEnumerable<bool>), typeof(TreeLineControl),
                new PropertyMetadata(null, OnAncestorLevelsChanged));

        public IEnumerable<bool> AncestorLevels
        {
            get => (IEnumerable<bool>)GetValue(AncestorLevelsProperty);
            set => SetValue(AncestorLevelsProperty, value);
        }

        public static readonly DependencyProperty SelectedLineLevelsProperty =
            DependencyProperty.Register(nameof(SelectedLineLevels), typeof(IEnumerable<bool>), typeof(TreeLineControl),
                new PropertyMetadata(null, OnSelectedLineLevelsChanged));

        public IEnumerable<bool> SelectedLineLevels
        {
            get => (IEnumerable<bool>)GetValue(SelectedLineLevelsProperty);
            set => SetValue(SelectedLineLevelsProperty, value);
        }

        public static readonly DependencyProperty LineStrokeProperty =
            DependencyProperty.Register(nameof(LineStroke), typeof(Brush), typeof(TreeLineControl),
                new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnLineStrokeChanged));

        public Brush LineStroke
        {
            get => (Brush)GetValue(LineStrokeProperty);
            set => SetValue(LineStrokeProperty, value);
        }

        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(TreeLineControl),
                new PropertyMetadata(1.0, OnLineThicknessChanged));

        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        public static readonly DependencyProperty SelectedLineStrokeProperty =
            DependencyProperty.Register(nameof(SelectedLineStroke), typeof(Brush), typeof(TreeLineControl),
                new PropertyMetadata(Brushes.Red, OnSelectedLineStrokeChanged));

        public Brush SelectedLineStroke
        {
            get => (Brush)GetValue(SelectedLineStrokeProperty);
            set => SetValue(SelectedLineStrokeProperty, value);
        }

        // Add this field to batch invalidation calls
        private bool _invalidationScheduled = false;
        
        private void ScheduleInvalidation()
        {
            if (_invalidationScheduled) return;
            
            _invalidationScheduled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _invalidationScheduled = false;
                InvalidateVisual();
            }), DispatcherPriority.Render);
        }

        private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnIndentWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnIsLastChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnHasChildrenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnAncestorLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnSelectedLineLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TreeLineControl)d;

            // Unsubscribe from old collection
            if (e.OldValue is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= control.SelectedLineLevels_CollectionChanged;

            // Subscribe to new collection
            if (e.NewValue is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += control.SelectedLineLevels_CollectionChanged;

            control.ScheduleInvalidation();
        }

        private static void OnLineStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnLineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private static void OnSelectedLineStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).ScheduleInvalidation();
        }

        private void SelectedLineLevels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        // Add these fields for caching
        private Pen _cachedPen;
        private Brush _cachedLineStroke;
        private double _cachedLineThickness;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (Level == 0) return;

            // Cache frequently used objects
            if (_cachedPen == null || !_cachedLineStroke.Equals(LineStroke) || _cachedLineThickness != LineThickness)
            {
                _cachedPen?.Freeze(); // Freeze old pen
                _cachedPen = new Pen(LineStroke, LineThickness);
                _cachedPen.DashStyle = new DashStyle(new double[] { 2, 1 }, 2);
                _cachedPen.Freeze(); // Freeze for better performance
                _cachedLineStroke = LineStroke;
                _cachedLineThickness = LineThickness;
            }

            // ... rest of rendering code using _cachedPen

            var pen = _cachedPen;
            var centerY = ActualHeight / 2;
            var selectedLevels = SelectedLineLevels?.ToArray();

            // Draw vertical lines for all ancestor levels
            if (AncestorLevels != null)
            {
                var ancestors = AncestorLevels.ToArray();
                
                for (int i = 0; i < ancestors.Length && i < Level - 1; i++)
                {
                    var x = (i + 1) * IndentWidth - IndentWidth / 2;

                    // Draw vertical line only if this ancestor has more siblings
                    if (!ancestors[i])
                    {
                        // Use highlight if selected
                        bool isAncestorSelected = selectedLevels != null && i < selectedLevels.Length && selectedLevels[i];
                        var ancestorLinePen = isAncestorSelected ? new Pen(SelectedLineStroke ?? Brushes.Red, LineThickness) { DashStyle = pen.DashStyle } : pen;
                        drawingContext.DrawLine(ancestorLinePen, new Point(x, 0), new Point(x, ActualHeight));
                    }
                }
            }

            // Draw lines for current level
            var currentX = Level * IndentWidth - IndentWidth / 2;
            bool isSelected = selectedLevels[Level-1];
            var linePen = isSelected ? new Pen(SelectedLineStroke ?? Brushes.Red, LineThickness) { DashStyle = pen.DashStyle } : pen;

            // Vertical line (up to center or full height)
            if (!IsLastChild)
            {
                // Draw full vertical line if not last child
                drawingContext.DrawLine(linePen, new Point(currentX, 0 ), new Point(currentX, ActualHeight ));
            }
            else
            {
                // Draw only up to center if last child
                drawingContext.DrawLine(linePen, new Point(currentX, 0), new Point(currentX, centerY));
            }
            
            // Horizontal line to the expander/content
            var expanderX = currentX + IndentWidth / 2;
            drawingContext.DrawLine(linePen, new Point(currentX, centerY), new Point(expanderX, centerY));
        }

    }
}