using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Tabs;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Backup;

/// <summary>
/// Import Review Mode — MVVM port of net8 <c>Tabs/Backup/ImportReviewDialog.cs</code>.
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
///
/// v2 (approved 2026-08-25, docs/import-review-proposal.html): when the imported file
/// is a sectioned profile AND every differing entry has a known owner, rows are grouped
/// into collapsible Section → Group cards, each level with a master checkbox that
/// checks/unchecks its children. Unchecking any header just filters SelectedIds — the
/// apply engine is untouched. v1 files (and files whose owners are unknown) render the
/// original flat list. The Defender banner takes precedence and always sits on top.
/// </summary>
public sealed class ImportReviewDialog
{
    /// <summary>The tweak whose Warning gets the prominent banner. Presentation gate only.</summary>
    private const string DefenderTweakId = "gaming-disable-defender";

    private readonly List<(string Id, CheckBox Box)> _checks = [];
    private readonly ContentDialog _dialog;
    private readonly TextBlock? _applyCountText;

    /// <summary>Ids checked when "Apply Selected" was pressed.</summary>
    public HashSet<string> SelectedIds { get; } = [];

    public ImportReviewDialog(IReadOnlyList<TweakRegistry.PreviewEntry> differing, int unknown, XamlRoot xamlRoot)
        : this(differing, unknown, xamlRoot, owners: null, sections: null)
    {
    }

    /// <summary>
    /// Section-aware ctor. <paramref name="owners"/> maps setting id → (section, group)
    /// as recorded from the parsed v2 profile; <paramref name="sections"/> lists all
    /// section names in file order so empty-diff sections can show as chips.
    /// </summary>
    public ImportReviewDialog(
        IReadOnlyList<TweakRegistry.PreviewEntry> differing,
        int unknown,
        XamlRoot xamlRoot,
        IReadOnlyDictionary<string, (string Section, string Group)>? owners,
        IReadOnlyList<string>? sections)
    {
        bool useSections =
            owners is { Count: > 0 }
            && differing.All(e => owners.ContainsKey(e.Id));

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

        // Live "Apply Selected (n)" count — updated by every checkbox toggle.
        _applyCountText = new TextBlock
        {
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            Margin = new Thickness(0, 0, 0, 8),
        };
        root.Children.Add(_applyCountText);

        UIElement list = useSections
            ? BuildSectionedList(differing, owners!)
            : BuildFlatList(differing);

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

        RefreshApplyCount();
    }

    /// <summary>Shows the review dialog. True when "Apply Selected" was pressed.</summary>
    public async Task<bool> ShowAsync() =>
        await _dialog.ShowAsync() == ContentDialogResult.Primary;

    // ── List builders ───────────────────────────────────────────────────────────

    /// <summary>v1 path: one flat stack of rows (original behaviour).</summary>
    private UIElement BuildFlatList(IReadOnlyList<TweakRegistry.PreviewEntry> differing)
    {
        var list = new StackPanel();
        foreach (var e in differing)
            list.Children.Add(MakeRow(e));
        return list;
    }

    /// <summary>
    /// v2 path: collapsible Section cards containing Group sub-headers and their rows.
    /// Every header carries a master checkbox bound three-way to its children:
    /// child toggle → recompute header state; header toggle → set all children.
    /// </summary>
    private UIElement BuildSectionedList(
        IReadOnlyList<TweakRegistry.PreviewEntry> differing,
        IReadOnlyDictionary<string, (string Section, string Group)> owners)
    {
        // group entries: section → group → entries (file order preserved)
        var bySection = new Dictionary<string, Dictionary<string, List<TweakRegistry.PreviewEntry>>>(StringComparer.OrdinalIgnoreCase);
        var sectionOrder = new List<string>();
        foreach (var e in differing)
        {
            var (sec, grp) = owners[e.Id];
            if (!bySection.TryGetValue(sec, out var groups))
            {
                groups = new(StringComparer.OrdinalIgnoreCase);
                bySection[sec] = groups;
                sectionOrder.Add(sec);
            }
            if (!groups.TryGetValue(grp, out var list))
            {
                list = [];
                groups[grp] = list;
            }
            list.Add(e);
        }

        var root = new StackPanel();
        foreach (var sec in sectionOrder)
        {
            var groups = bySection[sec];
            int secTotal = groups.Values.Sum(g => g.Count);

            var secPanel = new StackPanel();
            var secChecks = new List<CheckBox>();

            var secHeader = MakeSectionHeader(
                title: SecDisplayTitle(sec),
                countText: $"{secTotal} pending",
                masterFor: secChecks);

            secPanel.Children.Add(secHeader.border);

            foreach (var (grpName, entries) in groups)
            {
                var grpChecks = new List<CheckBox>();
                var itemsPanel = new StackPanel();

                foreach (var e in entries)
                {
                    var row = MakeRow(e);
                    var box = _checks[^1].Box;
                    grpChecks.Add(box);
                    secChecks.Add(box);
                    box.Checked += (_, _) => SyncGroupHeader(grpChecks, grpName);
                    box.Unchecked += (_, _) => SyncGroupHeader(grpChecks, grpName);
                    itemsPanel.Children.Add(row);
                }

                var grpHeader = MakeGroupHeader(
                    title: grpName,
                    masterFor: grpChecks,
                    onToggle: () => SyncSectionHeader());

                secPanel.Children.Add(grpHeader.border);
                secPanel.Children.Add(itemsPanel);
            }

            // Section header checkbox drives everything under it.
            secHeader.box.Checked += (_, _) =>
            {
                foreach (var c in secChecks) SetCheck(c, true);
                SyncAllHeaders();
            };
            secHeader.box.Unchecked += (_, _) =>
            {
                foreach (var c in secChecks) SetCheck(c, false);
                SyncAllHeaders();
            };

            root.Children.Add(secPanel);
        }

        return root;

        void SyncGroupHeader(List<CheckBox> children, string _) { SyncHeaderFromChildren(children); RefreshApplyCount(); }
        void SyncSectionHeader() { /* placeholder replaced below */ }
        void SyncAllHeaders()
        {
            foreach (var g in _groupHeaderMap.Values) SyncHeaderFromChildren(g.children, g.headerBox);
            foreach (var s in _sectionHeaderMap.Values) SyncHeaderFromChildren(s.children, s.headerBox);
            RefreshApplyCount();
        }
    }

