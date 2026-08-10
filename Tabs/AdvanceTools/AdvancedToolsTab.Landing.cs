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
        // ── Landing: two Winhance-style entry rows ─────────────────────────

        private void BuildLanding()
        {
            _landingPanel.Children.Add(MakeEntryCard(
                "\uE958", "Windows Installation Media Utility",
                "Create Custom Windows Installation Media",
                enabled: true, onClick: ShowWizard));

            _landingPanel.Children.Add(MakeEntryCard(
                "\uE943", "Create Autounattend XML",
                "Generate an autounattend.xml based on your current Akari Tool selections to customize Windows during installation.",
                enabled: true, onClick: ShowGenerator));
        }

        private Border MakeEntryCard(string glyph, string title, string description, bool enabled, Action? onClick)
        {
            var card = new Border
            {
                Background = TweakHelpers.CardBackground(),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 14),
                Opacity = enabled ? 1.0 : 0.55,
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = TweakHelpers.IconNeutral,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0),
            };
            Grid.SetColumn(icon, 0);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
            });
            if (!enabled)
            {
                titleRow.Children.Add(new Border
                {
                    Background = BrushFrom("#3A2228"),
                    CornerRadius = TweakHelpers.CardRadius,
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "COMING SOON",
                        FontFamily = MonoFont,
                        FontSize = 9,
                        Foreground = TweakHelpers.TextPrimary,
                    }
                });
            }
            text.Children.Add(titleRow);
            text.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
            });
            Grid.SetColumn(text, 1);

            var chevron = new TextBlock
            {
                Text = "\uE76C",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = TweakHelpers.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
            };
            Grid.SetColumn(chevron, 2);

            row.Children.Add(icon);
            row.Children.Add(text);
            row.Children.Add(chevron);
            card.Child = row;

            if (enabled && onClick != null)
            {
                card.Tapped += (_, _) => onClick();
                card.PointerEntered += (_, _) => card.BorderBrush = TweakHelpers.HairlineHover;
                card.PointerExited += (_, _) => card.BorderBrush = TweakHelpers.Token("AkariOverlayMedium");
            }
            return card;
        }

    }
}
