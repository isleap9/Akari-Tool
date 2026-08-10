using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Minimal WinUI replacement for WPF's <c>System.Windows.Controls.Primitives.UniformGrid</c>,
    /// which has no in-box WinUI equivalent.
    ///
    /// MIGRATION: the Software tab builds a *responsive* card grid — it recomputes
    /// <see cref="Columns"/> from the panel's width on SizeChanged and re-adds
    /// children when the search filter changes. Reimplementing that on
    /// ItemsRepeater/UniformGridLayout would have meant restructuring the card and
    /// search code (Children → ItemsSource), so instead this panel reproduces the
    /// exact API the existing code already uses (<c>Columns</c>, <c>Children</c>,
    /// <c>SizeChanged</c>, <c>ActualWidth</c>) and the call sites stay unchanged.
    ///
    /// Layout matches WPF's UniformGrid for the way it is used here: every cell is
    /// the same size, children fill left-to-right then top-to-bottom, and
    /// Collapsed children still occupy a cell (the Software tab relies on this —
    /// see the comment in SoftwareTab.UiHelpers about rebuilding Children).
    /// </summary>
    public sealed class UniformGrid : Panel
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(UniformGrid),
                new PropertyMetadata(0, OnLayoutPropertyChanged));

        /// <summary>Fixed column count; 0 = derive a square-ish grid from the child count.</summary>
        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register(nameof(Rows), typeof(int), typeof(UniformGrid),
                new PropertyMetadata(0, OnLayoutPropertyChanged));

        /// <summary>Fixed row count; 0 = derived from the child count and Columns.</summary>
        public int Rows
        {
            get => (int)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((UniformGrid)d).InvalidateMeasure();

        private (int Rows, int Cols) Resolve(int count)
        {
            int cols = Columns;
            int rows = Rows;

            if (cols <= 0 && rows <= 0)
            {
                cols = (int)Math.Ceiling(Math.Sqrt(count));
                if (cols < 1) cols = 1;
                rows = (int)Math.Ceiling(count / (double)cols);
            }
            else if (cols <= 0)
            {
                cols = (int)Math.Ceiling(count / (double)rows);
            }
            else if (rows <= 0)
            {
                rows = (int)Math.Ceiling(count / (double)cols);
            }

            return (Math.Max(rows, 1), Math.Max(cols, 1));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = Children.Count;
            if (count == 0) return new Size(0, 0);

            var (rows, cols) = Resolve(count);

            // Infinite width (e.g. inside a horizontally-unbounded parent) would make
            // the cell width infinite, so fall back to measuring children naturally.
            double cellW = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / cols;
            double cellH = double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height / rows;

            double maxChildH = 0;
            var childConstraint = new Size(cellW, double.PositiveInfinity);
            foreach (var child in Children)
            {
                child.Measure(childConstraint);
                if (child.DesiredSize.Height > maxChildH) maxChildH = child.DesiredSize.Height;
            }

            // Row height is driven by the tallest child (uniform rows), which is what
            // the card grid wants; width consumes whatever the parent offers.
            double totalW = double.IsInfinity(cellW) ? 0 : availableSize.Width;
            double totalH = double.IsInfinity(cellH) ? maxChildH * rows : Math.Min(maxChildH * rows, double.MaxValue);
            return new Size(totalW, totalH);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = Children.Count;
            if (count == 0) return finalSize;

            var (rows, cols) = Resolve(count);

            double cellW = finalSize.Width / cols;
            double cellH = rows > 0 ? finalSize.Height / rows : finalSize.Height;

            int i = 0;
            foreach (var child in Children)
            {
                int r = i / cols;
                int c = i % cols;
                child.Arrange(new Rect(c * cellW, r * cellH, cellW, cellH));
                i++;
            }

            return finalSize;
        }
    }
}
