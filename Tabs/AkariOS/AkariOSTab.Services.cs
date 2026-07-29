using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        private TextBlock? _servicePresetLabel;
        // ── Service Preset ─────────────────────────────────────────────────

        private void BuildServicePresetSection(StackPanel inner)
        {
            var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = "Service Preset", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new TextBlock
            {
                Text = "Apply a service configuration preset. Gaming disables 166 services (telemetry, Bluetooth, Xbox, Hyper-V guests, search, and more) including Windows Update and ISO mounting. Daily keeps the same optimizations but leaves Windows Update and ISO mounting working. Defender is always protected.",
                FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            _servicePresetLabel = new TextBlock { FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 4, 0, 0), FontStyle = FontStyles.Italic };
            info.Children.Add(_servicePresetLabel);
            SyncPresetLabel();

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(buttons, 1);

            var gamingBtn = new Button { Content = "Apply Gaming", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
            gamingBtn.Click += async (_, _) => { await ServicesPreset.ApplyAkariGaming(Service!); SyncPresetLabel(); };

            var dailyBtn = new Button { Content = "Apply Daily", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
            dailyBtn.Click += async (_, _) => { await ServicesPreset.ApplyAkariDaily(Service!); SyncPresetLabel(); };

            var restoreBtn = new Button { Content = "Restore Stock", Style = (Style)FindResource("UndoBtn") };
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
        private static void ApplyPresetLabel(TextBlock? label)
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
                    "AkariGaming"  => ("Current: AkariOS Gaming",          TweakHelpers.SuccessFg),
                    "Gaming"       => ("Current: Gaming (stock Windows)",  TweakHelpers.SuccessFg),
                    "AkariDaily"   => ("Current: AkariOS Daily",           TweakHelpers.InfoFg),
                    "Daily"        => ("Current: Daily (stock Windows)",   TweakHelpers.InfoFg),
                    "AkariMinimal" => ("Current: AkariOS Minimal",         TweakHelpers.SuccessFg),
                    "Minimal"      => ("Current: Minimal (stock Windows)", TweakHelpers.SuccessFg),
                    "AkariStock"   => ("Current: Windows Stock",           TweakHelpers.Accent),
                    _              => ("Current: Windows Stock",           TweakHelpers.Accent),
                }
                : result.Preset switch
                {
                    SystemStateReader.ServicePreset.AkariGaming => ("Current: Gaming",        TweakHelpers.SuccessFg),
                    SystemStateReader.ServicePreset.AkariDaily  => ("Current: Daily",         TweakHelpers.InfoFg),
                    SystemStateReader.ServicePreset.Stock       => ("Current: Windows Stock", TweakHelpers.Accent),
                    SystemStateReader.ServicePreset.Mixed       =>
                        ($"Current: Mixed ({result.Drift.Count} of {result.Total} probes differ)", TweakHelpers.WarnFg),
                    _ => ("Current: Unknown", TweakHelpers.TextSecondary),
                };

            label.Text       = v.Text;
            label.Foreground = v.Fg;

            // Drift detail on hover so "Mixed" is diagnosable without a rebuild.
            label.ToolTip = result.Drift.Count == 0
                ? null
                : string.Join(Environment.NewLine,
                    result.Drift.Select(d => $"{d.Service}: {d.Actual} -> expected {d.Expected}"));
        }

        // ── Playbook ───────────────────────────────────────────────────────

        private void BuildPlaybookSection(StackPanel inner)
        {
            var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = "AkariOS Playbook Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new TextBlock
            {
                Text = "Applies 30 registry tweaks, 15 ETW autologger disables, 12 IFEO process priorities, " +
                       "66 scheduled task disables, filesystem tweaks, and telemetry opt-outs from the AkariOS playbook. " +
                       "A system restore point is created automatically before applying.",
                FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = "\u2139  Registry/task changes are restore-point-recoverable. Memory compression and DISM changes are not.",
                FontSize = 11, Foreground = TweakHelpers.WarnFg, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(btns, 1);

            var applyBtn = new Button { Content = "Apply All", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
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

            var undoBtn = new Button { Content = "Undo All", Style = (Style)FindResource("UndoBtn") };
            undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await PlaybookTweaks.UndoAllAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

            btns.Children.Add(applyBtn);
            btns.Children.Add(undoBtn);
            section.Children.Add(info);
            section.Children.Add(btns);
            inner.Children.Add(section);
        }

        // ── BCD ────────────────────────────────────────────────────────────

        private void BuildBcdSection(StackPanel inner)
        {
            var section = new Grid { Margin = new Thickness(20, 18, 20, 18) };
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = "BCD Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new TextBlock
            {
                Text = "Legacy boot menu (F8), disable dynamic tick (better timer resolution), " +
                       "disable recovery mode (faster boot), disable hibernation (removes hiberfil.sys). " +
                       "Current values are backed up to registry before applying.",
                FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = "\u26a0  BCD changes are NOT covered by System Restore — values are backed up internally for undo.",
                FontSize = 11, Foreground = TweakHelpers.AccentText, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(btns, 1);

            var applyBtn = new Button { Content = "Apply", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
            applyBtn.Click += async (_, _) =>
            {
                applyBtn.IsEnabled = false;
                try { await BcdBackup.BackupAsync(Service!); await BcdBackup.ApplyAsync(Service!, new BcdTweakOptions()); }
                finally { applyBtn.IsEnabled = true; }
            };

            var undoBtn = new Button { Content = "Restore", Style = (Style)FindResource("UndoBtn") };
            undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await BcdBackup.RestoreAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

            btns.Children.Add(applyBtn);
            btns.Children.Add(undoBtn);
            section.Children.Add(info);
            section.Children.Add(btns);
            inner.Children.Add(section);
        }

    }
}