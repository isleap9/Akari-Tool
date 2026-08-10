using Microsoft.UI.Text;                 // FontWeights
using Microsoft.UI.Xaml;                 // Thickness, GridLength
using Microsoft.UI.Xaml.Controls;        // StackPanel, Grid, TextBlock, Button, Border
using Microsoft.UI.Xaml.Documents;       // Run, Hyperlink
using Microsoft.UI.Xaml.Media;           // ImageBrush, Stretch
using Microsoft.UI.Xaml.Media.Imaging;   // (ImageSource via ThemeService)
using Microsoft.UI.Xaml.Shapes;          // Ellipse
using Windows.UI;                        // Color
using AkariTool.Helpers;                 // AkariShadow
using AkariTool.Services;

namespace AkariTool.Tabs.About
{
    /// <summary>
    /// App info page: header card (logo + version + tagline), Environment & Credits
    /// cards, and the two external links (Repository, .NET 8 Runtime).
    /// </summary>
    public partial class AboutTab : BaseTab
    {
        private const string RepoUrl = "https://github.com/isleap9/Akari-Tool";
        private const string RuntimeUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";

        public AboutTab() => InitializeComponent();

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.MaxWidth = 860;
            RootPanel.HorizontalAlignment = HorizontalAlignment.Center;

            RootPanel.Children.Add(Eyebrow("About"));
            RootPanel.Children.Add(HeaderCard());
            RootPanel.Children.Add(InfoCards());
            RootPanel.Children.Add(LinkRow());
        }

        // Brand mark for the active theme — routes through the single source of truth.
        private static ImageBrush ThemeLogoBrush() =>
            new() { ImageSource = ThemeService.Logo, Stretch = Stretch.UniformToFill };

        // ── Header card: logo · title · version pill · tagline ──────────────────
        private static Border HeaderCard()
        {
            var card = Card(new Thickness(24, 22, 24, 22), new Thickness(0, 14, 0, 0));

            // WPF parity: logo (col 0, Auto) + text column (col 1, *). A Grid — not a
            // horizontal StackPanel — is what bounds the text column's width, so the
            // tagline's TextWrapping actually engages instead of clipping ("cleaner frame d…").
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Logo cell: an empty shadow host behind the brand ellipse carries the WPF logo
            // glow (black, BlurRadius 22, Opacity 0.5) via Composition (WinUI has no Effect).
            var logoCell = new Grid { Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
            var logoShadowHost = new Border();
            var logo = new Ellipse
            {
                Width = 58,
                Height = 58,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = ThemeLogoBrush(),
            };
            logoCell.Children.Add(logoShadowHost);
            logoCell.Children.Add(logo);
            Grid.SetColumn(logoCell, 0);
            row.Children.Add(logoCell);
            logo.Loaded += (_, _) =>
                AkariShadow.Attach(logoShadowHost, logo,
                    Color.FromArgb(0xFF, 0, 0, 0), blurRadius: 22, opacity: 0.5f);

            // Swap the brand mark when the theme changes; unsubscribe when the card leaves
            // the tree so the static event doesn't leak the destroyed Ellipse.
            void OnTheme(AkariTheme _) => logo.Fill = ThemeLogoBrush();
            ThemeService.ThemeChanged += OnTheme;
            logo.Unloaded += (_, _) => ThemeService.ThemeChanged -= OnTheme;

            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(textCol, 1);

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = "Akari Tool",
                FontFamily = DisplayFont,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            });
            titleRow.Children.Add(VersionPill());
            textCol.Children.Add(titleRow);

            textCol.Children.Add(new TextBlock
            {
                Text = "A gaming-first Windows optimization utility built for performance — " +
                       "lower DPC latency, faster response times, cleaner frame delivery.",
                FontSize = 13,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });
            row.Children.Add(textCol);

