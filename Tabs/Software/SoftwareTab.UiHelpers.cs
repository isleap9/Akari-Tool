// SoftwareTab — 1:1 functional port of Winhance's Software section, card-grid UI.
//
//   • Windows Apps  (panel "Bloatware"): 56 removable apps + 10 legacy
//     capabilities + 7 optional features as selectable cards with live
//     installed status, Winhance-style badges (Installed / Warning /
//     Permanent), select-all controls, and Install / Remove / Refresh.
//     Removals run the generated BloatRemoval.ps1 pipeline (+ dedicated
//     Edge/OneDrive scripts) and persist via a SYSTEM startup task.
//   • External Apps (panel "AppInstaller"): 193 winget apps in category
//     sections (Browsers, Compression, …), same card UI.
//   • Debloat       (panel "Debloat"): hosts Tabs/Debloat/DebloatTab —
//     the script-based one-click debloat groups.
//
// Cards mirror Winhance's card view: checkbox + avatar + name/description
// + badge row, click anywhere to select, responsive column count.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class SoftwareTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private Button MakeActionButton(string glyphAndText, bool primary)
        {
            var btn = new Button
            {
                Style = (Style)FindResource(primary ? "DlActionBtn" : "DlCancelBtn"),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var parts = glyphAndText.Split("  ", 2);
            stack.Children.Add(new TextBlock
            {
                Text = parts[0],
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            stack.Children.Add(new TextBlock { Text = parts[1], VerticalAlignment = VerticalAlignment.Center });
            btn.Content = stack;
            return btn;
        }

        private Border CountBadge(TextBlock inner) => new()
        {
            Background = TweakHelpers.Token("AkariOverlaySoft"),
            BorderBrush = TweakHelpers.Hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 5, 15, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = inner
        };

        private TextBlock MakeStatusText() => new()
        {
            Foreground = TweakHelpers.TextSecondary,
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // Resolve a pill colour: token key (starts with "Akari") → live managed brush; else literal hex.
        private static Brush PillBrush(string s) =>
            s.StartsWith("#") ? BrushFrom(s) : Services.ThemeService.ManagedBrush(s);

        private Border MakePill(string text, string bg, string border, string fg)
        {
            var tb = new TextBlock { FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = PillBrush(fg) };
            var parts = text.Split("  ", 2);
            if (parts.Length == 2)
            {
                tb.Inlines.Add(new System.Windows.Documents.Run(parts[0])
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10
                });
                tb.Inlines.Add(new System.Windows.Documents.Run("  " + parts[1]));
            }
            else tb.Text = text;

            return new Border
            {
                Background = PillBrush(bg),
                BorderBrush = PillBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 2, 9, 3),
                Margin = new Thickness(0, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = tb
            };
        }

        private void AddSearchRow(StackPanel panel, string placeholderText, Action<string> onSearch)
        {
            // WPF-UI TextBox with built-in placeholder + leading search icon.
            var box = new Wpf.Ui.Controls.TextBox
            {
                PlaceholderText          = placeholderText,
                Icon                     = new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Search24),
                ClearButtonEnabled       = true,
                FontSize                 = 13,
                Height                   = 36,
                Width                    = 280,
                HorizontalAlignment      = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin                   = new Thickness(0, 0, 0, 4),
                CaretBrush               = TweakHelpers.Accent,
                SelectionBrush           = TweakHelpers.Accent,
            };
            box.TextChanged += (_, _) => onSearch(box.Text.Trim());
            panel.Children.Add(box);
        }

        private FrameworkElement SelectAllRow(Action<bool> onAll, Action<bool> onInstalled, Action<bool> onNotInstalled)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 10, 0, 4) };

            CheckBox Make(string label, Action<bool> handler)
            {
                var cb = new CheckBox
                {
                    Style = (Style)Application.Current.Resources["AppCheckBox"],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 18, 0),
                    Content = new TextBlock
                    {
                        Text = label,
                        FontSize = 12,
                        Foreground = TweakHelpers.TextSecondary,
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                cb.Click += (_, _) => handler(cb.IsChecked == true);
                return cb;
            }

            row.Children.Add(Make("Select all", onAll));
            row.Children.Add(Make("Select installed", onInstalled));
            row.Children.Add(Make("Select not installed", onNotInstalled));
            return row;
        }

        // ── Selection / search / status plumbing ──────────────────────────────

        private void SetSelected(AppDefinition app, bool selected)
        {
            app.IsSelected = selected;
            var card = _windowsCards.FirstOrDefault(c => c.App == app) ??
                       _externalCards.FirstOrDefault(c => c.App == app);
            if (card != null)
            {
                card.Check.IsChecked = selected;
                card.Card.BorderBrush = selected ? TweakHelpers.Accent : TweakHelpers.Hairline;
            }
        }

        private void SetSelection(List<AppCard> cards, Func<AppDefinition, bool> predicate, bool selected)
        {
            foreach (var card in cards.Where(c => c.Visible && predicate(c.App)))
            {
                card.App.IsSelected = selected;
                card.Check.IsChecked = selected;
                card.Card.BorderBrush = selected ? TweakHelpers.Accent : TweakHelpers.Hairline;
            }
            RefreshCounts();
        }

        /// <summary>
        /// Search filter. UniformGrid reserves cells for collapsed children,
        /// so matching cards are re-added to each section's grid instead.
        /// </summary>
        private void ApplySearch(string query, List<CardSection> sections)
        {
            bool empty = query.Length == 0;

            foreach (var section in sections)
            {
                section.Grid.Children.Clear();
                bool any = false;
                foreach (var card in section.Cards)
                {
                    card.Visible = empty ||
                        card.App.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        card.App.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                    if (card.Visible)
                    {
                        section.Grid.Children.Add(card.Card);
                        any = true;
                    }
                }
                section.Header.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
                section.Grid.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshBadges(List<AppCard> cards)
        {
            foreach (var card in cards)
                card.InstalledBadge.Visibility = card.App.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshCounts()
        {
            _waSelectedCount.Text = $"{_windowsApps.Count(a => a.IsSelected)} selected";
            _eaSelectedCount.Text = $"{_externalApps.Count(a => a.IsSelected)} selected";
        }
    }
}
