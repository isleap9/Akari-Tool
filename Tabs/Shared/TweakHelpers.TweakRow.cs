using Microsoft.UI.Text;                   // FontWeights
using Microsoft.UI.Xaml;                   // Thickness, GridLength, Visibility, FrameworkElement
using Microsoft.UI.Xaml.Automation;        // AutomationProperties
using Microsoft.UI.Xaml.Controls;          // StackPanel, Grid, TextBlock, ComboBox, Border, Button
using Microsoft.UI.Xaml.Media;             // Brush, FontFamily
using Microsoft.UI.Xaml.Shapes;            // Rectangle (Windows-logo icon)
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        // ── TweakDefinition row renderer ─────────────────────────────────────

        /// <summary>
        /// Renders a full TweakDefinition as a list row (toggle or dropdown),
        /// complete with badge pills. Returns a refresh delegate that re-reads
        /// state from the system and updates badges + toggle visuals.
        ///
        /// MIGRATION NOTE: warning confirmations are now async (ContentDialog);
        /// interaction handlers await them before applying. Controls carry
        /// AutomationProperties.Name = def.Name for accessibility/UIA.
        /// </summary>
        public static Action AddTweakRow(StackPanel parent, TweakDefinition def)
        {
            // separator between rows
            if (parent.Children.Count > 0)
                parent.Children.Add(BuildRowSeparator());

            var row = new StackPanel { Margin = new Thickness(0, 13, 0, 13), Tag = $"search:{def.Name}|{def.Description}" };

            // ── Top: title row + control ──────────────────────────────────────
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(info, 0);

            info.Children.Add(new TextBlock
            {
                Text = def.Name,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = def.Description,
                FontSize = 12,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            // badge pill row (populated by refresh).
            // MIGRATION NOTE: was a WrapPanel; WinUI ships no in-box WrapPanel, and a
            // row carries at most ~3 short pills, so a horizontal StackPanel suffices.
            var pillRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            info.Children.Add(pillRow);

            topRow.Children.Add(info);

            // ── Control (toggle or dropdown) ──────────────────────────────────
            Action<bool>? toggleSetter = null;
            Action<int>? dropdownSetter = null;
            Action refreshBadges;

            if (def.InputKind == TweakInputKind.Dropdown && def.Options != null)
            {
                var dropdown = new ComboBox
                {
                    MinWidth = 210,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                AutomationProperties.SetName(dropdown, def.Name);
                foreach (var opt in def.Options)
                    dropdown.Items.Add(opt.Label);

                bool initialized = false;
                int lastIndex = 0;
                dropdown.Loaded += (_, _) => initialized = true;

                // ── Quick-set buttons (★ recommended, ⊞ default) before dropdown ──
                var ddWrapper = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(ddWrapper, 1);

                int recIdx = def.Options != null ? Array.FindIndex(def.Options, o => o.IsRecommended) : -1;
                int defIdx = def.Options != null ? Array.FindIndex(def.Options, o => o.IsDefault) : -1;

                if (recIdx >= 0)
                    ddWrapper.Children.Add(BuildQuickSetButton(
                        isRecommended: true,
                        tooltip: $"Apply recommended: {def.Options![recIdx].Label}",
                        onClick: async () =>
                        {
                            if (!await ConfirmWarningAsync(def.Name, def.GetOptionWarning(recIdx))) return;
                            ApplyOption(def, recIdx);
                            initialized = false;
                            dropdown.SelectedIndex = recIdx;
                            initialized = true;
                            lastIndex = recIdx;
                            RefreshPills(pillRow, def.ComputeDropdownBadges(recIdx));
                            NotifySectionChanged(parent);
                        }));

                if (defIdx >= 0)
                    ddWrapper.Children.Add(BuildQuickSetButton(
                        isRecommended: false,
                        tooltip: $"Apply Windows default: {def.Options![defIdx].Label}",
                        onClick: async () =>
                        {
                            if (!await ConfirmWarningAsync(def.Name, def.GetOptionWarning(defIdx))) return;
                            ApplyOption(def, defIdx);
                            initialized = false;
                            dropdown.SelectedIndex = defIdx;
                            initialized = true;
                            lastIndex = defIdx;
                            RefreshPills(pillRow, def.ComputeDropdownBadges(defIdx));
                            NotifySectionChanged(parent);
                        }));

                ddWrapper.Children.Add(dropdown);
                topRow.Children.Add(ddWrapper);

                dropdown.SelectionChanged += async (_, _) =>
                {
                    if (!initialized) return;
                    int newIdx = dropdown.SelectedIndex;
                    if (!await ConfirmWarningAsync(def.Name, def.GetOptionWarning(newIdx)))
                    {
                        // user cancelled — revert selection without re-applying
                        initialized = false;
                        dropdown.SelectedIndex = lastIndex;
                        initialized = true;
                        return;
                    }
                    lastIndex = newIdx;
                    ApplyOption(def, newIdx);
                    RefreshPills(pillRow, def.ComputeDropdownBadges(newIdx));
                    NotifySectionChanged(parent);
                };

                dropdownSetter = idx =>
                {
                    initialized = false;
                    dropdown.SelectedIndex = idx;
                    initialized = true;
                    lastIndex = idx;
                };

                refreshBadges = () =>
                {
                    // null from ReadCurrentIndex = "matches no option": leave unselected.
                    var idx = def.ReadCurrentIndex?.Invoke() ?? -1;
                    initialized = false;
                    dropdown.SelectedIndex = Math.Max(-1, idx);
                    initialized = true;
                    lastIndex = dropdown.SelectedIndex;
                    RefreshPills(pillRow, def.ComputeDropdownBadges(dropdown.SelectedIndex));
                };
            }
            else
            {
                // Toggle
                bool currentState = def.ReadState?.Invoke() ?? false;
                Action<bool>? setterRef = null; // assigned right after BuildToggle
                var (toggleControl, setter) = BuildToggle(async newState =>
                {
                    if (!await ConfirmWarningAsync(def.Name, def.GetToggleWarning(newState)))
                    {
                        // user cancelled — flip the visual back, don't apply
                        setterRef?.Invoke(!newState);
                        return;
                    }
                    ApplyToggle(def, newState);
                    RefreshPills(pillRow, def.ComputeToggleBadges(newState));
                    NotifySectionChanged(parent);
                });
                AutomationProperties.SetName(toggleControl, def.Name);
                setter(currentState);
                setterRef = setter;
                toggleSetter = setter;

                // ── Quick-set buttons (★ recommended, ⊞ default) before toggle ──
                var tgWrapper = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tgWrapper, 1);

                if (def.RecommendedState.HasValue)
                    tgWrapper.Children.Add(BuildQuickSetButton(
                        isRecommended: true,
                        tooltip: def.InvertBadgeLabelWording
                            ? $"Apply recommended: {(def.RecommendedState.Value ? "Off" : "On")}"
                            : $"Apply recommended: {(def.RecommendedState.Value ? "On" : "Off")}",
                        onClick: async () =>
                        {
                            bool v = def.RecommendedState.Value;
                            if (!await ConfirmWarningAsync(def.Name, def.GetToggleWarning(v))) return;
                            ApplyToggle(def, v);
                            toggleSetter?.Invoke(v);
                            RefreshPills(pillRow, def.ComputeToggleBadges(v));
                            NotifySectionChanged(parent);
                        }));

                if (def.DefaultState.HasValue)
                    tgWrapper.Children.Add(BuildQuickSetButton(
                        isRecommended: false,
                        tooltip: def.InvertBadgeLabelWording
                            ? $"Apply Windows default: {(def.DefaultState.Value ? "Off" : "On")}"
                            : $"Apply Windows default: {(def.DefaultState.Value ? "On" : "Off")}",
                        onClick: async () =>
                        {
                            bool v = def.DefaultState.Value;
                            if (!await ConfirmWarningAsync(def.Name, def.GetToggleWarning(v))) return;
                            ApplyToggle(def, v);
                            toggleSetter?.Invoke(v);
                            RefreshPills(pillRow, def.ComputeToggleBadges(v));
                            NotifySectionChanged(parent);
                        }));

                tgWrapper.Children.Add(toggleControl);
                topRow.Children.Add(tgWrapper);

                refreshBadges = () =>
                {
                    var state = def.ReadState?.Invoke() ?? false;
                    toggleSetter?.Invoke(state);
                    RefreshPills(pillRow, def.ComputeToggleBadges(state));
                };

                // seed initial badges without invoking Apply
                RefreshPills(pillRow, def.ComputeToggleBadges(currentState));
            }

            row.Children.Add(topRow);
            parent.Children.Add(row);

            // initial seed for dropdowns
            if (def.InputKind == TweakInputKind.Dropdown)
                refreshBadges();

            TweakRegistry.Register(def, refreshBadges);
            RegisterSectionEntry(parent, def, refreshBadges);

            return refreshBadges;
        }

        // ── Warning confirmation (async — see AkariDialogs migration note) ────

        /// <summary>
        /// Shows an OK/Cancel confirmation for a warned tweak value.
        /// Returns true when there is no warning or the user confirmed.
        /// </summary>
        private static Task<bool> ConfirmWarningAsync(string tweakName, string? warning)
        {
            if (string.IsNullOrEmpty(warning)) return Task.FromResult(true);
            return AkariDialogs.ConfirmOkCancelAsync(warning, tweakName);
        }

        // ── Chrome-free button (quick-set + bulk buttons) ─────────────────────
        // WinUI Button with its hover/press theme fills nulled out per-instance so
        // the content (glyph or pill Border) is the only visible chrome.

        internal static Button BuildChromelessButton()
        {
            var btn = new Button
            {
                Background = Transparent(),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            btn.Resources["ButtonBackgroundPointerOver"] = Transparent();
            btn.Resources["ButtonBackgroundPressed"]     = Transparent();
            btn.Resources["ButtonBorderBrushPointerOver"] = Transparent();
            btn.Resources["ButtonBorderBrushPressed"]     = Transparent();
            return btn;
        }

        // ── Badge pill renderer ───────────────────────────────────────────────

        private static void RefreshPills(StackPanel pillRow, TweakBadgePill[] pills)
        {
            pillRow.Children.Clear();
            foreach (var pill in pills)
                pillRow.Children.Add(BuildPill(pill));

            // Collapse the empty pill row so its 5px top margin doesn't linger.
            pillRow.Visibility = pills.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Quick-set button builder ──────────────────────────────────────
        // Transparent borderless button placed left of toggle/dropdown.
        // isRecommended=true → gold ★ (E735);  false → blue Windows logo (4 squares)

        /// <summary>
        /// The ⊞ Windows-logo mark as a 2×2 grid of squares. Replaces the WPF
        /// Geometry.Parse path (WinUI has no C# geometry parser).
        /// </summary>
        internal static FrameworkElement BuildWindowsLogoIcon(Brush fill, double size)
        {
            double cell = (size - 1) / 2.0;   // 1px gutter
            var g = new Grid { Width = size, Height = size };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
            foreach (var (r, c) in new[] { (0, 0), (0, 2), (2, 0), (2, 2) })
            {
                var sq = new Rectangle { Fill = fill };
                Grid.SetRow(sq, r);
                Grid.SetColumn(sq, c);
                g.Children.Add(sq);
            }
            return g;
        }

        private static FrameworkElement BuildQuickSetButton(bool isRecommended, string tooltip, Action onClick)
        {
            FrameworkElement icon;
            if (isRecommended)
            {
                // ★ filled star glyph (glow effect dropped — cosmetic pass)
                icon = new TextBlock
                {
                    Text = "",
                    FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = TweakHelpers.StarGold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
            }
            else
            {
                icon = BuildWindowsLogoIcon(TweakHelpers.WinBlueIcon, 13);
                icon.VerticalAlignment = VerticalAlignment.Center;
                icon.HorizontalAlignment = HorizontalAlignment.Center;
            }

            var btn = BuildChromelessButton();
            btn.Content = icon;
            btn.Width = 28;
            btn.Height = 28;
            btn.MinWidth = 28;
            btn.MinHeight = 28;
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            ToolTipService.SetToolTip(btn, tooltip);
            AutomationProperties.SetName(btn, tooltip);
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private static Border BuildPill(TweakBadgePill pill)
        {
            // design: transparent fill, 1px border + text in the pill colour;
            // active opacity 1.0, inactive 0.35.
            var brush = pill.Kind switch
            {
                TweakBadgeKind.Preference  => TweakHelpers.PillPreference,
                TweakBadgeKind.Recommended => TweakHelpers.PillRecommended,
                TweakBadgeKind.Default     => TweakHelpers.PillDefault,
                TweakBadgeKind.Custom      => TweakHelpers.PillCustom,
                _ => TweakHelpers.PillGeneric,
            };

            bool isRecommended = pill.Kind == TweakBadgeKind.Recommended;

            var b = new Border
            {
                Background = isRecommended ? TweakHelpers.PillAccentBg : Transparent(),
                BorderBrush = isRecommended ? TweakHelpers.PillAccentBorder : brush,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius, // design: 8 — do not change
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 0, 6, 0),
                Opacity = pill.IsActive ? 1.0 : 0.35,
            };
            ToolTipService.SetToolTip(b, pill.Tooltip);
            b.Child = new TextBlock
            {
                Text = pill.Label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = isRecommended ? TweakHelpers.PillAccentFg : brush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return b;
        }
    }
}
