using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AkariTool.ViewModels.Verify;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Verify page (rail tag "Verify"). Thin view over <see cref="VerifyViewModel"/>; the
/// scan is a read, so it re-runs on every navigation to the page (net8 re-scanned on
/// tab show via IsVisibleChanged — OnNavigatedTo is the WinUI equivalent, since this
/// page is NavigationCacheMode="Required" and Loaded fires only once).
/// </summary>
public sealed partial class VerifyPage : Page
{
    public VerifyViewModel ViewModel { get; }

    public VerifyPage()
    {
        // Resolve BEFORE InitializeComponent: x:Bind evaluates during Initialize.
        ViewModel = ServiceLocator.GetService<VerifyViewModel>();

        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Scan();
    }
}
