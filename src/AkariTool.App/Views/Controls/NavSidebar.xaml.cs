using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AkariTool.Views.Controls;

/// <summary>
/// Collapsible navigation sidebar hosting <see cref="NavButton"/> rows in a
/// top group / spacer / bottom group, with a "More" overflow flyout. Reimplemented
/// from Winhance's NavSidebar architecture against Akari's own namespace, icons, and
/// strings (no source copied). The six rail tags are SoftwareApps / Optimize /
/// Customize / AdvancedTools / Settings / More. The owning shell wires
/// <see cref="ItemClicked"/> to page routing and <see cref="MoreMenuInvoked"/> to
/// the overflow actions; badges/loading/locked are driven via the public setters.
/// </summary>
public sealed partial class NavSidebar : UserControl, INotifyPropertyChanged
{
    // Sidebar widths (matching NavigationView pane defaults).
    private const double ExpandedWidth = 80;
    private const double CompactWidth = 48;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a top/bottom nav button is activated. Tag is on the args.</summary>
    public event EventHandler<NavButtonClickedEventArgs>? ItemClicked;

    /// <summary>Raised when a More-menu item is chosen. String is the item tag
    /// ("OpenDocs", "ReportBug", "CheckUpdates", "OpenLogs", "CloseApp").</summary>
    public event EventHandler<string>? MoreMenuInvoked;

    /// <summary>Raised when the More flyout closes, so the shell can restore selection.</summary>
    public event EventHandler? MoreMenuClosed;

    private Dictionary<string, NavButton>? _navButtons;

    #region Dependency Properties

    public static readonly DependencyProperty IsPaneOpenProperty =
        DependencyProperty.Register(nameof(IsPaneOpen), typeof(bool), typeof(NavSidebar),
            new PropertyMetadata(true, OnIsPaneOpenChanged));

    public static readonly DependencyProperty SelectedTagProperty =
        DependencyProperty.Register(nameof(SelectedTag), typeof(string), typeof(NavSidebar),
            new PropertyMetadata(null, OnSelectedTagChanged));

    #endregion

    #region Properties

    /// <summary>Whether the pane is open (expanded) or closed (compact).</summary>
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>The currently selected navigation tag.</summary>
    public string? SelectedTag
    {
        get => (string?)GetValue(SelectedTagProperty);
        set => SetValue(SelectedTagProperty, value);
    }

    /// <summary>True when the pane is closed (compact mode).</summary>
    public bool IsCompact => !IsPaneOpen;

    /// <summary>Actual width based on pane state.</summary>
    public double ActualSidebarWidth => IsPaneOpen ? ExpandedWidth : CompactWidth;

    /// <summary>Nav-panel padding based on pane state.</summary>
    public Thickness NavPanelPadding => IsPaneOpen ? new Thickness(5, 0, 5, 0) : new Thickness(4, 0, 4, 0);

    #endregion

    public NavSidebar()
    {
        InitializeComponent();
        InitializeNavButtonDictionary();
        Loaded += NavSidebar_Loaded;
    }

    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        if (MoreMenuFlyout is not null)
        {
            MoreMenuFlyout.Closed += MoreMenuFlyout_Closed;
        }
    }

    private void InitializeNavButtonDictionary()
    {
        _navButtons = new Dictionary<string, NavButton>
        {
            { "SoftwareApps", SoftwareAppsButton },
            { "Optimize", OptimizeButton },
            { "Customize", CustomizeButton },
            { "AdvancedTools", AdvancedToolsButton },
            { "Settings", SettingsButton },
            { "More", MoreButton }
        };
    }

    /// <summary>Shows the More flyout anchored to the More button (toggles automatically).</summary>
    public void ShowMoreMenuFlyout() => FlyoutBase.ShowAttachedFlyout(MoreButton);

    private void MoreMenuFlyout_Closed(object? sender, object e) => MoreMenuClosed?.Invoke(this, EventArgs.Empty);

    private void MoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag })
        {
            MoreMenuInvoked?.Invoke(this, tag);
        }
    }

    #region Property Change Handlers

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavSidebar sidebar)
        {
            sidebar.NotifyPropertyChanged(nameof(IsCompact));
            sidebar.NotifyPropertyChanged(nameof(ActualSidebarWidth));
            sidebar.NotifyPropertyChanged(nameof(NavPanelPadding));
        }
    }

    private static void OnSelectedTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavSidebar sidebar) sidebar.UpdateSelectionState();
    }

    #endregion

    private void NavButton_Clicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        // "More" opens the overflow flyout rather than navigating.
        if (tag == "More")
        {
            ShowMoreMenuFlyout();
            return;
        }

        SelectedTag = tag;
        ItemClicked?.Invoke(this, e);
    }

    #region Public Methods

    /// <summary>Toggles the pane open/closed.</summary>
    public void TogglePane() => IsPaneOpen = !IsPaneOpen;

    private void UpdateSelectionState()
    {
        if (_navButtons is null) return;
        foreach (var kvp in _navButtons)
        {
            kvp.Value.IsSelected = kvp.Key == SelectedTag;
        }
    }

    /// <summary>Sets the loading state for a specific nav button.</summary>
    public void SetButtonLoading(string tag, bool isLoading)
    {
        if (_navButtons is not null && _navButtons.TryGetValue(tag, out var button))
        {
            button.IsLoading = isLoading;
        }
    }

    /// <summary>Sets the locked state for a specific nav button (dim + lock glyph + blocks clicks).</summary>
    public void SetButtonLocked(string tag, bool isLocked, string? tooltip = null)
    {
        if (_navButtons is null || !_navButtons.TryGetValue(tag, out var button)) return;

        button.IsLocked = isLocked;
        if (isLocked && !string.IsNullOrEmpty(tooltip))
        {
            ToolTipService.SetToolTip(button, tooltip);
        }
        else if (!isLocked)
        {
            ToolTipService.SetToolTip(button, null);
        }
    }

    /// <summary>Gets a NavButton by tag, or null.</summary>
    public NavButton? GetButton(string tag)
        => _navButtons is not null && _navButtons.TryGetValue(tag, out var button) ? button : null;

    /// <summary>Sets badge value + status on the nav button for the given tag.</summary>
    public void SetButtonBadge(string tag, int value, string status)
    {
        if (_navButtons is not null && _navButtons.TryGetValue(tag, out var button))
        {
            button.BadgeValue = value;
            button.BadgeStatus = status;
        }
    }

    /// <summary>Clears all badges from all nav buttons.</summary>
    public void ClearAllBadges()
    {
        if (_navButtons is null) return;
        foreach (var button in _navButtons.Values)
        {
            button.BadgeValue = -1;
            button.BadgeStatus = string.Empty;
        }
    }

    #endregion

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
