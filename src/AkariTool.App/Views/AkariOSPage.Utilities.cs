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
    //  UTILITIES  (net8 AkariOSTab.Account.cs BuildUtilitiesPanel — Account /
    //  Interface / System, ported near line-for-line)
    //
    //  Option A (card-wrapper choice 1): three plain standalone bordered cards; the
    //  collapsible BuildSectionCard factory (incl. Account's defaultExpanded:true) is
    //  deferred to the shared factory step. AccountService / SystemUtilities are
    //  already ported; no dialogs, no pickers. All action buttons WRITE system state —
    //  only the GetDisplayName pre-fill is a read.
    // ══════════════════════════════════════════════════════════════════════

    private void BuildUtilitiesPanel(StackPanel panel)
    {
        panel.Children.Add(BuildUtilityCard("Account",   BuildAccountContent));
        panel.Children.Add(BuildUtilityCard("Interface", BuildInterfaceContent));
        panel.Children.Add(BuildUtilityCard("System",    BuildSystemUtilContent));
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
        if (panel.Children.Count > 1)   // > 1: the card's title header is always child 0
            panel.Children.Add(new Border
            {
                Background = Res("DividerStrokeColorDefaultBrush"),
                Height = 1,
                Margin = new Thickness(-20, 12, -20, 12)
            });

        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush")
        });
        info.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
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
            Foreground = Res("TextFillColorPrimaryBrush")
        });
        info.Children.Add(new TextBlock
        {
            Text = "Set the name Windows shows on the lock screen, Start menu, and account tile for the current user. This does not rename the account or move your user folder.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
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
