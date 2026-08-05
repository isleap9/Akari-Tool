using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // POSTINSTALL BANNER
        // ══════════════════════════════════════════════════════════════════════

        private void BuildPostInstallBanner(StackPanel panel)
        {
            bool installed = PostInstallService.IsFullyInstalled;

            var banner = new Border
            {
                Background      = installed ? TweakHelpers.SuccessBg : TweakHelpers.CardBg,
                BorderBrush     = installed ? TweakHelpers.SuccessBorder : TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 0, 0, 16)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel();
            Grid.SetColumn(textStack, 0);

            if (installed)
            {
                textStack.Children.Add(new TextBlock
                {
                    Text       = "✓  PostInstall folder detected",
                    FontSize   = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TweakHelpers.SuccessFg
                });
                textStack.Children.Add(new TextBlock
                {
                    Text       = $"Found at {PostInstallService.LocalRoot} — all features available.",
                    FontSize   = 12,
                    Foreground = TweakHelpers.SuccessFg,
                    Margin     = new Thickness(0, 2, 0, 0)
                });
            }
            else
            {
                textStack.Children.Add(new TextBlock
                {
                    Text       = "⚠  PostInstall folder not found",
                    FontSize   = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TweakHelpers.AccentText
                });
                textStack.Children.Add(new TextBlock
                {
                    Text       = "GPU tools, network tweaks, and tool launchers require C:\\PostInstall.\n" +
                                 "Click Download to fetch it from GitHub (~30 MB).",
                    FontSize   = 12,
                    Foreground = TweakHelpers.TextSecondary,
                    Margin     = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            row.Children.Add(textStack);

            if (!installed)
            {
                var downloadBtn = new Button
                {
                    Content            = "Download PostInstall",
                    Style              = (Style)Application.Current.Resources["AccentButtonStyle"],
                    VerticalAlignment  = VerticalAlignment.Center,
                    Margin             = new Thickness(12, 0, 0, 0)
                };

                downloadBtn.Click += async (_, _) =>
                {
                    downloadBtn.IsEnabled = false;
                    downloadBtn.Content   = "Downloading…";

                    bool ok = await PostInstallService.EnsurePostInstallAsync(Service!);

                    if (ok)
                    {
                        // Replace banner with success state
                        banner.Background   = TweakHelpers.SuccessBg;
                        banner.BorderBrush  = TweakHelpers.SuccessBorder;
                        textStack.Children.Clear();
                        textStack.Children.Add(new TextBlock
                        {
                            Text       = "✓  PostInstall downloaded successfully",
                            FontSize   = 13,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = TweakHelpers.SuccessFg
                        });
                        textStack.Children.Add(new TextBlock
                        {
                            Text       = $"Saved to {PostInstallService.LocalRoot} — all features now available.",
                            FontSize   = 12,
                            Foreground = TweakHelpers.SuccessFg,
                            Margin     = new Thickness(0, 2, 0, 0)
                        });
                        row.Children.Remove(downloadBtn);
                    }
                    else
                    {
                        downloadBtn.Content   = "Retry Download";
                        downloadBtn.IsEnabled = true;
                        Service?.Log("[POSTINSTALL] Download incomplete — check your internet connection and try again.");
                    }
                };

                Grid.SetColumn(downloadBtn, 1);
                row.Children.Add(downloadBtn);
            }

            banner.Child = row;
            panel.Children.Add(banner);
        }

    }
}
