using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Tabs;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Backup;

/// <summary>
/// Import Review Mode — MVVM port of net8 <c>Tabs/Backup/ImportReviewDialog.cs</c>.
/// Shows only the entries that differ from the current system state, each with a
/// checkbox (default checked); returns the ticked Ids via <see cref="SelectedIds"/>
/// when "Apply Selected" is pressed. The checkbox still fully controls whether a row
/// is applied — this is the same behaviour net8 had.
///
/// ⚠ THE ONE ADDITION vs net8 (Phase 1 / LOG3, isleap Option 2): if the differing set
/// includes <c>gaming-disable-defender</c>, that row's own
/// <see cref="TweakDefinition.Warning"/> text is surfaced as a distinct callout banner
/// at the top of the dialog — the row itself is otherwise an ordinary checkbox, exactly
/// like every other entry. This is PRESENTATION ONLY: no Defender code is called,
/// referenced, or imported; the Warning string is read from the already-registered
/// tweak via <see cref="TweakRegistry.TryGetDefinition"/>, and nothing about the apply
/// path, the export/import engine, or what Backup can do changes.
/// </summary>
public sealed class ImportReviewDialog
{
    /// <summary>The tweak whose Warning gets the prominent banner. Presentation gate only.</summary>
    private const string DefenderTweakId = "gaming-disable-defender";

    private readonly List<(string Id, CheckBox Box)> _checks = [];
    private readonly ContentDialog _dialog;

    /// <summary>Ids checked when "Apply Selected" was pressed.</summary>
    public HashSet<string> SelectedIds { get; } = [];

    public ImportReviewDialog(IReadOnlyList<TweakRegistry.PreviewEntry> differing, int unknown, XamlRoot xamlRoot)
    {
        var root = new StackPanel { MinWidth = 560 };

        root.Children.Add(new TextBlock
        {
            Text = "Review changes before applying",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 4),
        });

        var sub = $"{differing.Count} tweak(s) in this file differ from your current settings. " +
                  "Uncheck anything you want to keep as-is.";
        if (unknown > 0)
            sub += $" {unknown} entr{(unknown == 1 ? "y is" : "ies are")} not recognized by this version and will be skipped.";
        root.Children.Add(new TextBlock
        {
            Text = sub,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        });

        // ⚠ Defender callout — only when this import actually changes Defender state.
        // The row is present in `differing`, so it IS a real state change; surface its
        // own Warning text prominently. Read-only registry lookup; no Defender code touched.
        if (differing.Any(e => string.Equals(e.Id, DefenderTweakId, StringComparison.OrdinalIgnoreCase))
            && TweakRegistry.TryGetDefinition(DefenderTweakId, out var def)
            && !string.IsNullOrEmpty(def.Warning))
        {
            root.Children.Add(BuildDefenderBanner(def.Warning!));
        }

        var list = new StackPanel();
        foreach (var e in differing)
            list.Children.Add(MakeRow(e));

        root.Children.Add(new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 4),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 380,
                Content = list,
            },
        });

        _dialog = new ContentDialog
        {
            Title = "Review Import",
            Content = root,
            PrimaryButtonText = "Apply Selected",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };
        _dialog.PrimaryButtonClick += (_, _) =>
        {
            SelectedIds.Clear();
            foreach (var (id, box) in _checks)
                if (box.IsChecked == true) SelectedIds.Add(id);
        };
    }

    /// <summary>Shows the review dialog. True when "Apply Selected" was pressed.</summary>
    public async Task<bool> ShowAsync() =>
        await _dialog.ShowAsync() == ContentDialogResult.Primary;

    /// <summary>
    /// The prominent Defender callout. Caution-styled banner: lead-in + the row's own
    /// Warning text verbatim. Minimal, factual lead-in — no invented urgency.
    /// </summary>
    private static UIElement BuildDefenderBanner(string warningText)
    {
        var stack = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        head.Children.Add(new FontIcon
        {
            Glyph = "",   // Warning
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 15,
            Foreground = Res("SystemFillColorCautionBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0),
        });
        head.Children.Add(new TextBlock
        {
            Text = "This backup will change Windows Defender protection:",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(head);

        // Verbatim Warning text from the tweak definition.
        stack.Children.Add(new TextBlock
        {
            Text = warningText,
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        });

        return new Border
        {
            Background = Res("SystemFillColorCautionBackgroundBrush"),
            BorderBrush = Res("SystemFillColorCautionBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack,
        };
    }

    private UIElement MakeRow(TweakRegistry.PreviewEntry e)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var box = new CheckBox
        {
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(box, 0);
        grid.Children.Add(box);
        _checks.Add((e.Id, box));

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = e.Name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        });

        var change = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        change.Children.Add(new TextBlock { Text = e.CurrentDisplay, FontSize = 12, Foreground = Res("TextFillColorSecondaryBrush") });
        change.Children.Add(new TextBlock { Text = "  →  ", FontSize = 12, Foreground = Res("TextFillColorTertiaryBrush") });
        change.Children.Add(new TextBlock { Text = e.ImportedDisplay, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorPrimaryBrush") });
        stack.Children.Add(change);

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        return new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(2, 3, 2, 3),
            CornerRadius = new CornerRadius(8),
            Background = Res("SubtleFillColorSecondaryBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = grid,
        };
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