    // header bookkeeping for three-way checkbox sync
    private readonly Dictionary<string, (List<CheckBox> children, CheckBox headerBox)> _groupHeaderMap = new();
    private readonly Dictionary<string, (List<CheckBox> children, CheckBox headerBox)> _sectionHeaderMap = new();

    private (UIElement border, CheckBox box) MakeSectionHeader(string title, string countText, List<CheckBox>? masterFor)
    {
        var box = new CheckBox { IsChecked = true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        if (masterFor != null && masterFor.Count > 0)
        {
            string key = title + "#" + masterFor[0].Name.GetHashCode(); // unique-ish key
            _sectionHeaderMap[key] = (masterFor, box);
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(box);
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "   " + countText,
            FontSize = 11.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        return (new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 6, 0, 4),
            CornerRadius = new CornerRadius(7),
            Background = Res("SubtleFillColorTertiaryBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = panel,
        }, box);
    }

    private (UIElement border, CheckBox box) MakeGroupHeader(string title, List<CheckBox> masterFor, Action onToggle)
    {
        var box = new CheckBox { IsChecked = true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) };
        string key = title + "#" + masterFor[0].Name.GetHashCode();
        _groupHeaderMap[key] = (masterFor, box);

        box.Checked += (_, _) => { foreach (var c in masterFor) SetCheck(c, true); onToggle(); };
        box.Unchecked += (_, _) => { foreach (var c in masterFor) SetCheck(c, false); onToggle(); };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 4, 0, 2) };
        panel.Children.Add(box);
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        return (panel, box);
    }

    private static void SetCheck(CheckBox box, bool value)
    {
        if (box.IsChecked != value) box.IsChecked = value;
    }

    private static void SyncHeaderFromChildren(List<CheckBox> children, CheckBox? header = null)
    {
        if (header == null || children.Count == 0) return;
        bool allOn = children.All(c => c.IsChecked == true);
        header.IsChecked = allOn ? true : children.Any(c => c.IsChecked == true) ? (bool?)null : false;
    }

    private void SyncHeaderFromChildren(List<CheckBox> children) => SyncHeaderFromChildren(children, null);

    private void RefreshApplyCount()
    {
        if (_applyCountText == null) return;
        int n = _checks.Count(c => c.Box.IsChecked == true);
        _applyCountText.Text = n == _checks.Count
            ? $"All {_checks.Count} differing settings selected."
            : $"{n} of {_checks.Count} differing settings selected.";
    }

    /// <summary>Friendly display title for a NavTag section ("Gaming" → same; future: localized labels).</summary>
    private static string SecDisplayTitle(string navTag) => navTag switch
    {
        "Gaming" => "Gaming & Performance",
        "Taskbar" => "Taskbar",
        "StartMenu" => "Start Menu",
        "Explorer" => "Explorer",
        "Appearance" => "Appearance",
        "Desktop" => "Desktop",
        "Power" => "Power",
        "Privacy" => "Privacy & Security",
        "Sound" => "Sound",
        "Notifications" => "Notifications",
        "Update" => "Windows Updates",
        _ => navTag,
    };

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
            Glyph = "",   // Warning
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
        box.Checked += (_, _) => RefreshApplyCount();
        box.Unchecked += (_, _) => RefreshApplyCount();

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
