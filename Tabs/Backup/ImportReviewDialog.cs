using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    public sealed class ImportReviewDialog : Wpf.Ui.Controls.FluentWindow
    {
        private readonly List<(string Id, CheckBox Box)> _checks = new();

        /// <summary>Ids checked when "Apply Selected" was clicked.</summary>
        public HashSet<string> SelectedIds { get; } = new();

        public ImportReviewDialog(IReadOnlyList<TweakRegistry.PreviewEntry> differing, int unknown)
        {
            Title = "Review Import";
            Width = 620;
            Height = 560;
            MinWidth = 480;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = TweakHelpers.CardBg;
            ResizeMode = ResizeMode.CanResize;
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.None;

            // outer shell: WPF-UI title bar on top, dialog body below
            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new Wpf.Ui.Controls.TitleBar
            {
                Title = "Review Import",
                ShowMinimize = false,
                ShowMaximize = false,
            };
            Grid.SetRow(titleBar, 0);
            shell.Children.Add(titleBar);

            var root = new Grid { Margin = new Thickness(20, 8, 20, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(root, 1);
            shell.Children.Add(root);

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
                    Content = list,
                },
            };
            Grid.SetRow(card, 2);
            root.Children.Add(card);

            // buttons
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
            };

            var cancel = MakeButton("Cancel", primary: false);
            cancel.Click += (_, _) => { DialogResult = false; Close(); };

            var apply = MakeButton("Apply Selected", primary: true);
            apply.Margin = new Thickness(10, 0, 0, 0);
            apply.Click += (_, _) =>
            {
                SelectedIds.Clear();
                foreach (var (id, box) in _checks)
                    if (box.IsChecked == true) SelectedIds.Add(id);
                DialogResult = true;
                Close();
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(apply);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Content = shell;
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

        private static Button MakeButton(string label, bool primary)
        {
            return new Wpf.Ui.Controls.Button
            {
                Content = label,
                FontSize = 13,
                Padding = new Thickness(16, 7, 16, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                Appearance = primary
                    ? Wpf.Ui.Controls.ControlAppearance.Primary
                    : Wpf.Ui.Controls.ControlAppearance.Secondary,
            };
        }
    }
}
