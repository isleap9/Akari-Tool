using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AkariTool.ViewModels.Common;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.Views.Controls;

/// <summary>
/// Reusable hub chrome (Optimize / Customize / Advanced Tools): header (icon + title +
/// description + search) → breadcrumb (root + section dropdown) → Quick Actions + View
/// menus → a content border that flips between overview cards and an inner detail
/// <see cref="Frame"/>. Data-driven: a host supplies header text and a <see cref="Cards"/>
/// collection of <see cref="HubCardViewModel"/>; the chrome renders the cards, handles
/// drill-in/out, and routes Quick Actions to the currently shown detail page's
/// <see cref="SettingPageViewModel"/>.
///
/// Reimplemented from Winhance's hub pages (which hand-code each card + pill set in a large
/// per-page code-behind) as a single shared control against Akari's own infrastructure.
/// </summary>
public sealed partial class HubView : UserControl
{
    private HubCardViewModel? _currentCard;

    /// <summary>Raised when the search box query is submitted (host wires global/page search).</summary>
    public event EventHandler<string>? SearchSubmitted;

    /// <summary>Raised after the inner frame navigates to a card's detail page.</summary>
    public event EventHandler<HubCardViewModel>? CardNavigated;

    /// <summary>Raised when the inner frame returns to the overview.</summary>
    public event EventHandler? OverviewShown;

    public HubView()
    {
        InitializeComponent();
        CardItems.ItemsSource = Cards;
        BreadcrumbFlyoutItems.ItemsSource = Cards;
    }

    /// <summary>Overview cards. A host populates this after construction.</summary>
    public ObservableCollection<HubCardViewModel> Cards { get; } = new();

    /// <summary>The settings page VM of the currently shown detail page, or null on overview.</summary>
    public SettingPageViewModel? CurrentPageViewModel { get; private set; }

    #region Header configuration

    /// <summary>Header title (also the breadcrumb root label unless overridden).</summary>
    public string HeaderTitle
    {
        get => HeaderTitleText.Text;
        set
        {
            HeaderTitleText.Text = value;
            if (string.IsNullOrEmpty(BreadcrumbRootText.Text)) BreadcrumbRootText.Text = value;
        }
    }

    public string HeaderDescription
    {
        get => HeaderDescriptionText.Text;
        set => HeaderDescriptionText.Text = value;
    }

    /// <summary>Segoe Fluent Icons glyph string for the header + breadcrumb-root icon.</summary>
    public string HeaderGlyph
    {
        set
        {
            HeaderIcon.Text = value;
            BreadcrumbRootIcon.Text = value;
        }
    }

    public string BreadcrumbRootLabel
    {
        get => BreadcrumbRootText.Text;
        set => BreadcrumbRootText.Text = value;
    }

    public string SearchPlaceholder
    {
        get => SearchBox.PlaceholderText;
        set => SearchBox.PlaceholderText = value;
    }

    /// <summary>Show/hide the header search box (hubs of bespoke pages have no per-page search).</summary>
    public bool ShowSearch
    {
        set => SearchBox.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region Overview / detail switching

    /// <summary>Shows the overview card list, clearing the inner detail frame.</summary>
    public void ShowOverview()
    {
        _currentCard = null;
        CurrentPageViewModel = null;

        OverviewScroll.Visibility = Visibility.Visible;
        DetailFrame.Visibility = Visibility.Collapsed;
        DetailFrame.Content = null;

        BreadcrumbSeparator.Visibility = Visibility.Collapsed;
        BreadcrumbSection.Visibility = Visibility.Collapsed;

        // Quick Actions + search act on a detail page only.
        QuickActionsButton.IsEnabled = false;
        SearchBox.Text = string.Empty;

        OverviewShown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drills into the card whose detail page matches <paramref name="pageType"/>.
    /// Returns false if no card owns that page. Used by external navigation (global search,
    /// Home cards) to land directly on a section.</summary>
    public bool TryShowDetailByPageType(Type pageType)
    {
        foreach (var card in Cards)
        {
            if (card.DetailPageType == pageType) { ShowDetail(card); return true; }
        }
        return false;
    }

    /// <summary>Drills into a card's detail page inside the inner frame.</summary>
    public void ShowDetail(HubCardViewModel card)
    {
        if (card is null) return;

        _currentCard = card;

        OverviewScroll.Visibility = Visibility.Collapsed;
        DetailFrame.Visibility = Visibility.Visible;
        DetailFrame.Navigate(card.DetailPageType);

        BreadcrumbSectionText.Text = card.Title;
        BreadcrumbSectionIcon.Text = card.Glyph;
        BreadcrumbSeparator.Visibility = Visibility.Visible;
        BreadcrumbSection.Visibility = Visibility.Visible;

        // Quick Actions apply to settings pages only (AkariOS/Backup/Verify have no VM).
        SearchBox.Text = string.Empty;
        QuickActionsButton.IsEnabled = card.HasCounts;
    }

    #endregion

    #region Event handlers

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HubCardViewModel card }) ShowDetail(card);
    }

    private void SectionFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HubCardViewModel card }) ShowDetail(card);
    }

    private void BreadcrumbRoot_Click(object sender, RoutedEventArgs e) => ShowOverview();

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // On a detail page the search box filters that page's rows (the page's own
        // SearchText → ApplySearch). On the overview it does nothing yet.
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && CurrentPageViewModel is { } vm)
        {
            vm.SearchText = sender.Text ?? string.Empty;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => SearchSubmitted?.Invoke(this, sender.Text?.Trim() ?? string.Empty);

    private void DetailFrame_Navigated(object sender, NavigationEventArgs e)
    {
        // Adopt the detail page's settings VM (if any) so Quick Actions target it.
        CurrentPageViewModel = (DetailFrame.Content as FrameworkElement)?.DataContext as SettingPageViewModel;
        if (_currentCard is not null) CardNavigated?.Invoke(this, _currentCard);
    }

    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPageViewModel is { } vm) await vm.ApplyAllRecommendedAsync();
    }

    private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPageViewModel is { } vm) await vm.RestoreDefaultsAsync();
    }

    private async void CreateRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPageViewModel is { } vm) await vm.CreateRestorePointAsync();
    }

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        // View-menu toggles (Technical Details / Info Badges) are surfaced here for chrome
        // parity; wiring them to the detail page's view state lands with the hub pages.
    }

    #endregion
}
