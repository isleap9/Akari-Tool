using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AkariTool.Views.Controls;

/// <summary>
/// Slim, collapsible navigation rail (Winhance NavSidebar port): icon-over-label
/// <see cref="NavButton"/>s in a top group + bottom group, an accent selection bar, and a
/// hamburger that toggles expanded (80px, icon+label) ↔ compact (48px, icon only).
///
/// Tag-driven and self-contained: it raises <see cref="ItemClicked"/> with the clicked tag,
/// reflects the selected item via <see cref="SelectedTag"/>, and shows per-item counts via
/// <see cref="SetBadge"/>. MainWindow owns routing/badge aggregation.
/// </summary>
public sealed partial class NavSidebar : UserControl, INotifyPropertyChanged
{
    private const double ExpandedWidth = 80, CompactWidth = 48;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NavButtonClickedEventArgs>? ItemClicked;

    private NavButton[] _buttons = [];

    public NavSidebar()
    {
        InitializeComponent();
        _buttons = [HomeButton, OptimizeButton, CustomizeButton, SoftwareButton, AdvancedButton, SettingsButton];
    }

    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen), typeof(bool), typeof(NavSidebar), new PropertyMetadata(true, OnIsPaneOpenChanged));

    public static readonly DependencyProperty SelectedTagProperty = DependencyProperty.Register(
        nameof(SelectedTag), typeof(string), typeof(NavSidebar), new PropertyMetadata(null, OnSelectedTagChanged));

    public bool IsPaneOpen { get => (bool)GetValue(IsPaneOpenProperty); set => SetValue(IsPaneOpenProperty, value); }
    public string? SelectedTag { get => (string?)GetValue(SelectedTagProperty); set => SetValue(SelectedTagProperty, value); }

    public bool IsCompact => !IsPaneOpen;
    public double ActualSidebarWidth => IsPaneOpen ? ExpandedWidth : CompactWidth;

    /// <summary>Sets (or clears) the pending-count badge on the button with this tag.</summary>
    public void SetBadge(string tag, int count)
    {
        if (_buttons.FirstOrDefault(b => b.NavigationTag == tag) is { } button)
            button.BadgeValue = count > 0 ? count : -1;
    }

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavSidebar s) return;
        s.Notify(nameof(IsCompact));
        s.Notify(nameof(ActualSidebarWidth));
    }

    private static void OnSelectedTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavSidebar s) return;
        var tag = e.NewValue as string;
        foreach (var b in s._buttons)
            b.IsSelected = b.NavigationTag == tag;
    }

    private void NavButton_Clicked(object? sender, NavButtonClickedEventArgs e)
        => ItemClicked?.Invoke(this, e);

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => IsPaneOpen = !IsPaneOpen;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
