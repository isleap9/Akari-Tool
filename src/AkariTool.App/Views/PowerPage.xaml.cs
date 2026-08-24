using AkariTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Power detail page. Hosted inside the Optimize hub's inner frame; the hub owns the
/// header, search and Quick Actions, so this page renders only its section rows.
/// Power-specific content (the plan row + hardware gating) is data on
/// <see cref="PowerViewModel"/>, rendered by the shared rendering layer.
/// </summary>
public sealed partial class PowerPage : Page
{
    public PowerViewModel ViewModel { get; }

    public PowerPage()
    {
        InitializeComponent();

        // SINGLETON: the view model owns the built catalog + row state, so the
        // frame reuses this page instance rather than rebuild it per navigation.
        ViewModel = ServiceLocator.GetService<PowerViewModel>();
        DataContext = ViewModel;

        // Build once. Safe to call repeatedly — Build() is idempotent.
        ViewModel.Build();
    }
}
