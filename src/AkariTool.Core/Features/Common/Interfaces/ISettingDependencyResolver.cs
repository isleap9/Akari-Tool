using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Port of Winhance's ISettingDependencyResolver + IDependencyManager pair.
/// The manager logic is folded into one class because Winhance's split exists only
/// to route through its IGlobalSettingsRegistry / ISystemSettingsDiscoveryService /
/// ISettingApplicationService seams, which have no Akari equivalent — Akari resolves
/// against the caller-supplied catalog, <see cref="ISettingStateReader"/> and
/// <see cref="ISettingOperationExecutor"/>.
/// </summary>
public interface ISettingDependencyResolver
{
    /// <summary>
    /// Fired whenever the resolver applies a setting as a side effect of another
    /// setting's apply (dependency fix, auto-enable, cascade reset, preset sync).
    /// Replaces Winhance's eventBus.Publish(new SettingAppliedEvent(...)) UI refresh.
    /// </summary>
    event Action<string>? SettingApplied;

    /// <summary>
    /// Runs around an apply: on enable satisfies RequiresEnabled/RequiresDisabled/
    /// RequiresSpecificValue dependencies and auto-enables AutoEnableSettingIds
    /// children; on disable cascades dependents back to defaults; on value change
    /// resets dependents whose RequiredValue no longer matches.
    /// </summary>
    Task HandleDependenciesAsync(string settingId, IReadOnlyList<SettingDefinition> allSettings, bool enable, object? value);

    /// <summary>
    /// Auto-fixes RequiresValueBeforeAnyChange prerequisites before the caller's own
    /// apply runs (Winhance HandleValuePrerequisitesAsync).
    /// </summary>
    Task HandleValuePrerequisitesAsync(SettingDefinition setting, string settingId, IReadOnlyList<SettingDefinition> allSettings);

    /// <summary>
    /// After a child changes, moves its RequiresValueBeforeAnyChange parent dropdown
    /// to a preset index when every preset child now matches (Winhance
    /// SyncParentToMatchingPresetAsync).
    /// </summary>
    Task SyncParentToMatchingPresetAsync(SettingDefinition setting, string settingId, IReadOnlyList<SettingDefinition> allSettings);

/// <summary>
/// Parent Selection -> children preset application (Winhance SettingApplicationService parity).
/// After the parent's own value lands, applies every child listed in SettingPresets[selectedIndex].
/// </summary>
Task ApplyParentPresetsAsync(SettingDefinition setting, object? value, IReadOnlyList<SettingDefinition> allSettings);
}
