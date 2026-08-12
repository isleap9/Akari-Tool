using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using AkariTool.ViewModels.Software;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Software ▸ Windows Apps page (rail tag "Bloatware"). Card interaction is wired the
/// same way as <see cref="ExternalAppsPage"/> — DataTemplate event handlers, so the
/// website glyph marks the tap handled and does not also toggle the card.
/// </summary>
public sealed partial class WindowsAppsPage : Page
{
    public WindowsAppsViewModel ViewModel { get; }

    public WindowsAppsPage()
    {
        // Resolve BEFORE InitializeComponent: x:Bind evaluates during Initialize.
        // SINGLETON — holds the built catalog and the live selection state (which the
        // Advanced Tools provider hook reads).
        ViewModel = ServiceLocator.GetService<WindowsAppsViewModel>();
        ViewModel.Build();

        InitializeComponent();

        Loaded += (_, _) => ViewModel.StartDeferredLoads();
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (Card(sender) is { } card) ViewModel.ToggleCard(card);
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        if (Card(sender) is { } card) ViewModel.SetCardSelected(card, card.IsSelected);
    }

    private async void Website_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
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
