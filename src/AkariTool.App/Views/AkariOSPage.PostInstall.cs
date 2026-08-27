using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.AkariOS;
using AkariTool.Core.Models.ShaderCache;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;
using AkariTool.Core.Competitive;

namespace AkariTool.Views;

public sealed partial class AkariOSPage
{
    // ══════════════════════════════════════════════════════════════════════
    //  POSTINSTALL BANNER  (net8 AkariOSTab.PostInstall.cs, ported near line-for-line)
    // ══════════════════════════════════════════════════════════════════════

    private void BuildPostInstallBanner(StackPanel panel)
    {
        bool installed = PostInstallService.IsFullyInstalled;

        var banner = new Border
        {
            Background      = installed ? Res("SystemFillColorSuccessBackgroundBrush") : Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush     = installed ? Res("SystemFillColorSuccessBrush") : Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
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
                Text       = "\u2713  PostInstall folder detected",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("SystemFillColorSuccessBrush")
            });
            textStack.Children.Add(new TextBlock
            {
                Text       = $"Found at {PostInstallService.LocalRoot} — all features available.",
                FontSize   = 12,
                Foreground = Res("SystemFillColorSuccessBrush"),
                Margin     = new Thickness(0, 2, 0, 0)
            });
        }
        else
        {
            textStack.Children.Add(new TextBlock
            {
                Text       = "\u26a0  PostInstall folder not found",
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("SystemFillColorCautionBrush")
            });
            textStack.Children.Add(new TextBlock
            {
                Text       = "GPU tools, network tweaks, and tool launchers require C:\\PostInstall.\n" +
                             "Click Download to fetch it from GitHub (~30 MB).",
                FontSize   = 12,
                Foreground = Res("TextFillColorSecondaryBrush"),
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
                downloadBtn.Content   = "Downloading\u2026";

                bool ok = await PostInstallService.EnsurePostInstallAsync(Service);

                if (ok)
                {
                    // Replace banner with success state
                    banner.Background   = Res("SystemFillColorSuccessBackgroundBrush");
                    banner.BorderBrush  = Res("SystemFillColorSuccessBrush");
                    textStack.Children.Clear();
                    textStack.Children.Add(new TextBlock
                    {
                        Text       = "\u2713  PostInstall downloaded successfully",
                        FontSize   = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Res("SystemFillColorSuccessBrush")
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text       = $"Saved to {PostInstallService.LocalRoot} — all features now available.",
                        FontSize   = 12,
                        Foreground = Res("SystemFillColorSuccessBrush"),
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
