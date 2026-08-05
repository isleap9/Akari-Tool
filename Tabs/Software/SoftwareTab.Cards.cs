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

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class SoftwareTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // CARD GRID BUILDERS (Winhance card view)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildCardSection(StackPanel panel, string title, List<AppDefinition> apps,
            List<AppCard> cardRegistry, List<CardSection> sectionRegistry, bool isWindowsApps)
        {
            var header = new TextBlock
            {
                Text = title,
                FontFamily = DisplayFont,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                Margin = new Thickness(2, 18, 0, 10)
            };
            panel.Children.Add(header);

            // Responsive UniformGrid — columns from available width (old App
            // Installer pattern). Search rebuilds Children, because UniformGrid
            // still reserves cells for collapsed children.
            var grid = new UniformGrid();
            grid.SizeChanged += (_, _) =>
            {
                int cols = Math.Max(1, (int)(grid.ActualWidth / CardMinWidth));
                if (grid.Columns != cols) grid.Columns = cols;
            };
            panel.Children.Add(grid);

            var cards = new List<AppCard>();
            foreach (var app in apps)
            {
                var card = BuildAppCard(app, isWindowsApps);
                cards.Add(card);
                cardRegistry.Add(card);
                grid.Children.Add(card.Card);
            }

            sectionRegistry.Add(new CardSection { Header = header, Grid = grid, Cards = cards });
        }

        private AppCard BuildAppCard(AppDefinition app, bool isWindowsApps)
        {
            // ── Checkbox (top-left, like Winhance) ────────────────────────────
            // MIGRATION: the crimson "AppCheckBox" ControlTemplate style was not
            // ported (trigger-based); native WinUI CheckBox chrome is used, and its
            // checked fill already comes from the crimson accent overrides in App.xaml.
            var check = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 12, 0),
                IsChecked = app.IsSelected
            };

            // ── Avatar (crimson letter tile — icon pipeline TBD) ──────────────
            var avatar = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = TweakHelpers.Token("AkariOverlaySoft"),
                BorderBrush = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = app.Name[0].ToString().ToUpperInvariant(),
                    FontFamily = DisplayFont,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFrom("#FF8A94"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // ── Name row (+ website link) ─────────────────────────────────────
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text = app.Name,
                FontFamily = DisplayFont,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (!string.IsNullOrEmpty(app.WebsiteUrl))
            {
                var link = new TextBlock
                {
                    Text = "\uE8A7",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10.5,
                    Foreground = TweakHelpers.TextMuted,
                    Margin = new Thickness(7, 1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTipService.SetToolTip(link, app.WebsiteUrl);
                var url = app.WebsiteUrl;
                link.Tapped += async (s, e) =>
                {
                    e.Handled = true; // don't toggle the card
                    await Service!.RunAction(new UrlAction(url));
                };
                link.PointerEntered += (_, _) => link.Foreground = TweakHelpers.Accent;
                link.PointerExited += (_, _) => link.Foreground = TweakHelpers.TextMuted;
                nameRow.Children.Add(link);
            }

            // ── Description ───────────────────────────────────────────────────
            var desc = new TextBlock
            {
                Text = app.Description,
                FontSize = 11.5,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 48,
                Margin = new Thickness(0, 3, 0, 0)
            };

            // ── Badge row (Installed / Warning / Permanent) ───────────────────
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            var installedBadge = MakePill("\uE73E  Installed", "AkariSuccessBgColor", "AkariSuccessBorderColor", "AkariSuccessFgColor");
            installedBadge.Visibility = Visibility.Collapsed;
            badges.Children.Add(installedBadge);

            if (app.HasInstabilityWarning)
            {
                var warn = MakePill("\uE7BA  Warning", "AkariWarnBgColor", "AkariWarnBorderColor", "AkariWarnFgColor");
                ToolTipService.SetToolTip(warn, "Removing this can affect Windows components that depend on it.");
                badges.Children.Add(warn);
            }

            if (isWindowsApps && !app.CanBeReinstalled)
            {
                var perm = MakePill("\uE7C1  Permanent", "AkariDangerBgColor", "AkariDangerBorderColor", "AkariDangerFgColor");
                ToolTipService.SetToolTip(perm, "Once removed, this item can't be reinstalled.");
                badges.Children.Add(perm);
            }

            var textCol = new StackPanel();
            textCol.Children.Add(nameRow);
            textCol.Children.Add(desc);
            textCol.Children.Add(badges);

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(check, 0);
            Grid.SetColumn(avatar, 1);
            Grid.SetColumn(textCol, 2);
            content.Children.Add(check);
            content.Children.Add(avatar);
            content.Children.Add(textCol);

            // ── Card border — click anywhere toggles selection ────────────────
            var card = new Border
            {
                MinHeight = 104,
                Margin = new Thickness(5),
                Background = TweakHelpers.CardBackground(),
                BorderBrush = app.IsSelected ? TweakHelpers.Accent : TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Child = content
            };

            void SyncVisual()
            {
                card.BorderBrush = app.IsSelected ? TweakHelpers.Accent : TweakHelpers.Hairline;
                check.IsChecked = app.IsSelected;
            }

            card.PointerEntered += (_, _) => { if (!app.IsSelected) card.BorderBrush = TweakHelpers.HairlineHover; };
            card.PointerExited += (_, _) => SyncVisual();
            card.Tapped += (_, _) =>
            {
                app.IsSelected = !app.IsSelected;
                SyncVisual();
                RefreshCounts();
            };
            check.Click += (_, _) =>
            {
                app.IsSelected = check.IsChecked == true;
                SyncVisual();
                RefreshCounts();
            };

            // ── Real app icon (async; crimson letter tile is the fallback) ────
            _ = LoadIconAsync(app, avatar);

            return new AppCard { App = app, Card = card, Check = check, InstalledBadge = installedBadge };
        }

        // Fire-and-forget: swap the letter tile for the real icon once it loads.
        // Icons are purely cosmetic, so any failure is swallowed — never crash the tab.
        private async Task LoadIconAsync(AppDefinition app, Border avatar)
        {
            try
            {
                var bmp = await AppIconService.GetIconAsync(app);
                if (bmp is null) return; // keep the letter fallback

                avatar.DispatcherQueue.TryEnqueue(() =>
                {
                    avatar.Background      = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    avatar.BorderThickness = new Thickness(0);
                    avatar.Child = new Image
                    {
                        Source  = bmp,
                        Stretch = Stretch.Uniform,
                        Margin  = new Thickness(4)
                    };
                });
            }
            catch { /* icons are cosmetic — must never crash the tab */ }
        }

    }
}
