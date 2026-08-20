using System;
using System.Linq;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class SettingStateReader(
    IPowerSettingsQueryService powerSettingsQueryService) : ISettingStateReader
{
    public bool ReadToggleState(SettingDefinition setting)
    {
        // PowerCfg-only settings (no RegistrySettings) have no registry state to read.
        // A non-zero active AC value counts as enabled (Winhance resolves the live
        // PowerCfg state against the AC value for both Separate and non-Separate).
        if (setting.PowerCfgSettings is { Count: > 0 } &&
            (setting.RegistrySettings == null || setting.RegistrySettings.Count == 0))
        {
            var (acValue, _) = ReadPowerCfgValues(setting.PowerCfgSettings[0]);
            return acValue.HasValue && acValue.Value != 0;
        }

        try
        {
            var registrySetting = SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting);
            if (registrySetting == null)
                return false;

            if (!TryOpenSubkey(registrySetting.KeyPath, out var subkey))
                return false;

            using (subkey)
            {
                bool isKeyExistence = SettingDefinitionToggleState.IsKeyExistenceToggle(registrySetting);

                if (subkey == null)
                {
                    // Key absent = disabled (whether or not this is a key-existence toggle).
                    return false;
                }

                if (isKeyExistence)
                {
                    // Key present = enabled.
                    return true;
                }

                var currentValue = subkey.GetValue(registrySetting.ValueName);
                if (currentValue == null)
                {
                    // Value absent: enabled if EnabledValue expresses the key-absent (null) sentinel.
                    return registrySetting.EnabledValue?.Contains(null) == true;
                }

                if (currentValue is byte[] blob && registrySetting.BinaryByteIndex.HasValue && registrySetting.BitMask.HasValue)
                {
                    int byteIdx = registrySetting.BinaryByteIndex.Value;
                    byte bitMask = registrySetting.BitMask.Value;
                    return byteIdx < blob.Length && (blob[byteIdx] & bitMask) != 0;
                }

                return ValuesEqual(currentValue, registrySetting.EnabledValue?[0]);
            }
        }
        catch
        {
            return false;
        }
    }

    public int ReadSelectionIndex(SettingDefinition setting)
    {
        try
        {
            // PowerCfg-only settings resolve their live index against the active
            // scheme's AC value mapped through each option's "PowerCfgValue".
            if (setting.PowerCfgSettings is { Count: > 0 } &&
                (setting.RegistrySettings == null || setting.RegistrySettings.Count == 0))
            {
                return ReadPowerCfgSelectionIndex(setting);
            }

            var options = setting.ComboBox?.Options;
            if (options == null || options.Count == 0)
                return -1;

            // Read the live value of every backing registry setting into a
            // ValueName-keyed map (missing/unopenable = null).
            var currentValues = new Dictionary<string, object?>();
            foreach (var registrySetting in setting.RegistrySettings)
            {
                object? readValue = null;
                if (TryOpenSubkey(registrySetting.KeyPath, out var subkey))
                {
                    using (subkey)
                    {
                        readValue = subkey?.GetValue(registrySetting.ValueName);
                    }
                }

                currentValues[registrySetting.ValueName ?? "KeyExists"] = readValue;
            }

            // Whole-map match: an option wins only when every value it maps
            // equals the corresponding live value.
            for (int i = 0; i < options.Count; i++)
            {
                var mappings = options[i].ValueMappings;
                if (mappings == null)
                    continue;

                bool allMatch = true;
                foreach (var expected in mappings)
                {
                    currentValues.TryGetValue(expected.Key, out var currentValue);
                    if (!ValueComparer.ValuesAreEqual(currentValue, expected.Value))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch && mappings.Count > 0)
                    return i;
            }

            // No option matched. A pristine system (all backing values absent)
            // resolves to the IsDefault option; otherwise it is a custom state.
            if (currentValues.Count > 0 && currentValues.Values.All(v => v is null))
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].IsDefault)
                        return i;
                }
            }

            return ComboBoxConstants.CustomStateIndex;
        }
        catch
        {
            return -1;
        }
    }

    public (int? acValue, int? dcValue) ReadNumericValue(SettingDefinition setting)
    {
        try
        {
            if (setting.PowerCfgSettings is not { Count: > 0 })
                return (null, null);

            var (acSystem, dcSystem) = ReadPowerCfgValues(setting.PowerCfgSettings[0]);
            string? displayUnits = setting.NumericRange?.Units ?? setting.PowerCfgSettings[0].Units;
            return (
                acSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(acSystem.Value, displayUnits) : null,
                dcSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(dcSystem.Value, displayUnits) : null);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Resolves a PowerCfg-only Selection setting's live index from the active
    /// scheme's AC value, matching each option's "PowerCfgValue" mapping.
    /// Winhance resolves the index against the AC value for both Separate and
    /// non-Separate settings (1:1 port). A null read (query failure) falls back to
    /// the IsDefault option.
    /// </summary>
    private int ReadPowerCfgSelectionIndex(SettingDefinition setting)
    {
        var options = setting.ComboBox?.Options;
        if (options == null || options.Count == 0)
            return -1;

        var (acValue, _) = ReadPowerCfgValues(setting.PowerCfgSettings![0]);

        var currentValues = new Dictionary<string, object?>
        {
            ["PowerCfgValue"] = acValue
        };

        for (int i = 0; i < options.Count; i++)
        {
            var mappings = options[i].ValueMappings;
            if (mappings == null)
                continue;

            bool allMatch = true;
            foreach (var expected in mappings)
            {
                currentValues.TryGetValue(expected.Key, out var currentValue);
                if (!ValueComparer.ValuesAreEqual(currentValue, expected.Value))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch && mappings.Count > 0)
                return i;
        }

        // Query failed to produce a value: treat as a pristine/unknown state.
        if (acValue == null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].IsDefault)
                    return i;
            }
        }

        return ComboBoxConstants.CustomStateIndex;
    }

    /// <summary>
    /// Reads the active scheme's AC/DC values for a PowerCfg setting. The query
    /// service runs its native reads on a background thread, so blocking here is
    /// safe (no sync context) and keeps the badge path synchronous.
    /// </summary>
    private (int? acValue, int? dcValue) ReadPowerCfgValues(PowerCfgSetting powerCfgSetting)
    {
        try
        {
            return powerSettingsQueryService
                .GetPowerSettingACDCValuesAsync(powerCfgSetting)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Parses the hive and subkey path from a full KeyPath and opens the subkey read-only.
    /// Returns false when the hive prefix is unrecognized; otherwise true, with <paramref name="subkey"/>
    /// possibly null when the key does not exist.
    /// </summary>
    private static bool TryOpenSubkey(string keyPath, out RegistryKey? subkey)
    {
        subkey = null;

        const string HklmPrefix = @"HKEY_LOCAL_MACHINE\";
        const string HkcuPrefix = @"HKEY_CURRENT_USER\";

        RegistryKey hive;
        string subPath;

        if (keyPath.StartsWith(HklmPrefix, StringComparison.Ordinal))
        {
            hive = Registry.LocalMachine;
            subPath = keyPath.Substring(HklmPrefix.Length);
        }
        else if (keyPath.StartsWith(HkcuPrefix, StringComparison.Ordinal))
        {
            hive = Registry.CurrentUser;
            subPath = keyPath.Substring(HkcuPrefix.Length);
        }
        else
        {
            return false;
        }

        subkey = hive.OpenSubKey(subPath, writable: false);
        return true;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        try
        {
            return Convert.ToInt64(a) == Convert.ToInt64(b);
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
