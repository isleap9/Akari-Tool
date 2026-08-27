using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Pure badge-pill computation extracted from <see cref="SettingItemViewModel"/> (Winhance 1:1
/// evidence-folding logic). Takes a snapshot of the row's current values and returns the
/// Recommended/Default/Custom/Preference pills to display — no I/O, no side effects.
/// </summary>
public static class SettingBadgeCalculator
{
    public static IReadOnlyList<BadgePillState> Compute(
        SettingDefinition definition,
        InputType inputType,
        bool isOn,
        int selectedIndex,
        int numericValue,
        int acNumericValue,
        int dcNumericValue,
        bool hasBattery,
        bool supportsSeparateACDC)
    {
        var result = new List<BadgePillState>();

        if (definition.InputType == InputType.Action)
            return result;

        bool hasBadgeData =
            definition.RegistrySettings.Count > 0
            || definition.ScheduledTaskSettings.Count > 0
            || definition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true
            || (definition.PowerCfgSettings?.Any(p =>
                p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue
                || p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true);
        if (!hasBadgeData)
            return result;

        if (inputType == InputType.Toggle || inputType == InputType.CheckBox)
        {
            // Winhance 1:1: seed from the explicit toggle-level override, then fold ALL
            // evidence sources (registry, scheduled tasks) — every source with an opinion
            // can dim the flag; sources without one abstain (leave the flag untouched).
            bool matchesRec = true;
            bool matchesDef = true;

            if (definition.RecommendedToggleState.HasValue
                && isOn != definition.RecommendedToggleState.Value)
                matchesRec = false;
            if (definition.DefaultToggleState.HasValue
                && isOn != definition.DefaultToggleState.Value)
                matchesDef = false;

            foreach (var reg in definition.RegistrySettings)
            {
                var (regRec, regDef) = EvaluateRegistrySetting(definition, inputType, isOn, reg);
                if (!regRec) matchesRec = false;
                if (!regDef) matchesDef = false;
            }

            foreach (var task in definition.ScheduledTaskSettings)
            {
                // Toggle ON = task enabled. RecommendedState/DefaultState both represent the
                // task-enabled state, so compare IsOn directly (Winhance parity).
                if (task.RecommendedState.HasValue && isOn != task.RecommendedState.Value)
                    matchesRec = false;
                if (task.DefaultState.HasValue && isOn != task.DefaultState.Value)
                    matchesDef = false;
            }

            if (definition.IsSubjectivePreference)
            {
                result.Add(new BadgePillState(SettingBadgeKind.Preference, true, "Preference", "This is a preference setting"));
            }
            else
            {
                if (HasAnyRecommendedData(definition, inputType))
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (HasAnyDefaultData(definition, inputType))
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
            }
        }
        else if (inputType == InputType.Selection)
        {
            int optionCount = definition.ComboBox?.Options?.Count ?? 0;

            if (definition.IsSubjectivePreference)
            {
                result.Add(new BadgePillState(SettingBadgeKind.Preference, true, "Preference", "This is a preference setting"));
            }
            else
            {
                bool matchesRec;
                bool matchesDef;
                bool isCustom;

                if (supportsSeparateACDC)
                {
                    // Winhance 1:1: PowerCfg-backed Separate AC/DC selections drive via AC/DC
                    // indices compared against RecommendedValueAC/DC — NOT SelectedIndex vs
                    // option flags. On battery-less systems DC isn't writable/visible; skip DC
                    // comparisons or a refresh would visibly flip the badge.
                    bool considerDc = hasBattery;
                    matchesRec = true;
                    matchesDef = true;
                    var pcfgList = definition.PowerCfgSettings;
                    var pcfg = pcfgList?.FirstOrDefault();

                    if (pcfg != null)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && !PowerCfgIndexMatchesValue(definition, selectedIndex, pcfg.RecommendedValueAC.Value))
                            matchesRec = false;
                        if (considerDc && pcfg.RecommendedValueDC.HasValue && !PowerCfgIndexMatchesValue(definition, selectedIndex, pcfg.RecommendedValueDC.Value))
                            matchesRec = false;
                        if (pcfg.DefaultValueAC.HasValue && !PowerCfgIndexMatchesValue(definition, selectedIndex, pcfg.DefaultValueAC.Value))
                            matchesDef = false;
                        if (considerDc && pcfg.DefaultValueDC.HasValue && !PowerCfgIndexMatchesValue(definition, selectedIndex, pcfg.DefaultValueDC.Value))
                            matchesDef = false;
                    }

                    isCustom = !IsKnownSelectionValue(definition, inputType, selectedIndex, supportsSeparateACDC);
                }
                else
                {
                    // Winhance 1:1: light pills when the currently-selected OPTION carries the
                    // flag (multiple options may carry either — e.g. measurement system marks
                    // both Metric and Imperial default per locale). Custom only when the value
                    // is unmapped (out of range / Custom sentinel), never for a known option.
                    bool anyRecommended = definition.ComboBox?.Options?.Any(o => o.IsRecommended) == true;
                    bool anyDefault = definition.ComboBox?.Options?.Any(o => o.IsDefault) == true;

                    matchesRec = anyRecommended && selectedIndex >= 0 && selectedIndex < optionCount
                        && definition.ComboBox!.Options[selectedIndex].IsRecommended;
                    matchesDef = anyDefault && selectedIndex >= 0 && selectedIndex < optionCount
                        && definition.ComboBox!.Options[selectedIndex].IsDefault;
                    isCustom = !IsKnownSelectionValue(definition, inputType, selectedIndex, supportsSeparateACDC);
                }

                if (HasAnyRecommendedData(definition, inputType))
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (HasAnyDefaultData(definition, inputType))
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
                if (isCustom)
                    result.Add(new BadgePillState(SettingBadgeKind.Custom, true, "Custom", "Custom value"));
            }
        }
        else if (inputType == InputType.NumericRange)
        {
            var pcfg = definition.PowerCfgSettings?.FirstOrDefault();
            if (pcfg == null) return result;

            // Separate AC/DC with a battery present: per-mode pills so the user can see
            // which mode matches recommended/default and which is custom. On battery-less
            // systems DC is hidden and not writable — keep single-pill behaviour (1:1
            // with Winhance).
            bool perModeBadges = supportsSeparateACDC
                && hasBattery
                && pcfg.PowerModeSupport == PowerModeSupport.Separate;

            if (perModeBadges)
            {
                AddAcDcRecommendedPills(result, pcfg, definition, acNumericValue, dcNumericValue);
                AddAcDcDefaultPills(result, pcfg, definition, acNumericValue, dcNumericValue);
                AddAcDcCustomPills(result, pcfg, definition, acNumericValue, dcNumericValue);
            }
            else
            {
                // Compare display units; pcfg values are in system units.
                bool considerDc = hasBattery;
                bool matchesRec = true;
                bool matchesDef = true;

                if (supportsSeparateACDC)
                {
                    if (pcfg.RecommendedValueAC.HasValue && acNumericValue != ConvertFromSystemUnits(definition, pcfg.RecommendedValueAC.Value))
                        matchesRec = false;
                    if (considerDc && pcfg.RecommendedValueDC.HasValue && dcNumericValue != ConvertFromSystemUnits(definition, pcfg.RecommendedValueDC.Value))
                        matchesRec = false;
                    if (pcfg.DefaultValueAC.HasValue && acNumericValue != ConvertFromSystemUnits(definition, pcfg.DefaultValueAC.Value))
                        matchesDef = false;
                    if (considerDc && pcfg.DefaultValueDC.HasValue && dcNumericValue != ConvertFromSystemUnits(definition, pcfg.DefaultValueDC.Value))
                        matchesDef = false;
                }
                else
                {
                    if (pcfg.RecommendedValueAC.HasValue && numericValue != ConvertFromSystemUnits(definition, pcfg.RecommendedValueAC.Value))
                        matchesRec = false;
                    if (pcfg.DefaultValueAC.HasValue && numericValue != ConvertFromSystemUnits(definition, pcfg.DefaultValueAC.Value))
                        matchesDef = false;
                }

                bool hasRecData = pcfg.RecommendedValueAC.HasValue || (considerDc && pcfg.RecommendedValueDC.HasValue);
                bool hasDefData = pcfg.DefaultValueAC.HasValue || (considerDc && pcfg.DefaultValueDC.HasValue);

                if (hasRecData)
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (hasDefData)
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
                if (hasRecData || hasDefData)
                    result.Add(new BadgePillState(SettingBadgeKind.Custom, !matchesRec && !matchesDef, "Custom", "Custom value"));
            }
        }

        return result;
    }

    private static void AddAcDcRecommendedPills(List<BadgePillState> row, PowerCfgSetting pcfg, SettingDefinition definition, int acNumericValue, int dcNumericValue)
    {
        if (pcfg.RecommendedValueAC.HasValue)
        {
            bool match = acNumericValue == ConvertFromSystemUnits(definition, pcfg.RecommendedValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, "Recommended", "Akari's recommended value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue)
        {
            bool match = dcNumericValue == ConvertFromSystemUnits(definition, pcfg.RecommendedValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, "Recommended", "Akari's recommended value (on battery)", SettingBadgeMode.DC));
        }
    }

    private static void AddAcDcDefaultPills(List<BadgePillState> row, PowerCfgSetting pcfg, SettingDefinition definition, int acNumericValue, int dcNumericValue)
    {
        if (pcfg.DefaultValueAC.HasValue)
        {
            bool match = acNumericValue == ConvertFromSystemUnits(definition, pcfg.DefaultValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, "Windows Default", "Windows default value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.DefaultValueDC.HasValue)
        {
            bool match = dcNumericValue == ConvertFromSystemUnits(definition, pcfg.DefaultValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, "Windows Default", "Windows default value (on battery)", SettingBadgeMode.DC));
        }
    }

    private static void AddAcDcCustomPills(List<BadgePillState> row, PowerCfgSetting pcfg, SettingDefinition definition, int acNumericValue, int dcNumericValue)
    {
        if (pcfg.RecommendedValueAC.HasValue || pcfg.DefaultValueAC.HasValue)
        {
            bool acRec = pcfg.RecommendedValueAC.HasValue && acNumericValue == ConvertFromSystemUnits(definition, pcfg.RecommendedValueAC.Value);
            bool acDef = pcfg.DefaultValueAC.HasValue && acNumericValue == ConvertFromSystemUnits(definition, pcfg.DefaultValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, !acRec && !acDef, "Custom", "Custom value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue || pcfg.DefaultValueDC.HasValue)
        {
            bool dcRec = pcfg.RecommendedValueDC.HasValue && dcNumericValue == ConvertFromSystemUnits(definition, pcfg.RecommendedValueDC.Value);
            bool dcDef = pcfg.DefaultValueDC.HasValue && dcNumericValue == ConvertFromSystemUnits(definition, pcfg.DefaultValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, !dcRec && !dcDef, "Custom", "Custom value (on battery)", SettingBadgeMode.DC));
        }
    }

    private static (bool matchesRec, bool matchesDef) EvaluateRegistrySetting(SettingDefinition definition, InputType inputType, bool isOn, RegistrySetting reg)
    {
        // Winhance 1:1 — PURE computation against IsOn (no live registry reads).
        // Resolution order for Recommended:
        //   1. SettingDefinition.RecommendedToggleState (explicit toggle-level flag)
        //   2. Per-RegistrySetting RecommendedValue mapped strictly (no null-sentinel derivation)
        //   3. null → abstain (no badge match evidence)
        // Default still derives from the null sentinel via ToggleTargetState — settings
        // that ship with the registry key absent (e.g. EnabledValue = [1, null],
        // DefaultValue = null) produce a Default badge matching the key-absent state.
        if (!(inputType == InputType.Toggle || inputType == InputType.CheckBox))
            return (true, true); // Selection/Numeric handled in Compute

        bool? recommendedState = definition.RecommendedToggleState
            ?? (reg.RecommendedValue == null
                ? (bool?)null
                : SettingDefinitionToggleState.ToggleTargetState(reg.RecommendedValue, reg.EnabledValue, reg.DisabledValue));
        bool matchesRec = recommendedState == isOn;

        // A group-policy reg with no declared DefaultValue usually has no opinion
        // on the Windows default. However, for toggle settings the null-sentinel
        // convention CAN express a meaningful default state ("key absent = policy not
        // applied = Windows default behaviour"). When ToggleTargetState yields a result,
        // use it; otherwise abstain.
        bool matchesDef;
        if (reg.IsGroupPolicy && reg.DefaultValue == null)
        {
            var gpDefaultState = SettingDefinitionToggleState.ToggleTargetState(reg.DefaultValue, reg.EnabledValue, reg.DisabledValue);
            if (gpDefaultState.HasValue)
                matchesDef = gpDefaultState == isOn;
            else
                matchesDef = true; // no opinion → abstain
        }
        else if (SettingDefinitionToggleState.IsKeyExistenceToggle(reg))
        {
            // Key-existence toggles: Windows default is key-present (enabled = true).
            matchesDef = isOn == true;
        }
        else
        {
            var defaultState = SettingDefinitionToggleState.ToggleTargetState(reg.DefaultValue, reg.EnabledValue, reg.DisabledValue);
            matchesDef = defaultState == isOn;
        }

        return (matchesRec, matchesDef);
    }

    /// <summary>Winhance 1:1: pill visibility — any evidence source carrying a recommendation.</summary>
    private static bool HasAnyRecommendedData(SettingDefinition definition, InputType inputType)
    {
        // Toggle-level explicit flag wins.
        if ((inputType == InputType.Toggle || inputType == InputType.CheckBox)
            && definition.RecommendedToggleState.HasValue)
            return true;
        // Recommended is strict otherwise — explicit non-null per-RegistrySetting value.
        if (definition.RegistrySettings.Any(r => r.RecommendedValue != null))
            return true;
        if (inputType == InputType.Selection
            && definition.ComboBox?.Options?.Any(o => o.IsRecommended) == true)
            return true;
        if (definition.ScheduledTaskSettings.Any(t => t.RecommendedState.HasValue))
            return true;
        if (definition.PowerCfgSettings?.Any(
                p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true)
            return true;
        return false;
    }

    /// <summary>Winhance 1:1: pill visibility for Default, including GP null-sentinel nuance.</summary>
    private static bool HasAnyDefaultData(SettingDefinition definition, InputType inputType)
    {
        bool isToggleLike = inputType == InputType.Toggle || inputType == InputType.CheckBox;
        // Toggle-level explicit flag wins.
        if (isToggleLike && definition.DefaultToggleState.HasValue)
            return true;
        // GP regs with null DefaultValue are usually write-only enforcers, but the
        // null-sentinel convention can express a meaningful default for toggles.
        if (definition.RegistrySettings.Any(r =>
                (!(r.IsGroupPolicy && r.DefaultValue == null)
                 || (isToggleLike && SettingDefinitionToggleState.ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue))
                && (isToggleLike
                    ? SettingDefinitionToggleState.IsKeyExistenceToggle(r)
                      || SettingDefinitionToggleState.ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue
                    : r.DefaultValue != null)))
            return true;
        if (inputType == InputType.Selection
            && definition.ComboBox?.Options?.Any(o => o.IsDefault) == true)
            return true;
        if (definition.ScheduledTaskSettings.Any(t => t.DefaultState.HasValue))
            return true;
        if (definition.PowerCfgSettings?.Any(
                p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true)
            return true;
        return false;
    }

    /// <summary>
    /// Winhance 1:1: Custom means UNMAPPED — index out of range or Custom sentinel.
    /// A known option that simply carries no flag is never "Custom".
    /// Separate AC/DC selections validate both AC/DC indices.
    /// </summary>
    private static bool IsKnownSelectionValue(SettingDefinition definition, InputType inputType, int selectedIndex, bool supportsSeparateACDC)
    {
        if (inputType != InputType.Selection) return true;
        var options = definition.ComboBox?.Options;
        if (options == null || options.Count == 0) return true;
        if (supportsSeparateACDC)
            return selectedIndex >= 0; // AC/DC selections drive via a single index in Akari's VM
        return selectedIndex >= 0 && selectedIndex < options.Count;
    }

    /// <summary>Winhance 1:1: does the option at <paramref name="index"/> map to the target PowerCfg value?</summary>
    private static bool PowerCfgIndexMatchesValue(SettingDefinition definition, int index, int targetPowerCfgValue)
    {
        var options = definition.ComboBox?.Options;
        if (options == null || index < 0 || index >= options.Count) return false;

        if (options[index].ValueMappings is { } mapping &&
            mapping.TryGetValue("PowerCfgValue", out var val) && val != null)
        {
            try { return Convert.ToInt32(val) == targetPowerCfgValue; }
            catch { }
        }
        return false;
    }

    private static int ConvertFromSystemUnits(SettingDefinition definition, int systemValue) =>
        AkariTool.Infrastructure.Features.Common.Utilities.NumericConversionHelper
            .ConvertFromSystemUnits(systemValue, definition.NumericRange?.Units);
}
