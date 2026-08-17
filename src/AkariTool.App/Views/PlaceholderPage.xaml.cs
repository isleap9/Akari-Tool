using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AkariTool.ViewModels;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Shown for every rail destination that has no page yet. The rail tag travels
/// through the navigation parameter so the placeholder can identify itself.
/// </summary>
public sealed partial class PlaceholderPage : Page
{
    public PlaceholderViewModel ViewModel { get; }

    public PlaceholderPage()
    {
        InitializeComponent();
        ViewModel = ServiceLocator.GetService<PlaceholderViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.TabTag = e.Parameter as string ?? "Unknown";
    }
}
