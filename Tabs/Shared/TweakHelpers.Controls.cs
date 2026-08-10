using Microsoft.UI;                        // Colors
using Microsoft.UI.Text;                   // FontWeights
using Microsoft.UI.Xaml;                   // Thickness, GridLength, Visibility, FrameworkElement
using Microsoft.UI.Xaml.Controls;          // Border, StackPanel, Grid, TextBlock, ToggleSwitch
using Microsoft.UI.Xaml.Hosting;           // ElementCompositionPreview (rounded clip)
using Microsoft.UI.Xaml.Media;             // SolidColorBrush
using Microsoft.Win32;
using AkariTool.Helpers;                   // AkariShadow
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        // WinUI has no Brushes class — small helper for fresh transparent brushes.
        private static SolidColorBrush Transparent() => new(Colors.Transparent);

        // ── Toggle control ────────────────────────────────────────────────────

        /// <summary>
        /// Builds a toggle switch and returns both the control and a setter delegate
        /// that can flip the visual state programmatically (e.g. from ReadSettings).
        ///
        /// MIGRATION NOTE: WPF-UI's ToggleSwitch had a Click event that fired only on
        /// user interaction. WinUI's Toggled fires on programmatic IsOn changes too,
        /// so a suppress flag restores the original semantics — the setter can flip
        /// the visual without re-firing Apply (warning-revert relies on that).
        /// </summary>
        public static (FrameworkElement Control, Action<bool> Setter) BuildToggle(Action<bool>? onToggle = null)
        {
            var toggle = new ToggleSwitch
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                // Compact, label-free like the WPF-UI switch.
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
                Background = Transparent(),
                BorderBrush = Transparent(),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(4),
                Padding = new Thickness(16, 14, 16, 14),
                Tag = $"search:{title}|{description}"
            };
            // V3 hover: neutral surface lift + hairline
            cell.PointerEntered += (_, _) => { cell.Background = CardBgHover; cell.BorderBrush = HairlineHover; };
            cell.PointerExited  += (_, _) => { cell.Background = Transparent(); cell.BorderBrush = Transparent(); };

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
        /// Clips a Border's children to its own CornerRadius via a composition
        /// rounded-rectangle clip (WinUI's XAML Clip only supports plain rectangles).
        /// Best-effort: failure just means square-cornered children can overdraw the
        /// curve, which is cosmetic.
        /// </summary>
        public static void ApplyRoundedClip(Border border)
        {
            void Update()
            {
                try
                {
                    if (border.ActualWidth <= 0 || border.ActualHeight <= 0) return;
                    var visual = ElementCompositionPreview.GetElementVisual(border);
                    var compositor = visual.Compositor;
                    float r = (float)border.CornerRadius.TopLeft;
                    var geometry = compositor.CreateRoundedRectangleGeometry();
                    geometry.CornerRadius = new System.Numerics.Vector2(r, r);
                    geometry.Size = new System.Numerics.Vector2((float)border.ActualWidth, (float)border.ActualHeight);
                    visual.Clip = compositor.CreateGeometricClip(geometry);
                }
                catch { /* cosmetic best-effort */ }
            }

            border.SizeChanged += (_, _) => Update();
            border.Loaded      += (_, _) => Update();
            Update();
        }

        /// <summary>
        /// Card shell wrapper. The WPF original layered a DropShadowEffect +
        /// BitmapCache on an outer Border; WinUI has no Effect, so this now only
        /// applies the rounded clip and returns a plain outer wrapper (API preserved
        /// for existing call sites). Shadows return in the post-migration cosmetic
        /// pass (Composition/ThemeShadow).
        /// </summary>
        public static Border ShadowWrapCard(Border card)
        {
            var outer = new Border { Margin = card.Margin };
            card.Margin = new Thickness(0);
            ApplyRoundedClip(card);

            // Host layer BEHIND the card carries its Composition drop shadow (WPF
            // AkariCardShadow; WinUI has no UIElement.Effect). A sibling Grid keeps the
            // shadow under the card in z-order — SetElementChildVisual renders above its
            // host's own content, so the host must not be the card's ancestor.
            var host = new Border();
            var cell = new Grid();
            cell.Children.Add(host);
            cell.Children.Add(card);
            outer.Child = cell;

            void Attach()
            {
                // Black, blur 12, depth 2, theme-dependent opacity — WPF AkariCardShadow
                // (0.45 dark / 0.18 light). NB: rectangular (Border exposes no alpha mask),
                // so shadow corners are not radius-matched — a minor, soft deviation.
                var shadow = AkariShadow.Attach(host, card,
                    Colors.Black, blurRadius: 12, opacity: CardShadowOpacity(), offsetY: 2);
                void OnTheme(AkariTheme _) => shadow.Opacity = CardShadowOpacity();
                ThemeService.ThemeChanged += OnTheme;
                card.Unloaded += (_, _) => ThemeService.ThemeChanged -= OnTheme;
            }
            if (card.ActualWidth > 0) Attach();
            else
            {
                void OnLoaded(object s, RoutedEventArgs e) { card.Loaded -= OnLoaded; Attach(); }
                card.Loaded += OnLoaded;
            }
            return outer;
        }

        // WPF AkariCardShadow opacity, per theme.
        private static float CardShadowOpacity() =>
            ThemeService.Current == AkariTheme.Light ? 0.18f : 0.45f;

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
            // V3 flat premium section card: neutral surface + hairline border.
            // (WPF carried Effect = CardShadow(); dropped — cosmetic pass.)
            var card = new Border
            {
                Background = CardBackground(),
                BorderBrush = CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(0, 0, 0, 16),
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
                    Background = Transparent(),         // whole row hit-testable
                    Margin     = new Thickness(0, 12, 0, 12)
                };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
            // unambiguously.
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

                header.Tapped += (_, _) =>
                {
                    st.UserCollapsed = !st.UserCollapsed;
                    WriteUiPref(prefKey, st.UserCollapsed);
                    st.ForcedOpenBySearch = false;
                    st.Render();
                };
            }

            return body;
        }

        // ── Row separator (WinUI has no Separator control) ────────────────────

        /// <summary>1px full-bleed divider; tagged so search filtering can pair it with its row.</summary>
        private static Border BuildRowSeparator() => new()
        {
            Background = Token("AkariOverlayStrong"),
            Height = 1,
            Margin = new Thickness(-18, 0, -18, 0),
            Tag = "separator",
        };

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
                parent.Children.Add(BuildRowSeparator());

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
                Child = grid,
            };
            ApplyRoundedClip(wrapper);
            parent.Children.Add(wrapper);

            return setters;
        }
    }
}
