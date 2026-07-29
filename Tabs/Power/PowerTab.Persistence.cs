using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENT "AKARI PERFORMANCE" SCHEME
        //
        // powercfg writes against SCHEME_CURRENT on built-in plans don't
        // reliably survive reboots/Windows updates. Instead, the first Power
        // tab write duplicates the active scheme into a dedicated
        // "Akari Performance" plan, activates it, and stores its GUID under
        // HKCU\Software\AkariTool. All later reads/writes target that GUID.
        // ══════════════════════════════════════════════════════════════════════

        private const string StateKeyPath    = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string SchemeGuidValue = "AkariPowerSchemeGuid";
        private const string AkariPlanName   = "Akari Performance";

        // Resolved once per session; null = no valid Akari scheme (→ SCHEME_CURRENT).
        private static string? _schemeTarget;
        private static bool _schemeResolved;

        // True when the stored Akari scheme still exists but Windows (or an OEM tool)
        // has since made a different plan active. Writes still land in the Akari
        // scheme by GUID, and SetPowerCfg's trailing /SETACTIVE reactivates it.
        private static bool _schemeInactive;

        private TextBlock? _persistIndicator;
        private Button? _revertButton;

        private static string? ReadStoredSchemeGuid() =>
            Registry.GetValue(StateKeyPath, SchemeGuidValue, null) as string;

        /// <summary>
        /// Returns the Akari scheme GUID if the stored one still exists on the
        /// system, else null. Validated once per session (powercfg /list is slow).
        /// </summary>
        private static string? ResolveSchemeTarget()
        {
            if (_schemeResolved) return _schemeTarget;
            _schemeResolved = true;

            var stored = ReadStoredSchemeGuid();
            if (stored != null && ListPowerPlans().Any(p =>
                    p.Guid.Equals(stored, StringComparison.OrdinalIgnoreCase)))
            {
                _schemeTarget = stored;

                // A valid GUID is not necessarily the ACTIVE one — Windows updates and
                // OEM tools switch plans behind our back. ReadActivePowerPlan is a
                // registry read, so this costs no extra powercfg /list invocation.
                var (_, activeGuid) = SystemStateReader.ReadActivePowerPlan();
                _schemeInactive = !stored.Equals(activeGuid, StringComparison.OrdinalIgnoreCase);
            }

            return _schemeTarget;
        }

        /// <summary>
        /// Clears the drift flag after a write has reactivated the Akari scheme.
        /// Returns true when the flag actually changed, so callers only repaint then.
        /// </summary>
        private static bool ClearSchemeDrift()
        {
            if (!_schemeInactive) return false;
            _schemeInactive = false;
            return true;
        }

        // Hardware/driver support probes (PowerSettingExists, ProbePowerSetting,
        // the label parser, and the cache) live in PowerTab.Probes.cs.

        /// <summary>
        /// Ensures the Akari Performance scheme exists (creating it from the
        /// currently active plan if the stored GUID is missing or was removed
        /// by Windows) and returns its GUID. Falls back to "SCHEME_CURRENT"
        /// if creation fails so writes still land somewhere.
        /// </summary>
        private string EnsureAkariScheme()
        {
            var existing = ResolveSchemeTarget();
            if (existing != null) return existing;

            var (_, activeGuid) = SystemStateReader.ReadActivePowerPlan();
            string baseGuid = activeGuid ?? BalancedGuid;

            var before = ListPowerPlans()
                .Select(p => p.Guid)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            RunPowerCfg($"/duplicatescheme {baseGuid}");

            string? created = ListPowerPlans()
                .FirstOrDefault(p => !before.Contains(p.Guid)).Guid;
            if (created == null)
            {
                Service?.Log("Power: could not create the Akari Performance scheme — writing to the active plan instead (may not persist).");
                return "SCHEME_CURRENT";
            }

            RunPowerCfg($"/changename {created} \"{AkariPlanName}\" \"Persistent power plan managed by Akari Tool\"");
            RunPowerCfg($"/setactive {created}");
            Registry.SetValue(StateKeyPath, SchemeGuidValue, created);

            _schemeTarget = created;
            _schemeResolved = true;
            Service?.Log($"Power: created persistent '{AkariPlanName}' scheme (GUID: {created}) and set it active.");

            RootPanel.Dispatcher.Invoke(() => { RefreshPersistIndicator(); RefreshActiveCard(); });
            return created;
        }

        // ── Indicator + revert button (rendered under the plan cards) ─────────

        private void BuildPersistIndicator(StackPanel panel)
        {
            var row = new Grid { Margin = new Thickness(4, -8, 4, 16) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _persistIndicator = new TextBlock
            {
                Text = $"Power plan: {AkariPlanName} (persistent)",
                FontFamily = MonoFont,
                FontSize = 11,
                Foreground = TweakHelpers.SuccessFg,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_persistIndicator, 0);
            row.Children.Add(_persistIndicator);

            _revertButton = new Button
            {
                Content = "Revert to Balanced",
                FontSize = 12,
                Padding = new Thickness(12, 4, 12, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)Application.Current.Resources["GridBtn"],
                Foreground = TweakHelpers.TextPrimary,
                BorderBrush = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                ToolTip = "Reactivate the Windows Balanced plan and delete the Akari Performance scheme",
            };
            _revertButton.Click += async (_, _) => await RevertToBalanced();
            Grid.SetColumn(_revertButton, 1);
            row.Children.Add(_revertButton);

            panel.Children.Add(row);
            RefreshPersistIndicator();
        }

        private void RefreshPersistIndicator()
        {
            bool active = ResolveSchemeTarget() != null;
            bool drifted = active && _schemeInactive;
            if (_persistIndicator != null)
                _persistIndicator.Text = !active
                    ? "Power plan: not persisted yet — first change creates the Akari Performance plan"
                    : drifted
                        ? $"Power plan: {AkariPlanName} exists but is not active — the next change reactivates it"
                        : $"Power plan: {AkariPlanName} (persistent)";
            if (_persistIndicator != null)
                _persistIndicator.Foreground = !active ? TweakHelpers.TextSecondary
                    : drifted ? TweakHelpers.WarnFg
                    : TweakHelpers.SuccessFg;
            if (_revertButton != null)
                _revertButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RevertToBalanced()
        {
            Service?.Log("Power: reverting to the Balanced plan...");

            int exit = await Service!.RunProcess("powercfg", $"/setactive {BalancedGuid}", timeoutMilliseconds: 10_000);
            if (exit != 0)
            {
                Service.Log($"Power: failed to activate Balanced (exit {exit}) — Akari scheme left in place.");
                return;
            }

            var guid = ReadStoredSchemeGuid();
            if (guid != null)
                await Service.RunProcess("powercfg", $"/delete {guid}", timeoutMilliseconds: 10_000);
            TweakHelpers.ClearState(SchemeGuidValue);

            _schemeTarget = null;
            _schemeResolved = true; // resolved: reads go back to SCHEME_CURRENT

            Service.Log("Power: Balanced plan active; Akari Performance scheme removed.");
            RootPanel.Dispatcher.Invoke(() =>
            {
                RefreshPersistIndicator();
                RefreshActiveCard();
                foreach (var r in _refreshActions)
                    try { r(); } catch { }
            });
        }
    }
}
