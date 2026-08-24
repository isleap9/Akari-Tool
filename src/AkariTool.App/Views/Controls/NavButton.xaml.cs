using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;

namespace AkariTool.Views.Controls;

/// <summary>
/// Custom navigation button with icon-over-text layout, left selection indicator,
/// InfoBadge overlay, loading + locked overlays, and compact-mode support. Reimplemented
/// from Winhance's NavSidebar/NavButton architecture against Akari's own namespace and
/// icon set (no source copied). Raises <see cref="Clicked"/> with its NavigationTag;
/// the owning NavSidebar/MainWindow decides routing.
/// </summary>
public sealed partial class NavButton : UserControl, INotifyPropertyChanged
{
    // Expanded dimensions.
    private const double ExpandedWidth = 70;
    private const double ExpandedHeight = 60;

    // Compact dimensions (icon-only, matching NavigationView compact items).
    private const double CompactWidth = 40;
    private const double CompactHeight = 40;

    // Icon sizes.
    private const double ExpandedIconSize = 20;
    private const double CompactIconSize = 16;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NavButtonClickedEventArgs>? Clicked;

    #region Dependency Properties

    public static readonly DependencyProperty IconSymbolProperty =
        DependencyProperty.Register(nameof(IconSymbol), typeof(string), typeof(NavButton),
            new PropertyMetadata(null, OnIconPropertyChanged));

