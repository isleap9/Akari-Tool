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
    // ── Useful Tools ──────────────────────────────────────────────────
    //
    //  net8 AkariOSTab.GpuTools.cs BuildToolsContent, ported near line-for-line.
    //  All 14 buttons are ToolFetchService.LaunchAsync external-tool download+launch
    //  (fetch-to-cache + spawn a process); no in-app writes, registry, or elevation.
    //  Rendered as the last plain Option-A card (net8 Build() order). Reuses the
    //  Phase-18 AddButtonGrid.
    private void BuildToolsContent(StackPanel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = "Tools are downloaded once and cached locally. First launch requires an internet connection.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 0, 4, 6)
        });

        AddButtonGrid(panel, new (string, Action)[]
        {
            ("Autoruns",         () => _ = ToolFetchService.LaunchAsync("Autoruns",          Service!)),
            ("Devmanview",       () => _ = ToolFetchService.LaunchAsync("DevManView",        Service!)),
            ("Serviwin",         () => _ = ToolFetchService.LaunchAsync("ServiWin",          Service!)),
            ("InSpectre",        () => _ = ToolFetchService.LaunchAsync("InSpectre",         Service!)),
            ("MouseTester",      () => _ = ToolFetchService.LaunchAsync("MouseTester",       Service!)),
            ("CRU",              () => _ = ToolFetchService.LaunchAsync("CRU",               Service!)),
            ("AUTO DSCP",        () => _ = ToolFetchService.LaunchAsync("AutoDSCP",          Service!)),
            ("DISM++",           () => _ = ToolFetchService.LaunchAsync("DismPP",            Service!)),
            ("Dev. Cleanup",     () => _ = ToolFetchService.LaunchAsync("DeviceCleanup",     Service!)),
            ("Interrupt AFPT",   () => _ = ToolFetchService.LaunchAsync("InterruptAffinity", Service!)),
            ("HIDUSB",           () => _ = ToolFetchService.LaunchAsync("HidUsbF",           Service!)),
            ("MeasureSleep",     () => _ = ToolFetchService.LaunchAsync("MeasureSleep",      Service!)),
            ("Process Explorer", () => _ = ToolFetchService.LaunchAsync("ProcessExplorer",   Service!)),
            ("ReservedCPUSets",  () => _ = ToolFetchService.LaunchAsync("ReservedCpuSets",   Service!)),
        });
    }

    // Shell-execute helper (net8 AkariOSTab.xaml.cs RunShellProcess) — opens a URL
    // or launches an external target via the shell. Used by AMD "Driver Download".
    private static void RunShellProcess(string fileName, string arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RunShellProcess error: {ex.Message}"); }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  STUB SECTIONS  (unported net8 Build() calls — visible, not dropped)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Renders a visible placeholder card for a net8 section not yet ported, so the page
    /// mirrors net8's card order and the remaining work is obvious on-screen. Replaced by
    /// the real builder when its sub-section is signed off.
    /// </summary>
    private void BuildStubSection(StackPanel panel, string title, string contents)
    {
        var card = new Border
        {
            Background      = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush     = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(20, 16, 20, 16),
            Margin          = new Thickness(0, 0, 0, 12),
            Opacity         = 0.72
        };

        var inner = new StackPanel();

        inner.Children.Add(new TextBlock
        {
            Text       = $"\U0001F512  {title} — not yet ported",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorSecondaryBrush")
        });
        inner.Children.Add(new TextBlock
        {
            Text         = $"Placeholder scaffold (Phase 11). Will contain: {contents}.",
            FontSize     = 12,
            Foreground   = Res("TextFillColorTertiaryBrush"),
            Margin       = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = inner;
        panel.Children.Add(card);
    }
}