            card.Child = row;
            return card;
        }

        private static Border VersionPill() => new()
        {
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(10, 3, 10, 3),
            CornerRadius = TweakHelpers.ControlRadius,
            Background = TweakHelpers.Token("AkariOverlaySoft"),
            BorderBrush = TweakHelpers.Hairline,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = UpdateService.CurrentVersionDisplay,
                FontFamily = MonoFont,
                FontSize = 10.5,
                Foreground = TweakHelpers.AccentTextMuted
            }
        };

        // ── Environment + Credits cards side by side ────────────────────────────
        private static UIElement InfoCards()
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var env = Card(new Thickness(18, 16, 18, 16), new Thickness(0));
            var envStack = new StackPanel();
            envStack.Children.Add(CardLabel("Environment"));
            envStack.Children.Add(InfoRow("Platform", "Windows 11 (x64)"));
            envStack.Children.Add(InfoRow("Framework", ".NET 8 Desktop"));
            envStack.Children.Add(InfoRow("UI", "WinUI 3 (Windows App SDK)"));
            envStack.Children.Add(InfoRow("License", "MIT"));
            env.Child = envStack;
            Grid.SetColumn(env, 0);

            var credits = Card(new Thickness(18, 16, 18, 16), new Thickness(0));
            var creditsStack = new StackPanel();
            creditsStack.Children.Add(CardLabel("Credits"));
            var body = new TextBlock
            {
                FontSize = 12.5,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };
            body.Inlines.Add(new Run { Text = "Registry tweak references from " });
            body.Inlines.Add(new Run { Text = "CTT WinUtil", Foreground = TweakHelpers.AccentTextMuted });
            body.Inlines.Add(new Run { Text = " and " });
            body.Inlines.Add(new Run { Text = "Winhance", Foreground = TweakHelpers.AccentTextMuted });
            body.Inlines.Add(new Run { Text = ". Advanced Tools ISO flow ported from Winhance. " +
                                     "NVIDIA profile applied with nvidiaProfileInspector by Orbmu2k (MIT). " +
                                     "Made for the AkariOS ecosystem." });
            body.Inlines.Add(new Run { Text = " Sidebar icons by " });
            var icons8 = new Hyperlink { NavigateUri = new Uri("https://icons8.com") };
            icons8.Inlines.Add(new Run { Text = "Icons8" });
            body.Inlines.Add(icons8);
            body.Inlines.Add(new Run { Text = "." });
            creditsStack.Children.Add(body);
            credits.Child = creditsStack;
            Grid.SetColumn(credits, 2);

            grid.Children.Add(env);
            grid.Children.Add(credits);
            return grid;
        }

        private static UIElement InfoRow(string label, string value)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 7)
            };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 96,
                FontFamily = MonoFont,
                FontSize = 12,
                Foreground = TweakHelpers.IconNeutral
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                FontFamily = MonoFont,
                FontSize = 12,
                Foreground = TweakHelpers.TextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            return row;
        }

        // ── Link buttons: Repository · .NET 8 Runtime ───────────────────────────
        private UIElement LinkRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 9,
                Margin = new Thickness(0, 12, 0, 0)
            };
            row.Children.Add(LinkButton("Repository", RepoUrl));
            row.Children.Add(LinkButton(".NET 8 Runtime", RuntimeUrl));
            return row;
        }

        private Button LinkButton(string text, string url)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(16, 6, 16, 6)
            };
            btn.Click += (_, _) => Service?.OpenUrl(url);
            return btn;
        }

        // ── Shared card / label helpers ─────────────────────────────────────────
        private static Border Card(Thickness padding, Thickness margin) => new()
        {
            Padding = padding,
            Margin = margin,
            CornerRadius = TweakHelpers.CardRadius,
            Background = TweakHelpers.CardBg,
            BorderBrush = TweakHelpers.CardElevationBorder,
            BorderThickness = new Thickness(1)
        };

        private static TextBlock CardLabel(string text) => new()
        {
            Text = text,
            FontFamily = MonoFont,
            FontSize = 10,
            Foreground = TweakHelpers.TextMuted,
            Margin = new Thickness(0, 0, 0, 10)
        };

        private static TextBlock Eyebrow(string text) => new()
        {
            Text = text,
            FontFamily = MonoFont,
            FontSize = 10,
            Foreground = TweakHelpers.TextMuted,
            Margin = new Thickness(0, 4, 0, 0)
        };
    }
}