    public static readonly DependencyProperty IconMarginProperty =
        DependencyProperty.Register(nameof(IconMargin), typeof(Thickness), typeof(NavButton),
            new PropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(NavButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(NavButton),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(NavButton),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(NavButton),
            new PropertyMetadata(false, OnIsLockedChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(NavButton),
            new PropertyMetadata(false, OnIsCompactChanged));

    public static readonly DependencyProperty NavigationTagProperty =
        DependencyProperty.Register(nameof(NavigationTag), typeof(object), typeof(NavButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty BadgeValueProperty =
        DependencyProperty.Register(nameof(BadgeValue), typeof(int), typeof(NavButton),
            new PropertyMetadata(-1, OnBadgePropertyChanged));

    public static readonly DependencyProperty BadgeStatusProperty =
        DependencyProperty.Register(nameof(BadgeStatus), typeof(string), typeof(NavButton),
            new PropertyMetadata(string.Empty, OnBadgePropertyChanged));

    #endregion

    #region Properties

    /// <summary>The Fluent System Icon name to display (e.g. "Apps", "Settings").</summary>
    public string? IconSymbol
    {
        get => (string?)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    /// <summary>Optional margin for fine-tuning icon positioning.</summary>
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    /// <summary>The text label displayed below the icon.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether this button is currently selected.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Whether the button is loading (shows spinner, blocks clicks).</summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>Whether the button is locked (dims content, blocks clicks, shows lock glyph).</summary>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    /// <summary>Whether the button renders in compact mode (icon only).</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>Navigation identifier carried on the Clicked event.</summary>
    public object? NavigationTag
    {
        get => GetValue(NavigationTagProperty);
        set => SetValue(NavigationTagProperty, value);
    }

    /// <summary>Badge value; set to -1 to hide the badge.</summary>
    public int BadgeValue
    {
        get => (int)GetValue(BadgeValueProperty);
        set => SetValue(BadgeValueProperty, value);
    }

    /// <summary>Badge status: "Attention", "Success", "SuccessIcon", or "" (hidden).</summary>
    public string BadgeStatus
    {
        get => (string)GetValue(BadgeStatusProperty);
        set => SetValue(BadgeStatusProperty, value);
    }

    // Computed properties for x:Bind.
    public double ActualButtonWidth => IsCompact ? CompactWidth : ExpandedWidth;
    public double ActualButtonHeight => IsCompact ? CompactHeight : ExpandedHeight;
    public double IconSize => IsCompact ? CompactIconSize : ExpandedIconSize;
    public Visibility TextVisibility => IsCompact ? Visibility.Collapsed : Visibility.Visible;
    public Visibility IndicatorVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LockedVisibility => IsLocked ? Visibility.Visible : Visibility.Collapsed;
    public double ContentOpacity => IsLocked ? 0.4 : 1.0;

    public Visibility FluentIconVisibility =>
        !string.IsNullOrEmpty(IconSymbol) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BadgeVisibility =>
        BadgeValue >= 0 || BadgeStatus == "SuccessIcon" ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    private bool _isPointerOver;
    private bool _isFocused;

    public NavButton()
    {
        InitializeComponent();
        UpdateVisualState();

        KeyDown += NavButton_KeyDown;
        GotFocus += NavButton_GotFocus;
        LostFocus += NavButton_LostFocus;
    }

    private void NavButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (IsLoading || IsLocked) return;

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NavButtonAutomationPeer(this);

    // Lets the automation peer route Narrator's Invoke through the same gates as pointer/keyboard.
    internal void InvokeFromAutomation()
    {
        if (IsLoading || IsLocked) return;
        Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
    }

    private void NavButton_GotFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = true;
        UpdateVisualState();
    }

    private void NavButton_LostFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = false;
        UpdateVisualState();
    }

    #region Property Change Handlers

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b)
        {
            b.NotifyPropertyChanged(nameof(IndicatorVisibility));
            b.UpdateVisualState();
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b)
        {
            b.NotifyPropertyChanged(nameof(LoadingVisibility));
            b.UpdateVisualState();
        }
    }

    private static void OnIsLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b)
        {
            b.NotifyPropertyChanged(nameof(LockedVisibility));
            b.NotifyPropertyChanged(nameof(ContentOpacity));
            b.UpdateVisualState();
        }
    }

    private static void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b)
        {
            b.NotifyPropertyChanged(nameof(ActualButtonWidth));
            b.NotifyPropertyChanged(nameof(ActualButtonHeight));
            b.NotifyPropertyChanged(nameof(IconSize));
            b.NotifyPropertyChanged(nameof(TextVisibility));
        }
    }

    private static void OnBadgePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton b)
        {
            b.NotifyPropertyChanged(nameof(BadgeVisibility));
            b.ApplyBadgeStyle();
        }
    }

    private void ApplyBadgeStyle()
    {
        if (Badge is null) return;

        if (string.IsNullOrEmpty(BadgeStatus) || (BadgeValue < 0 && BadgeStatus != "SuccessIcon"))
        {
            Badge.Visibility = Visibility.Collapsed;
            return;
        }

        Badge.Visibility = Visibility.Visible;
        var styleKey = BadgeStatus switch
        {
            "Attention" => "AttentionValueInfoBadgeStyle",
            "Success" => "InformationalValueInfoBadgeStyle",
            "SuccessIcon" => "SuccessIconInfoBadgeStyle",
            _ => "AttentionValueInfoBadgeStyle"
        };

        if (Application.Current.Resources.TryGetValue(styleKey, out var style) && style is Style badgeStyle)
        {
            Badge.Style = badgeStyle;
        }
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavButton b) return;

        b.NotifyPropertyChanged(nameof(FluentIconVisibility));

        // Icon is an enum, so it can't be x:Bind'd directly from the string DP.
        if (!string.IsNullOrEmpty(b.IconSymbol) && b.ButtonFluentIcon is not null
            && Enum.TryParse<FluentIcons.Common.Icon>(b.IconSymbol, ignoreCase: true, out var fluentIcon))
        {
            b.ButtonFluentIcon.Icon = fluentIcon;
        }
    }

    #endregion

    #region Pointer Events

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        UpdateVisualState();
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
        UpdateVisualState();
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsLoading || IsLocked) return;
        RootGrid.CapturePointer(e.Pointer);
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        RootGrid.ReleasePointerCapture(e.Pointer);
        if (IsLoading || IsLocked) return;

        // Only fire if the pointer is still over the button.
        if (_isPointerOver)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
        }
    }

    #endregion

    #region Visual State Management

    private void UpdateVisualState()
    {
        if (IsSelected)
        {
            BackgroundBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
        }
        else if ((_isPointerOver || _isFocused) && !IsLoading && !IsLocked)
        {
            BackgroundBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else
        {
            BackgroundBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    #endregion

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Event args for <see cref="NavButton.Clicked"/>.</summary>
public class NavButtonClickedEventArgs : EventArgs
{
    public object? NavigationTag { get; }

    public NavButtonClickedEventArgs(object? navigationTag) => NavigationTag = navigationTag;
}

/// <summary>
/// Automation peer exposing NavButton as a Button to UI Automation clients (Narrator etc.)
/// and routing the Invoke pattern through <see cref="NavButton.InvokeFromAutomation"/>.
/// </summary>
public sealed partial class NavButtonAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    public NavButtonAutomationPeer(NavButton owner) : base(owner) { }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(NavButton);

    protected override object GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

    public void Invoke()
    {
        if (Owner is NavButton navButton) navButton.InvokeFromAutomation();
    }
}
