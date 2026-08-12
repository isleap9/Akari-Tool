using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels.Software;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Software ▸ Debloat page (rail tag "Debloat"). Pure data + templates — Run/Undo are
/// commands on the row VMs, so there is no card-interaction code-behind (unlike the
/// Bloatware/External card pages).
/// </summary>
public sealed partial class DebloatPage : Page
{
    public DebloatViewModel ViewModel { get; }

    public DebloatPage()
    {
        // Resolve BEFORE InitializeComponent: x:Bind evaluates during Initialize.
        // SINGLETON — holds the built group/row tree and the applied-titles list.
        ViewModel = ServiceLocator.GetService<DebloatViewModel>();
        ViewModel.Build();

        InitializeComponent();
    }
}
