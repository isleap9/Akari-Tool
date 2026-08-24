using AkariTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Start Menu detail page. Hosted inside the Customize hub's inner frame; the hub owns the
/// header, search, breadcrumb and Quick Actions, so this page renders only its section
/// rows. DataContext is set to the VM so the hub's Quick Actions can act on it.
/// </summary>
public sealed partial class StartMenuPage : Page
{
    public StartMenuViewModel ViewModel { get; }

    public StartMenuPage()
    {
        InitializeComponent();

        // SINGLETON: rows register with TweakRegistry on construction and there is
        // no unregister, so the view model must not be rebuilt per navigation.
        ViewModel = ServiceLocator.GetService<StartMenuViewModel>();
        DataContext = ViewModel;

        // Build once. Idempotent — the startup warm-up may already have built it.
        ViewModel.Build();
    }
}
