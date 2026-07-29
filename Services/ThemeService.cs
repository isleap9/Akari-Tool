using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace AkariTool.Services
{
    public enum AkariTheme { Dark, Light }

    /// <summary>
    /// Runtime theme switcher. Swaps the active Themes/Akari.*.xaml token dictionary,
    /// re-applies the WPF-UI application theme + the fixed Akari crimson accent, and
    /// raises <see cref="ThemeChanged"/> so code-built UI (splash pips, tab factories)
    /// can refresh raw Color/Brush values that don't live-update via DynamicResource.
    /// Persists the choice under HKCU\Software\AkariTool (same store as the power scheme).
    /// </summary>
    public static class ThemeService
    {
        private const string StateKeyPath = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string ThemeValue   = "Theme";

        // The fixed Akari crimson accent (identical in both themes). WPF-UI 4.1.0 otherwise
        // re-derives the palette keys from the Windows personalisation colour, so we must
        // re-apply this after every ApplicationThemeManager.Apply.
        private static readonly Color AccentBase      = FromHex("#E0142A");
        private static readonly Color AccentPrimary   = FromHex("#E0142A");
        private static readonly Color AccentSecondary = FromHex("#FF2438");
        private static readonly Color AccentTertiary  = FromHex("#FF4150");

        public static AkariTheme Current { get; private set; } = AkariTheme.Dark;

        /// <summary>
        /// The brand mark for the active theme — the SINGLE source of truth for the logo.
        /// AkariLogo.png = light-coloured mark for DARK backgrounds; AkariLogoLight.png =
        /// black/red mark for LIGHT backgrounds. Every logo surface must read this property
        /// (in its ctor and again on <see cref="ThemeChanged"/>); no call site hardcodes a URI.
        /// </summary>
        public static ImageSource Logo =>
            new BitmapImage(new Uri(Current == AkariTheme.Light
                ? "pack://application:,,,/Resource/AkariLogoLight.png"
                : "pack://application:,,,/Resource/AkariLogo.png"));

        /// <summary>Raised after a theme is applied. Argument is the new theme.</summary>
        public static event Action<AkariTheme>? ThemeChanged;

        /// <summary>
        /// The startup theme: an explicit user choice if one was persisted,
        /// otherwise whatever Windows is currently using (apps theme).
        /// </summary>
        public static AkariTheme LoadPersisted()
        {
            var stored = Registry.GetValue(StateKeyPath, ThemeValue, null) as string;
            if (string.Equals(stored, "Light", StringComparison.OrdinalIgnoreCase)) return AkariTheme.Light;
            if (string.Equals(stored, "Dark",  StringComparison.OrdinalIgnoreCase)) return AkariTheme.Dark;
            return DetectSystemTheme();
        }

        /// <summary>Windows apps theme: AppsUseLightTheme 1 = Light, 0/absent = Dark.</summary>
        private static AkariTheme DetectSystemTheme()
        {
            var v = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", null);
            return v is int i && i != 0 ? AkariTheme.Light : AkariTheme.Dark;
        }

        private static void Persist(AkariTheme t) =>
            Registry.SetValue(StateKeyPath, ThemeValue, t.ToString());

        /// <summary>Applies <paramref name="theme"/>, persists it, and raises <see cref="ThemeChanged"/>.</summary>
        public static void Apply(AkariTheme theme)
        {
            SwapTokenDictionary(theme);

            ApplicationThemeManager.Apply(
                theme == AkariTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark);

            // Re-assert the Akari crimson accent (WPF-UI would otherwise re-derive from Windows).
            ApplicationAccentColorManager.Apply(AccentBase, AccentPrimary, AccentSecondary, AccentTertiary);

            Current = theme;
            RefreshManagedBrushes();
            ThemeChanged?.Invoke(theme);
        }

        /// <summary>Cycles Dark ⇄ Light and applies.</summary>
        public static void Toggle()
        {
            var next = Current == AkariTheme.Dark ? AkariTheme.Light : AkariTheme.Dark;
            Apply(next);
            Persist(next);   // only an explicit toggle pins the theme; startup follows Windows
        }

        // Replaces the merged Akari.Dark/Light dictionary in place (same slot ordering)
        // so the agnostic AkariFluentTheme styles still resolve tokens after it.
        private static void SwapTokenDictionary(AkariTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            var uri = new Uri(
                $"pack://application:,,,/Themes/Akari.{theme}.xaml", UriKind.Absolute);
            var next = new ResourceDictionary { Source = uri };

            var merged = app.Resources.MergedDictionaries;
            int idx = -1;
            for (int i = 0; i < merged.Count; i++)
            {
                var src = merged[i].Source?.OriginalString ?? "";
                if (src.Contains("Akari.Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("Akari.Light.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0) merged[idx] = next;
            else merged.Add(next);
        }

        // ── Helpers for C# call sites ─────────────────────────────────────────
        /// <summary>Resolves a Brush token from the active theme dictionary.</summary>
        public static Brush Brush(string key) =>
            (Application.Current?.Resources[key] as Brush) ?? Brushes.Transparent;

        /// <summary>
        /// Resolves a Color from the active theme dictionary. Accepts either a Color
        /// token key or a SolidColorBrush token key (returns the brush's colour).
        /// </summary>
        public static Color Color(string key)
        {
            var r = Application.Current?.Resources[key];
            return r switch
            {
                Color c => c,
                SolidColorBrush b => b.Color,
                _ => Colors.Transparent,
            };
        }

        // ── Managed brushes (live theme switching for C#-built UI) ─────────────
        // C#-built controls assign a Brush snapshot that DynamicResource can't reach.
        // ManagedBrush returns ONE persistent, unfrozen SolidColorBrush per token that
        // every call site shares; on Apply we mutate each brush's Colour in place, so
        // all controls referencing it re-paint with no per-call-site work.
        private static readonly Dictionary<string, SolidColorBrush> _managed = new();

        /// <summary>
        /// A persistent shared brush for <paramref name="key"/> (a Color- or Brush-token
        /// key) that live-updates on theme switch. Reuse the same key everywhere.
        /// </summary>
        public static SolidColorBrush ManagedBrush(string key)
        {
            if (!_managed.TryGetValue(key, out var b))
            {
                b = new SolidColorBrush(Color(key));
                _managed[key] = b;
            }
            return b;
        }

        private static void RefreshManagedBrushes()
        {
            foreach (var (key, brush) in _managed)
            {
                // A shared brush must never be handed to a code-built Style Setter — sealing
                // the style freezes it. Guard anyway so a stray frozen brush can't crash the
                // switch (it just won't live-update). Use ManagedBrush(key).Clone() in setters.
                if (!brush.IsFrozen)
                    brush.Color = Color(key);
            }
            RefreshCardElevation();
        }

        // ── Managed card-elevation gradient (live theme switching) ─────────────
        // The gradient counterpart to ManagedBrush: one shared, unfrozen vertical
        // LinearGradientBrush whose two stops are mutated in place on theme switch,
        // so C#-built card borders re-paint like the solid managed brushes do.
        // XAML consumers use {DynamicResource AkariCardElevationBorder} instead.
        private static LinearGradientBrush? _cardElevation;

        /// <summary>
        /// Shared vertical elevation stroke (lit top → shadow bottom) for card and
        /// content-frame edges built in C#. Live-updates on theme switch.
        /// </summary>
        public static LinearGradientBrush CardElevationBorder
        {
            get
            {
                if (_cardElevation is null)
                {
                    _cardElevation = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint   = new Point(0, 1),
                        GradientStops =
                        {
                            new GradientStop(Color("AkariCardElevationLitColor"),    0),
                            new GradientStop(Color("AkariCardElevationShadowColor"), 1),
                        },
                    };
                }
                return _cardElevation;
            }
        }

        private static void RefreshCardElevation()
        {
            if (_cardElevation is null || _cardElevation.IsFrozen) return;
            _cardElevation.GradientStops[0].Color = Color("AkariCardElevationLitColor");
            _cardElevation.GradientStops[1].Color = Color("AkariCardElevationShadowColor");
            RefreshCardShadow();
        }

        // ── Managed card shadow (live theme switching) ─────────────────────────
        // Same shared-instance trick as the brushes: one unfrozen DropShadowEffect
        // whose Opacity is re-read on switch, since dark needs a much stronger
        // shadow than light to read at all against its canvas.
        private static System.Windows.Media.Effects.DropShadowEffect? _cardShadow;

        /// <summary>
        /// Shared card-face shadow. Callers MUST also set
        /// <c>CacheMode = new BitmapCache()</c> — the effect pipeline is what costs
        /// scroll and resize latency, and caching is what makes it affordable.
        /// </summary>
        public static System.Windows.Media.Effects.DropShadowEffect CardShadowEffect
        {
            get
            {
                _cardShadow ??= new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Colors.Black,
                    Direction   = 270,
                    ShadowDepth = 2,
                    BlurRadius  = 12,
                    Opacity     = CardShadowOpacity(),
                };
                return _cardShadow;
            }
        }

        private static double CardShadowOpacity() =>
            Application.Current?.Resources["AkariCardShadow"]
                is System.Windows.Media.Effects.DropShadowEffect e ? e.Opacity : 0.45;

        private static void RefreshCardShadow()
        {
            if (_cardShadow is null || _cardShadow.IsFrozen) return;
            _cardShadow.Opacity = CardShadowOpacity();
        }

        private static Color FromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
