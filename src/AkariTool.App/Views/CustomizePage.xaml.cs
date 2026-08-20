using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AkariTool.Views;

/// <summary>
/// Customize landing hub. Not a tweak page — owns no ViewModel and registers nothing
/// with TweakRegistry. The Customize tweaks live on five category pages (TaskbarPage /
/// ExplorerPage / AppearancePage / StartMenuPage / DesktopPage), each
/// with its own TweakPageViewModel. This page builds a card grid (the same visuals as
/// HomePage's quick-nav cards); each card navigates the content Frame straight to its
/// category page. SyncSelectedItem maps all six pages to the single "Customize" rail
/// item, so the rail highlight never leaves Customize while inside this group.
/// </summary>
public sealed partial class CustomizePage : Page
{
    private sealed record CardDef(string Title, string Glyph, string Desc, Type Page);

    public CustomizePage()
    {
        InitializeComponent();
        BuildCards();
    }

    private void BuildCards()
    {
        AddCards(new[]
        {
            new CardDef("Taskbar",      G("E7C4"), "Layout, behavior & button grouping",            typeof(TaskbarPage)),
            new CardDef("Explorer",     G("EC50"), "View, behavior, associations & This PC",        typeof(ExplorerPage)),
            new CardDef("Appearance",   G("E771"), "Theme, transparency, color & window style",     typeof(AppearancePage)),
            new CardDef("Start Menu",   G("E8FC"), "Start menu layout & behavior",                  typeof(StartMenuPage)),
            new CardDef("Desktop",      G("E7F4"), "Icons, shortcuts, startup, devices & region",   typeof(DesktopPage)),
        });
    }

    private void AddCards(CardDef[] defs)
    {
        const int columns = 3;
        var grid = new Grid { ColumnSpacing = 14, RowSpacing = 14 };
        for (int c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int rows = (defs.Length + columns - 1) / columns;
        for (int r = 0; r < rows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < defs.Length; i++)
        {
            var card = BuildCard(defs[i]);
            Grid.SetColumn(card, i % columns);
            Grid.SetRow(card, i / columns);
            grid.Children.Add(card);
        }
        CardsPanel.Children.Add(grid);
    }

    private Border BuildCard(CardDef d)
    {
        var row = new Grid { ColumnSpacing = 13, VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBox = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            Background = Res("SubtleFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = d.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = Res("AccentTextFillColorPrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(iconBox, 0);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = d.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
        });
        text.Children.Add(new TextBlock
        {
            Text = d.Desc,
            FontSize = 11.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(text, 1);

        var chevron = new TextBlock
        {
            Text = G("E76C"),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = Res("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(chevron, 2);

        row.Children.Add(iconBox);
        row.Children.Add(text);
        row.Children.Add(chevron);

        var card = new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Child = row,
        };
        card.Tapped += (_, _) => Frame.Navigate(d.Page);
        card.PointerEntered += (_, _) => card.BorderBrush = Res("ControlStrokeColorSecondaryBrush");
        card.PointerExited += (_, _) => card.BorderBrush = Res("CardStrokeColorDefaultBrush");
        return card;
    }

    private static string G(string hex) => ((char)Convert.ToInt32(hex, 16)).ToString();
    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
