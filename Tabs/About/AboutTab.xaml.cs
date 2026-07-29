using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AkariTool.Services;

namespace AkariTool.Tabs.About
{
    /// <summary>
    /// App info page: header card (logo + version + tagline), Environment & Credits
    /// cards, and the two remaining external links (Repository, .NET 8 Runtime).
    /// Replaces the old GitHub nav link.
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
            new(ThemeService.Logo) { Stretch = Stretch.UniformToFill };

        // ── Header card: logo · title · version pill · tagline ──────────────────
        private static Border HeaderCard()
        {
            var card = Card(new Thickness(24, 22, 24, 22), new Thickness(0, 14, 0, 0));

            var row = new StackPanel { Orientation = Orientation.Horizontal };

            var logo = new Ellipse
            {
                Width = 58,
                Height = 58,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 18, 0),
                Fill = ThemeLogoBrush(),
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#000000"),
                    BlurRadius = 22,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };
            row.Children.Add(logo);

            // Swap the brand mark when the theme changes; unsubscribe when the card leaves
            // the tree so the static event doesn't leak the destroyed Ellipse.
            void OnTheme(AkariTheme _) => logo.Fill = ThemeLogoBrush();
            ThemeService.ThemeChanged += OnTheme;
            logo.Unloaded += (_, _) => ThemeService.ThemeChanged -= OnTheme;

            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

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
            envStack.Children.Add(InfoRow("UI", "WPF-UI 4.1 (Fluent)"));
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
            body.Inlines.Add(new Run("Registry tweak references from "));
            body.Inlines.Add(new Run("CTT WinUtil") { Foreground = TweakHelpers.AccentTextMuted });
            body.Inlines.Add(new Run(" and "));
            body.Inlines.Add(new Run("Winhance") { Foreground = TweakHelpers.AccentTextMuted });
            body.Inlines.Add(new Run(". Advanced Tools ISO flow ported from Winhance. " +
                                     "NVIDIA profile applied with nvidiaProfileInspector by Orbmu2k (MIT). " +
                                     "Made for the AkariOS ecosystem."));
            body.Inlines.Add(new Run(" Sidebar icons by "));
            var icons8 = new Hyperlink(new Run("Icons8")) { NavigateUri = new System.Uri("https://icons8.com") };
            icons8.RequestNavigate += (_, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            };
            body.Inlines.Add(icons8);
            body.Inlines.Add(new Run("."));
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
                Margin = new Thickness(0, 12, 0, 0)
            };
            row.Children.Add(LinkButton("Repository", RepoUrl, new Thickness(0, 0, 9, 0)));
            row.Children.Add(LinkButton(".NET 8 Runtime", RuntimeUrl, new Thickness(0)));
            return row;
        }

        private Button LinkButton(string text, string url, Thickness margin)
        {
            var btn = new Button
            {
                Content = text,
                Margin = margin,
                Style = (Style)Application.Current.Resources["AppButtonSecondary"],
                Padding = new Thickness(16, 0, 16, 0)
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
