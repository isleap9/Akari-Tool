namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// A plan Akari knows about ahead of time, used to surface built-in plans
/// (and Akari's own custom plan) in the Power Plan combo box even when the
/// system does not currently carry them.
/// </summary>
public sealed record PredefinedPowerPlan(string Name, string Description, string LocalizationKey, string Guid);

/// <summary>
/// One entry in the Power Plan combo box. <see cref="PredefinedPlan"/> is set for
/// known plans; <see cref="SystemPlan"/> is set when the plan exists on this
/// system. Unmatched system plans are appended with <see cref="PredefinedPlan"/>
/// left null.
/// </summary>
public sealed record PowerPlanComboBoxOption
{
    public string DisplayName { get; init; } = string.Empty;
    public PredefinedPowerPlan? PredefinedPlan { get; init; }
    public PowerPlan? SystemPlan { get; init; }
    public bool ExistsOnSystem { get; init; }
    public bool IsActive { get; init; }
    public int Index { get; init; }
}

public sealed record PowerPlanImportResult(bool Success, string ImportedGuid, string ErrorMessage = "");

public sealed record PowerPlanResolutionResult
{
    public bool Success { get; init; }
    public string Guid { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// The predefined plans Akari surfaces in the Power Plan combo box.
/// LocalizationKey carries the literal English display name (Akari catalogs use
/// literal English strings — Winhance would key this to a localization resource).
/// The "Akari Power Plan" keeps Winhance's custom-plan GUID
/// (57696e68-… hex-encodes "Winhance-Power") for 1:1 behavioral compatibility:
/// a system that already carries that GUID matches, and its display name is
/// rebranded per isleap's decision.
/// </summary>
public static class PowerPlanDefinitions
{
    public static readonly IReadOnlyList<PredefinedPowerPlan> BuiltInPowerPlans = new List<PredefinedPowerPlan>
    {
        new("Power saver", "Saves energy by reducing computer performance", "Power saver", "a1841308-3541-4fab-bc81-f71556f20b4a"),
        new("Balanced", "Balances performance with energy consumption", "Balanced", "381b4222-f694-41f0-9685-ff5bb260df2e"),
        new("High performance", "Favors performance over energy consumption", "High performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        new("Ultimate Performance", "Maximum performance with no power saving measures", "Ultimate Performance", "e9a42b02-d5df-448d-aa00-03f14749eb61"),
        new("Akari Power Plan", "Optimized power plan for gaming and performance", "Akari Power Plan", "57696e68-616e-6365-506f-776572000000")
    };

    /// <summary>
    /// True when the GUID belongs to Akari's custom plan (whether it is spelled
    /// as a scheme GUID or matched by its friendly name).
    /// </summary>
    public static bool IsAkariPowerPlan(string? guid, string? name = null) =>
        string.Equals(guid, "57696e68-616e-6365-506f-776572000000", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name?.Trim(), "Akari Power Plan", StringComparison.OrdinalIgnoreCase);
}