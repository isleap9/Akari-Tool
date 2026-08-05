using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // ACCOUNT
        // Replaces the PostInstall "Change Name" batch file, targeting the
        // current user instead of a hardcoded Administrator.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Page 2 of the AkariOS tab — hardcoded PostInstall utilities.</summary>
        private void BuildUtilitiesPanel(StackPanel panel)
        {
            // Expanded by default: a single collapsed card would leave the page
            // looking empty. Collapsing it still persists.
            panel.Children.Add(BuildSectionCard("Account",   BuildAccountContent, defaultExpanded: true));
            panel.Children.Add(BuildSectionCard("Interface", BuildInterfaceContent));
            panel.Children.Add(BuildSectionCard("System",    BuildSystemUtilContent));
        }

        private void BuildInterfaceContent(StackPanel panel)
        {
            AddActionRow(panel, "Alt-Tab Style",
                "Classic shows the compact XP-style switcher; Immersive is the modern Windows 11 " +
                "switcher with desktop hover. Explorer restarts to apply.",
                ("Classic",   () => SystemUtilities.SetAltTabClassic(true,  Service!.Log)),
                ("Immersive", () => SystemUtilities.SetAltTabClassic(false, Service!.Log)));
        }

        private void BuildSystemUtilContent(StackPanel panel)
        {
            AddActionRow(panel, "Resync System Time",
                "Temporarily starts the Windows Time service, resyncs the clock against the " +
                "configured time server, then disables it again. Useful on AkariOS where w32time " +
                "is disabled by default.",
                ("Resync Now", () => _ = SystemUtilities.ResyncTimeAsync(Service!.Log)));

            AddActionRow(panel, "DMA Remapping",
                "Controls kernel DMA protection on devices that support it. Disabling can reduce " +
                "device latency; enabling restores the security protection. Restart required.",
                ("Enable",  () => SystemUtilities.SetDmaRemapping(true,  Service!.Log)),
                ("Disable", () => SystemUtilities.SetDmaRemapping(false, Service!.Log)));
        }

        /// <summary>Action row: title + description + one or more buttons, matching the card's other rows.</summary>
        private void AddActionRow(StackPanel panel, string title, string desc,
                                  params (string Label, Action OnClick)[] actions)
        {
            if (panel.Children.Count > 0)
                panel.Children.Add(new Border
                {
                    Background = TweakHelpers.Token("AkariOverlayStrong"),
                    Height = 1,
                    Margin = new Thickness(-20, 12, -20, 12)
                });

            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            info.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(0, 2, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (label, onClick) in actions)
            {
                var btn = new Button
                {
                    Content = label,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(18, 10, 18, 10),
                    FontSize = 13
                };
                btn.Click += (_, _) =>
                {
                    try { onClick(); }
                    catch (Exception ex) { Service?.Log($"ERROR {label}: {ex.Message}"); }
                };
                buttons.Children.Add(btn);
            }

            info.Children.Add(buttons);
            panel.Children.Add(info);
        }

        private void BuildAccountContent(StackPanel panel)
        {
            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = "Change Display Name",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            info.Children.Add(new TextBlock
            {
                Text = "Set the name Windows shows on the lock screen, Start menu, and account tile for the current user. This does not rename the account or move your user folder.",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(0, 2, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            var entry = new Grid();
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new TextBox
            {
                PlaceholderText          = "Enter a new display name",
                Text                     = AccountService.GetDisplayName() ?? string.Empty,
                FontSize                 = 13,
                Height                   = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(box, 0);
            entry.Children.Add(box);

            var apply = new Button
            {
                Content   = "Apply",
                Margin    = new Thickness(8, 0, 0, 0),
                Padding   = new Thickness(18, 10, 18, 10),
                FontSize  = 13,
                IsEnabled = !string.IsNullOrWhiteSpace(box.Text)
            };
            Grid.SetColumn(apply, 1);
            entry.Children.Add(apply);

            box.TextChanged += (_, _) => apply.IsEnabled = !string.IsNullOrWhiteSpace(box.Text);

            apply.Click += (_, _) =>
            {
                try
                {
                    if (AccountService.SetDisplayName(box.Text.Trim(), Service!.Log))
                        box.Text = AccountService.GetDisplayName() ?? box.Text;
                }
                catch (Exception ex) { Service?.Log($"ERROR Change Display Name: {ex.Message}"); }
            };

            info.Children.Add(entry);
            panel.Children.Add(info);
        }
    }
}
