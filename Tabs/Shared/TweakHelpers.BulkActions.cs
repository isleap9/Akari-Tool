using Microsoft.UI.Text;                   // FontWeights
using Microsoft.UI.Xaml;                   // Thickness, GridLength, FrameworkElement
using Microsoft.UI.Xaml.Controls;          // StackPanel, Grid, TextBlock, Border
using Microsoft.UI.Xaml.Media;             // Brush, FontFamily

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        // ══════════════════════════════════════════════════════════════════════
        //  SECTION BULK ACTIONS  (Winhance "Quick Actions" equivalent)
        //
        //  AddTweakRow() registers every rendered tweak against its parent
        //  section panel. A tab can then call:
        //
        //      TweakHelpers.AttachBulkActions(section);
        //
        //  AFTER all rows have been added. Inserts a header bar with a live
        //  "N pending" pill, ★ Recommended bulk-apply and ⊞ Defaults bulk-reset.
        //  MIGRATION NOTE: confirmations are async ContentDialogs now.
        // ══════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<StackPanel, List<(TweakDefinition Def, Action Refresh)>> _sectionEntries = new();
        private static readonly Dictionary<StackPanel, Action> _sectionPillRefresh = new();

        /// <summary>Called by AddTweakRow for every rendered tweak.</summary>
        private static void RegisterSectionEntry(StackPanel section, TweakDefinition def, Action refresh)
        {
            if (!_sectionEntries.TryGetValue(section, out var list))
                _sectionEntries[section] = list = new();
            list.Add((def, refresh));
        }

        /// <summary>
        /// Refreshes every section's pending pill. Called by TweakRegistry
        /// after a config import so pills reflect the imported state.
        /// </summary>
        public static void RefreshAllSectionPills()
        {
            foreach (var refresh in _sectionPillRefresh.Values)
                try { refresh(); } catch { }
        }

        /// <summary>Refreshes the section's pending pill after a row changed.</summary>
        private static void NotifySectionChanged(StackPanel section)
        {
            if (_sectionPillRefresh.TryGetValue(section, out var refresh))
                try { refresh(); } catch { }
        }

        // ── Target resolution ─────────────────────────────────────────────────

        private static bool TryGetRecommendedTarget(TweakDefinition def, out bool toggleTarget, out int optionTarget)
        {
            toggleTarget = false; optionTarget = -1;

            if (def.InputKind == TweakInputKind.Toggle)
            {
                if (!def.RecommendedState.HasValue || def.Apply == null) return false;
                toggleTarget = def.RecommendedState.Value;
                return true;
            }
            if (def.Options is { Length: > 0 } && def.ApplyIndex != null)
            {
                optionTarget = Array.FindIndex(def.Options, o => o.IsRecommended);
                return optionTarget >= 0;
            }
            return false;
        }

        private static bool TryGetDefaultTarget(TweakDefinition def, out bool toggleTarget, out int optionTarget)
        {
            toggleTarget = false; optionTarget = -1;

            if (def.InputKind == TweakInputKind.Toggle)
            {
                if (!def.DefaultState.HasValue || def.Apply == null) return false;
                toggleTarget = def.DefaultState.Value;
                return true;
            }
            if (def.Options is { Length: > 0 } && def.ApplyIndex != null)
            {
                optionTarget = Array.FindIndex(def.Options, o => o.IsDefault);
                return optionTarget >= 0;
            }
            return false;
        }

        /// <summary>True when the tweak's current state differs from the given target.</summary>
        private static bool IsMismatched(TweakDefinition def, bool toggleTarget, int optionTarget)
        {
            try
            {
                if (def.InputKind == TweakInputKind.Toggle)
                {
                    var cur = def.ReadState?.Invoke();
                    return cur.HasValue && cur.Value != toggleTarget;   // unknown state → don't count
                }
                var idx = def.ReadCurrentIndex?.Invoke();
                return idx.HasValue && idx.Value >= 0 && idx.Value != optionTarget;
            }
            catch { return false; }
        }

        /// <summary>
        /// Shared predicate for every bulk surface (section bars, tab-level Quick
        /// Actions counts AND the bulk engine).
        /// </summary>
        internal static List<(TweakDefinition Def, Action Refresh, bool ToggleTarget, int OptionTarget)> CollectPending(
            IEnumerable<(TweakDefinition Def, Action Refresh)> entries, bool useRecommended)
        {
            var work = new List<(TweakDefinition Def, Action Refresh, bool ToggleTarget, int OptionTarget)>();
            foreach (var (def, refresh) in entries)
            {
                bool ok = useRecommended
                    ? TryGetRecommendedTarget(def, out var t, out var o)
                    : TryGetDefaultTarget(def, out t, out o);
                if (ok && IsMismatched(def, t, o))
                    work.Add((def, refresh, t, o));
            }
            return work;
        }

        /// <summary>
        /// All registered tweak entries whose section panel lives under
        /// <paramref name="root"/> in the element tree — the tab-scoping mechanism
        /// for Quick Actions.
        /// </summary>
        internal static List<(TweakDefinition Def, Action Refresh)> GetEntriesUnder(FrameworkElement root)
        {
            var result = new List<(TweakDefinition Def, Action Refresh)>();
            foreach (var (section, entries) in _sectionEntries)
            {
                for (FrameworkElement? el = section; el != null;
                     el = (el.Parent as FrameworkElement)
                          ?? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(el) as FrameworkElement)
                {
                    if (ReferenceEquals(el, root)) { result.AddRange(entries); break; }
                }
            }
            return result;
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Inserts the bulk-action header bar at the top of a section card.
        /// Call once per section, after all AddTweakRow calls for it.
        /// No-op if the section contains no tweaks with a Recommended value.
        /// </summary>
        public static void AttachBulkActions(StackPanel section)
        {
            if (!_sectionEntries.TryGetValue(section, out var entries)) return;
            if (!entries.Any(e => TryGetRecommendedTarget(e.Def, out _, out _))) return;

            // ── Pending pill ──────────────────────────────────────────────────
            var pillText = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var pill = new Border
            {
                Background = PillAccentBg,
                BorderBrush = PillAccentBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius, // design: 8 — do not change
                Padding = new Thickness(10, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = pillText,
            };
            pillText.Foreground = PillAccentFg;

            void RefreshPill()
            {
                int pending = entries.Count(e =>
                    TryGetRecommendedTarget(e.Def, out var t, out var o) && IsMismatched(e.Def, t, o));

                pillText.Text = pending > 0 ? $"{pending} pending" : "All recommended";
                ToolTipService.SetToolTip(pill, pending > 0
                    ? $"{pending} setting{(pending == 1 ? "" : "s")} in this section differ from the recommended value"
                    : "Every setting in this section matches its recommended value");
                pill.Opacity = pending > 0 ? 1.0 : 0.6;
            }

            // ── Bulk buttons ──────────────────────────────────────────────────
            var applyBtn = BuildBulkButton("", "Recommended",
                PillAccentBg, PillAccentBorder, PillAccentFg, PillAccentFg,
                "Apply the recommended value to every setting in this section",
                async () => await RunBulkAsync(entries, useRecommended: true, RefreshPill));

            var defaultBtn = BuildBulkButton(null, "Defaults",
                PillNeutralBg, PillNeutralBorder, PillNeutralFg, WinBlueIcon,
                "Reset every setting in this section to its Windows default",
                async () => await RunBulkAsync(entries, useRecommended: false, RefreshPill));

            // ── Header bar layout ─────────────────────────────────────────────
            var bar = new Grid { Margin = new Thickness(0, 2, 0, 4), Tag = "bulkbar" };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(pill, 0);
            pill.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Children.Add(pill);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(btnRow, 1);
            btnRow.Children.Add(applyBtn);
            btnRow.Children.Add(defaultBtn);
            bar.Children.Add(btnRow);

            section.Children.Insert(0, bar);

            _sectionPillRefresh[section] = RefreshPill;
            RefreshPill();
        }

        // ── Bulk execution (async dialogs) ────────────────────────────────────

        private static async Task RunBulkAsync(
            List<(TweakDefinition Def, Action Refresh)> entries,
            bool useRecommended,
            Action refreshPill)
        {
            var work = CollectPending(entries, useRecommended);

            if (work.Count == 0)
            {
                await AkariDialogs.InfoAsync(
                    useRecommended
                        ? "Every setting in this section already matches its recommended value."
                        : "Every setting in this section already matches its Windows default.",
                    "Nothing to change");
                return;
            }

            var warned = work
                .Select(w => (w.Def.Name, Warning: w.Def.InputKind == TweakInputKind.Toggle
                    ? w.Def.GetToggleWarning(w.ToggleTarget)
                    : w.Def.GetOptionWarning(w.OptionTarget)))
                .Where(w => w.Warning != null)
                .ToList();

            var msg = $"{(useRecommended ? "Apply recommended values to" : "Reset to Windows defaults")} " +
                      $"{work.Count} setting{(work.Count == 1 ? "" : "s")} in this section?";
            if (warned.Count > 0)
            {
                msg += "\n\nNote — the following carry warnings:\n" +
                       string.Join("\n", warned.Take(5).Select(w => $"  • {w.Name}: {w.Warning}"));
                if (warned.Count > 5) msg += $"\n  …and {warned.Count - 5} more.";
            }

            if (!await AkariDialogs.ConfirmOkCancelAsync(msg, useRecommended ? "Apply Recommended" : "Reset to Defaults"))
                return;

            int applied = 0, failed = 0;
            ExplorerRestart.BeginBatch();   // coalesce per-tweak Explorer restarts into one
            DriftBaseline.BeginBatch();     // coalesce per-tweak baseline writes into one
            try
            {
                foreach (var (def, refresh, toggleTarget, optionTarget) in work)
                {
                    try
                    {
                        if (def.InputKind == TweakInputKind.Toggle) ApplyToggle(def, toggleTarget);
                        else ApplyOption(def, optionTarget);
                        applied++;
                    }
                    catch { failed++; }
                    try { refresh(); } catch { }
                }
            }
            finally { DriftBaseline.EndBatch(); ExplorerRestart.EndBatch(); }

            refreshPill();

            if (failed > 0)
                await AkariDialogs.InfoAsync(
                    $"Applied {applied} setting{(applied == 1 ? "" : "s")}; {failed} failed " +
                    "(some tweaks require running Akari Tool as administrator).",
                    "Bulk apply finished");
        }

        // ── Button builder ────────────────────────────────────────────────────
        // Pill-shaped text button matching the quick-set icon language:
        // ★ for Recommended, ⊞ (4-square logo) for Windows Defaults.

        private static FrameworkElement BuildBulkButton(
            string? glyph, string label,
            Brush pillBg, Brush pillBorder, Brush pillFg, Brush iconBrush,
            string tooltip, Action onClick)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            if (glyph != null)
            {
                content.Children.Add(new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 11,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 5, 0),
                });
            }
            else
            {
                var logo = BuildWindowsLogoIcon(iconBrush, 10);
                logo.VerticalAlignment = VerticalAlignment.Center;
                logo.Margin = new Thickness(0, 0, 5, 0);
                content.Children.Add(logo);
            }
            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = pillFg,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var border = new Border
            {
                Background = pillBg,
                BorderBrush = pillBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius, // design: 8 — do not change
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(6, 0, 0, 0),
                Child = content,
            };

            var btn = BuildChromelessButton();
            btn.Content = border;
            ToolTipService.SetToolTip(btn, tooltip);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, label);
            // Hover: wash over the pill bg; restore the themed bg on exit.
            btn.PointerEntered += (_, _) => border.Background = TweakHelpers.Token("AkariOverlayMedium");
            btn.PointerExited  += (_, _) => border.Background = pillBg;
            btn.Click += (_, _) => onClick();
            return btn;
        }
    }
}
