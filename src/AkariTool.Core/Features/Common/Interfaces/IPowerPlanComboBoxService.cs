using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Builds and resolves the Power Plan combo box options (predefined plans matched
/// against system plans, unmatched system plans appended). Used by the
/// <c>power-plan-selection</c> setting's LoadDynamicOptions path.
/// </summary>
public interface IPowerPlanComboBoxService
{
    Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync();
    Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Task<PowerPlanResolutionResult> ResolvePowerPlanByIndexAsync(int index);
    void InvalidateCache();
}