using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Plan-level power operations: query, activate, delete, and import/create plans.
/// Implementation in Infrastructure; ported from Winhance's PowerService with the
/// event-bus / config-import / recommended-apply plumbing trimmed (Session C wires
/// the catalog-backed recommended-apply for the Akari Power Plan).
/// </summary>
public interface IPowerService
{
    Task<PowerPlan?> GetActivePowerPlanAsync();
    Task<IReadOnlyList<PowerPlan>> GetAvailablePowerPlansAsync();

    /// <summary>
    /// Activates an existing plan by GUID. Returns false when the plan is already
    /// active (idempotent success), the call fails, or an error occurs.
    /// </summary>
    Task<bool> ActivatePowerPlanAsync(string powerPlanGuid);

    /// <summary>
    /// Deletes a plan by GUID. Returns false when the plan is the active plan,
    /// deletion fails, or an error occurs.
    /// </summary>
    Task<bool> DeletePowerPlanAsync(string powerPlanGuid);

    /// <summary>
    /// Imports (or re-uses) a predefined plan. For the Akari Power Plan this
    /// creates the plan by duplicating Ultimate Performance to the fixed GUID and
    /// (when <paramref name="powerCatalog"/> is supplied) applies each setting's
    /// recommended PowerCfg values to the new plan. Null catalog skips that step.
    /// </summary>
    Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan, IReadOnlyList<SettingDefinition>? powerCatalog = null);
}