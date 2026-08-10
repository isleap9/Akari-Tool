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
        // UNIFIED GAMING TWEAKS CARD
        // Service Preset + Playbook + BCD all in one container
        // ══════════════════════════════════════════════════════════════════════

        private void BuildGamingTweaksCard(StackPanel panel)
        {
            // ── Service Preset + Playbook + BCD ───────────────────────────
            var presetsCard = new Border
            {
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(0, 0, 0, 16),
            };
            var presetsInner = new StackPanel();
            presetsCard.Child = presetsInner;

            // Scope-of-change warning banner. Same Warning tier as the BCD note below:
            // a preset rewrites service startup types machine-wide, which is the widest
            // blast radius on this card.
            var presetWarning = new Border
            {
                Background = TweakHelpers.WarnBg,
                BorderBrush = TweakHelpers.WarnBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var presetWarningText = new StackPanel();
            presetWarningText.Children.Add(new TextBlock
            {
                Text = "⚠  Applies machine-wide changes to 166 service startup types. Restart required.",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.WarnFg,
                TextWrapping = TextWrapping.Wrap
            });
            presetWarningText.Children.Add(new TextBlock
            {
                Text = "Gaming also disables Windows Update and Explorer ISO mounting — choose Daily if you need either. Defender and boot-critical services are never touched.",
                FontSize = 12,
                Foreground = TweakHelpers.WarnFg,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            presetWarning.Child = presetWarningText;
            presetsInner.Children.Add(presetWarning);

            BuildServicePresetSection(presetsInner);
            presetsInner.Children.Add(new Border { Background = TweakHelpers.Token("AkariOverlayStrong"), Height = 1 });
            BuildPlaybookSection(presetsInner);
            presetsInner.Children.Add(new Border { Background = TweakHelpers.Token("AkariOverlayStrong"), Height = 1 });
            BuildBcdSection(presetsInner);

            panel.Children.Add(TweakHelpers.ShadowWrapCard(presetsCard));

            // ── Card 1: Competitive Mode ──────────────────────────────────
            panel.Children.Add(BuildSectionCard("Competitive Mode", BuildCompetitiveContent));

            // ── Card 2: Shader Cache Cleaner ──────────────────────────────
            panel.Children.Add(BuildSectionCard("Shader Cache Cleaner", BuildShaderCacheContent));

            // ── Card 3: Gaming Tweaks ─────────────────────────────────────
            panel.Children.Add(BuildSectionCard("Gaming Tweaks", BuildGamingToggleContent));

            // ── Card 4: NVIDIA ────────────────────────────────────────────
            panel.Children.Add(BuildSectionCard("NVIDIA", BuildNvidiaContent));

            // ── Card 5: AMD ───────────────────────────────────────────────
            panel.Children.Add(BuildSectionCard("AMD", BuildAmdContent));

        }

        // Added last by Build(), after the Utilities sections, so Useful Tools
        // stays at the bottom of the page.
        private void BuildUsefulToolsCard(StackPanel panel)
        {
            panel.Children.Add(BuildSectionCard("Useful Tools", BuildToolsContent));
        }

        /// <param name="defaultExpanded">
        /// First-run state when the user has no saved choice. Cards on the main page
        /// default to collapsed; a lone card on an otherwise empty page passes true.
        /// </param>
        private Border BuildSectionCard(string title, Action<StackPanel> builder, bool defaultExpanded = false)
        {
            var card = new Border
            {
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(0, 0, 0, 12),
            };
            var inner = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            card.Child = inner;

            // ── Clickable header row: title + chevron ─────────────────────────
            var header = new Grid();   // Cursors.Hand dropped (no WinUI equivalent)
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text       = title,
                FontSize   = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            header.Children.Add(titleText);

            var chevron = new TextBlock
            {
                FontSize   = 13,
                Foreground = TweakHelpers.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chevron, 1);
            header.Children.Add(chevron);

            // Transparent background so the whole row is hit-testable, not just the glyph
            var headerHit = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Child = header };
            inner.Children.Add(headerHit);

            // ── Content, in its own panel so it can collapse ──────────────────
            var content = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            builder(content);
            inner.Children.Add(content);

            // ── Persisted collapsed state (per section title) ─────────────────
            // The marker always records the NON-default choice, so absence means
            // "user hasn't chosen" and the caller's default applies. Flipping its
            // meaning with defaultExpanded is what lets both defaults persist a
            // deviation — storing "expanded" for a default-expanded section would
            // make collapsing it unpersistable (clearing == back to the default).
            string stateKey = (defaultExpanded ? "SectionCollapsed_" : "SectionExpanded_")
                            + title.Replace(" ", "");
            bool collapsed  = defaultExpanded ? TweakHelpers.HasState(stateKey)
                                              : !TweakHelpers.HasState(stateKey);

            void ApplyState()
            {
                content.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
                chevron.Text       = collapsed ? "▸" : "▾";   // ▸ / ▾
            }
            ApplyState();

            headerHit.Tapped += (_, _) =>
            {
                collapsed = !collapsed;
                // Marker present only while the section deviates from its default.
                if (collapsed == defaultExpanded) TweakHelpers.SaveState(stateKey);
                else                              TweakHelpers.ClearState(stateKey);
                ApplyState();
            };

            return TweakHelpers.ShadowWrapCard(card);
        }

        // ── Gaming Tweaks: toggles + dropdowns ────────────────────────────

        private readonly Dictionary<string, Action<bool>> _gamingSetters = new();

        private void BuildGamingToggleContent(StackPanel panel)
        {
            void AddRow(string name, string desc, Action<bool> apply, string? disabledNote = null)
            {
                var sep = panel.Children.Count > 0;
                if (sep) panel.Children.Add(new Border { Background = TweakHelpers.Token("AkariOverlayStrong"), Height = 1, Margin = new Thickness(-20, 0, -20, 0) });

                var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                Grid.SetColumn(info, 0);
                info.Children.Add(new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
                info.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

                // Unavailable rows read like the AkariOS-only banner: a short reason
                // in the info tint, with the control disabled rather than hidden.
                if (disabledNote is not null)
                    info.Children.Add(new TextBlock
                    {
                        Text = disabledNote,
                        FontSize = 11.5,
                        Foreground = TweakHelpers.InfoFg,
                        Margin = new Thickness(0, 4, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    });

                var (toggleCtrl, setter) = TweakHelpers.BuildToggle(apply);
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
        /// Reboot warning for the network batches. Awaited directly rather than via
        /// AkariDialogs, for two reasons: AkariDialogs pumps a nested dispatcher
        /// frame, which would re-enter the UI thread from an async caller that is
        /// already suspended; and ConfirmYesNo cannot label its buttons, which this
        /// prompt needs. Owner is set, matching the other dialogs in this tab.
        /// </summary>
        private async Task<bool> ConfirmNetworkRebootAsync(bool enable)
        {
            string what = enable
                ? "Applying AkariOS network tweaks"
                : "Reverting AkariOS network tweaks";

            // MIGRATION: WPF-UI MessageBox (+ Owner) → WinUI ContentDialog via AkariDialogs.
            // Wording and button labels unchanged.
            return await AkariDialogs.ConfirmContentAsync(
                new TextBlock
                {
                    Text = $"{what} runs a script that restarts your computer immediately " +
                           "when it finishes. Save your work before continuing.\n\nContinue?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                "This will restart your PC",
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
            var asm = typeof(AkariOSTab).Assembly;
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
        /// Applies or reverts the embedded network batch.
        ///
        /// The batch is extracted to temp and its exit code checked before any state is
        /// saved. The old code shelled cmd.exe fire-and-forget: cmd always starts even
        /// when the .bat is missing, so nothing threw, state was saved, and the toggle
        /// came back ON every launch for a tweak that never ran.
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

                // Both network batches end in `pause` + `shutdown -r -t 01`. Under the
                // old fire-and-forget path a console appeared and `pause` actually
                // waited; with a redirected, window-less runner it hits EOF and the
                // machine reboots ~1s later. So the app must warn before running either.
                if (!await ConfirmNetworkRebootAsync(enable))
                {
                    Service?.Log("Network Optimization cancelled — nothing was changed.");
                    Revert();
                    return;
                }

                bat = await ExtractNetworkBatAsync(enable ? NetworkApplyBat : NetworkRevertBat);

                // ToolService.RunProcess — the existing shared runner. Already
                // UseShellExecute=false with stdout/stderr redirected into the app log
                // and an awaited exit code, which is exactly what this needs.
                int exit = await Service!.RunProcess("cmd.exe", $"/c \"{bat}\"",
                    timeoutMilliseconds: 120_000);

                if (exit != 0)
                {
                    Service.Log($"Network Optimization {(enable ? "apply" : "revert")} failed (exit {exit}). Nothing was saved.");
                    Revert();
                    return;
                }

                // The batch has already scheduled the reboot by the time it exits, so
                // this write races a ~1s timer. Registry.SetValue is synchronous and
                // the shutdown flushes the hive, but read it back and say so rather
                // than assuming it landed.
                if (enable) TweakHelpers.SaveState("NetworkOptimization");
                else        TweakHelpers.ClearState("NetworkOptimization");

                bool persisted = TweakHelpers.HasState("NetworkOptimization") == enable;
                if (!persisted)
                    Service.Log("WARNING: Network Optimization state did not persist before the restart.");

                Service.Log($"Network Optimization {(enable ? "applied" : "reverted")}. Restarting…");
            }
            catch (Exception ex)
            {
                Service?.Log($"ERROR SetNetworkOptimization: {ex.Message}");
                Revert();
            }
            finally
            {
                // Best-effort: on the success path the batch has already armed a ~1s
                // reboot, so this may not get the chance to run to completion.
                try { if (bat is not null && File.Exists(bat)) File.Delete(bat); }
                catch (Exception ex) { Service?.Log($"Could not delete temporary batch: {ex.Message}"); }
            }
        }

    }
}