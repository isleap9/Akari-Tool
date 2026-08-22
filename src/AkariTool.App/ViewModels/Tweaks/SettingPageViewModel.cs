using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Declarative replacement for TweakPageViewModel. Builds its sections from
/// <see cref="SettingGroup"/> records instead of TweakDefinition catalog arrays. No
/// TweakRegistry bracketing yet (Phase 3b-ii) — that wiring lands in a later phase.
/// </summary>
public abstract partial class SettingPageViewModel : ViewModelBase
{
    protected readonly ISettingStateReader _stateReader;
        protected readonly ISettingOperationExecutor _executor;
        protected readonly TweakDialogs _dialogs;
        protected readonly INewBadgeService? _newBadgeService;
        protected readonly ISettingDependencyResolver? _dependencyResolver;
        protected readonly ILocalizationService? _localizationService;
        protected readonly IDispatcherService? _dispatcherService;
        protected readonly IRegeditLauncher? _regeditLauncher;
        protected readonly IEventBus? _eventBus;

        private volatile bool _built;

        protected SettingPageViewModel(
            ISettingStateReader stateReader,
            ISettingOperationExecutor executor,
            TweakDialogs dialogs,
            INewBadgeService? newBadgeService = null,
            ISettingDependencyResolver? dependencyResolver = null,
            ILocalizationService? localizationService = null,
            IDispatcherService? dispatcherService = null,
            IRegeditLauncher? regeditLauncher = null,
            IEventBus? eventBus = null)
        {
            _stateReader = stateReader;
            _executor = executor;
            _dialogs = dialogs;
            _newBadgeService = newBadgeService;
            _dependencyResolver = dependencyResolver;
            _localizationService = localizationService;
            _dispatcherService = dispatcherService;
            _regeditLauncher = regeditLauncher;
            _eventBus = eventBus;
        }

    public abstract string NavTag { get; }
    public abstract string NavLabel { get; }

    protected abstract IReadOnlyList<SettingGroup> BuildSettingGroups();

    /// <summary>
            /// Row factory. Virtual so a page (Power) can hand plan-special services to its
            /// LoadDynamicOptions row without touching the shared Build path.
            /// </summary>
            protected virtual SettingItemViewModel CreateItem(SettingDefinition s)
                => new(s, _stateReader, _executor, _dialogs, newBadgeService: _newBadgeService,
                       dependencyResolver: _dependencyResolver,
                       localizationService: _localizationService,
                       dispatcherService: _dispatcherService,
                       regeditLauncher: _regeditLauncher,
                       eventBus: _eventBus,
                       logService: WinUI.Framework.IoC.ServiceLocator.GetService<ILogService>());

    /// <summary>
    /// Extra catalogs the dependency resolver may need beyond this page's own rows
    /// (Winhance resolves cross-feature dependencies via its global settings
    /// registry; Akari pages declare their cross-catalog universe here instead —
    /// e.g. Power's start-power-lock-option requires Privacy's privacy-lock-screen).
    /// </summary>
    protected virtual IReadOnlyList<SettingDefinition> AdditionalResolutionCatalogs() => [];

