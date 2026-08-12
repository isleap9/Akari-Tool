using AkariTool.Services;
using AkariTool.Tabs;

namespace AkariTool.ViewModels.AdvancedTools;

/// <summary>
/// Advanced Tools VM — MVVM port of net8 <c>AdvancedToolsTab</c>'s cross-cutting state.
/// The tab is heavily imperative (collapsible step cards, live status, busy/cancel), so
/// the interactive UI is built in the page code-behind (faithful to net8); this VM owns
/// the pieces DI needs to reach: the two already-ported services and the selected-apps
/// provider hook.
///
/// ⚠ Bespoke, registers NOTHING with TweakRegistry (neither feature is TweakDefinition-
/// backed), so the <c>[WARMUP]</c> total is unchanged.
///
/// No Defender code referenced (Phase 2 recon confirmed the tab has none).
/// </summary>
public sealed class AdvancedToolsViewModel
{
    /// <summary>Ported byte-identical; drives the WIM wizard.</summary>
    public WimUtilService Wim { get; }

    /// <summary>Ported; drives the Autounattend generator.</summary>
    public AutounattendService Xml { get; }

    // net8 AdvancedToolsTab._selectedAppsProvider — set by MainWindow so the generator
    // can read the apps currently ticked in Software ▸ Windows Apps.
    private Func<List<AppDefinition>>? _selectedAppsProvider;

    public AdvancedToolsViewModel(ToolService tool)
    {
        Wim = new WimUtilService(tool);
        Xml = new AutounattendService(tool);
    }

    /// <summary>net8 <c>AdvancedToolsTab.SetSelectedAppsProvider</c>. The one-line hook
    /// Phase 25's <c>WindowsAppsViewModel.GetSelectedWindowsApps()</c> was built for.</summary>
    public void SetSelectedAppsProvider(Func<List<AppDefinition>> provider) =>
        _selectedAppsProvider = provider;

    /// <summary>net8 <c>GetSelectedApps()</c> — live read of the Bloatware selection.</summary>
    public List<AppDefinition> GetSelectedApps() => _selectedAppsProvider?.Invoke() ?? [];
}
