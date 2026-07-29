using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
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
        /// </summary>
        public static Action AddTweakRow(StackPanel parent, TweakDefinition def)
        {
            // separator between rows
            if (parent.Children.Count > 0)
                parent.Children.Add(new Separator
                {
                    Background = TweakHelpers.Token("AkariOverlayStrong"), // V3 neutral row divider
                    Height = 1,
                    Margin = new Thickness(-18, 0, -18, 0)
                });

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

            // badge pill row (populated by refresh)
            var pillRow = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
            info.Children.Add(pillRow);

            topRow.Children.Add(info);

            // ── Control (toggle or dropdown) ──────────────────────────────────
            Action<bool>? toggleSetter = null;
            Action<int>? dropdownSetter = null;
            Action refreshBadges;

            if (def.InputKind == TweakInputKind.Dropdown && def.Options != null)
            {
                // Styling comes from the WPF-UI implicit ComboBox style (ui:ControlsDictionary).
                var dropdown = new ComboBox
                {
                    MinWidth = 210,
                    VerticalAlignment = VerticalAlignment.Center,
                };
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
                        onClick: () =>
                        {
                            if (!ConfirmWarning(def.Name, def.GetOptionWarning(recIdx))) return;
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
                        onClick: () =>
                        {
                            if (!ConfirmWarning(def.Name, def.GetOptionWarning(defIdx))) return;
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

                dropdown.SelectionChanged += (_, _) =>
                {
                    if (!initialized) return;
                    int newIdx = dropdown.SelectedIndex;
                    if (!ConfirmWarning(def.Name, def.GetOptionWarning(newIdx)))
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
                    // ReadCurrentIndex returning null means "current value matches no
                    // option" (custom locale, vendor-specific index). Leave the
                    // dropdown unselected (-1) rather than clamping to option 0 and
                    // implying a value the machine does not actually hold — nothing is
                    // written until the user picks an option.
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
                var (toggleControl, setter) = BuildToggle(newState =>
                {
                    if (!ConfirmWarning(def.Name, def.GetToggleWarning(newState)))
                    {
                        // user cancelled — flip the visual back, don't apply
                        setterRef?.Invoke(!newState);
                        return;
                    }
                    ApplyToggle(def, newState);
                    RefreshPills(pillRow, def.ComputeToggleBadges(newState));
                    NotifySectionChanged(parent);
                });
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
                        onClick: () =>
                        {
                            bool v = def.RecommendedState.Value;
                            if (!ConfirmWarning(def.Name, def.GetToggleWarning(v))) return;
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
                        onClick: () =>
                        {
                            bool v = def.DefaultState.Value;
                            if (!ConfirmWarning(def.Name, def.GetToggleWarning(v))) return;
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

        // ── Warning confirmation ──────────────────────────────────────────────

        /// <summary>
        /// Shows an OK/Cancel confirmation for a warned tweak value.
        /// Returns true when there is no warning or the user confirmed.
        /// </summary>
        private static bool ConfirmWarning(string tweakName, string? warning)
        {
            if (string.IsNullOrEmpty(warning)) return true;
            return AkariDialogs.ConfirmOkCancel(warning, tweakName);
        }

        // ── Chrome-free WPF-UI button (quick-set + bulk buttons) ─────────────
        // Transparent appearance with hover/press fills nulled out so the
        // content (glyph or pill Border) is the only visible chrome.

        internal static Wpf.Ui.Controls.Button BuildChromelessButton()
        {
            return new Wpf.Ui.Controls.Button
            {
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MouseOverBackground = System.Windows.Media.Brushes.Transparent,
                MouseOverBorderBrush = System.Windows.Media.Brushes.Transparent,
                PressedBackground = System.Windows.Media.Brushes.Transparent,
                PressedBorderBrush = System.Windows.Media.Brushes.Transparent,
                FocusVisualStyle = null,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        // ── Badge pill renderer ───────────────────────────────────────────────

        private static void RefreshPills(WrapPanel pillRow, TweakBadgePill[] pills)
        {
            pillRow.Children.Clear();
            foreach (var pill in pills)
                pillRow.Children.Add(BuildPill(pill));

            // A definition with no Recommended/Default/Preference metadata must render
            // exactly like the old AddToggleRow — an empty WrapPanel still contributes
            // its 5px top margin, so collapse it to reclaim the space.
            pillRow.Visibility = pills.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Quick-set button builder ──────────────────────────────────────
        // Transparent borderless button placed left of toggle/dropdown.
        // isRecommended=true → green ★ (E735);  false → grey Windows logo (4-square path)

        private static readonly System.Windows.Media.Geometry _windowsLogoGeometry =
            System.Windows.Media.Geometry.Parse("M0,0 H5 V5 H0 Z M6,0 H11 V5 H6 Z M0,6 H5 V11 H0 Z M6,6 H11 V11 H6 Z");

        private static FrameworkElement BuildQuickSetButton(bool isRecommended, string tooltip, Action onClick)
        {
            FrameworkElement icon;
            if (isRecommended)
            {
                // ★ filled star glyph
                icon = new TextBlock
                {
                    Text = "\uE735",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = TweakHelpers.StarGold,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Services.ThemeService.Color("AkariStarGoldColor"), BlurRadius = 9, ShadowDepth = 0, Opacity = 0.95 },
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
            }
            else
            {
                // ⊞ Windows logo — four squares path
                icon = new System.Windows.Shapes.Path
                {
                    Data = _windowsLogoGeometry,
                    Fill = TweakHelpers.WinBlueIcon,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Services.ThemeService.Color("AkariWinBlueIconColor"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 },
                    Width = 13,
                    Height = 13,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
            }

            var btn = BuildChromelessButton();
            btn.Content = icon;
            btn.Width = 28;
            btn.Height = 28;
            btn.MinWidth = 28;
            btn.MinHeight = 28;
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            btn.ToolTip = tooltip;
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private static Border BuildPill(TweakBadgePill pill)
        {
            // design: CornerRadius 8 — do not change
            // README 5a: transparent fill, 1px border + text in the pill colour;
            // active opacity 1.0, inactive 0.35.
            // Themed per-kind brush (live-updating; darkened in Light so it reads on white).
            var brush = pill.Kind switch
            {
                TweakBadgeKind.Preference  => TweakHelpers.PillPreference,
                TweakBadgeKind.Recommended => TweakHelpers.PillRecommended,
                TweakBadgeKind.Default     => TweakHelpers.PillDefault,
                TweakBadgeKind.Custom      => TweakHelpers.PillCustom,
                _ => TweakHelpers.PillGeneric,
            };

            // Recommended shares the header button's accent trio so the two
            // treatments stay identical: transparent fill, exact brand accent as both
            // the 1px border and the label, in both themes. Every other kind keeps
            // its own outline colour.
            bool isRecommended = pill.Kind == TweakBadgeKind.Recommended;

            var b = new Border
            {
                Background = isRecommended ? TweakHelpers.PillAccentBg : Brushes.Transparent,
                BorderBrush = isRecommended ? TweakHelpers.PillAccentBorder : brush,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius, // design: 8 — do not change
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = pill.Tooltip,
                Opacity = pill.IsActive ? 1.0 : 0.35,
            };
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
