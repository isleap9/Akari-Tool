using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AkariTool.Tabs
{
    /// <summary>
    /// The drift banner: one row under the title bar, shown only when a scan
    /// found tweaks that no longer match what Akari wrote.
    ///
    /// Wording is deliberately conservative. Drift detection cannot prove
    /// Windows Update caused a change — the user may have changed it in
    /// Settings themselves. The banner reports the observation, not a culprit.
    /// </summary>
    public static class DriftBanner
    {
        /// <summary>
        /// Builds the banner for <paramref name="result"/>.
        /// <paramref name="onReview"/> fires from the primary button,
        /// <paramref name="onDismiss"/> from the close glyph.
        /// </summary>
        public static Border Build(DriftScanResult result, Action onReview, Action onDismiss)
        {
            var grid = new Grid { Margin = new Thickness(14, 0, 8, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // glyph
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // review
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // dismiss

            // ── warning glyph ────────────────────────────────────────────
            var glyph = new TextBlock
            {
                Text = "!",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = TweakHelpers.WarnFg,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var glyphRing = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = TweakHelpers.CardRadius,
                BorderThickness = new Thickness(1),
                BorderBrush = TweakHelpers.WarnFg,
                Child = glyph,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            Grid.SetColumn(glyphRing, 0);
            grid.Children.Add(glyphRing);

            // ── message ──────────────────────────────────────────────────
            int n = result.Drifted.Count;
            var text = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = TweakHelpers.TextPrimary,
                FontSize = 12.5,
            };
            text.Inlines.Add(new System.Windows.Documents.Run(
                n == 1 ? "1 tweak no longer matches what Akari set"
                       : $"{n} tweaks no longer match what Akari set"));
            text.Inlines.Add(new System.Windows.Documents.Run("   " + BuildDetail(result))
            {
                Foreground = TweakHelpers.TextMuted,
                FontSize = 11.5,
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            // ── review ───────────────────────────────────────────────────
            var review = new Button
            {
                Content = "Review",
                Padding = new Thickness(14, 4, 14, 5),
                Margin = new Thickness(12, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            review.Click += (_, _) => onReview();
            Grid.SetColumn(review, 2);
            grid.Children.Add(review);

            // ── dismiss ──────────────────────────────────────────────────
            var dismiss = new Button
            {
                Content = "✕",
                Width = 26,
                Height = 24,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = TweakHelpers.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Dismiss until next launch",
            };
            dismiss.Click += (_, _) => onDismiss();
            Grid.SetColumn(dismiss, 3);
            grid.Children.Add(dismiss);

            return new Border
            {
                Background = TweakHelpers.WarnBg,
                BorderBrush = TweakHelpers.WarnBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 7, 8, 7),
                Child = grid,
            };
        }

        private static string BuildDetail(DriftScanResult r)
        {
            var parts = new List<string>();
            if (r.RevertedCount > 0) parts.Add($"{r.RevertedCount} reverted to Windows default");
            if (r.ChangedCount  > 0) parts.Add($"{r.ChangedCount} changed to another value");
            return parts.Count == 0 ? "" : "·  " + string.Join("  ·  ", parts);
        }
    }
}
