using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Validation;

/// <summary>
/// One problem found while validating a setting catalog.
/// </summary>
public sealed record CatalogViolation(string SettingId, string GroupName, string Message);

/// <summary>
/// Catalog-invariant checks for authored <see cref="SettingGroup"/> definitions.
///
/// Selection recommendation/default shape (semantics match Winhance's validator):
///
///   • <b>Dynamic</b>   — options populated at runtime (PowerRecommendation.LoadDynamicOptions).
///                        Skipped — nothing to validate statically.
///   • <b>PowerCfg</b>  — PowerCfgSettings present. Recommendation/default live on
///                        PowerRecommendation + PowerCfgSetting.DefaultValueAC/DC. Options must
///                        NOT use per-option IsRecommended/IsDefault, because AC and DC can
///                        recommend different options.
///   • <b>Subjective</b>— IsSubjectivePreference = true. Badges render as "Preference"
///                        regardless of flags, but authors may still mark a preferred option
///                        as a Quick Actions hint — at most one of each.
///   • <b>Standard</b>  — everything else. Exactly one IsRecommended AND exactly one
///                        IsDefault option.
///
/// Akari-only checks (run by <see cref="Validate(System.Collections.Generic.IEnumerable{SettingGroup})"/>):
///   • Setting ids unique across the whole catalog set (backup-file compatibility).
///   • RegistrySetting.KeyPath well-formed (known hive + non-empty subkey).
///   • Every ComboBox ValueMappings key resolves to a declared RegistrySetting.ValueName —
///     apply and detection both iterate declared settings only; unmatched keys are silently
///     skipped on write and read as null on read (skipped for handler-routed settings whose
///     RegistrySettings list is deliberately empty).
/// </summary>
public static class SettingCatalogValidator
{
    private static readonly string[] KnownHives =
    [
        "HKEY_LOCAL_MACHINE",
        "HKEY_CURRENT_USER",
        "HKEY_CLASSES_ROOT",
        "HKEY_USERS",
        "HKEY_CURRENT_CONFIG",
    ];

    /// <summary>Validates a single group's Selection recommendation/default shape.</summary>
    public static IReadOnlyList<CatalogViolation> Validate(SettingGroup group)
    {
        var violations = new List<CatalogViolation>();
        if (group?.Settings is null) return violations;

        foreach (var setting in group.Settings)
        {
            if (setting.InputType != InputType.Selection) continue;
            ValidateSelectionShape(setting, group.Name, violations);
        }
        return violations;
    }

