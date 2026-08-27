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

/// <summary>
/// AkariOS page — faithful code-behind port of net8 <c>AkariOSTab</c> (+ its partials).
/// Logic is the already-ported backing services; this file adapts net8's imperative UI:
/// TweakHelpers tokens → Fluent <c>ThemeResource</c> brushes (per prior waves),
/// <c>Service</c> → <see cref="ToolService"/>.
///
/// ⚠ PORT IN PROGRESS. net8 <c>Build()</c> makes four calls:
///   1. <c>BuildPostInstallBanner</c>  — PORTED (Phase 11).
///   2. <c>BuildGamingTweaksCard</c>   — STUB (Service Preset, Playbook, BCD, Competitive,
///                                        Shader Cache, Gaming Tweaks, NVIDIA, AMD).
///   3. <c>BuildUtilitiesPanel</c>     — STUB (Account, Interface, System).
///   4. <c>BuildUsefulToolsCard</c>    — STUB (Useful Tools).
/// The three unported calls render a VISIBLE stub card (not silently dropped) so the page
/// composition matches net8's order and the remaining work is obvious on-screen.
///
/// No Defender code is referenced. When the Service Preset section is ported its copy
/// ("Defender is always protected", "Defender and boot-critical services are never
/// touched") must be carried verbatim — it is accurate against the preset guard set.
/// </summary>
public sealed partial class AkariOSPage : Page
{
    public AkariOSViewModel ViewModel { get; }

    /// <summary>Shared headless tool service (net8 <c>Service</c>).</summary>
    private ToolService Service => ViewModel.Tool;

    // App-local dialog helper (net8 AkariDialogs analogue). Its ConfirmContentAsync maps
    // 1:1 to net8's, modulo the (title, content) arg order. XamlRoot is wired at startup
    // in MainWindow; TweakDialogs fails safe (returns false) if it isn't ready yet.
    private readonly TweakDialogs _dialogs;

    // App-local file dialog service (Win32 IFileOpenDialog via AkariFileService) — the
    // elevation-safe picker; a raw WinRT picker throws COMException 0x80004005 under the
    // app's requireAdministrator launch (Phase 4). Used by Competitive ▸ Browse….
    private readonly IFileService _files;

    // net8 AkariOSTab._servicePresetLabel — the italic "Current: …" status line.
    private TextBlock? _servicePresetLabel;

    public AkariOSPage()
    {
        ViewModel = ServiceLocator.GetService<AkariOSViewModel>();
        _dialogs  = ServiceLocator.GetService<TweakDialogs>();
        _files    = ServiceLocator.GetService<IFileService>();

        InitializeComponent();

        Build();
    }

    // net8 AkariOSTab.Build() — one continuous page of collapsible cards. Order preserved.
    private void Build()
    {
        BuildPostInstallBanner(RootPanel);                                     // PORTED (Phase 11)
        BuildPlaybookServicesSection(RootPanel);                               // AME Playbook service-list picker (AkariOS-volume gated) — reuses the native ServicesPreset pipeline
        BuildPresetsCard(RootPanel);                                           // PORTED (Phase 15: Service Preset + Playbook + BCD grouped under the shared warning banner)
        BuildShaderCacheCard(RootPanel);                                       // PORTED (Phase 16)
        RootPanel.Children.Add(BuildUtilityCard("Competitive Mode", BuildCompetitiveContent)); // PORTED (Phase 21, sub-part A: picker; B–D stubbed)
        RootPanel.Children.Add(BuildUtilityCard("Gaming Tweaks", BuildGamingToggleContent)); // PORTED (Phase 20)
        RootPanel.Children.Add(BuildUtilityCard("NVIDIA", BuildNvidiaContent)); // PORTED (Phase 18)
        RootPanel.Children.Add(BuildUtilityCard("AMD",    BuildAmdContent));    // PORTED (Phase 18)
        BuildUtilitiesPanel(RootPanel);                                        // PORTED (Phase 17: Account / Interface / System)
        RootPanel.Children.Add(BuildUtilityCard("Useful Tools", BuildToolsContent)); // PORTED (Phase 19) — last card, net8 order
    }

    // Plain Option-A card + title header, standing in for net8's collapsible
    // BuildSectionCard (deferred to the shared factory step).
    private Border BuildUtilityCard(string title, Action<StackPanel> builder)
    {
        var card = new Border
        {
            Background      = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush     = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Margin          = new Thickness(0, 0, 0, 12),
        };
        var inner = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        card.Child = inner;

        inner.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 14)
        });

        builder(inner);

        return card;
    }

    // ── Brush helpers (matching AdvancedToolsPage) ─────────────────────────────────
    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>Literal-colour brush from a hex string (#RRGGBB or #AARRGGBB). Used for the
    /// two net8 preset-label tints that must stay distinct from the crimson accent:
    /// Daily blue (#4CC2FF) and brand crimson (#E0142A).</summary>
    private static Brush Hex(string hex)
    {
        var s = hex.TrimStart('#');
        byte a = 0xFF, r, g, b;
        if (s.Length == 8)
        {
            a = Convert.ToByte(s.Substring(0, 2), 16);
            r = Convert.ToByte(s.Substring(2, 2), 16);
            g = Convert.ToByte(s.Substring(4, 2), 16);
            b = Convert.ToByte(s.Substring(6, 2), 16);
        }
        else
        {
            r = Convert.ToByte(s.Substring(0, 2), 16);
            g = Convert.ToByte(s.Substring(2, 2), 16);
            b = Convert.ToByte(s.Substring(4, 2), 16);
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
    }
}
