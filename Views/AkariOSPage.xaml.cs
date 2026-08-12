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
using WinUI.Framework.IoC;
using WinUI.Framework.Services;

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

    // ══════════════════════════════════════════════════════════════════════
    //  PRESETS CARD  (net8 AkariOSTab.GamingTweaksCard.cs BuildGamingTweaksCard,
    //  the presets block lines 24-73 — one grouped card: shared warning banner
    //  header, then Service Preset → Playbook → BCD with hairline separators.
    //  Ported line-for-line; the three *Section builders below are unchanged and
    //  simply re-parented from their former standalone Option-A cards into this
    //  container (Phase 15 reassembly).
    //
    //  ⚠ The banner's 2nd TextBlock carries the 2nd Defender-protection string
    //  ("Defender and boot-critical services are never touched") — ported verbatim.
    // ══════════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════════
    //  AKARIOS PLAYBOOK SERVICES  (real feature — AkariOS-volume gated)
    //
    //  Lets a user ON AN ACTUAL AkariOS-built machine pick one of the AME Playbook's
    //  own service-list .reg files (C:\PostInstall\Services\*.reg) and apply it through
    //  Akari Tool's EXISTING native service pipeline (ServicesPreset.ApplyPlaybookFileAsync
    //  → the same Apply + ApplyLockedServicesAsync + StampPreset the built-in presets use).
    //  No MinSudo, no regedit.exe.
    //
    //  GATING (mandatory, runs first): ServicesPreset.IsAkariOsVolume() — the C: volume
    //  label must be exactly "AkariOS". This is STRONGER than "PostInstall exists": a stock
    //  Windows user who downloaded PostInstall via the app's own button must NOT get this.
    //  Three states, each rendered disabled-with-a-reason (never hidden), each with distinct
    //  copy so they are never confused:
    //    • not AkariOS            → "Only available on AkariOS-built systems."
    //    • AkariOS, no .reg files → "No service lists found in C:\PostInstall\Services."
    //    • AkariOS, files present → dropdown populated; Apply enabled once a list is picked.
    //  The "not AkariOS" state is deliberately NOT the PostInstall-missing banner/download
    //  pattern — that is a different condition.
    //
    //  ⚠ Independent of the Service Preset section below it — this neither touches nor
    //  depends on Apply Gaming / Daily / Restore Stock. Status is reported on THIS section's
    //  own line (see the label note in the report); the shared "Current: …" preset label is
    //  intentionally left untouched.
    //
    //  Placed between the PostInstall banner and the presets card (whose first element is the
    //  shared "166 service startup types" warning). Mirrors BuildServicePresetSection's shape:
    //  an info column (title + description + status line) beside a right-aligned control column,
    //  wrapped in a standalone card like the page's other cards, with a trailing hairline divider.
    // ══════════════════════════════════════════════════════════════════════

    // Per-user setting: the RAW filename (e.g. "AkariOS-Daily-services.reg") of the last
    // dropdown choice. Stored raw — never the display name — so it survives a label rename.
    private const string PlaybookServicesSettingKey = "AkariOSPlaybookServicesFile";

    private void BuildPlaybookServicesSection(StackPanel panel)
    {
        var card = new Border
        {
            Background      = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush     = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Margin          = new Thickness(0, 0, 0, 16),
        };
        var inner = new StackPanel();
        card.Child = inner;

        var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        Grid.SetColumn(info, 0);
        info.Children.Add(new TextBlock { Text = "AkariOS Playbook Services", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
        info.Children.Add(new TextBlock
        {
            Text = "Apply a service-startup list shipped by the AkariOS AME Playbook " +
                   "(C:\\PostInstall\\Services). Machine-wide changes to service startup types — restart required.",
            FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        // This section's own status/reason line (italic, like the Service Preset "Current:"
        // label). Carries the gated-off reason OR the post-apply result — this section reports
        // its own state and does NOT feed the shared preset-detection label.
        var status = new TextBlock
        {
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            Margin = new Thickness(0, 4, 0, 0),
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
        };
        info.Children.Add(status);

        // ── Control column: dropdown + explicit Apply button ──────────────
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(controls, 1);

        var combo = new ComboBox
        {
            PlaceholderText          = "Select a service list",
            FontSize                 = 13,
            MinWidth                 = 200,
            MinHeight                = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin                   = new Thickness(0, 0, 6, 0),
        };

        // Explicit Apply — never on SelectionChanged (arrow-keying must not fire a
        // machine-wide, restart-required write). Styled like the Service Preset buttons.
        var applyBtn = new Button
        {
            Content = "Apply",
            Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
            VerticalAlignment = VerticalAlignment.Center,
        };

        controls.Children.Add(combo);
        controls.Children.Add(applyBtn);

        section.Children.Add(info);
        section.Children.Add(controls);
        inner.Children.Add(section);
        inner.Children.Add(new Border { Background = Res("DividerStrokeColorDefaultBrush"), Height = 1 });
        panel.Children.Add(card);

        // ── Gate: volume label FIRST, then folder/file presence ───────────
        if (!ServicesPreset.IsAkariOsVolume())
        {
            status.Text     = "Only available on AkariOS-built systems.";
            combo.IsEnabled = false;
            applyBtn.IsEnabled = false;
            return;
        }

        var files = ServicesPreset.DiscoverPlaybookServiceFiles();
        if (files.Count == 0)
        {
            status.Text     = $"No service lists found in {ServicesPreset.PlaybookServicesDir}.";
            combo.IsEnabled = false;
            applyBtn.IsEnabled = false;
            return;
        }

        // Curated display names on top of dynamic discovery: known files get a friendly label
        // (ServicesPreset.PlaybookDisplayName), unknown files still show under their raw name so
        // a newly-shipped playbook file never silently vanishes. The display string is COSMETIC —
        // shown[idx].Path is the real on-disk file that parses/applies, and shown[idx].File (the
        // raw filename) is what persistence stores/compares.
        var shown = files
            .Select(p => (Path: p, File: Path.GetFileName(p)))
            .ToList();

        foreach (var s in shown)
            combo.Items.Add(ServicesPreset.PlaybookDisplayName(Path.GetFileNameWithoutExtension(s.File)));

        // Fix 1 — restore the last-chosen file. Match on the RAW filename, not an index: the
        // discovered list can change as files are added/removed. Set BEFORE wiring the handler so
        // the restore itself doesn't re-persist. Saved file gone → leave nothing selected.
        var settings = ServiceLocator.GetService<ISettingsService>();
        var savedFile = settings.Get(PlaybookServicesSettingKey, "");
        if (!string.IsNullOrEmpty(savedFile))
        {
            int savedIdx = shown.FindIndex(s => string.Equals(s.File, savedFile, StringComparison.OrdinalIgnoreCase));
            if (savedIdx >= 0) combo.SelectedIndex = savedIdx;
        }

        applyBtn.IsEnabled = combo.SelectedIndex >= 0;   // may already be pre-selected from settings

        combo.SelectionChanged += (_, _) =>
        {
            applyBtn.IsEnabled = combo.SelectedIndex >= 0;
            // Persist the RAW filename on every selection change (mirrors the old Companion —
            // "remember what's picked", not "what was last applied").
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < shown.Count)
                settings.Set(PlaybookServicesSettingKey, shown[combo.SelectedIndex].File);
        };

        applyBtn.Click += async (_, _) =>
        {
            int idx = combo.SelectedIndex;
            if (idx < 0 || idx >= shown.Count) return;

            var path    = shown[idx].Path;                              // real file — never the display name
            var name    = Path.GetFileNameWithoutExtension(path);
            var display = ServicesPreset.PlaybookDisplayName(name);     // cosmetic label for the status line

            combo.IsEnabled = false;
            applyBtn.IsEnabled = false;
            var prevContent = applyBtn.Content;
            applyBtn.Content = "Applying\u2026";
            status.Text = $"Applying {display}\u2026";
            status.Foreground = Res("TextFillColorSecondaryBrush");

            try
            {
                bool ok = await ServicesPreset.ApplyPlaybookFileAsync(path, Service!);
                status.Text = ok
                    ? $"Applied {display}. Restart to take full effect."
                    : $"Apply failed for {display} — see log.";
            }
            catch (Exception ex)
            {
                status.Text = $"Apply failed: {ex.Message}";
                Service?.Log($"[SERVICES] Playbook apply failed for {name}: {ex.Message}");
            }
            finally
            {
                applyBtn.Content = prevContent;
                combo.IsEnabled = true;
                applyBtn.IsEnabled = combo.SelectedIndex >= 0;
            }
        };
    }

    private void BuildPresetsCard(StackPanel panel)
    {
        // Outer card (net8 presetsCard). Elevation gradient → flat card stroke;
        // shadow deferred to the cosmetic pass, consistent with every prior wave.
        var presetsCard = new Border
        {
            Background      = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush     = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Margin          = new Thickness(0, 0, 0, 16),
        };
        var presetsInner = new StackPanel();
        presetsCard.Child = presetsInner;

        // ── Shared warning banner (net8 presetWarning) — top header region ──
        var presetWarning = new Border
        {
            Background       = Res("SystemFillColorCautionBackgroundBrush"),
            BorderBrush      = Res("SystemFillColorCautionBrush"),
            BorderThickness  = new Thickness(0, 0, 0, 1),
            CornerRadius     = new CornerRadius(8, 8, 0, 0),
            Padding          = new Thickness(20, 10, 20, 10)
        };
        var presetWarningText = new StackPanel();
        presetWarningText.Children.Add(new TextBlock
        {
            Text = "⚠  Applies machine-wide changes to 166 service startup types. Restart required.",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("SystemFillColorCautionBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        presetWarningText.Children.Add(new TextBlock
        {
            Text = "Gaming also disables Windows Update and Explorer ISO mounting — choose Daily if you need either. Defender and boot-critical services are never touched.",
            FontSize = 12,
            Foreground = Res("SystemFillColorCautionBrush"),
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        presetWarning.Child = presetWarningText;
        presetsInner.Children.Add(presetWarning);

        // ── The three preset sections, separated by hairlines (net8 order) ──
        BuildServicePresetSection(presetsInner);
        presetsInner.Children.Add(new Border { Background = Res("DividerStrokeColorDefaultBrush"), Height = 1 });
        BuildPlaybookSection(presetsInner);
        presetsInner.Children.Add(new Border { Background = Res("DividerStrokeColorDefaultBrush"), Height = 1 });
        BuildBcdSection(presetsInner);

        panel.Children.Add(presetsCard);
    }

    private void BuildServicePresetSection(StackPanel inner)
    {
        var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        Grid.SetColumn(info, 0);
        info.Children.Add(new TextBlock { Text = "Service Preset", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
        info.Children.Add(new TextBlock
        {
            Text = "Apply a service configuration preset. Gaming disables 166 services (telemetry, Bluetooth, Xbox, Hyper-V guests, search, and more) including Windows Update and ISO mounting. Daily keeps the same optimizations but leaves Windows Update and ISO mounting working. Defender is always protected.",
            FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        _servicePresetLabel = new TextBlock { FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 4, 0, 0), FontStyle = Windows.UI.Text.FontStyle.Italic };
        info.Children.Add(_servicePresetLabel);
        SyncPresetLabel();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(buttons, 1);

        var gamingBtn = new Button { Content = "Apply Gaming", Style = (Style)Application.Current.Resources["AccentButtonStyle"], Margin = new Thickness(0, 0, 6, 0) };
        gamingBtn.Click += async (_, _) => { await ServicesPreset.ApplyAkariGaming(Service!); SyncPresetLabel(); };

        var dailyBtn = new Button { Content = "Apply Daily", Style = (Style)Application.Current.Resources["AccentButtonStyle"], Margin = new Thickness(0, 0, 6, 0) };
        dailyBtn.Click += async (_, _) => { await ServicesPreset.ApplyAkariDaily(Service!); SyncPresetLabel(); };

        var restoreBtn = new Button { Content = "Restore Stock" };
        restoreBtn.Click += async (_, _) => { await ServicesPreset.ApplyStockDefault(Service!); SyncPresetLabel(); };

        buttons.Children.Add(gamingBtn);
        buttons.Children.Add(dailyBtn);
        buttons.Children.Add(restoreBtn);
        section.Children.Add(info);
        section.Children.Add(buttons);
        inner.Children.Add(section);
    }

    private void SyncPresetLabel() => ApplyPresetLabel(_servicePresetLabel);

    // ── Shared preset label rendering ──────────────────────────────────
    // Detection is authoritative for drift; the stamp only supplies the human
    // name once detection agrees on the family. The AkariOS and stock buttons
    // write identical service values, so the stamp is the ONLY way to tell
    // "AkariOS Daily" from "Daily (stock Windows)".
    private void ApplyPresetLabel(TextBlock? label)
    {
        if (label is null) return;

        var result = SystemStateReader.DetectServicePresetDetailed();
        var stamp  = ServicesPreset.ReadPresetStamp();

        bool stampAgrees = result.Preset switch
        {
            SystemStateReader.ServicePreset.AkariDaily  => stamp is "AkariDaily" or "Daily",
            SystemStateReader.ServicePreset.AkariGaming => stamp is "AkariGaming" or "Gaming"
                                                                 or "AkariMinimal" or "Minimal",
            SystemStateReader.ServicePreset.Stock       => stamp is "AkariStock" or "Stock",
            _ => false,
        };

        (string Text, Brush Fg) v = stampAgrees
            ? stamp switch
            {
                "AkariGaming"  => ("Current: AkariOS Gaming",          Res("SystemFillColorSuccessBrush")),
                "Gaming"       => ("Current: Gaming (stock Windows)",  Res("SystemFillColorSuccessBrush")),
                "AkariDaily"   => ("Current: AkariOS Daily",           Hex("#4CC2FF")),
                "Daily"        => ("Current: Daily (stock Windows)",   Hex("#4CC2FF")),
                "AkariMinimal" => ("Current: AkariOS Minimal",         Res("SystemFillColorSuccessBrush")),
                "Minimal"      => ("Current: Minimal (stock Windows)", Res("SystemFillColorSuccessBrush")),
                "AkariStock"   => ("Current: Windows Stock",           Hex("#E0142A")),
                _              => ("Current: Windows Stock",           Hex("#E0142A")),
            }
            : result.Preset switch
            {
                SystemStateReader.ServicePreset.AkariGaming => ("Current: Gaming",        Res("SystemFillColorSuccessBrush")),
                SystemStateReader.ServicePreset.AkariDaily  => ("Current: Daily",         Hex("#4CC2FF")),
                SystemStateReader.ServicePreset.Stock       => ("Current: Windows Stock", Hex("#E0142A")),
                SystemStateReader.ServicePreset.Mixed       =>
                    ($"Current: Mixed ({result.Drift.Count} of {result.Total} probes differ)", Res("SystemFillColorCautionBrush")),
                _ => ("Current: Unknown", Res("TextFillColorSecondaryBrush")),
            };

        label.Text       = v.Text;
        label.Foreground = v.Fg;

        // Drift detail on hover so "Mixed" is diagnosable without a rebuild.
        ToolTipService.SetToolTip(label, result.Drift.Count == 0
            ? null
            : string.Join(Environment.NewLine,
                result.Drift.Select(d => $"{d.Service}: {d.Actual} -> expected {d.Expected}")));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PLAYBOOK  (net8 AkariOSTab.Services.cs BuildPlaybookSection, ported near
    //  line-for-line)
    //
    //  Rendered inside the grouped presets card (BuildPresetsCard, Phase 15),
    //  between Service Preset and BCD under the shared warning-banner header.
    //
    //  ⚠ NO CONFIRMATION DIALOG — verbatim net8 behavior (isleap Phase 13 decision).
    //  Apply All is destructive and PARTLY irreversible (the ⓘ line: memory
    //  compression + DISM changes are NOT restore-point-recoverable) yet fires on a
    //  single click. Not an oversight — a deliberate deferral; a confirm gate, if
    //  ever wanted, is its own explicit change, not this migration step. The restore
    //  point + all tweak writes live inside the already-ported RestorePointHelper /
    //  PlaybookTweaks; this section only calls them.
    // ══════════════════════════════════════════════════════════════════════

    private void BuildPlaybookSection(StackPanel inner)
    {
        var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        Grid.SetColumn(info, 0);
        info.Children.Add(new TextBlock { Text = "AkariOS Playbook Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
        info.Children.Add(new TextBlock
        {
            Text = "Applies 30 registry tweaks, 15 ETW autologger disables, 12 IFEO process priorities, " +
                   "66 scheduled task disables, filesystem tweaks, and telemetry opt-outs from the AkariOS playbook. " +
                   "A system restore point is created automatically before applying.",
            FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        info.Children.Add(new TextBlock
        {
            Text = "ℹ  Registry/task changes are restore-point-recoverable. Memory compression and DISM changes are not.",
            FontSize = 11, Foreground = Res("SystemFillColorCautionBrush"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(btns, 1);

        var applyBtn = new Button { Content = "Apply All", Style = (Style)Application.Current.Resources["AccentButtonStyle"], Margin = new Thickness(0, 0, 6, 0) };
        applyBtn.Click += async (_, _) =>
        {
            applyBtn.IsEnabled = false;
            try
            {
                Service!.Log("[RESTORE] Creating system restore point...");
                bool rpOk = await RestorePointHelper.EnsureRestorePointAsync(Service!);
                Service!.Log(rpOk ? "[RESTORE] ✓ Restore point ready." : "[RESTORE] ⚠ Could not create restore point — proceeding anyway.");
                await PlaybookTweaks.ApplyAllAsync(Service!);
            }
            finally { applyBtn.IsEnabled = true; }
        };

        var undoBtn = new Button { Content = "Undo All" };
        undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await PlaybookTweaks.UndoAllAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

        btns.Children.Add(applyBtn);
        btns.Children.Add(undoBtn);
        section.Children.Add(info);
        section.Children.Add(btns);
        inner.Children.Add(section);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  BCD  (net8 AkariOSTab.Services.cs BuildBcdSection, ported near line-for-line)
    //
    //  Rendered inside the grouped presets card (BuildPresetsCard, Phase 15), last of
    //  the three preset sections under the shared warning-banner header.
    //
    //  ⚠ NO CONFIRMATION DIALOG — verbatim net8 (matches Service Preset / Playbook).
    //  Apply runs bcdedit + `powercfg -h off` against live boot config; Restore replays
    //  the HKCU\Software\AkariTool\BcdBackup registry backup (or stock fallback). All of
    //  that lives inside the already-ported BcdBackup; this section only calls it.
    // ══════════════════════════════════════════════════════════════════════

    private void BuildBcdSection(StackPanel inner)
    {
        var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        Grid.SetColumn(info, 0);
        info.Children.Add(new TextBlock { Text = "BCD Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
        info.Children.Add(new TextBlock
        {
            Text = "Legacy boot menu (F8), disable dynamic tick (better timer resolution), " +
                   "disable recovery mode (faster boot), disable hibernation (removes hiberfil.sys). " +
                   "Current values are backed up to registry before applying.",
            FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        info.Children.Add(new TextBlock
        {
            // AccentText tint (net8 #FFFF8A94 dark). Hex() is single-value, so the light-theme
            // #FFB01020 is deferred to the cosmetic pass (isleap Phase 14 decision) — kept
            // distinct from Playbook's amber Caution ⓘ, which is a different token/job.
            Text = "⚠  BCD changes are NOT covered by System Restore — values are backed up internally for undo.",
            FontSize = 11, Foreground = Hex("#FF8A94"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
        });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(btns, 1);

        var applyBtn = new Button { Content = "Apply", Style = (Style)Application.Current.Resources["AccentButtonStyle"], Margin = new Thickness(0, 0, 6, 0) };
        applyBtn.Click += async (_, _) =>
        {
            applyBtn.IsEnabled = false;
            try { await BcdBackup.BackupAsync(Service!); await BcdBackup.ApplyAsync(Service!, new BcdTweakOptions()); }
            finally { applyBtn.IsEnabled = true; }
        };

        var undoBtn = new Button { Content = "Restore" };
        undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await BcdBackup.RestoreAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

        btns.Children.Add(applyBtn);
        btns.Children.Add(undoBtn);
        section.Children.Add(info);
        section.Children.Add(btns);
        inner.Children.Add(section);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SHADER CACHE CLEANER  (net8 AkariOSTab.ShaderCache.cs, ported near
    //  line-for-line)
    //
    //  Option A (isleap-approved, card-wrapper choice 1): plain standalone bordered
    //  card now; the collapsible BuildSectionCard factory is deferred to its own
    //  dedicated step covering all five remaining Gaming-card sections at once.
    //
    //  Scan is read-only (enumerate + FileInfo.Length); Clean deletes cache file
    //  contents via the already-ported ShaderCacheService. The confirm dialog is the
    //  first AkariDialogs→TweakDialogs swap: net8 AkariDialogs.ConfirmContentAsync
    //  (content, title, primaryText) → _dialogs.ConfirmContentAsync(title, content,
    //  primaryText) — arg order reversed; copy verbatim.
    // ══════════════════════════════════════════════════════════════════════

    private sealed class ShaderCacheRow
    {
        public required ShaderCacheTarget Target { get; init; }
        public required CheckBox          Box    { get; init; }
        public required TextBlock         Label  { get; init; }
    }

    private readonly List<ShaderCacheRow> _shaderRows = new();
    private Button?    _shaderRescanBtn;
    private Button?    _shaderCleanBtn;
    private TextBlock? _shaderStatus;
    private ProgressRing? _shaderRing;
    private bool _shaderScanStarted;

    private void BuildShaderCacheCard(StackPanel panel)
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
            Text = "Shader Cache Cleaner",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 14)
        });

        BuildShaderCacheContent(inner);

        panel.Children.Add(card);
    }

    private void BuildShaderCacheContent(StackPanel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = "Clears DirectX, NVIDIA, AMD, Intel and Steam per-game shader caches. " +
                   "Games will rebuild shaders on next launch, which may cause brief stutter the first time.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // ── One checkbox per target ───────────────────────────────────────
        // WinUI has no WrapPanel; these rows carry long "<target> — <size>" labels,
        // so they stack vertically (one per line) instead of wrapping.
        var boxes = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        foreach (var target in ShaderCacheService.GetTargets())
        {
            // Steam only appears when Steam is actually installed — an empty
            // Steam row would just be a permanently greyed "not found".
            if (target.Id == "steam" && !ShaderCacheService.IsSteamInstalled()) continue;

            var label = new TextBlock
            {
                Text = $"{target.DisplayName} — scanning…",
                FontSize = 12.5,
                Foreground = Res("TextFillColorPrimaryBrush"),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var box = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 22, 4),
                Content = label,
                // Nothing is selectable until the scan says what exists.
                IsEnabled = false
            };

            boxes.Children.Add(box);
            _shaderRows.Add(new ShaderCacheRow { Target = target, Box = box, Label = label });
        }

        panel.Children.Add(boxes);

        // ── Buttons + spinner ─────────────────────────────────────────────
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        _shaderRescanBtn = new Button
        {
            Content = "Rescan",
            Padding = new Thickness(18, 10, 18, 10),
            Margin  = new Thickness(0, 0, 8, 0),
            FontSize = 13,
            IsEnabled = false
        };
        _shaderRescanBtn.Click += (_, _) => _ = RunShaderScanAsync();
        actions.Children.Add(_shaderRescanBtn);

        _shaderCleanBtn = new Button
        {
            Content = "Clean Now",
            Padding = new Thickness(18, 10, 18, 10),
            Margin  = new Thickness(0, 0, 8, 0),
            FontSize = 13,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            IsEnabled = false
        };
        _shaderCleanBtn.Click += (_, _) => _ = RunShaderCleanAsync();
        actions.Children.Add(_shaderCleanBtn);

        _shaderRing = new ProgressRing
        {
            IsIndeterminate = true,
            Width = 20,
            Height = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        actions.Children.Add(_shaderRing);

        panel.Children.Add(actions);

        _shaderStatus = new TextBlock
        {
            Text = "Scanning…",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_shaderStatus);

        // The first scan is deferred to Loaded so page construction stays
        // synchronous — enumerating a multi-gigabyte cache would otherwise
        // stall the navigation that created this page.
        if (IsLoaded) OnShaderCacheLoaded(this, new RoutedEventArgs());
        else          Loaded += OnShaderCacheLoaded;
    }

    private void OnShaderCacheLoaded(object sender, RoutedEventArgs e)
    {
        if (_shaderScanStarted) return;   // Loaded fires again on re-parenting
        _shaderScanStarted = true;
        _ = RunShaderScanAsync();
    }

    // ── Scan ──────────────────────────────────────────────────────────────

    private async Task RunShaderScanAsync()
    {
        SetShaderBusy(true);
        if (_shaderStatus is not null) _shaderStatus.Text = "Scanning…";

        try
        {
            // Re-resolved every scan: a Steam library added since the page was
            // built would otherwise stay invisible.
            var targets = ShaderCacheService.GetTargets()
                                            .ToDictionary(t => t.Id, StringComparer.Ordinal);

            var toScan = _shaderRows
                .Select(r => targets.TryGetValue(r.Target.Id, out var t) ? t : r.Target)
                .ToList();

            var results = (await ShaderCacheService.ScanAsync(toScan))
                          .ToDictionary(r => r.TargetId, StringComparer.Ordinal);

            long total = 0;
            foreach (var row in _shaderRows)
            {
                if (!results.TryGetValue(row.Target.Id, out var res)) continue;

                if (res.Exists)
                {
                    row.Label.Text       = $"{row.Target.DisplayName} — {ShaderCacheService.FormatBytes(res.TotalBytes)}";
                    row.Label.Foreground = Res("TextFillColorPrimaryBrush");
                    row.Box.IsEnabled    = true;
                    row.Box.IsChecked    = true;
                    total += res.TotalBytes;
                }
                else
                {
                    row.Label.Text       = $"{row.Target.DisplayName} — not found";
                    row.Label.Foreground = Res("TextFillColorSecondaryBrush");
                    row.Box.IsChecked    = false;
                    row.Box.IsEnabled    = false;
                }
            }

            if (_shaderStatus is not null)
                _shaderStatus.Text = $"{ShaderCacheService.FormatBytes(total)} of shader cache found.";
        }
        catch (Exception ex)
        {
            if (_shaderStatus is not null) _shaderStatus.Text = $"Scan failed: {ex.Message}";
            Service?.Log($"ERROR Shader cache scan: {ex.Message}");
        }
        finally
        {
            SetShaderBusy(false);
        }
    }

    // ── Clean ─────────────────────────────────────────────────────────────

    private async Task RunShaderCleanAsync()
    {
        var selected = _shaderRows.Where(r => r.Box.IsChecked == true).ToList();
        if (selected.Count == 0)
        {
            if (_shaderStatus is not null) _shaderStatus.Text = "Select at least one cache to clean.";
            return;
        }

        // Sizes are measured again right before the prompt so the confirmation
        // quotes what is actually there, not a stale scan.
        SetShaderBusy(true);
        IReadOnlyList<ShaderCacheScanResult> sizes;
        try
        {
            sizes = await ShaderCacheService.ScanAsync(selected.Select(r => r.Target));
        }
        catch
        {
            sizes = Array.Empty<ShaderCacheScanResult>();
        }
        finally
        {
            SetShaderBusy(false);
        }

        long totalBytes = sizes.Sum(s => s.TotalBytes);
        string message =
            "The following shader caches will be cleared:\n\n" +
            string.Join("\n", selected.Select(r => "  • " + r.Target.DisplayName)) +
            $"\n\nAbout {ShaderCacheService.FormatBytes(totalBytes)} will be freed. " +
            "Games will rebuild their shaders on next launch.";

        if (ShaderCacheService.IsSteamRunning())
            message += "\n\nSteam is running. Close Steam and any games before cleaning to avoid errors.";

        if (!await ConfirmShaderCleanAsync(message)) return;

        SetShaderBusy(true);
        try
        {
            var progress = new Progress<string>(text =>
            {
                if (_shaderStatus is not null) _shaderStatus.Text = text;
            });

            var targets = selected.Select(r => r.Target).ToList();
            var results = await ShaderCacheService.CleanAsync(targets, progress);

            long freed   = results.Sum(r => r.BytesFreed);
            int  deleted = results.Sum(r => r.FilesDeleted);
            int  skipped = results.Sum(r => r.FilesSkipped);
            bool errored = results.Any(r => r.Error is not null);

            string status = $"Freed {ShaderCacheService.FormatBytes(freed)} across {deleted} files.";
            if (skipped > 0) status += $" ({skipped} files in use were skipped.)";
            if (errored)     status += " Some locations could not be accessed.";

            Service?.Log($"Shader cache cleaned — {status}");
            if (_shaderStatus is not null) _shaderStatus.Text = status;

            foreach (var r in results.Where(r => r.Error is not null))
                Service?.Log($"ERROR Shader cache ({r.TargetId}): {r.Error}");

            // Refresh so the labels drop to ~0 and reflect what is left behind.
            await RunShaderScanAsync();
            if (_shaderStatus is not null) _shaderStatus.Text = status;
        }
        catch (Exception ex)
        {
            if (_shaderStatus is not null) _shaderStatus.Text = $"Clean failed: {ex.Message}";
            Service?.Log($"ERROR Shader cache clean: {ex.Message}");
        }
        finally
        {
            // A finally block, not a trailing call: an exception above must never
            // leave the section permanently disabled.
            SetShaderBusy(false);
        }
    }

    /// <summary>
    /// Clean / Cancel confirmation. net8 went through AkariDialogs.ConfirmContentAsync;
    /// build #3 uses the app-local TweakDialogs.ConfirmContentAsync — same semantics,
    /// arg order reversed to (title, content, primaryText). Returns true iff "Clean".
    /// </summary>
    private async Task<bool> ConfirmShaderCleanAsync(string message)
    {
        return await _dialogs.ConfirmContentAsync(
            "Clean Shader Caches",
            new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            primaryText: "Clean");
    }

    // ── Busy state ────────────────────────────────────────────────────────

    private void SetShaderBusy(bool busy)
    {
        if (_shaderRescanBtn is not null) _shaderRescanBtn.IsEnabled = !busy;
        if (_shaderCleanBtn  is not null) _shaderCleanBtn.IsEnabled  = !busy;
        if (_shaderRing      is not null) _shaderRing.Visibility      = busy ? Visibility.Visible : Visibility.Collapsed;
    }

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

    // ══════════════════════════════════════════════════════════════════════
    //  COMPETITIVE MODE — Sub-part A: scaffold + game picker + file picker
    //  (net8 AkariOSTab.Competitive.cs, ported near line-for-line)
    //
    //  Session-scoped tweaks around a single game launch. This sub-part ports the
    //  shell + picker (combo/Browse/Create-Shortcut/Start) + read-only Steam game
    //  detection + the file-picker (FilePickers → IFileService, elevation-safe).
    //  Anti-cheat notice (B), option groups (C), and status/session state machine (D)
    //  are STUBBED below — filled in later signed-off sub-parts.
    // ══════════════════════════════════════════════════════════════════════

    // ── Game picker ───────────────────────────────────────────────────
    private ComboBox? _cmGameCombo;
    private Button?   _cmBrowseBtn;
    private Button?   _cmShortcutBtn;
    private Button?   _cmPrimaryBtn;
    private TextBlock? _cmPickerHint;
    private TextBlock? _cmLaunchInfo;
    private readonly List<DetectedGame> _cmGames = new();
    private bool _cmDetectionStarted;
    // Session-control state (_cmSelectedPath, _cmBusy, _cmCts, _cmActiveSchemeName) moved to
    // AkariOSViewModel (E1). The page reads them via ViewModel.SelectedPath/IsBusy/ActiveSchemeName.

    // ── Options (sub-part C) ──────────────────────────────────────────
    private CheckBox? _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                      _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                      _cmLaunchThroughSteam;
    private ComboBox? _cmPriorityLevel, _cmIoPriority, _cmCpuSets;
    private StackPanel? _cmPrioritySubOptions;

    // ── Status UI (sub-part D; session-control state lives in the VM since E1) ──
    private TextBlock? _cmStatusHeadline;
    private StackPanel? _cmStatusDetail;
    private DispatcherTimer? _cmElapsedTimer;

    private void BuildCompetitiveContent(StackPanel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = "Applies a set of temporary, session-scoped tweaks around a single game launch " +
                   "and undoes all of them when the game exits.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        BuildCompetitivePicker(panel);
        BuildCompetitiveAntiCheatNotice(panel);   // PORTED (sub-part B)
        BuildCompetitiveOptionGroups(panel);      // PORTED (sub-part C)
        BuildCompetitiveStatus(panel);            // PORTED (sub-part D)

        LoadCompetitiveOptionsIntoUi(CompetitivePrefs.LoadOptions());

        // The session-control machine lives in the VM (E1). Render its events; OnVmStateChanged
        // also picks up an already-active session on a mid-session page rebuild (E1 flag 5).
        ViewModel.Status += SetCompetitiveStatus;
        ViewModel.StateChanged += OnVmStateChanged;
        Unloaded += (_, _) =>
        {
            ViewModel.Status -= SetCompetitiveStatus;
            ViewModel.StateChanged -= OnVmStateChanged;
        };
        OnVmStateChanged();   // initial sync (net8 SyncCompetitiveControlStates) + timer if active

        // Detection walks every .exe under every Steam library — seconds of I/O — so it
        // is deferred to Loaded rather than run during page construction.
        if (IsLoaded) BeginCompetitiveGameDetection();
        else          Loaded += OnCompetitiveLoaded;
    }

    // Renders VM session/busy state: re-syncs page controls + drives the elapsed timer
    // (page-owned UI). Called on every VM StateChanged and once on (re)build — so a page
    // rebuilt mid-session reflects the already-active session instead of resetting to idle.
    private void OnVmStateChanged()
    {
        SyncCompetitiveControlStates();
        if (CompetitiveService.IsSessionActive) StartCompetitiveTimer();
        else                                    StopCompetitiveTimer();
    }

    // ── Game picker row ───────────────────────────────────────────────────

    private void BuildCompetitivePicker(StackPanel panel)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _cmGameCombo = new ComboBox
        {
            FontSize = 13,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _cmGameCombo.SelectionChanged += (_, _) =>
        {
            if (_cmGameCombo!.SelectedIndex >= 0 && _cmGameCombo.SelectedIndex < _cmGames.Count)
            {
                var chosen = _cmGames[_cmGameCombo.SelectedIndex].ExePath;
                ViewModel.SelectedPath = chosen;
                CompetitivePrefs.SaveLastGamePath(chosen);
            }
            SyncCompetitiveControlStates();
        };
        Grid.SetColumn(_cmGameCombo, 0);
        row.Children.Add(_cmGameCombo);

        _cmBrowseBtn = MakeCompetitiveButton("Browse…", "GridBtn", BrowseForGame);
        Grid.SetColumn(_cmBrowseBtn, 1);
        row.Children.Add(_cmBrowseBtn);

        _cmShortcutBtn = MakeCompetitiveButton("Create Shortcut", "GridBtn", CreateCompetitiveShortcut);
        Grid.SetColumn(_cmShortcutBtn, 2);
        row.Children.Add(_cmShortcutBtn);

        _cmPrimaryBtn = MakeCompetitiveButton("Start Competitive Mode", "RunBtn",
            () => _ = ViewModel.PrimaryClickAsync(ReadCompetitiveOptionsFromUi()));
        Grid.SetColumn(_cmPrimaryBtn, 3);
        row.Children.Add(_cmPrimaryBtn);

        panel.Children.Add(row);

        _cmPickerHint = new TextBlock
        {
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(_cmPickerHint);

        // Which exe detection resolved to, and how it will be started.
        _cmLaunchInfo = new TextBlock
        {
            FontSize = 11.5,
            Foreground = Res("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(_cmLaunchInfo);
    }

    /// <summary>Updates the "&lt;exe&gt; — via Steam (AppID n) / direct launch" line.</summary>
    private void RefreshCompetitiveLaunchInfo()
    {
        if (_cmLaunchInfo is null) return;

        var selected = ViewModel.SelectedPath;
        if (selected is null)
        {
            _cmLaunchInfo.Visibility = Visibility.Collapsed;
            return;
        }

        string method;
        try
        {
            var plan = CompetitiveService.ResolveLaunch(selected, ReadCompetitiveOptionsFromUi());
            method = plan.ViaSteam ? $"via Steam (AppID {plan.AppId})" : "direct launch";
        }
        catch { method = "direct launch"; }

        _cmLaunchInfo.Text = $"{Path.GetFileName(selected)} — {method}";
        _cmLaunchInfo.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// MIGRATION: net8 mapped WPF style keys → native chrome: RunBtn → AccentButtonStyle,
    /// anything else → default Button. The style param is kept so call sites are unchanged.
    /// </summary>
    private Button MakeCompetitiveButton(string label, string style, Action onClick)
    {
        var btn = new Button
        {
            Content = label,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(16, 10, 16, 10),
            FontSize = 13,
        };
        if (style == "RunBtn")
            btn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        btn.Click += (_, _) =>
        {
            try { onClick(); }
            catch (Exception ex) { SetCompetitiveStatus($"Error: {ex.Message}"); Service?.Log($"ERROR {label}: {ex.Message}"); }
        };
        return btn;
    }

    private void OnCompetitiveLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCompetitiveLoaded;
        BeginCompetitiveGameDetection();
    }

    /// <summary>Runs Steam detection off the UI thread, then populates the picker.</summary>
    private void BeginCompetitiveGameDetection()
    {
        if (_cmDetectionStarted) return;
        _cmDetectionStarted = true;

        if (_cmPickerHint is not null)
        {
            _cmPickerHint.Text = "Detecting installed games…";
            _cmPickerHint.Visibility = Visibility.Visible;
        }

        _ = Task.Run(() =>
        {
            IReadOnlyList<DetectedGame> found;
            try { found = GameDetection.DetectSteamGames(); }
            catch { found = Array.Empty<DetectedGame>(); }

            return DispatcherQueue.TryEnqueue(() => PopulateCompetitiveGames(found));
        });
    }

    private void PopulateCompetitiveGames(IReadOnlyList<DetectedGame> detected)
    {
        _cmGames.Clear();
        _cmGames.AddRange(detected);

        // A previously browsed exe that detection does not know about is added so the
        // persisted choice survives a restart.
        string? last = CompetitivePrefs.LoadLastGamePath();
        if (last is not null && !_cmGames.Any(g => g.ExePath.Equals(last, StringComparison.OrdinalIgnoreCase)))
        {
            try { if (File.Exists(last)) _cmGames.Insert(0, new DetectedGame(Path.GetFileNameWithoutExtension(last), last)); }
            catch { }
        }

        if (_cmGameCombo is not null)
        {
            _cmGameCombo.Items.Clear();
            foreach (var g in _cmGames) _cmGameCombo.Items.Add(g.Name);
        }

        if (_cmPickerHint is not null)
        {
            bool empty = _cmGames.Count == 0;
            _cmPickerHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _cmPickerHint.Text = "No games detected. Use Browse to pick an .exe manually.";
        }

        // Restore the persisted selection.
        if (last is not null && _cmGameCombo is not null)
        {
            int i = _cmGames.FindIndex(g => g.ExePath.Equals(last, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { _cmGameCombo.SelectedIndex = i; ViewModel.SelectedPath = _cmGames[i].ExePath; }
        }

        SyncCompetitiveControlStates();
    }

    // MIGRATION: WinUI pickers are async, so the body is BrowseForGameAsync and this stays
    // a void Action for MakeCompetitiveButton's signature.
    private void BrowseForGame() => _ = BrowseForGameAsync();

    private async Task BrowseForGameAsync()
    {
        // net8 FilePickers.OpenFileAsync(".exe") → IFileService.PickSingleFileAsync (Win32
        // IFileOpenDialog via AkariFileService — elevation-safe; a raw WinRT picker throws
        // COMException 0x80004005 under requireAdministrator, Phase 4). Returns a StorageFile.
        var picked = await _files.PickSingleFileAsync(new[] { ".exe" });
        if (picked is null) return;

        string path = picked.Path;
        string name = Path.GetFileNameWithoutExtension(path);

        int existing = _cmGames.FindIndex(g => g.ExePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing < 0)
        {
            _cmGames.Insert(0, new DetectedGame(name, path));
            _cmGameCombo?.Items.Insert(0, name);
            existing = 0;
        }

        if (_cmGameCombo is not null) _cmGameCombo.SelectedIndex = existing;
        ViewModel.SelectedPath = path;
        CompetitivePrefs.SaveLastGamePath(path);

        if (_cmPickerHint is not null) _cmPickerHint.Visibility = Visibility.Collapsed;
        SyncCompetitiveControlStates();
    }

    private void CreateCompetitiveShortcut()
    {
        string? path = ViewModel.SelectedPath;
        if (string.IsNullOrWhiteSpace(path)) { SetCompetitiveStatus("Select a game first."); return; }

        try
        {
            string gameName = Path.GetFileNameWithoutExtension(path);
            string desktop  = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string linkPath = Path.Combine(desktop, $"{SanitizeFileName(gameName)} (Competitive).lnk");

            string? akariExe = Environment.ProcessPath;
            if (akariExe is null) { SetCompetitiveStatus("Could not resolve the Akari Tool executable path."); return; }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) { SetCompetitiveStatus("Windows Script Host is unavailable — shortcut not created."); return; }

            object? shell = Activator.CreateInstance(shellType);
            if (shell is null) { SetCompetitiveStatus("Could not create the shortcut."); return; }

            object? link = shellType.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            if (link is null) { SetCompetitiveStatus("Could not create the shortcut."); return; }

            var linkType = link.GetType();
            void Set(string prop, string value) => linkType.InvokeMember(prop,
                System.Reflection.BindingFlags.SetProperty, null, link, new object[] { value });

            Set("TargetPath",       akariExe);
            Set("Arguments",        $"--competitive \"{path}\"");
            Set("WorkingDirectory", Path.GetDirectoryName(akariExe) ?? "");
            Set("IconLocation",     path);
            Set("Description",      $"Launch {gameName} with Akari Tool Competitive Mode");

            linkType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);

            SetCompetitiveStatus($"Shortcut created on the Desktop: {Path.GetFileName(linkPath)}");
            Service?.Log($"Competitive Mode shortcut created: {linkPath}");
        }
        catch (Exception ex)
        {
            SetCompetitiveStatus($"Could not create the shortcut: {ex.Message}");
            Service?.Log($"ERROR Create Shortcut: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    // ── Anti-cheat notice (sub-part B) ────────────────────────────────────

    private void BuildCompetitiveAntiCheatNotice(StackPanel panel)
    {
        // ⚠ glyph + Caution tint (net8 WarnFg): at 12px body size the brand crimson is
        // below AA on dark; caution amber clears AA in both themes.
        panel.Children.Add(new TextBlock
        {
            Text = "⚠  Experimental — use at your own risk. Competitive Mode temporarily suspends " +
                   "apps, stops services and changes process priorities around a game launch, then " +
                   "restores everything when the game exits. Some anti-cheats block priority and I/O " +
                   "changes. Akari Tool only uses standard Windows APIs and never modifies game memory. " +
                   "If Akari Tool is closed unexpectedly during a session, it will offer to restore your " +
                   "settings on next launch.",
            FontSize = 12,
            Foreground = Res("SystemFillColorCautionBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
    }

    // ── Option groups (sub-part C) ────────────────────────────────────────

    private void BuildCompetitiveOptionGroups(StackPanel panel)
    {
        // ── Game Process ──────────────────────────────────────────────────
        var gameProcess = AddCompetitiveGroup(panel, "Game Process");

        _cmBoostPriority = AddCompetitiveCheck(gameProcess, "Boost Game Priority", null);
        _cmBoostPriority.Click += (_, _) => { SyncCompetitiveControlStates(); SaveCompetitiveOptions(); };

        _cmPrioritySubOptions = new StackPanel { Margin = new Thickness(26, 4, 0, 0) };
        gameProcess.Children.Add(_cmPrioritySubOptions);

        // Realtime is intentionally absent — it starves the audio and input threads.
        _cmPriorityLevel = AddCompetitiveDropdown(_cmPrioritySubOptions, "Priority Level",
            new[] { "Above Normal", "High" }, 1);
        _cmIoPriority = AddCompetitiveDropdown(_cmPrioritySubOptions, "I/O Priority",
            new[] { "Normal", "High" }, 1);
        _cmCpuSets = AddCompetitiveDropdown(_cmPrioritySubOptions, "CPU Sets",
            new[] { "All Cores" }, 0);

        _cmLaunchThroughSteam = AddCompetitiveCheck(gameProcess, "Launch through Steam when available",
            "Some games fail to authenticate when their .exe is started directly.");
        _cmLaunchThroughSteam.Click += (_, _) => { SaveCompetitiveOptions(); RefreshCompetitiveLaunchInfo(); };

        // ── Background Activity ───────────────────────────────────────────
        var background = AddCompetitiveGroup(panel, "Background Activity");

        _cmGameFocus = AddCompetitiveCheck(background, "Game Focus",
            "Suspends browsers, chat and launcher apps for the session, then resumes them. Nothing is closed.");
        _cmPauseServices = AddCompetitiveCheck(background, "Pause Non-Essential Services", null);
        _cmCloseAfterLaunch = AddCompetitiveCheck(background, "Close Akari Tool After Game Launch",
            "Hides the window; the session keeps running so your settings are restored on exit.");

        // ── System ────────────────────────────────────────────────────────
        var system = AddCompetitiveGroup(panel, "System");

        _cmConsistentPerf = AddCompetitiveCheck(system, "Consistent Performance",
            "Switches to the Ultimate/High Performance power plan and opts the game out of CPU power throttling.");
        _cmClearStandby = AddCompetitiveCheck(system, "Clear Standby Memory",
            "Frees cached memory at launch. Effect on framerate is usually negligible.");

        foreach (var cb in new[] { _cmGameFocus, _cmPauseServices, _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby })
            cb.Click += (_, _) => SaveCompetitiveOptions();
    }

    private StackPanel AddCompetitiveGroup(StackPanel parent, string title)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 8, 0, 6)
        });

        var group = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        parent.Children.Add(group);
        return group;
    }

    private CheckBox AddCompetitiveCheck(StackPanel parent, string label, string? description)
    {
        var content = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = Res("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        if (description is not null)
            content.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11.5,
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0)
            });

        var cb = new CheckBox
        {
            Margin = new Thickness(0, 5, 0, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        parent.Children.Add(cb);
        return cb;
    }

    private ComboBox AddCompetitiveDropdown(StackPanel parent, string label, string[] items, int defaultIndex)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var combo = new ComboBox
        {
            FontSize = 12.5,
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        foreach (string i in items) combo.Items.Add(i);
        combo.SelectedIndex = defaultIndex;
        combo.SelectionChanged += (_, _) => SaveCompetitiveOptions();
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);

        parent.Children.Add(row);
        return combo;
    }

    // ── Options <-> UI (sub-part C) ────────────────────────────────────────

    private void LoadCompetitiveOptionsIntoUi(CompetitiveOptions o)
    {
        if (_cmBoostPriority    is not null) _cmBoostPriority.IsChecked    = o.BoostGamePriority;
        if (_cmGameFocus        is not null) _cmGameFocus.IsChecked        = o.GameFocus;
        if (_cmPauseServices    is not null) _cmPauseServices.IsChecked    = o.PauseNonEssentialServices;
        if (_cmCloseAfterLaunch is not null) _cmCloseAfterLaunch.IsChecked = o.CloseAfterLaunch;
        if (_cmConsistentPerf   is not null) _cmConsistentPerf.IsChecked   = o.ConsistentPerformance;
        if (_cmClearStandby     is not null) _cmClearStandby.IsChecked     = o.ClearStandbyMemory;
        if (_cmLaunchThroughSteam is not null) _cmLaunchThroughSteam.IsChecked = o.LaunchThroughSteam;

        if (_cmPriorityLevel is not null) _cmPriorityLevel.SelectedIndex = o.PriorityLevel == GamePriorityLevel.High ? 1 : 0;
        if (_cmIoPriority    is not null) _cmIoPriority.SelectedIndex    = o.IoPriority    == GameIoPriority.High    ? 1 : 0;
        if (_cmCpuSets       is not null) _cmCpuSets.SelectedIndex       = 0;
    }

    private CompetitiveOptions ReadCompetitiveOptionsFromUi() => new(
        BoostGamePriority:         _cmBoostPriority?.IsChecked    == true,
        PriorityLevel:             _cmPriorityLevel?.SelectedIndex == 1 ? GamePriorityLevel.High : GamePriorityLevel.AboveNormal,
        IoPriority:                _cmIoPriority?.SelectedIndex    == 1 ? GameIoPriority.High    : GameIoPriority.Normal,
        CpuSets:                   CpuSetMode.AllCores,
        GameFocus:                 _cmGameFocus?.IsChecked        == true,
        PauseNonEssentialServices: _cmPauseServices?.IsChecked    == true,
        ConsistentPerformance:     _cmConsistentPerf?.IsChecked   == true,
        CloseAfterLaunch:          _cmCloseAfterLaunch?.IsChecked == true,
        ClearStandbyMemory:        _cmClearStandby?.IsChecked     == true,
        LaunchThroughSteam:        _cmLaunchThroughSteam?.IsChecked == true);

    private void SaveCompetitiveOptions()
    {
        try { CompetitivePrefs.SaveOptions(ReadCompetitiveOptionsFromUi()); }
        catch (Exception ex) { Service?.Log($"Competitive Mode: could not save options — {ex.Message}"); }
    }

    /// <summary>
    /// Single place that decides what is enabled. Called after every state change so the
    /// enable/disable rules cannot drift apart across handlers. (Real version, sub-part C —
    /// replaces the A stub; calls the sub-part-D RefreshCompetitiveStatus stub.)
    /// </summary>
    private void SyncCompetitiveControlStates()
    {
        bool active = CompetitiveService.IsSessionActive;
        bool locked = active || ViewModel.IsBusy;

        if (_cmPrimaryBtn is not null)
        {
            _cmPrimaryBtn.Content   = active ? "End Session"
                                      : ViewModel.IsBusy ? "Cancel"
                                      : "Start Competitive Mode";
            _cmPrimaryBtn.IsEnabled = true;
        }

        if (_cmGameCombo   is not null) _cmGameCombo.IsEnabled   = !locked;
        if (_cmBrowseBtn   is not null) _cmBrowseBtn.IsEnabled   = !locked;
        if (_cmShortcutBtn is not null) _cmShortcutBtn.IsEnabled = !locked && ViewModel.SelectedPath is not null;

        foreach (var cb in new[] { _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                                   _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                                   _cmLaunchThroughSteam })
            if (cb is not null) cb.IsEnabled = !locked;

        // The three sub-dropdowns follow their parent checkbox as well as the session lock.
        bool subs = !locked && _cmBoostPriority?.IsChecked == true;
        foreach (var combo in new[] { _cmPriorityLevel, _cmIoPriority, _cmCpuSets })
            if (combo is not null) combo.IsEnabled = subs;
        if (_cmPrioritySubOptions is not null)
            _cmPrioritySubOptions.Opacity = subs ? 1.0 : 0.5;

        RefreshCompetitiveLaunchInfo();
        RefreshCompetitiveStatus();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Status panel (sub-part D UI; the session state machine lives in the VM since E1)
    //  RefreshCompetitiveStatus is READ-ONLY — it reads CompetitiveService.CurrentState +
    //  ViewModel.ActiveSchemeName and never reactivates a scheme (the CLAUDE.md power-plan
    //  invariant; the scheme write lives only in CompetitiveService.StartAsync).
    // ══════════════════════════════════════════════════════════════════════

    private void BuildCompetitiveStatus(StackPanel panel)
    {
        panel.Children.Add(new Border
        {
            Background = Res("DividerStrokeColorDefaultBrush"),
            Height = 1,
            Margin = new Thickness(-20, 6, -20, 10)
        });

        _cmStatusHeadline = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_cmStatusHeadline);

        _cmStatusDetail = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(_cmStatusDetail);
    }

    private void SetCompetitiveStatus(string text)
    {
        if (_cmStatusHeadline is not null) _cmStatusHeadline.Text = text;
    }

    /// <summary>Idle summary, or the live session readout with elapsed time. READ-ONLY —
    /// reads CurrentState + the already-resolved _cmActiveSchemeName; never writes a scheme.</summary>
    private void RefreshCompetitiveStatus()
    {
        if (_cmStatusHeadline is null || _cmStatusDetail is null) return;
        _cmStatusDetail.Children.Clear();

        var state = CompetitiveService.CurrentState;
        if (state is null)
        {
            var selected = ViewModel.SelectedPath;
            _cmStatusHeadline.Text = selected is null
                ? "Idle. Select a game to begin."
                : $"Idle. {Path.GetFileNameWithoutExtension(selected)} selected.";
            return;
        }

        var elapsed = DateTime.UtcNow - state.StartedUtc;
        _cmStatusHeadline.Text =
            $"Active — {state.GameProcessName} — {elapsed:hh\\:mm\\:ss}";

        void Bullet(string text) => _cmStatusDetail!.Children.Add(new TextBlock
        {
            Text = "•  " + text,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1)
        });

        if (state.SuspendedProcesses.Count > 0)
            Bullet($"{state.SuspendedProcesses.Count} background apps suspended");

        if (state.PreviousPowerSchemeGuid is not null)
            Bullet($"Power plan: {ViewModel.ActiveSchemeName ?? "performance plan"}");

        foreach (var svc in state.StoppedServices)
            Bullet($"{FriendlyServiceName(svc.Name)} paused");

        if (state.TuningFailures.Count > 0)
            Bullet("Some tuning was blocked by anti-cheat.");
    }

    private static string FriendlyServiceName(string name) => name switch
    {
        "WSearch" => "Windows Search",
        "SysMain" => "SysMain (Superfetch)",
        _         => name,
    };

    private void StartCompetitiveTimer()
    {
        _cmElapsedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cmElapsedTimer.Tick -= OnCompetitiveTimerTick;
        _cmElapsedTimer.Tick += OnCompetitiveTimerTick;
        _cmElapsedTimer.Start();
    }

    // WinUI DispatcherTimer.Tick is EventHandler<object> (WPF used EventHandler).
    private void OnCompetitiveTimerTick(object? sender, object e) => RefreshCompetitiveStatus();

    private void StopCompetitiveTimer() => _cmElapsedTimer?.Stop();

    // ══════════════════════════════════════════════════════════════════════
    //  GAMING TWEAKS (toggles)  (net8 AkariOSTab.GamingTweaksCard.cs, ported near
    //  line-for-line)
    //
    //  Three toggle rows: Disable Preemption (NVIDIA), Disable HDCP, Network
    //  Optimization. Preemption/HDCP are native registry writes with read-back
    //  restore-on-build (SystemStateReader); Network Optimization extracts an embedded
    //  .bat that REBOOTS the machine, gated by the reboot confirm dialog. The restore
    //  reads set the toggle via a SUPPRESSING setter (BuildToggle), so page load never
    //  fires a write. Second AkariDialogs→TweakDialogs swap (arg order reversed).
    // ══════════════════════════════════════════════════════════════════════

    private readonly Dictionary<string, Action<bool>> _gamingSetters = new();

    private void BuildGamingToggleContent(StackPanel panel)
    {
        void AddRow(string name, string desc, Action<bool> apply, string? disabledNote = null)
        {
            var sep = panel.Children.Count > 1;   // > 1: card title header is child 0
            if (sep) panel.Children.Add(new Border { Background = Res("DividerStrokeColorDefaultBrush"), Height = 1, Margin = new Thickness(-20, 0, -20, 0) });

            var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
            info.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

            // Unavailable rows read like the AkariOS-only banner: a short reason
            // in the info tint, with the control disabled rather than hidden.
            if (disabledNote is not null)
                info.Children.Add(new TextBlock
                {
                    Text = disabledNote,
                    FontSize = 11.5,
                    Foreground = Hex("#4CC2FF"),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });

            var (toggleCtrl, setter) = BuildToggle(apply);
            Grid.SetColumn(toggleCtrl, 1);
            _gamingSetters[name] = setter;

            if (disabledNote is not null)
            {
                // WinUI: IsEnabled lives on Control, not FrameworkElement.
                if (toggleCtrl is Control c) c.IsEnabled = false;
                info.Opacity = 0.6;
            }

            row.Children.Add(info);
            row.Children.Add(toggleCtrl);
            panel.Children.Add(row);
        }

        AddRow("Disable Preemption (NVIDIA)", "Disable GPU preemption for lower latency", SetPreemption);
        AddRow("Disable HDCP", "Disable HDCP for lower GPU overhead", SetHdcp);
        // The network batches ship inside the app as embedded resources, so this
        // row is always available — no C:\PostInstall payload check.
        AddRow("Network Optimization", "Apply AkariOS network driver tweaks", SetNetworkOptimization);

        // ── Restore from the REGISTRY, not from markers ───────────────────
        // Preemption and HDCP are native, so their real values are readable and
        // cannot go stale. A marker only records that the app once ran something;
        // a read records what the system actually says.
        void RestoreFromRead(string name, bool? state)
        {
            if (state == true && _gamingSetters.TryGetValue(name, out var s)) s(true);
        }

        RestoreFromRead("Disable Preemption (NVIDIA)", SystemStateReader.ReadPreemption());
        RestoreFromRead("Disable HDCP",                SystemStateReader.ReadHdcp());

        // Network Optimization still shells out to a batch, whose effects span NIC
        // driver properties and netsh global state — not meaningfully readable until
        // it is ported to native code. It keeps its marker until that port lands.
        if (TweakHelpers.HasState("NetworkOptimization") &&
            _gamingSetters.TryGetValue("Network Optimization", out var net))
            net(true);
    }

    // net8 TweakHelpers.BuildToggle — a ToggleSwitch whose setter sets IsOn WITHOUT
    // re-firing onToggle (the `suppress` flag). Load-bearing: restore-on-build uses
    // the setter, so reading current state never triggers a write. Ported verbatim
    // (build #3 has no TweakHelpers.BuildToggle — the old factory was replaced).
    private static (FrameworkElement Control, Action<bool> Setter) BuildToggle(Action<bool>? onToggle = null)
    {
        var toggle = new ToggleSwitch
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            OnContent = null,
            OffContent = null,
            MinWidth = 0,
        };

        bool suppress = false;
        toggle.Toggled += (_, _) =>
        {
            if (suppress) return;
            onToggle?.Invoke(toggle.IsOn);
        };

        Action<bool> setter = state =>
        {
            if (toggle.IsOn == state) return;
            suppress = true;
            toggle.IsOn = state;
            suppress = false;
        };

        return (toggle, setter);
    }

    private void SetPreemption(bool disable)
    {
        try
        {
            if (disable)
            {
                if (TweakHelpers.HasState("DisablePreemption")) return;
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler", "EnablePreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisableCudaContextPreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "EnableCEPreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemptionOnS3S4", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "ComputePreemption", 0, RegistryValueKind.DWord);
                TweakHelpers.SaveState("DisablePreemption");
            }
            else
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler", "EnablePreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisableCudaContextPreemption", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "EnableCEPreemption", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "DisablePreemptionOnS3S4", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "ComputePreemption", 1, RegistryValueKind.DWord);
                TweakHelpers.ClearState("DisablePreemption");
            }
            Service?.Log($"NVIDIA Preemption {(disable ? "disabled" : "enabled")}. Restart to apply.");
        }
        catch (Exception ex) { Service?.Log($"ERROR SetPreemption: {ex.Message}"); }
    }

    /// <summary>
    /// HDCP on/off, fully native — no C:\PostInstall dependency. Both directions
    /// write RMHdcpKeyglobZero across every enumerated adapter, so revert is a true
    /// inverse of apply. State is read back from the registry, not from a marker.
    /// </summary>
    private void SetHdcp(bool disable)
    {
        try
        {
            int written = GpuTweaks.SetHdcpDisabled(disable, m => Service?.Log(m));

            if (written <= 0)
            {
                Service?.Log($"HDCP {(disable ? "disable" : "enable")} failed — no adapters were written.");
                if (_gamingSetters.TryGetValue("Disable HDCP", out var set)) set(!disable);
                return;
            }

            Service?.Log($"HDCP {(disable ? "disabled" : "enabled")} on {written} adapter(s). Restart to apply.");
        }
        catch (Exception ex)
        {
            Service?.Log($"ERROR SetHdcp: {ex.Message}");
            if (_gamingSetters.TryGetValue("Disable HDCP", out var set)) set(!disable);
        }
    }

    /// <summary>
    /// Reboot warning for the network batches. net8 went through AkariDialogs; build #3
    /// uses TweakDialogs.ConfirmContentAsync (arg order reversed to title, content,
    /// primaryText). Wording and button labels unchanged.
    /// </summary>
    private async Task<bool> ConfirmNetworkRebootAsync(bool enable)
    {
        string what = enable
            ? "Applying AkariOS network tweaks"
            : "Reverting AkariOS network tweaks";

        return await _dialogs.ConfirmContentAsync(
            "This will restart your PC",
            new TextBlock
            {
                Text = $"{what} runs a script that restarts your computer immediately " +
                       "when it finishes. Save your work before continuing.\n\nContinue?",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
            },
            primaryText: enable ? "Restart and apply" : "Restart and revert");
    }

    // The batches ship inside the assembly (csproj embeds Scripts\Network\*.bat),
    // so the feature no longer depends on the C:\PostInstall payload being present.
    private const string NetworkApplyBat  = "network-apply.bat";
    private const string NetworkRevertBat = "network-revert.bat";

    /// <summary>
    /// Writes an embedded batch to a unique temp path and returns it. Resource
    /// lookup matches ToolService's .ps1 pattern — by name suffix, so it survives
    /// a root-namespace change. The caller deletes the file; the batch reboots the
    /// machine, so that cleanup is best-effort.
    /// </summary>
    private static async Task<string> ExtractNetworkBatAsync(string fileName)
    {
        var asm = typeof(AkariOSPage).Assembly;
        string suffix = $".Scripts.Network.{fileName}";

        string name = asm.GetManifestResourceNames()
                         .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                      ?? throw new FileNotFoundException($"Embedded resource not found: {suffix}");

        string path = Path.Combine(Path.GetTempPath(), $"AkariTool-{Guid.NewGuid():N}-{fileName}");

        await using (var rs = asm.GetManifestResourceStream(name)!)
        await using (var fs = File.Create(path))
            await rs.CopyToAsync(fs);

        return path;
    }

    private void SetNetworkOptimization(bool enable) => _ = SetNetworkOptimizationAsync(enable);

    /// <summary>
    /// Applies or reverts the embedded network batch. The batch is extracted to temp
    /// and its exit code checked before any state is saved. Both batches end in
    /// `shutdown -r -t 01`, so the app warns (ConfirmNetworkRebootAsync) before running.
    /// </summary>
    private async Task SetNetworkOptimizationAsync(bool enable)
    {
        bool previous = !enable;   // the position to fall back to on any failure

        void Revert()
        {
            // BuildToggle's setter never re-fires the callback, so this cannot recurse.
            if (_gamingSetters.TryGetValue("Network Optimization", out var set)) set(previous);
        }

        string? bat = null;

        try
        {
            if (enable && TweakHelpers.HasState("NetworkOptimization")) return;

            // Both network batches end in `pause` + `shutdown -r -t 01`; with a redirected,
            // window-less runner `pause` hits EOF and the machine reboots ~1s later. So the
            // app must warn before running either.
            if (!await ConfirmNetworkRebootAsync(enable))
            {
                Service?.Log("Network Optimization cancelled — nothing was changed.");
                Revert();
                return;
            }

            bat = await ExtractNetworkBatAsync(enable ? NetworkApplyBat : NetworkRevertBat);

            // ToolService.RunProcess — UseShellExecute=false, stdout/stderr into the log,
            // awaited exit code.
            int exit = await Service!.RunProcess("cmd.exe", $"/c \"{bat}\"",
                timeoutMilliseconds: 120_000);

            if (exit != 0)
            {
                Service.Log($"Network Optimization {(enable ? "apply" : "revert")} failed (exit {exit}). Nothing was saved.");
                Revert();
                return;
            }

            // The tweak script now exits cleanly after applying (pause/shutdown removed — the
            // unattended `pause` hung RunProcess to its 120s timeout, so the reboot never ran
            // and the exit was always -1). Exit 0 now means the work actually completed → persist.
            if (enable) TweakHelpers.SaveState("NetworkOptimization");
            else        TweakHelpers.ClearState("NetworkOptimization");

            bool persisted = TweakHelpers.HasState("NetworkOptimization") == enable;
            if (!persisted)
                Service.Log("WARNING: Network Optimization state did not persist before the restart.");

            Service.Log($"Network Optimization {(enable ? "applied" : "reverted")}. Restarting…");

            // Reboot is now app-owned (was buried in the .bat's shutdown line, which never
            // reached it). /t 5 gives the marker write + temp-bat cleanup a margin before the
            // machine goes down.
            RunShellProcess("shutdown", "/r /t 5");
        }
        catch (Exception ex)
        {
            Service?.Log($"ERROR SetNetworkOptimization: {ex.Message}");
            Revert();
        }
        finally
        {
            // Temp bat is just the tweak script now (no reboot); safe to delete before the
            // app-scheduled reboot fires.
            try { if (bat is not null && File.Exists(bat)) File.Delete(bat); }
            catch (Exception ex) { Service?.Log($"Could not delete temporary batch: {ex.Message}"); }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  NVIDIA + AMD  (net8 AkariOSTab.GpuTools.cs, ported near line-for-line)
    //
    //  Two plain Option-A cards (card-wrapper choice 1). Every button writes system
    //  state or launches an external tool — NONE driven in verification. The one
    //  read path is the AMD shader-cache segmented control's initial Refresh()
    //  (ReadAmdShaderCache → Registry.GetValue, no elevation). The NVIDIA "Miscellaneous"
    //  bundle and the AMD segment write both go through ElevationService.RunAsSystem.
    // ══════════════════════════════════════════════════════════════════════

    // ── NVIDIA ────────────────────────────────────────────────────────

    private void BuildNvidiaContent(StackPanel panel)
    {
        AddButtonGrid(panel, new (string, Action)[]
        {
            ("Apply NVIDIA Profile",    () => _ = NvidiaProfileService.ApplyAkariProfileAsync(Service!)),
            ("P-State 0",               () => GpuTweaks.SetPState0(Service!.Log)),
            ("Disable ECC",             () => GpuTweaks.DisableEcc(Service!.Log)),
            ("Disable Telemetry",       () => GpuTweaks.DisableNvidiaTelemetry(Service!.Log)),
            ("Unrestrict Clock Policy", () => GpuTweaks.UnrestrictClockPolicy(Service!.Log)),
            ("NVCleanstall",            () => _ = ToolFetchService.LaunchAsync("NVCleanstall", Service!)),
            ("Miscellaneous",           ApplyNvidiaMisc),
        });
    }

    private void ApplyNvidiaMisc()
    {
        try
        {
            const string gpuClass = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";
            bool ok = ElevationService.RunAsSystem(() =>
            {
            Registry.SetValue(gpuClass, "RmDisableHwFaultBuffer",     1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMD3Feature",                1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMDisableGpuASPMFlags",      3,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMBlcg",                     286331153,  RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMElcg",                     1431655765, RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMElpg",                     4095,       RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMFspg",                     15,         RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMSlcg",                     262143,     RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "EnableRuntimePowerManagement", 0,        RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "DisableOverlay",              1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "D3PCLatency",                 1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "F1TransitionLatency",         1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "Node3DLowLatency",            1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "PciLatencyTimerControl",      1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMDeepL1EntryLatencyUsec",    1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RmGspcMaxFtuS",               1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RmGspcMinFtuS",               1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RmGspcPerioduS",              1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMLpwrEiIdleThresholdUs",     1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMLpwrGrIdleThresholdUs",     1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMLpwrGrRgIdleThresholdUs",   1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RMLpwrMsIdleThresholdUs",     1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "PreferSystemMemoryContiguous", 1,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "TCCSupported",                 0,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "RmCacheLoc",                  0,          RegistryValueKind.DWord);
            Registry.SetValue(gpuClass, "TrackResetEngine",             0,          RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm\FTS", "EnableRID61684", 1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "DisplayPowerSaving",          0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "RmGpsPsEnablePerCpuCoreDpc",  1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "DisableWriteCombining",        1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogWarningEntries",            0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogPagingEntries",             0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogEventEntries",              0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogErrorEntries",              0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "PlatformSupportMiracast", 0, RegistryValueKind.DWord);
            }, Service!.Log);

            if (ok) Service?.Log("NVIDIA Miscellaneous tweaks applied. Restart to apply.");
            else    Service?.Log("NVIDIA Miscellaneous: SYSTEM elevation failed — no values written.");
        }
        catch (Exception ex) { Service?.Log($"ERROR ApplyNvidiaMisc: {ex.Message}"); }
    }

    // ── AMD ───────────────────────────────────────────────────────────

    private void BuildAmdContent(StackPanel panel)
    {
        AddButtonGrid(panel, new (string, Action)[]
        {
            ("DWORDS",                () => GpuTweaks.ApplyAmdDwords(Service!.Log)),
            ("RSS",                   () => _ = ToolFetchService.LaunchAsync("RadeonSlimmer",   Service!)),
            ("Driver Download",       () => RunShellProcess("https://www.amd.com/en/support/download/drivers.html", "")),
            ("Disable DXNAVI",        () => _ = ToolFetchService.LaunchAsync("DisableDx11Navi", Service!)),
        });

        // Shader Cache is a genuine two-state setting, so it renders as a stateful
        // segmented control that reads the live value on build rather than a pair of
        // fire-and-forget buttons.
        BuildAmdShaderCacheControl(panel);
    }

    // ── AMD Shader Cache: stateful segmented control ──────────────────
    //
    // 0x32 => AlwaysON, 0x31 => Default, absent => unset. The read path uses
    // Registry.GetValue (no elevation needed); only the write goes through SYSTEM.
    private const string AmdShaderCacheKey =
        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000\UMD";

    private enum ShaderCacheState { Unset, Default, AlwaysOn }

    private static ShaderCacheState ReadAmdShaderCache()
    {
        try
        {
            if (Registry.GetValue(AmdShaderCacheKey, "ShaderCache", null) is byte[] b && b.Length > 0)
                return b[0] switch
                {
                    0x32 => ShaderCacheState.AlwaysOn,
                    0x31 => ShaderCacheState.Default,
                    _    => ShaderCacheState.Unset
                };
        }
        catch { /* unreadable key reads as Unset, never throws */ }
        return ShaderCacheState.Unset;
    }

    /// <returns>true when the value was written under SYSTEM elevation.</returns>
    private bool ApplyAmdShaderCache(bool alwaysOn)
    {
        try
        {
            bool ok = ElevationService.RunAsSystem(() =>
            {
                Registry.SetValue(AmdShaderCacheKey, "ShaderCache",
                    alwaysOn ? new byte[] { 0x32, 0x00 } : new byte[] { 0x31, 0x00 },
                    RegistryValueKind.Binary);
            }, Service!.Log);

            if (ok) Service?.Log($"AMD Shader Cache set to {(alwaysOn ? "AlwaysON" : "Default")}.");
            else    Service?.Log("AMD Shader Cache: SYSTEM elevation failed — nothing written.");
            return ok;
        }
        catch (Exception ex)
        {
            Service?.Log($"ERROR ApplyAmdShaderCache: {ex.Message}");
            return false;
        }
    }

    private void BuildAmdShaderCacheControl(StackPanel panel)
    {
        var caption = new TextBlock
        {
            Text = "Shader Cache",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            Margin = new Thickness(4, 8, 4, 6)
        };
        panel.Children.Add(caption);

        // Two segments sharing one hairline track; each rounds only its outer corners.
        var track = new Grid { Margin = new Thickness(4, 0, 4, 4) };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (onSeg,  onText)  = MakeSegment("AlwaysON", new CornerRadius(8, 0, 0, 8), new Thickness(1));
        var (offSeg, offText) = MakeSegment("Default",  new CornerRadius(0, 8, 8, 0), new Thickness(0, 1, 1, 1));
        Grid.SetColumn(onSeg, 0);
        Grid.SetColumn(offSeg, 1);
        track.Children.Add(onSeg);
        track.Children.Add(offSeg);
        panel.Children.Add(track);

        void Paint(Border seg, TextBlock txt, bool active)
        {
            // Inactive = the app's default-button fill + primary text (was transparent + dim,
            // which read as washed-out empty outlines when neither segment is selected).
            seg.Background  = active ? Hex("#E0142A") : Res("ControlFillColorDefaultBrush");
            txt.Foreground  = active ? Hex("#FF8A94") : Res("TextFillColorPrimaryBrush");
            txt.FontWeight  = active ? FontWeights.SemiBold : FontWeights.Normal;
            seg.Tag         = active;   // remembered so hover doesn't fight the active fill
        }

        void Refresh()
        {
            var s = ReadAmdShaderCache();
            Paint(onSeg,  onText,  s == ShaderCacheState.AlwaysOn);
            Paint(offSeg, offText, s == ShaderCacheState.Default);
        }

        void Wire(Border seg, TextBlock txt, bool alwaysOn)
        {
            seg.PointerEntered += (_, _) =>
            {
                if (seg.Tag is bool active && !active) seg.Background = Res("ControlFillColorSecondaryBrush");
            };
            seg.PointerExited += (_, _) =>
            {
                if (seg.Tag is bool active && !active) seg.Background = Res("ControlFillColorDefaultBrush");
            };
            seg.Tapped += (_, _) =>
            {
                if (ApplyAmdShaderCache(alwaysOn)) Refresh();
            };
        }

        Wire(onSeg,  onText,  true);
        Wire(offSeg, offText, false);
        Refresh();
    }

    private static (Border seg, TextBlock text) MakeSegment(string label, CornerRadius radius, Thickness border)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var seg = new Border
        {
            Child = text,
            Padding = new Thickness(0, 10, 0, 10),
            CornerRadius = radius,
            BorderThickness = border,
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            Background = Res("ControlFillColorDefaultBrush"),   // Refresh() repaints; consistent initial
        };
        return (seg, text);
    }

    // ── Shared button grid (net8 AddButtonGrid) — 3-col auto-row grid ──
    private void AddButtonGrid(StackPanel parent, (string Label, Action Action)[] buttons)
    {
        const int cols = 3;
        int rows = (int)Math.Ceiling(buttons.Length / (double)cols);
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < rows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < buttons.Length; i++)
        {
            var (label, action) = buttons[i];
            var btn = new Button
            {
                Content = label,
                Margin = new Thickness(4),
                // Fill the grid column + real horizontal padding (matching AddActionRow) so the
                // grid reads as uniform, substantial buttons instead of small left-aligned ones
                // with large empty gaps. Layout only — same default WinUI button style/colors.
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(18, 10, 18, 10),
                FontSize = 13
            };
            btn.Click += (_, _) =>
            {
                try { action(); }
                catch (Exception ex) { Service?.Log($"ERROR {label}: {ex.Message}"); }
            };
            Grid.SetColumn(btn, i % cols);
            Grid.SetRow(btn, i / cols);
            grid.Children.Add(btn);
        }
        parent.Children.Add(grid);
    }

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
