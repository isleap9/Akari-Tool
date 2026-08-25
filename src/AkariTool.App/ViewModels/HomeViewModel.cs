using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AkariTool.Services;
using AkariTool.Tabs;                                 // DriftScanner
using AkariTool.ViewModels.Tweaks;                    // SettingPageViewModel
using AkariTool.Core.Features.Common.Interfaces;      // INavBadgeService, IDispatcherService
using AkariTool.Core.Interfaces;                      // IUpdateService
using AkariTool.Core.Models.Update;                   // UpdateStatus
using WinUI.Framework.Mvvm;
using WinUI.Framework.Services;

namespace AkariTool.ViewModels;

/// <summary>
/// Home page ("Command Center") view model. Owns the system-information banner plus the
/// read-only health signals surfaced by the dashboard: aggregate recommended-pending count
/// (same value that drives the sidebar nav badges), config drift, and update status.
///
/// Everything here is read-only aggregation over services that already exist — Home never
/// mutates other tabs. The interactive parts (global search wired to
/// <c>SettingBackupService.Search</c>, the quick-nav card grid, and the quick actions) live
/// in the page code-behind.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly ILogService _log;
    private readonly INavBadgeService _navBadges;
    private readonly IUpdateService _updateService;
    private readonly IDispatcherService _dispatcher;
    private readonly IEnumerable<SettingPageViewModel> _pages;
    private readonly TweakDialogs _dialogs;
    private IDisposable? _navBadgeSub;

    [ObservableProperty] public partial string Edition { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Version { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Cpu { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Gpu { get; set; } = "Detecting…";
    [ObservableProperty] public partial string Memory { get; set; } = "Detecting…";

    /// <summary>True while the background gather is still running.</summary>
    [ObservableProperty] public partial bool IsGathering { get; set; } = true;

    // ── Health signals (read-only) ────────────────────────────────────────────
    [ObservableProperty] public partial int RecommendedPendingCount { get; set; }
    [ObservableProperty] public partial int DriftedCount { get; set; }
    [ObservableProperty] public partial string UpdateStatusText { get; set; } = "Checking…";
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }

    // Display strings (x:Bind targets a string; expose ToString'd values explicitly).
    public string RecommendedPendingDisplay => RecommendedPendingCount.ToString();
    public string DriftedDisplay => DriftedCount.ToString();

    /// <summary>Right-aligned hero status pill text.</summary>
    public string HealthSummary =>
        RecommendedPendingCount > 0 ? $"{RecommendedPendingCount} need attention"
        : DriftedCount > 0 ? $"{DriftedCount} drifted"
        : "Healthy";

    public bool IsHealthy => RecommendedPendingCount == 0 && DriftedCount == 0 && !IsUpdateAvailable;

    /// <summary>True while an "apply all" pass is running (disables the button).</summary>
    [ObservableProperty] public partial bool IsApplyingAll { get; set; }

    // The global button mirrors the per-tab "Apply all recommended": always available (it runs
    // each page's own bulk apply, which re-applies every recommended row, subjective included),
    // greyed only while a pass is in flight. It is deliberately NOT gated on the pending count —
    // that count excludes subjective preferences (e.g. VBS), so gating on it would hide exactly
    // the rows the per-tab button still applies.
    public bool CanApplyAll => !IsApplyingAll;
    public string ApplyAllLabel => IsApplyingAll ? "Applying…" : "Apply all recommended";

    partial void OnRecommendedPendingCountChanged(int value)
    {
        OnPropertyChanged(nameof(RecommendedPendingDisplay));
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(IsHealthy));
    }

    partial void OnIsApplyingAllChanged(bool value)
    {
        OnPropertyChanged(nameof(CanApplyAll));
        OnPropertyChanged(nameof(ApplyAllLabel));
    }

    partial void OnDriftedCountChanged(int value)
    {
        OnPropertyChanged(nameof(DriftedDisplay));
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(IsHealthy));
    }

    partial void OnIsUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(IsHealthy));

    public HomeViewModel(
        ILogService log,
        INavBadgeService navBadges,
        IUpdateService updateService,
        IDispatcherService dispatcher,
        IEnumerable<SettingPageViewModel> pages,
        TweakDialogs dialogs)
    {
        _log = log;
        _navBadges = navBadges;
        _updateService = updateService;
        _dispatcher = dispatcher;
        _pages = pages;
        _dialogs = dialogs;

        Title = "Akari Tool";
        Subtitle = "Your control center for Windows — optimization, software & utilities";

        SeedHealth();
        _ = RefreshSystemInfoAsync();
        _ = CheckUpdateAsync();
    }

    private void SeedHealth()
    {
        RefreshHealth();

        // Keep the aggregate live: warm-up may finish (or the user may apply tweaks on
        // another tab) after Home is already on screen. Marshalled to the UI thread — the
        // badge event can be raised from a background apply (MainWindow marshals it too).
        _navBadgeSub = _navBadges.Subscribe((_, _) =>
            _dispatcher.RunOnUIThread(RefreshHealth));
    }

    /// <summary>Re-reads the aggregate pending count and last drift scan. Cheap; safe to
    /// call on navigation and on badge changes.</summary>
    public void RefreshHealth()
    {
        RecommendedPendingCount = _navBadges.ComputeNavBadges().Sum(b => b.Count);
        DriftedCount = DriftScanner.Last?.Drifted.Count ?? 0;
    }

    /// <summary>
    /// Applies the recommended settings across every declarative page by invoking each page's
    /// own <c>ApplyAllRecommendedAsync()</c> — the exact code path the per-tab "Apply all
    /// recommended" button runs. Delegating (rather than reimplementing a filtered loop here)
    /// keeps Home 1:1 with the tabs: every recommended row is applied, including subjective
    /// preferences such as VBS, and each page shows its own TaskProgress card while it works.
    /// </summary>
    public async Task ApplyAllRecommendedAsync()
    {
        if (IsApplyingAll) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "Apply all recommended",
            "Apply the recommended settings across all tabs?\n\n" +
            "Consider creating a restore point first — some settings require a restart.",
            "Apply");
        if (!confirmed) return;

        IsApplyingAll = true;
        try
        {
            // Sequential await: each page starts and completes its own progress card, so no
            // nesting. Runs the same tested path as clicking Apply-All on each tab.
            foreach (var page in _pages)
                await page.ApplyAllRecommendedAsync();
        }
        catch (Exception ex)
        {
            _log.Info($"Home apply-all-recommended failed: {ex.Message}");
        }
        finally
        {
            IsApplyingAll = false;
            RefreshHealth();
        }
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

    private async Task CheckUpdateAsync()
    {
        try
        {
            var result = await _updateService.CheckAsync();
            IsUpdateAvailable = result.Status == UpdateStatus.UpdateAvailable;
            UpdateStatusText = result.Status switch
            {
                UpdateStatus.UpToDate => "Up to date",
                UpdateStatus.UpdateAvailable => string.IsNullOrEmpty(result.LatestTag)
                    ? "Update available"
                    : $"Update available ({result.LatestTag})",
                _ => "—",
            };
        }
        catch (Exception ex)
        {
            UpdateStatusText = "—";
            _log.Info($"Home update check failed: {ex.Message}");
        }
    }
}
