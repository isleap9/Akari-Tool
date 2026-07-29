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
        // ── Toggle control ────────────────────────────────────────────────────

        /// <summary>
        /// Builds a toggle switch and returns both the control and a setter delegate
        /// that can flip the visual state programmatically (e.g. from ReadSettings).
        /// </summary>
        public static (FrameworkElement Control, Action<bool> Setter) BuildToggle(Action<bool>? onToggle = null)
        {
            // WPF-UI ToggleSwitch — the checked fill/glow comes from the crimson
            // accent overrides in AkariFluentTheme.xaml, no per-instance styling.
            var toggle = new Wpf.Ui.Controls.ToggleSwitch
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FocusVisualStyle = null,
            };

            // Click fires only on user interaction (mouse or keyboard), never when
            // IsChecked is written programmatically — the setter below relies on
            // that so warning-revert can flip the visual without re-firing Apply.
            toggle.Click += (_, _) => onToggle?.Invoke(toggle.IsChecked == true);

            Action<bool> setter = state =>
            {
                if ((toggle.IsChecked == true) == state) return;
                toggle.IsChecked = state;
            };

            return (toggle, setter);
        }

        // ── Tweak card (two-column grid cell) ─────────────────────────────────

        /// <summary>
        /// Builds a single tweak cell for use in a two-column toggle grid.
        /// Returns the cell Border and a setter delegate for ReadSettings restore.
        /// </summary>
        public static (Border Cell, Action<bool> Setter) BuildTweakCell(
            string title,
            string description,
            Action<bool>? onToggle)
        {
            var cell = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(4),
                Padding = new Thickness(16, 14, 16, 14),
                Tag = $"search:{title}|{description}"
            };
            // V3 hover: neutral surface lift + hairline (no icon in a tweak cell)
            cell.MouseEnter += (_, _) => { cell.Background = CardBgHover; cell.BorderBrush = HairlineHover; };
            cell.MouseLeave += (_, _) => { cell.Background = Brushes.Transparent; cell.BorderBrush = Brushes.Transparent; };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var (toggleControl, setter) = BuildToggle(onToggle);
            Grid.SetColumn(toggleControl, 1);

            row.Children.Add(info);
            row.Children.Add(toggleControl);
            cell.Child = row;

            return (cell, setter);
        }

        // ── Rounded clipping + shadow wrapping ────────────────────────────────

        /// <summary>
        /// Clips a Border's children to its own CornerRadius.
        ///
        /// WPF's Border does NOT clip children to the corner curve, and
        /// ClipToBounds is no help — it clips to the rectangular layout bounds.
        /// So any child with an opaque square-cornered Background that reaches the
        /// card's edge (a header banner, a full-bleed footer row) paints over the
        /// curve. This installs a real rounded geometry instead, recomputed on
        /// SizeChanged so it survives resize and content growth — never hardcoded.
        /// </summary>
        public static void ApplyRoundedClip(Border border)
        {
            void Update()
            {
                if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
                {
                    border.Clip = null;
                    return;
                }
                double r = border.CornerRadius.TopLeft;
                border.Clip = new RectangleGeometry(
                    new Rect(0, 0, border.ActualWidth, border.ActualHeight), r, r);
            }

            border.SizeChanged += (_, _) => Update();
            border.Loaded      += (_, _) => Update();
            Update();
        }

        /// <summary>
        /// Two-layer card shell: an OUTER Border carrying the shadow + BitmapCache,
        /// wrapping the supplied card which keeps its fill, gradient border and
        /// corner radius and gains a rounded clip.
        ///
        /// The two must not be the same element. A clip applies to the element's own
        /// rendering, so clipping the shadow-carrying Border would cut the blur off
        /// at the card edge; and a rectangular clip makes the effect's silhouette
        /// rectangular, which is what produced square shadow hooks outside the
        /// rounded corners. Separating them keeps the shadow outside the clip.
        /// </summary>
        public static Border ShadowWrapCard(Border card)
        {
            // The margin belongs to the outer element, or the shadow would be
            // offset relative to the space the card reserves in layout.
            var outer = new Border
            {
                Margin    = card.Margin,
                Effect    = ThemeService.CardShadowEffect,
                CacheMode = new BitmapCache(),
            };

            card.Margin       = new Thickness(0);
            card.Effect       = null;
            card.CacheMode    = null;
            card.ClipToBounds = false;   // superseded by the rounded clip below
            ApplyRoundedClip(card);

            outer.Child = card;
            return outer;
        }

        // ── Collapsible section card ──────────────────────────────────────────

        /// <summary>
        /// Tracks one section's collapse state. Collapse drives the INNER content
        /// panel's Visibility — never the card's — because search already owns the
        /// card's Visibility and the two must not fight.
        /// </summary>
        internal sealed class SectionCollapse
        {
            public required TextBlock  Chevron;
            public required StackPanel Body;
            public required string     Title;
            public bool UserCollapsed;          // the user's persisted choice
            public bool ForcedOpenBySearch;     // temporarily expanded for a query

            public void Render()
            {
                bool show = !UserCollapsed || ForcedOpenBySearch;
                Body.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                Chevron.Text    = show ? "▾" : "▸";
            }
        }

        internal static readonly Dictionary<StackPanel, SectionCollapse> SectionCollapseStates = new();

        // ── UI preference persistence ─────────────────────────────────────────
        // Collapse state is a UI preference, not tweak state, so it uses the same
        // HKCU\Software\AkariTool mechanism as ThemeService rather than
        // HasState/SaveState (which mark applied tweaks).

        private const string UiPrefKeyPath = @"HKEY_CURRENT_USER\Software\AkariTool";

        private static bool ReadUiPref(string name)
        {
            try { return Registry.GetValue(UiPrefKeyPath, name, null) is int i && i != 0; }
            catch { return false; }
        }

        private static void WriteUiPref(string name, bool value)
        {
            try { Registry.SetValue(UiPrefKeyPath, name, value ? 1 : 0, RegistryValueKind.DWord); }
            catch { /* best-effort — a lost UI preference must never break the tab */ }
        }

        /// <summary>
        /// Builds a collapsible card with an optional titled header row inside it.
        /// Returns the collapsible BODY StackPanel to add toggle rows into — the
        /// header lives above it in the card and stays visible when collapsed.
        /// </summary>
        public static StackPanel BuildSection(StackPanel parent, string? title = null)
        {
            // V3 flat premium section card: neutral surface + hairline border, subtle neutral shadow.
            var card = new Border
            {
                Background = CardBackground(),
                BorderBrush = CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(0, 0, 0, 16),
                Effect = CardShadow()
            };

            var content = new StackPanel { Margin = new Thickness(18, 4, 18, 4) };
            card.Child = content;
            parent.Children.Add(card);

            TextBlock? chevron = null;
            Grid? header = null;

            if (title is not null)
            {
                header = new Grid
                {
                    Tag        = "sectionheader",       // excluded from search filtering
                    Background = Brushes.Transparent,   // whole row hit-testable
                    Cursor     = System.Windows.Input.Cursors.Hand,
                    Margin     = new Thickness(0, 12, 0, 12)
                };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // In-card clickable header, matching the AkariOS tab's section headers
                var titleText = new TextBlock
                {
                    Text       = title,                      // NOT ToUpperInvariant — use as written
                    FontSize   = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextPrimary,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleText, 0);
                header.Children.Add(titleText);

                chevron = new TextBlock
                {
                    FontSize   = 13,
                    Foreground = TextSecondary,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(chevron, 1);
                header.Children.Add(chevron);

                content.Children.Add(header);
            }

            // The collapsible body. Tagged so BaseTab.FilterTweaks can find it
            // unambiguously — "first StackPanel child" would misfire on cards that
            // were not built here.
            var body = new StackPanel { Tag = "sectionbody" };
            content.Children.Add(body);

            if (title is not null && header is not null && chevron is not null)
            {
                string prefKey = "SectionCollapsed_" + title.Replace(" ", "");
                var st = new SectionCollapse
                {
                    Chevron = chevron, Body = body, Title = title,
                    UserCollapsed = ReadUiPref(prefKey)      // absent = false = EXPANDED default
                };
                SectionCollapseStates[body] = st;
                st.Render();

                header.MouseLeftButtonUp += (_, _) =>
                {
                    st.UserCollapsed = !st.UserCollapsed;
                    WriteUiPref(prefKey, st.UserCollapsed);
                    st.ForcedOpenBySearch = false;
                    st.Render();
                };
            }

            return body;
        }

        // ── Toggle row (for list-style sections) ──────────────────────────────

        /// <summary>
        /// Adds a single toggle row (separator + title/description + toggle) to a section panel.
        /// Returns the setter for ReadSettings restore.
        /// </summary>
        public static Action<bool> AddToggleRow(
            StackPanel parent,
            string title,
            string description,
            Action<bool>? onToggle = null)
        {
            if (parent.Children.Count > 0)
                parent.Children.Add(new Separator
                {
                    Background = Token("AkariOverlayStrong"), // V3 neutral row divider
                    Height = 1,
                    Margin = new Thickness(-18, 0, -18, 0)
                });

            var row = new Grid { Margin = new Thickness(0, 13, 0, 13), Tag = $"search:{title}|{description}" };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary
            });
            info.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var (toggleControl, setter) = BuildToggle(onToggle);
            Grid.SetColumn(toggleControl, 1);

            row.Children.Add(info);
            row.Children.Add(toggleControl);
            parent.Children.Add(row);

            return setter;
        }

        // ── Two-column toggle grid builder ────────────────────────────────────

        /// <summary>
        /// Builds a two-column grid of tweak cells from a list of (title, description, callback) tuples.
        /// Returns a dictionary of title → setter for use in ReadSettings.
        /// </summary>
        public static Dictionary<string, Action<bool>> BuildTweakGrid(
            StackPanel parent,
            (string Title, string Description, Action<bool>? Callback)[] tweaks)
        {
            var setters = new Dictionary<string, Action<bool>>();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rowCount = (int)Math.Ceiling(tweaks.Length / 2.0);
            for (int r = 0; r < rowCount; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < tweaks.Length; i++)
            {
                var (title, description, callback) = tweaks[i];
                var (cell, setter) = BuildTweakCell(title, description, callback);

                setters[title] = setter;

                Grid.SetColumn(cell, i % 2);
                Grid.SetRow(cell, i / 2);
                grid.Children.Add(cell);
            }

            var wrapper = new Border
            {
                Background = CardBackground(),
                BorderBrush = CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                ClipToBounds = true,
                Child = grid,
                Effect = CardShadow()
            };
            parent.Children.Add(wrapper);

            return setters;
        }

    }
}
