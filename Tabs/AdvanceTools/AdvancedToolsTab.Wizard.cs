using System.IO;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AdvancedTools
{
    public partial class AdvancedToolsTab
    {
        // ═════════════════════════════════════════════════════════════════
        //  Wizard
        // ═════════════════════════════════════════════════════════════════

        private void BuildWizard()
        {
            // ── Back row: ‹ back button + wizard title + cancel ────────────
            var backRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var backBtn = MakeButton("");
            var backContent = new StackPanel { Orientation = Orientation.Horizontal };
            backContent.Children.Add(new TextBlock
            {
                Text = "\uE72B",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
            });
            backContent.Children.Add(new TextBlock { Text = "Back", VerticalAlignment = VerticalAlignment.Center });
            backBtn.Content = backContent;
            backBtn.Click += (_, _) => ShowLanding();

            var wizardTitle = new TextBlock
            {
                Text = "Windows Installation Media Utility",
                FontFamily = DisplayFont,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
            };
            Grid.SetColumn(wizardTitle, 1);

            _cancelBtn = MakeButton("Cancel", "#FF7A88");
            _cancelBtn.Visibility = Visibility.Collapsed;
            _cancelBtn.Click += (_, _) => _cts?.Cancel();
            Grid.SetColumn(_cancelBtn, 2);

            backRow.Children.Add(backBtn);
            backRow.Children.Add(wizardTitle);
            backRow.Children.Add(_cancelBtn);
            _wizardPanel.Children.Add(backRow);

            BuildStep1();
            BuildStep2();
            BuildStep3();
            BuildStep4();
            UpdateStepStates();
        }

        // ── Step card factory ──────────────────────────────────────────────

        private StackPanel MakeStepCard(int index, string title, string glyph, string initialStatus)
        {
            var ui = new StepUi();
            _steps[index - 1] = ui;

            ui.Card = new Border
            {
                Background = TweakHelpers.CardBackground(),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 12),
            };

            var outer = new StackPanel();

            // Header: (n) icon Title ····· status ▾
            var header = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ui.BadgeText = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = TweakHelpers.TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ui.Badge = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = TweakHelpers.CardRadius,
                Background = TweakHelpers.Token("AkariOverlaySoft"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Child = ui.BadgeText,
            };
            Grid.SetColumn(ui.Badge, 0);

            var iconAndTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            iconAndTitle.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = TweakHelpers.IconNeutral,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0),
            });
            iconAndTitle.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(iconAndTitle, 1);

            ui.Status = new TextBlock
            {
                Text = initialStatus,
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 12, 0),
            };
            Grid.SetColumn(ui.Status, 3);

            ui.Chevron = new TextBlock
            {
                Text = index == 1 ? "\uE70E" : "\uE70D", // step 1 starts expanded
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = TweakHelpers.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(ui.Chevron, 4);

            header.Children.Add(ui.Badge);
            header.Children.Add(iconAndTitle);
            header.Children.Add(ui.Status);
            header.Children.Add(ui.Chevron);

            ui.Body = new StackPanel
            {
                Margin = new Thickness(36, 14, 0, 0),
                Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed,
            };

            header.Tapped += (_, _) =>
            {
                if (!ui.Card.IsHitTestVisible) return;   // see UpdateStepStates
                var open = ui.Body.Visibility == Visibility.Visible;
                ui.Body.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                ui.Chevron.Text = open ? "\uE70D" : "\uE70E";
            };

            outer.Children.Add(header);
            outer.Children.Add(ui.Body);
            ui.Card.Child = outer;
            _wizardPanel.Children.Add(ui.Card);
            return ui.Body;
        }

        // ── Small element helpers ──────────────────────────────────────────

        private Button MakeButton(string label, string color = "#F2F2F4")
        {
            var b = new Button
            {
                Content = label,
                Foreground = BrushFrom(color),
                BorderBrush = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
            };
            _actionButtons.Add(b);
            return b;
        }

        private Button MakePrimaryButton(string label)
        {
            var b = new Button
            {
                Content = label,
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            _actionButtons.Add(b);
            return b;
        }

        private TextBlock MakeHint(string text) => new()
        {
            Text = text,
            FontSize = 12.5,
            Foreground = TweakHelpers.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };

        private TextBlock MakePathText(string text) => new()
        {
            Text = text,
            FontSize = 12.5,
            Foreground = TweakHelpers.TextSecondary,
            FontFamily = MonoFont,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private static StackPanel HRow(params UIElement[] children)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var c in children) p.Children.Add(c);
            return p;
        }

    }
}