    /// <summary>
    /// Validates a full catalog set: single-group shape checks plus cross-group id
    /// uniqueness, registry-path well-formedness, and combo-mapping consistency.
    /// </summary>
    public static IReadOnlyList<CatalogViolation> Validate(IEnumerable<SettingGroup> groups)
    {
        var violations = new List<CatalogViolation>();
        if (groups is null) return violations;

        var seenIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            if (group?.Settings is null) continue;

            violations.AddRange(Validate(group));

            foreach (var setting in group.Settings)
            {
                if (!string.IsNullOrEmpty(setting.Id))
                {
                    if (seenIds.TryGetValue(setting.Id, out var existingGroup))
                        violations.Add(new(setting.Id, group.Name,
                            $"Duplicate setting id — already defined in group '{existingGroup}'."));
                    else
                        seenIds[setting.Id] = group.Name;
                }

                ValidateRegistryPaths(setting, group.Name, violations);
                ValidateComboMappings(setting, group.Name, violations);
            }
        }
        return violations;
    }

    // ── Selection recommendation/default shape ────────────────────────────────

    private static void ValidateSelectionShape(SettingDefinition setting, string groupName, List<CatalogViolation> violations)
    {
        var options = setting.ComboBox?.Options ?? (IReadOnlyList<ComboBoxOption>)Array.Empty<ComboBoxOption>();

        int recommendedCount = options.Count(o => o.IsRecommended);
        int defaultCount = options.Count(o => o.IsDefault);

        switch (Categorize(setting))
        {
            case Category.Dynamic:
                return;

            case Category.PowerCfg:
                if (recommendedCount > 0)
                    violations.Add(new(setting.Id, groupName,
                        $"PowerCfg-backed Selection must not set ComboBoxOption.IsRecommended (found {recommendedCount}). " +
                        "Recommendation lives on PowerRecommendation.RecommendedOptionAC/DC."));
                if (defaultCount > 0)
                    violations.Add(new(setting.Id, groupName,
                        $"PowerCfg-backed Selection must not set ComboBoxOption.IsDefault (found {defaultCount}). " +
                        "Default lives on PowerCfgSetting.DefaultValueAC/DC."));
                break;

            case Category.Subjective:
                if (recommendedCount > 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Subjective Selection has {recommendedCount} IsRecommended options; expected 0 or 1."));
                if (defaultCount > 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Subjective Selection has {defaultCount} IsDefault options; expected 0 or 1."));
                break;

            case Category.Standard:
                if (options.Count == 0)
                {
                    violations.Add(new(setting.Id, groupName,
                        "Selection has no ComboBox options."));
                    return;
                }
                if (recommendedCount != 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Selection must have exactly one IsRecommended option (found {recommendedCount})."));
                if (defaultCount != 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Selection must have exactly one IsDefault option (found {defaultCount})."));
                break;
        }
    }

    private enum Category { Standard, Subjective, PowerCfg, Dynamic }

    private static Category Categorize(SettingDefinition s)
    {
        if (s.Recommendation?.LoadDynamicOptions == true) return Category.Dynamic;
        if (s.PowerCfgSettings?.Count > 0) return Category.PowerCfg;
        if (s.IsSubjectivePreference) return Category.Subjective;
        return Category.Standard;
    }

    // ── Akari-only: registry path well-formedness ────────────────────────────

    private static void ValidateRegistryPaths(SettingDefinition setting, string groupName, List<CatalogViolation> violations)
    {
        foreach (var reg in setting.RegistrySettings)
        {
            if (string.IsNullOrWhiteSpace(reg.KeyPath))
            {
                violations.Add(new(setting.Id, groupName,
                    "RegistrySetting has an empty KeyPath."));
                continue;
            }

            bool hiveOk = false;
            foreach (var hive in KnownHives)
            {
                if (reg.KeyPath.StartsWith(hive + @"\", StringComparison.OrdinalIgnoreCase)
                    && reg.KeyPath.Length > hive.Length + 1)
                {
                    hiveOk = true;
                    break;
                }
            }
            if (!hiveOk)
                violations.Add(new(setting.Id, groupName,
                    $@"RegistrySetting KeyPath '{reg.KeyPath}' must start with a known hive (HKLM/HKCU/HKCR/HKU/HKCC) followed by a non-empty subkey."));
        }
    }

    // ── Akari-only: combo mapping ↔ declared value-name consistency ──────────

    private static void ValidateComboMappings(SettingDefinition setting, string groupName, List<CatalogViolation> violations)
    {
        if (setting.InputType != InputType.Selection) return;
        var options = setting.ComboBox?.Options;
        if (options is null || options.Count == 0) return;

        // Handler-routed settings (e.g. updates-policy-mode) deliberately ship an empty
        // RegistrySettings list — their state lives behind ISpecialSettingHandler, so the
        // declared-settings consistency rule does not apply to them.
        if (setting.RegistrySettings.Count == 0) return;

        var declaredNames = new HashSet<string>(
            setting.RegistrySettings
                .Where(r => !string.IsNullOrEmpty(r.ValueName))
                .Select(r => r.ValueName!),
            StringComparer.Ordinal);

        foreach (var option in options)
        {
            if (option.ValueMappings is null) continue;
            foreach (var key in option.ValueMappings.Keys)
            {
                if (!declaredNames.Contains(key))
                    violations.Add(new(setting.Id, groupName,
                        $"ComboBox option '{option.DisplayName}' maps value key '{key}' which has no matching RegistrySetting.ValueName. " +
                        "Apply and detection iterate declared settings only — unmatched keys are silently skipped."));
            }
        }
    }
}
