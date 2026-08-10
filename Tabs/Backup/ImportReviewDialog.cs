using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs.Backup
{
    /// <summary>
    /// Import Review Mode — modeled on Winhance's Config Import Review.
    /// Shows only the entries that differ from the current system state,
    /// each with a checkbox (default checked). Returns the set of Ids the
    /// user chose to apply via <see cref="SelectedIds"/> when DialogResult
    /// is true.
    /// </summary>
    public sealed class ImportReviewDialog
    {
        private readonly List<(string Id, CheckBox Box)> _checks = new();
        private readonly ContentDialog _dialog;

        /// <summary>Ids checked when "Apply Selected" was clicked.</summary>
        public HashSet<string> SelectedIds { get; } = new();

        public ImportReviewDialog(IReadOnlyList<TweakRegistry.PreviewEntry> differing, int unknown)
        {
            // MIGRATION: was a WPF-UI FluentWindow with its own TitleBar shown via
            // ShowDialog(). WinUI has no modal secondary window, so this is now a
            // ContentDialog; its own chrome replaces the shell + title bar.
            var root = new Grid { MinWidth = 560 };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // header
            var header = new TextBlock
            {
                Text = "Review changes before applying",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                Margin = new Thickness(0, 0, 0, 4),
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            string sub = $"{differing.Count} tweak(s) in this file differ from your current settings. " +
                         "Uncheck anything you want to keep as-is.";
            if (unknown > 0) sub += $" {unknown} entr{(unknown == 1 ? "y is" : "ies are")} not recognized by this version and will be skipped.";
            var subText = new TextBlock
            {
                Text = sub,
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
            };
            Grid.SetRow(subText, 1);
            root.Children.Add(subText);

            // scrollable card with the differing rows
            var list = new StackPanel();
            foreach (var e in differing)
                list.Children.Add(MakeRow(e));

            var card = new Border
            {
                Background = TweakHelpers.CardBackground(),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Padding = new Thickness(6, 4, 6, 4),
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 380,   // ContentDialog has no window height to fill
                    Content = list,
                },
            };
            Grid.SetRow(card, 2);
            root.Children.Add(card);

            // Buttons are the ContentDialog's own Primary/Close; the Apply handler
            // harvests the ticked Ids exactly as the WPF version did.
            _dialog = new ContentDialog
            {
                Title = "Review Import",
                Content = root,
                PrimaryButtonText = "Apply Selected",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };
            _dialog.PrimaryButtonClick += (_, _) =>
            {
                SelectedIds.Clear();
                foreach (var (id, box) in _checks)
                    if (box.IsChecked == true) SelectedIds.Add(id);
            };
        }

        /// <summary>Shows the review dialog. True when "Apply Selected" was pressed.</summary>
        public async Task<bool> ShowAsync()
        {
            _dialog.XamlRoot = AkariDialogs.XamlRoot;
            if (_dialog.XamlRoot is null) return false;   // no visual tree -> treat as cancel
            return await _dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private UIElement MakeRow(TweakRegistry.PreviewEntry e)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(2, 3, 2, 3),
                CornerRadius = TweakHelpers.CardRadius,
                Background = TweakHelpers.Token("AkariOverlayMedium"),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var box = new CheckBox
            {
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(box, 0);
            grid.Children.Add(box);
            _checks.Add((e.Id, box));

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = e.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
            });

            var change = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            change.Children.Add(new TextBlock
            {
                Text = e.CurrentDisplay,
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
            });
            change.Children.Add(new TextBlock
            {
                Text = "  →  ",
                FontSize = 12,
                Foreground = TweakHelpers.IconNeutral,
            });
            change.Children.Add(new TextBlock
            {
                Text = e.ImportedDisplay,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
            });
            stack.Children.Add(change);

            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            row.Child = grid;
            return row;
        }

    }
}
