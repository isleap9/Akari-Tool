using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using AkariTool.ViewModels.Software;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Software ▸ External Apps page (rail tag "AppInstaller").
///
/// Card interaction is wired with DataTemplate event handlers rather than commands,
/// which is what keeps net8's exact semantics: the website glyph marks the tap
/// handled so it does NOT also toggle the card, and the checkbox consumes its own
/// click the same way the net8 CheckBox did.
/// </summary>
public sealed partial class ExternalAppsPage : Page
{
    public ExternalAppsViewModel ViewModel { get; }

    public ExternalAppsPage()
    {
        // Resolve BEFORE InitializeComponent: x:Bind evaluates during Initialize.
        // SINGLETON — holds the built catalog and the live selection state.
        ViewModel = ServiceLocator.GetService<ExternalAppsViewModel>();
        ViewModel.Build();

        InitializeComponent();

        // Icons + the first installed-status read start on the UI thread once the
        // tree exists (BitmapImage is UI-thread affine).
        Loaded += (_, _) => ViewModel.StartDeferredLoads();

        // View mode (Card / Table / Compact) swaps the section ItemTemplate. Driven by the
        // shared Software toolbar via the VM's ViewMode.
        ApplyViewMode();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExternalAppsViewModel.ViewMode)) ApplyViewMode();
        };
    }

    private void ApplyViewMode()
    {
        SectionsHost.ItemTemplate = ViewModel.ViewMode switch
        {
            SoftwareViewMode.Table => (DataTemplate)Application.Current.Resources["SoftwareSectionTableTemplate"],
            SoftwareViewMode.Compact => (DataTemplate)Application.Current.Resources["SoftwareSectionCompactTemplate"],
            _ => (DataTemplate)Resources["AppSectionTemplate"],   // Card (page-local)
        };
    }

    // ── Card interaction (net8 card.Tapped / check.Click / link.Tapped) ───────

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (Card(sender) is { } card) ViewModel.ToggleCard(card);
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        // The IsChecked TwoWay binding has already written the new value through to
        // the definition; this only refreshes the "N selected" count, as net8 did.
        if (Card(sender) is { } card) ViewModel.SetCardSelected(card, card.IsSelected);
    }

    private async void Website_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;   // net8: don't toggle the card
        if (Card(sender)?.WebsiteUrl is { Length: > 0 } url)
        {
            await ViewModel.OpenWebsiteAsync(url);
        }
    }

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Card(sender) is { } card) card.IsHovered = true;
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (Card(sender) is { } card) card.IsHovered = false;
    }

    private static AppCardViewModel? Card(object sender) =>
        (sender as FrameworkElement)?.DataContext as AppCardViewModel;
}
