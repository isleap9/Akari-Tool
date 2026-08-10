using Microsoft.UI;                       // Colors
using Microsoft.UI.Xaml;                  // Application, FrameworkElement, ElementTheme, ResourceDictionary
using Microsoft.UI.Xaml.Media;            // Brush, SolidColorBrush, LinearGradientBrush, GradientStop
using Microsoft.UI.Xaml.Media.Imaging;    // BitmapImage
using Windows.Foundation;                 // Point
using Windows.UI;                         // Color
using Microsoft.Win32;

namespace AkariTool.Services
{
    public enum AkariTheme { Dark, Light }

    /// <summary>
    /// Runtime theme switcher (WinUI 3). Flips the root element's
    /// <see cref="ElementTheme"/> — which re-resolves every {ThemeResource} against the
    /// Akari token ThemeDictionaries in App.xaml — and refreshes the shared "managed"
    /// brushes that C#-built UI (tab factories) assign directly and that ThemeResource
    /// cannot reach. Raises <see cref="ThemeChanged"/> so code-built surfaces (e.g. the
    /// About logo) can refresh raw Brush/ImageSource values. Persists the choice under
    /// HKCU\Software\AkariTool.
    ///
    /// WinUI notes vs the WPF original: no WPF-UI ApplicationThemeManager /
    /// ApplicationAccentColorManager (the crimson accent is baked into the App.xaml
    /// ThemeDictionaries as SystemAccentColor / AccentFillColor / ToggleSwitch overrides);
    /// no dictionary hot-swap (theme dictionaries switch by ElementTheme); no
    /// DropShadowEffect (CardShadowEffect dropped — see MIGRATION_LOG).
    /// </summary>
    public static class ThemeService
    {
        private const string StateKeyPath = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string ThemeValue   = "Theme";

        // The root element whose RequestedTheme drives {ThemeResource} re-resolution.
        // MainWindow supplies it via AttachRoot before the first Apply.
        private static FrameworkElement? _root;

        public static AkariTheme Current { get; private set; } = AkariTheme.Dark;

        /// <summary>
        /// Sets the element whose <see cref="FrameworkElement.RequestedTheme"/> drives
        /// {ThemeResource} resolution. Called first by App with the SPLASH root (so the
        /// splash paints in the right theme), then again by MainWindow with the shell
        /// root. Null is ignored so a caller can pass a not-yet-realised Content.
        /// </summary>
        public static void AttachRoot(FrameworkElement? root)
        {
            if (root is null) return;
            _root = root;
            // Re-assert on the new root so a window created after Apply() still matches.
            root.RequestedTheme = Current == AkariTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
        }

        /// <summary>
        /// The brand mark for the active theme — the SINGLE source of truth for the logo.
        /// AkariLogo.png = light mark for DARK backgrounds; AkariLogoLight.png = dark mark
        /// for LIGHT backgrounds.
        /// </summary>
        public static ImageSource Logo =>
            new BitmapImage(new Uri(Current == AkariTheme.Light
                ? "ms-appx:///Resource/AkariLogoLight.png"
                : "ms-appx:///Resource/AkariLogo.png"));

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

        /// <summary>Applies <paramref name="theme"/> and raises <see cref="ThemeChanged"/> (does NOT persist).</summary>
        public static void Apply(AkariTheme theme)
        {
            Current = theme;                                   // set first: Color(key) reads the active dict
            if (_root != null)
                _root.RequestedTheme = theme == AkariTheme.Light ? ElementTheme.Light : ElementTheme.Dark;

            RefreshManagedBrushes();
            ThemeChanged?.Invoke(theme);
        }

        /// <summary>Cycles Dark ⇄ Light, applies, and pins the choice.</summary>
        public static void Toggle()
        {
            var next = Current == AkariTheme.Dark ? AkariTheme.Light : AkariTheme.Dark;
            Apply(next);
            Persist(next);   // only an explicit toggle pins the theme; startup follows Windows
        }

        // ── Palette resolution from the App.xaml ThemeDictionaries ─────────────

        private static ResourceDictionary? ThemeDict(AkariTheme theme)
        {
            var name = theme == AkariTheme.Light ? "Light" : "Default";
            var app = Application.Current;
            if (app is null) return null;
            var tds = app.Resources.ThemeDictionaries;
            return tds.ContainsKey(name) ? tds[name] as ResourceDictionary : null;
        }

        /// <summary>Resolves a Brush token from the active theme dictionary.</summary>
        public static Brush Brush(string key)
        {
            var td = ThemeDict(Current);
            if (td != null && td.ContainsKey(key) && td[key] is Brush b) return b;
            return new SolidColorBrush(Colors.Transparent);
        }

        /// <summary>
        /// Resolves a Color from the active theme dictionary. Accepts either a Color
        /// token key or a SolidColorBrush token key (returns the brush's colour).
        /// </summary>
        public static Color Color(string key)
        {
            var td = ThemeDict(Current);
            if (td != null && td.ContainsKey(key))
            {
                return td[key] switch
                {
                    Color c => c,
                    SolidColorBrush b => b.Color,
                    _ => Colors.Transparent,
                };
            }
            return Colors.Transparent;
        }

        // ── Managed brushes (live theme switching for C#-built UI) ─────────────
        // C#-built controls assign a Brush snapshot that ThemeResource can't reach.
        // ManagedBrush returns ONE persistent SolidColorBrush per token that every call
        // site shares; on Apply we mutate each brush's Colour in place, so all controls
        // referencing it re-paint with no per-call-site work.
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
                brush.Color = Color(key);
            RefreshCardElevation();
        }

        // ── Managed card-elevation gradient (live theme switching) ─────────────
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
                            new GradientStop { Color = Color("AkariCardElevationLitColor"),    Offset = 0 },
                            new GradientStop { Color = Color("AkariCardElevationShadowColor"), Offset = 1 },
                        },
                    };
                }
                return _cardElevation;
            }
        }

        private static void RefreshCardElevation()
        {
            if (_cardElevation is null) return;
            _cardElevation.GradientStops[0].Color = Color("AkariCardElevationLitColor");
            _cardElevation.GradientStops[1].Color = Color("AkariCardElevationShadowColor");
        }
    }
}
