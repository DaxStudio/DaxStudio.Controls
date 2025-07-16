using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.Controls
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

        private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnIndentWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnIsLastChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnHasChildrenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnAncestorLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnLineStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        private static void OnLineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeLineControl)d).InvalidateVisual();
        }

        const int topOffset = -2;
        const int bottomOffset = 2;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            
            if (Level == 0) return;

            var pen = new Pen(LineStroke, LineThickness);
            pen.DashStyle = new DashStyle(new double[] { 2, 1 },2);
            var centerY = ActualHeight / 2;

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
                        drawingContext.DrawLine(pen, new Point(x, 0 + topOffset), new Point(x, ActualHeight + bottomOffset));
                    }
                }
                Debug.WriteLine($"Rendering TreeLineControl at Level {Level}, LastChild: {IsLastChild}, HasChildren: {HasChildren}, Ancestors: {ancestors.Count()} ");
            }
            else
            {
                Debug.WriteLine($"Rendering TreeLineControl at Level {Level}, LastChild: {IsLastChild}, HasChildren: {HasChildren}, Ancestors: 0 ");

            }

                // Draw lines for current level
                var currentX = Level * IndentWidth - IndentWidth / 2;

            // Vertical line (up to center or full height)
            if (!IsLastChild)
            {
                // Draw full vertical line if not last child
                drawingContext.DrawLine(pen, new Point(currentX, 0 + topOffset), new Point(currentX, ActualHeight + bottomOffset));
            }
            else
            {
                // Draw only up to center if last child
                drawingContext.DrawLine(pen, new Point(currentX, 0), new Point(currentX, centerY));
            }

            // Horizontal line to the expander/content
            //var expanderX = Level * IndentWidth - 8; // 8 is half the expander width
            var expanderX = currentX + IndentWidth / 2;
            drawingContext.DrawLine(pen, new Point(currentX, centerY), new Point(expanderX, centerY));
        }
    }
}