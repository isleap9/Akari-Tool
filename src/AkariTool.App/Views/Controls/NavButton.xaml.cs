using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace AkariTool.Views.Controls;

/// <summary>Click payload for <see cref="NavButton"/> — carries the button's navigation tag.</summary>
public sealed class NavButtonClickedEventArgs(string? tag) : EventArgs
{
    public string? Tag { get; } = tag;
}

/// <summary>
/// Slim icon-over-label navigation button (Winhance NavSidebar look): a colored Fluent icon
/// over a two-line label, a left accent selection bar, an InfoBadge, and an expanded/compact
/// mode. Self-contained — no ViewModel or localization dependency.
/// </summary>
public sealed partial class NavButton : UserControl, INotifyPropertyChanged
{
    private const double ExpandedWidth = 70, ExpandedHeight = 60;
    private const double CompactWidth = 40, CompactHeight = 40;
    private const double ExpandedIconSize = 20, CompactIconSize = 18;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NavButtonClickedEventArgs>? Clicked;

    private bool _isPointerOver;

    public NavButton()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        UpdateVisualState();
    }

    // ── Dependency properties ───────────────────────────────────────────────

    public static readonly DependencyProperty IconSymbolProperty = DependencyProperty.Register(
        nameof(IconSymbol), typeof(string), typeof(NavButton), new PropertyMetadata(null, OnIconChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(NavButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NavigationTagProperty = DependencyProperty.Register(
        nameof(NavigationTag), typeof(string), typeof(NavButton), new PropertyMetadata(null));

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(NavButton), new PropertyMetadata(false, OnIsCompactChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(NavButton), new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty BadgeValueProperty = DependencyProperty.Register(
        nameof(BadgeValue), typeof(int), typeof(NavButton), new PropertyMetadata(-1, OnBadgeChanged));

    public string? IconSymbol { get => (string?)GetValue(IconSymbolProperty); set => SetValue(IconSymbolProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? NavigationTag { get => (string?)GetValue(NavigationTagProperty); set => SetValue(NavigationTagProperty, value); }
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>Badge count; &lt; 1 hides the badge.</summary>
    public int BadgeValue { get => (int)GetValue(BadgeValueProperty); set => SetValue(BadgeValueProperty, value); }

    // ── Computed (x:Bind) ───────────────────────────────────────────────────

    public double ActualButtonWidth => IsCompact ? CompactWidth : ExpandedWidth;
    public double ActualButtonHeight => IsCompact ? CompactHeight : ExpandedHeight;
    public double IconSize => IsCompact ? CompactIconSize : ExpandedIconSize;
    public Visibility TextVisibility => IsCompact ? Visibility.Collapsed : Visibility.Visible;
    public Visibility IndicatorVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BadgeVisibility => BadgeValue > 0 ? Visibility.Visible : Visibility.Collapsed;

    // ── Change handlers ─────────────────────────────────────────────────────

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b && !string.IsNullOrEmpty(b.IconSymbol) && b.ButtonFluentIcon is not null
            && Enum.TryParse<FluentIcons.Common.Icon>(b.IconSymbol, ignoreCase: true, out var icon))
        {
            b.ButtonFluentIcon.Icon = icon;
        }
    }

    private static void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavButton b) return;
        b.Notify(nameof(ActualButtonWidth));
        b.Notify(nameof(ActualButtonHeight));
        b.Notify(nameof(IconSize));
        b.Notify(nameof(TextVisibility));
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavButton b) return;
        b.Notify(nameof(IndicatorVisibility));
        b.UpdateVisualState();
    }

    private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => (d as NavButton)?.Notify(nameof(BadgeVisibility));

    // ── Interaction ─────────────────────────────────────────────────────────

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e) { _isPointerOver = true; UpdateVisualState(); }
    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e) { _isPointerOver = false; UpdateVisualState(); }
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e) => RootGrid.CapturePointer(e.Pointer);

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        RootGrid.ReleasePointerCapture(e.Pointer);
        if (_isPointerOver) Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
            e.Handled = true;
        }
    }

    private void UpdateVisualState()
    {
        BackgroundBorder.Background =
            IsSelected ? (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"]
            : _isPointerOver ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
