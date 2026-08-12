using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariTool.Services;
using AkariTool.ViewModels;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// The merged Settings page (Appearance · Updates · About). Its view model is resolved
/// via <see cref="ServiceLocator"/>, matching every other page. The About section's
/// static content is built here in code-behind, relocated verbatim from the former
/// AboutPage (which is deleted in this change).
/// </summary>
public sealed partial class SettingsPage : Page
{
    private const string RepoUrl = "https://github.com/isleap9/Akari-Tool";
    private const string RuntimeUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/10.0";

    private readonly ToolService _tool;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        _tool = ServiceLocator.GetService<ToolService>();
        ViewModel = ServiceLocator.GetService<SettingsViewModel>();
        InitializeComponent();

        // About section content (relocated verbatim from AboutPage).
        AboutHost.Children.Add(HeaderCard());
        AboutHost.Children.Add(InfoCards());
        AboutHost.Children.Add(LinkRow());
    }

    // ── Header: logo · title · version pill · tagline ─────────────────────────
    private Border HeaderCard()
    {
        var row = new Grid { ColumnSpacing = 18 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logo = new Image
        {
            Width = 58,
            Height = 58,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage(new Uri("ms-appx:///Assets/AkariLogo.png")),
        };
        Grid.SetColumn(logo, 0);
        row.Children.Add(logo);

        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(textCol, 1);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = "Akari Tool",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleRow.Children.Add(VersionPill());
        textCol.Children.Add(titleRow);

        textCol.Children.Add(new TextBlock
        {
            Text = "A gaming-first Windows optimization utility built for performance — " +
                   "lower DPC latency, faster response times, cleaner frame delivery.",
            FontSize = 13,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });
        row.Children.Add(textCol);

        return Card(new Thickness(24, 22, 24, 22), new Thickness(0), row);
    }

    private Border VersionPill() => new()
    {
        Margin = new Thickness(10, 0, 0, 0),
        Padding = new Thickness(10, 3, 10, 3),
        CornerRadius = new CornerRadius(6),
        Background = Res("SubtleFillColorSecondaryBrush"),
        BorderBrush = Res("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = UpdateService.CurrentVersionDisplay,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10.5,
            Foreground = Res("AccentTextFillColorPrimaryBrush"),
        },
    };

    // ── Environment + Credits ─────────────────────────────────────────────────
    private UIElement InfoCards()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var envStack = new StackPanel();
        envStack.Children.Add(CardLabel("Environment"));
        envStack.Children.Add(InfoRow("Platform", "Windows 11 (x64)"));
        envStack.Children.Add(InfoRow("Framework", ".NET 10 Desktop"));
        envStack.Children.Add(InfoRow("UI", "WinUI 3 (Windows App SDK)"));
        envStack.Children.Add(InfoRow("License", "MIT"));
        var env = Card(new Thickness(18, 16, 18, 16), new Thickness(0), envStack);
        Grid.SetColumn(env, 0);

        var creditsStack = new StackPanel();
        creditsStack.Children.Add(CardLabel("Credits"));
        var body = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
        };
        body.Inlines.Add(new Run { Text = "Registry tweak references from " });
        body.Inlines.Add(new Run { Text = "CTT WinUtil", Foreground = Res("AccentTextFillColorPrimaryBrush") });
        body.Inlines.Add(new Run { Text = " and " });
        body.Inlines.Add(new Run { Text = "Winhance", Foreground = Res("AccentTextFillColorPrimaryBrush") });
        body.Inlines.Add(new Run
        {
            Text = ". Advanced Tools ISO flow ported from Winhance. " +
                   "NVIDIA profile applied with nvidiaProfileInspector by Orbmu2k (MIT). " +
                   "Made for the AkariOS ecosystem.",
        });
        body.Inlines.Add(new Run { Text = " Sidebar icons by " });
        var icons8 = new Hyperlink { NavigateUri = new Uri("https://icons8.com") };
        icons8.Inlines.Add(new Run { Text = "Icons8" });
        body.Inlines.Add(icons8);
        body.Inlines.Add(new Run { Text = "." });
        creditsStack.Children.Add(body);
        var credits = Card(new Thickness(18, 16, 18, 16), new Thickness(0), creditsStack);
        Grid.SetColumn(credits, 1);

        grid.Children.Add(env);
        grid.Children.Add(credits);
        return grid;
    }

    private UIElement InfoRow(string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = 96,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Res("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        return row;
    }

    // ── Links: Repository · .NET 10 Runtime ───────────────────────────────────
    private UIElement LinkRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        row.Children.Add(LinkButton("Repository", RepoUrl));
        row.Children.Add(LinkButton(".NET 10 Runtime", RuntimeUrl));
        return row;
    }

    private Button LinkButton(string text, string url)
    {
        var btn = new Button { Content = text, Padding = new Thickness(16, 6, 16, 6) };
        btn.Click += (_, _) => _tool.OpenUrl(url);
        return btn;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Border Card(Thickness padding, Thickness margin, UIElement child) => new()
    {
        Padding = padding,
        Margin = margin,
        CornerRadius = new CornerRadius(8),
        Background = Res("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Res("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        Child = child,
    };

    private TextBlock CardLabel(string text) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 10,
        Foreground = Res("TextFillColorTertiaryBrush"),
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
