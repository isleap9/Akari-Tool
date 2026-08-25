using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUI.Framework.IoC;
using AkariTool.ViewModels.Software;

namespace AkariTool.Views;

/// <summary>
/// Software &amp; Apps page (Winhance-style tabbed layout). A hub-style header + search, a shared
/// toolbar (Install / Uninstall / Refresh + selected count) driving the active tab's VM, a
/// two-segment tab header, and an inner frame hosting the catalog pages
/// (<see cref="WindowsAppsPage"/> / <see cref="ExternalAppsPage"/>). The toolbar + search bind
/// to the active catalog VM through the page DataContext, updated on each tab switch.
/// </summary>
public sealed partial class SoftwareAppsPage : Page
{
    private readonly ISoftwareCatalogViewModel _windowsVm;
    private readonly ISoftwareCatalogViewModel _externalVm;

    public SoftwareAppsPage()
    {
        InitializeComponent();
        _windowsVm = ServiceLocator.GetService<WindowsAppsViewModel>();
        _externalVm = ServiceLocator.GetService<ExternalAppsViewModel>();
        SelectTab(typeof(WindowsAppsPage));   // default tab
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;
        var target = (btn.Tag as string) == "External" ? typeof(ExternalAppsPage) : typeof(WindowsAppsPage);
        SelectTab(target);
    }

    private void SelectTab(Type pageType)
    {
        bool windows = pageType == typeof(WindowsAppsPage);
        TabWindows.IsChecked = windows;
        TabExternal.IsChecked = !windows;

        // The shared toolbar + search bind to the active catalog VM.
        var vm = windows ? _windowsVm : _externalVm;
        DataContext = vm;
        SyncSortRadios(vm.SortMode);
        SyncViewToggles(vm.ViewMode);

        if (TabFrame.CurrentSourcePageType != pageType)
            TabFrame.Navigate(pageType);
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag }
            && DataContext is ISoftwareCatalogViewModel vm
            && Enum.TryParse<SoftwareViewMode>(tag, out var mode))
        {
            vm.ViewMode = mode;
            SyncViewToggles(mode);   // keep the trio exclusive even if the same one is re-clicked
        }
    }

    private void SyncViewToggles(SoftwareViewMode mode)
    {
        ViewCard.IsChecked = mode == SoftwareViewMode.Card;
        ViewTable.IsChecked = mode == SoftwareViewMode.Table;
        ViewCompact.IsChecked = mode == SoftwareViewMode.Compact;
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: string tag }
            && DataContext is ISoftwareCatalogViewModel vm
            && Enum.TryParse<SoftwareSortMode>(tag, out var mode))
        {
            vm.SortMode = mode;
        }
    }

    private void SyncSortRadios(SoftwareSortMode mode)
    {
        SortNameAsc.IsChecked = mode == SoftwareSortMode.NameAsc;
        SortNameDesc.IsChecked = mode == SoftwareSortMode.NameDesc;
        SortInstalledFirst.IsChecked = mode == SoftwareSortMode.InstalledFirst;
        SortNotInstalledFirst.IsChecked = mode == SoftwareSortMode.NotInstalledFirst;
    }

    /// <summary>Selects the tab whose catalog page matches <paramref name="detailPageType"/>
    /// (used by rail/search routing). Returns false for anything that isn't a Software tab.</summary>
    public bool ShowDetailFor(Type detailPageType)
    {
        if (detailPageType == typeof(WindowsAppsPage) || detailPageType == typeof(ExternalAppsPage))
        {
            SelectTab(detailPageType);
            return true;
        }
        return false;
    }
}