    /// <summary>
    /// The resolver applied a setting as a side effect of another row (auto-enable,
    /// cascade reset, dependency fix, preset sync): re-read that row from the system
    /// so its toggle/dropdown reflects reality (Winhance SettingAppliedEvent parity).
    /// </summary>
    private void OnResolverSettingApplied(string settingId)
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (item.Id == settingId)
                    item.RefreshFromSystem();
        RefreshQuickActionCounts();
    }

    /// <summary>
    /// A Power Plan row landed a plan change: re-read every sibling PowerCfg row
    /// (their values are scoped to the active plan). The plan row repopulates its
    /// own dropdown and is skipped here.
    /// </summary>
    private void OnPowerPlanChanged()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (!item.IsPowerPlanSetting)
                    item.RefreshFromSystem();
        RefreshQuickActionCounts();
    }

    /// <summary>
    /// 4h shared compatibility pipeline (Winhance CompatibleSettingsRegistry parity,
    /// filtered mode): every page's catalog passes the Windows-version filter first,
    /// then the hardware filter, before rows are materialized. Power additionally
    /// existence-validates inside its BuildSettingGroups override (Winhance gates
    /// existence Power-only too). Filters resolve via ServiceLocator so the base VM
    /// carries no extra ctor parameters; when unregistered the gate is a no-op.
    /// Blocking GetAwaiter matches Power's pre-existing pattern — Build() is
    /// synchronous and warm-up runs on a background thread.
    /// </summary>
    private IReadOnlyList<SettingGroup> ApplyCompatibilityGates(IReadOnlyList<SettingGroup> groups)
    {
        var windowsFilter = WinUI.Framework.IoC.ServiceLocator.GetService<IWindowsCompatibilityFilter>();
        var hardwareFilter = WinUI.Framework.IoC.ServiceLocator.GetService<IHardwareCompatibilityFilter>();
        if (windowsFilter is null && hardwareFilter is null) return groups;

        var result = new List<SettingGroup>(groups.Count);
        foreach (var group in groups)
        {
            IEnumerable<SettingDefinition> settings = group.Settings;
            if (windowsFilter is not null)
                settings = windowsFilter.FilterSettingsByWindowsVersion(settings);
            if (hardwareFilter is not null)
                settings = hardwareFilter.FilterSettingsByHardwareAsync(settings).GetAwaiter().GetResult();

            var kept = settings.ToList();
            if (kept.Count == 0) continue;   // empty group drops out (section template renders nothing for it)
            result.Add(group with { Settings = kept });
        }
        return result;
    }

    /// <summary>
    /// A gated row was permanently unlocked ("don't show again" checked): unlock
    /// every other RequiresAdvancedUnlock row on this page (Winhance's
    /// ParentFeatureViewModel sibling loop).
    /// </summary>
    private void OnAdvancedUnlockPersisted()
    {        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (item.RequiresAdvancedUnlock)
                    item.IsLocked = false;
    }

    public ObservableCollection<SettingSectionViewModel> Sections { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearch(value);

    private void ApplySearch(string query)
    {
        foreach (var section in Sections)
            section.ApplySearch(query);
    }

    public void Build()
    {
        if (_built) return;

        lock (this)
        {
            if (_built) return;

            foreach (var group in ApplyCompatibilityGates(BuildSettingGroups()))
            {
                var items = group.Settings.Select(CreateItem).ToList();
                var section = new SettingSectionViewModel(group.Name, items);
                Sections.Add(section);

                // Power Plan rows repopulate their own dropdown; the rest of the
                // page must re-read after the active plan changes.
                // RequiresAdvancedUnlock rows unlock their siblings when the user
                // checks "don't show again" in the warning dialog (Winhance parity).
                foreach (var item in items)
                {
                    if (item.IsPowerPlanSetting)
                        item.PowerPlanChanged += OnPowerPlanChanged;
                    if (item.RequiresAdvancedUnlock)
                        item.AdvancedUnlockPersisted += OnAdvancedUnlockPersisted;
                }
            }

            // Dependency resolution universe: this page's rows plus any cross-catalog
            // overrides. Rows get it after all sections exist so the list is complete.
            if (_dependencyResolver != null)
            {
                _dependencyResolver.SettingApplied -= OnResolverSettingApplied;
                _dependencyResolver.SettingApplied += OnResolverSettingApplied;

                var allSettings = Sections.SelectMany(s => s.Items.OfType<SettingItemViewModel>())
                    .Select(i => i.Definition)
                    .Concat(AdditionalResolutionCatalogs())
                    .ToList();
                foreach (var item in Sections.SelectMany(s => s.Items.OfType<SettingItemViewModel>()))
                    item.SetDependencyContext(allSettings);
            }

            _built = true;
            RefreshQuickActionCounts();
        }
    }

    // ── Quick Actions (tab scope) ─────────────────────────────────────────────

    [ObservableProperty]
    public partial int RecommendedPendingCount { get; set; }

    [ObservableProperty]
    public partial int DefaultPendingCount { get; set; }

    public string RecommendedPendingSubtitle => RecommendedPendingCount > 0 ? $"{RecommendedPendingCount} pending" : "All applied";
    public string DefaultPendingSubtitle => DefaultPendingCount > 0 ? $"{DefaultPendingCount} pending" : "All applied";

    partial void OnRecommendedPendingCountChanged(int value) => OnPropertyChanged(nameof(RecommendedPendingSubtitle));
    partial void OnDefaultPendingCountChanged(int value) => OnPropertyChanged(nameof(DefaultPendingSubtitle));

    /// <summary>
    /// Recomputes the pending counts: an item is "pending recommended" when it has a
    /// recommended target and its Recommended badge is not currently highlighted.
    /// </summary>
    public void RefreshQuickActionCounts()
    {
        int rec = 0, def = 0;
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
            {
                // Subjective-preference rows carry no recommendation to apply — they render a
                // Preference pill only and must never inflate quick-action counts.
                if (item.Definition.IsSubjectivePreference)
                    continue;

                if (item.HasRecommendedQuickSet
                    && !item.Badges.Any(b => b.Kind == AkariTool.Core.Features.Common.Enums.SettingBadgeKind.Recommended && b.IsHighlighted))
                    rec++;
                if (item.HasDefaultQuickSet
                    && !item.Badges.Any(b => b.Kind == AkariTool.Core.Features.Common.Enums.SettingBadgeKind.Default && b.IsHighlighted))
                    def++;
            }
        RecommendedPendingCount = rec;
        DefaultPendingCount = def;
    }

    [RelayCommand]
    public async Task ApplyAllRecommendedAsync()
    {
        var progress = WinUI.Framework.IoC.ServiceLocator.GetService<ITaskProgressService>();
        var cts = progress.StartTask("Applying recommended settings…");
        try
        {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
            {
                if (cts.IsCancellationRequested) break;
                if (item.HasRecommendedQuickSet)
                    await item.ApplyRecommendedCommand.ExecuteAsync(null);
            }
        RefreshQuickActionCounts();
        }
        finally
        {
            progress.CompleteTask();
        }
    }

    [RelayCommand]
    public async Task RestoreDefaultsAsync()
    {
        var progress = WinUI.Framework.IoC.ServiceLocator.GetService<ITaskProgressService>();
        var cts = progress.StartTask("Restoring default settings…");
        try
        {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
            {
                if (cts.IsCancellationRequested) break;
                if (item.HasDefaultQuickSet)
                    await item.ApplyDefaultCommand.ExecuteAsync(null);
            }
        RefreshQuickActionCounts();
        }
        finally
        {
            progress.CompleteTask();
        }
    }

    [RelayCommand]
    public async Task CreateRestorePointAsync()
    {
        // 4g — Winhance SettingsViewModel.CreateRestorePointAsync shape: the quick
        // action runs through the TaskProgressService card (indeterminate + Cancel)
        // and reports success/failure from BackupResult.
        var progressService = WinUI.Framework.IoC.ServiceLocator.GetService<ITaskProgressService>();
        var backup = WinUI.Framework.IoC.ServiceLocator.GetService<ISystemBackupService>();
        if (progressService is null || backup is null) return;

        var cts = progressService.StartTask("Creating system restore point...", isIndeterminate: true);
        var progress = progressService.CreateDetailedProgress();
        try
        {
            var result = await backup.CreateRestorePointAsync(
                progress: progress, cancellationToken: cts.Token).ConfigureAwait(true);

            if (result.Success && result.RestorePointCreated)
            {
                await _dialogs.InfoAsync("Create Restore Point",
                    "System Restore point created successfully.");
            }
            else
            {
                var failMsg = "Failed to create System Restore point.";
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    failMsg += $"\n\n{result.ErrorMessage}";
                await _dialogs.InfoAsync("Create Restore Point", failMsg);
            }
        }
        catch (Exception ex)
        {
            ToolService.Current?.Log($"[RESTORE] Quick action restore point failed: {ex.Message}");
            await _dialogs.InfoAsync("Create Restore Point", "Failed to create System Restore point.");
        }
        finally
        {
            progressService.CompleteTask();
        }
    }
}
