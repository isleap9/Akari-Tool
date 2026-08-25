using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using AkariTool.Tabs;
using AkariTool.Services;
using AkariTool.ViewModels;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Home page. Banner is bound to <see cref="HomeViewModel"/>; the global search and the
/// quick-nav card grid are built here in code (Phase 9 port of net8's HomeTab). Search is
/// wired to <see cref="SettingBackupService.Search"/>; cards route through
/// <see cref="MainWindow.SelectRailTag"/>, which drives the same rail routing as a real
/// click (so a card lands on the real page built in a prior wave, or PlaceholderPage for
/// the still-unported tabs). Registers nothing.
/// </summary>
public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    private readonly SettingBackupService _backup;

    /// <summary>Per-tab recommended-pending counts (NavTag → count) used for the card chips.
    /// Refreshed each time the page is navigated to.</summary>
    private IReadOnlyDictionary<string, int> _pendingCounts = new Dictionary<string, int>();

    private sealed record CardDef(string Title, string Glyph, string Desc, string Tag);

    public HomePage()
    {
        ViewModel = ServiceLocator.GetService<HomeViewModel>();
        _backup = ServiceLocator.GetService<SettingBackupService>();
        InitializeComponent();
        BuildCards();
    }

    // The page is cached (NavigationCacheMode="Required"), so refresh the live signals and
    // the card pending-chips each time it's shown — the user may have applied tweaks on
    // another tab since the last visit.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshHealth();
        CardsPanel.Children.Clear();
        BuildCards();
    }

    // ── Quick-nav cards (net8 SOFTWARE / OPTIMIZE / ADVANCED groupings) ────────

    private void BuildCards()
    {
        // Same aggregate the sidebar nav badges use — read-only, per NavTag.
        _pendingCounts = ServiceLocator.GetService<INavBadgeService>()
            .ComputeNavBadges()
            .ToDictionary(b => b.Tag, b => b.Count);

        AddCardSection("SOFTWARE", isFirst: true, new[]
        {
            new CardDef("Windows Apps",  G("E71D"), "Manage built-in Windows apps & features",      "Bloatware"),
            new CardDef("External Apps", G("E896"), "Install apps via WinGet",                       "AppInstaller"),
        });

        AddCardSection("OPTIMIZE", isFirst: false, new[]
        {
            new CardDef("AkariOS",       G("E734"), "AkariOS presets & environment tools",           "AkariOS"),
            new CardDef("Gaming",        G("E7FC"), "Latency, GPU & gaming performance tweaks",      "Gaming"),
            new CardDef("Privacy",       G("E72E"), "Telemetry, tracking & privacy hardening",       "Privacy"),
            new CardDef("Update",        G("E895"), "Windows Update behavior & policies",            "Update"),
            new CardDef("Notifications", G("EA8F"), "Notification, tips & suggestion controls",      "Notifications"),
            new CardDef("Power",         G("E945"), "Power plans & performance settings",            "Power"),
            new CardDef("Customize",     G("E771"), "Taskbar, Explorer, Start menu & context menu",  "Customize"),
            new CardDef("Tools",         G("E90F"), "System info, repair, network & maintenance",    "Tools"),
        });

        AddCardSection("ADVANCED", isFirst: false, new[]
        {
            new CardDef("Advanced Tools",   G("EC7A"), "ISO wizard & unattended setup builder",      "Advanced"),
            new CardDef("Backup & Restore", G("E74E"), "Export & import your tweak configuration",   "Backup"),
            new CardDef("Verify System",    G("E7BA"), "Track tweaks Windows reverted",              "Verify"),
        });
    }

    private void AddCardSection(string label, bool isFirst, CardDef[] defs)
    {
        CardsPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Medium,
            Foreground = Res("TextFillColorTertiaryBrush"),
            Margin = new Thickness(2, isFirst ? 0 : 20, 0, 10),
        });

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

        var trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Recommended-pending chip (only tabs whose NavTag matches a SettingPageViewModel;
        // aggregate/hub tabs like Customize or Tools show none — honest, no fake zeros).
        if (_pendingCounts.TryGetValue(d.Tag, out var pending) && pending > 0)
        {
            trailing.Children.Add(new Border
            {
                Background = Res("AccentFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = pending.ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                },
            });
        }

        trailing.Children.Add(new TextBlock
        {
            Text = G("E76C"),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Foreground = Res("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(trailing, 2);

        row.Children.Add(iconBox);
        row.Children.Add(text);
        row.Children.Add(trailing);

        var card = new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Child = row,
        };
        card.Tapped += (_, _) => Navigate(d.Tag);
        card.PointerEntered += (_, _) => card.BorderBrush = Res("ControlStrokeColorSecondaryBrush");
        card.PointerExited += (_, _) => card.BorderBrush = Res("CardStrokeColorDefaultBrush");
        return card;
    }

    // ── Global search (TweakRegistry.Search) ──────────────────────────────────

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var query = sender.Text.Trim();
        ResultsPanel.Children.Clear();

        if (query.Length == 0)
        {
            DashboardPanel.Visibility = Visibility.Visible;
            CardsPanel.Visibility = Visibility.Visible;
            ResultsCard.Visibility = Visibility.Collapsed;
            return;
        }

        DashboardPanel.Visibility = Visibility.Collapsed;
        CardsPanel.Visibility = Visibility.Collapsed;

        // net8 grouped hits by tab; SettingBackupService.Search already returns TabLabel
        // per hit, so group by it and render a header per group, rows navigating to TabTag.
        var hits = _backup.Search(query, max: 40);
        if (hits.Count == 0)
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = $"No tweaks found matching \"{query}\"",
                FontSize = 14,
                Foreground = Res("TextFillColorTertiaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 16),
            });
            ResultsCard.Visibility = Visibility.Visible;
            return;
        }

        bool firstGroup = true;
        foreach (var group in hits.GroupBy(h => h.TabLabel))
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = Res("TextFillColorTertiaryBrush"),
                Margin = new Thickness(0, firstGroup ? 4 : 14, 0, 6),
            });
            firstGroup = false;

            foreach (var hit in group)
                ResultsPanel.Children.Add(BuildResultRow(hit));
        }
        ResultsCard.Visibility = Visibility.Visible;
    }

    private Border BuildResultRow(TweakRegistry.SearchHit hit)
    {
        var grid = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text = hit.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
        });
        if (!string.IsNullOrEmpty(hit.Description))
            info.Children.Add(new TextBlock
            {
                Text = hit.Description,
                FontSize = 12,
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        Grid.SetColumn(info, 0);

        var badge = new Border
        {
            Background = Res("SubtleFillColorSecondaryBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = hit.TabLabel,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Res("TextFillColorSecondaryBrush"),
            },
        };
        Grid.SetColumn(badge, 1);

        grid.Children.Add(info);
        grid.Children.Add(badge);

        var row = new Border { Padding = new Thickness(0, 10, 0, 10), Child = grid };
        row.Tapped += (_, _) => Navigate(hit.TabTag);
        return row;
    }

    // ── Quick actions ─────────────────────────────────────────────────────────

    // Apply-all-recommended — the VM runs the confirmation + unified progress loop over the
    // pending rows across every page. Rebuild the cards afterwards so the pending chips update.
    private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyAllRecommendedAsync();
        CardsPanel.Children.Clear();
        BuildCards();
    }

    // Restore-point quick action — mirrors SettingPageViewModel.CreateRestorePointAsync:
    // runs through the TaskProgressService card (indeterminate + Cancel) and reports the
    // BackupResult. Home only triggers the existing service; it adds no restore logic.
    private async void RestorePointButton_Click(object sender, RoutedEventArgs e)
    {
        var progressService = ServiceLocator.GetService<ITaskProgressService>();
        var backup = ServiceLocator.GetService<ISystemBackupService>();
        var dialogs = ServiceLocator.GetService<TweakDialogs>();
        if (progressService is null || backup is null || dialogs is null) return;

        RestorePointButton.IsEnabled = false;
        var cts = progressService.StartTask("Creating system restore point...", isIndeterminate: true);
        var progress = progressService.CreateDetailedProgress();
        try
        {
            var result = await backup.CreateRestorePointAsync(progress: progress, cancellationToken: cts.Token);

            if (result.Success && result.RestorePointCreated)
            {
                await dialogs.InfoAsync("Create Restore Point", "System Restore point created successfully.");
            }
            else
            {
                var failMsg = "Failed to create System Restore point.";
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    failMsg += $"\n\n{result.ErrorMessage}";
                await dialogs.InfoAsync("Create Restore Point", failMsg);
            }
        }
        catch (Exception ex)
        {
            ToolService.Current?.Log($"[RESTORE] Home quick action restore point failed: {ex.Message}");
            await dialogs.InfoAsync("Create Restore Point", "Failed to create System Restore point.");
        }
        finally
        {
            progressService.CompleteTask();
            RestorePointButton.IsEnabled = true;
        }
    }

    // Back up config — routes to the Backup tab (export/import lives there); no new logic.
    private void BackupButton_Click(object sender, RoutedEventArgs e) => Navigate("Backup");

    // ── Nav ───────────────────────────────────────────────────────────────────

    private static void Navigate(string tag) => (App.MainWindow as MainWindow)?.SelectRailTag(tag);

    private static string G(string hex) => ((char)Convert.ToInt32(hex, 16)).ToString();
    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
