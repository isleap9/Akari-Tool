using CommunityToolkit.Mvvm.ComponentModel;
using AkariTool.Services;
using WinUI.Framework.Mvvm;
using WinUI.Framework.Services;

namespace AkariTool.ViewModels;

/// <summary>
/// Home page view model — the system-information banner only. The interactive parts of
/// the real net8 Home (global search wired to <c>TweakRegistry.Search</c>, and the
/// quick-nav card grid) are built in the page code-behind (Phase 9), matching how the
/// other bespoke pages are structured.
///
/// The Phase-A demo actions (ShowAbout / LogSomething / GoToTab→PlaceholderPage) were
/// removed entirely — nothing here registers tweaks or navigates to placeholders.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly ILogService _log;

    [ObservableProperty] public partial string Edition { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Version { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Cpu { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Gpu { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Memory { get; set; } = "Detecting…";

    /// <summary>True while the background gather is still running.</summary>
    [ObservableProperty] public partial bool IsGathering { get; set; } = true;

    public HomeViewModel(ILogService log)
    {
        _log = log;
        Title = "Akari Tool";
        Subtitle = "Your control center for Windows — optimization, software & utilities";
        _ = RefreshSystemInfoAsync();
    }

    private async Task RefreshSystemInfoAsync()
    {
        var info = await Task.Run(SystemInfoService.Gather);
        Edition = info.Edition;
        Version = info.Version;
        Cpu = info.Cpu;
        Gpu = info.Gpu;
        Memory = info.Memory;
        IsGathering = false;
        _log.Info("System information gathered.");
    }
}
