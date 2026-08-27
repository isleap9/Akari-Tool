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
                    var backup = ServiceLocator.GetService<AkariTool.Core.Features.Common.Interfaces.ISystemBackupService>();
                    var rpResult = backup is null
                        ? AkariTool.Core.Features.Common.Models.BackupResult.CreateFailure("backup service unavailable")
                        : await backup.CreateRestorePointAsync("AkariOS Playbook Pre-Tweak Backup");
                    Service!.Log(rpResult.Success
                        ? "[RESTORE] ✓ Restore point ready."
                        : "[RESTORE] ⚠ Could not create restore point — proceeding anyway.");
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
}
