using System;
using System.Linq;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class SettingStateReader : ISettingStateReader
{
    public bool ReadToggleState(SettingDefinition setting)
    {
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
            var options = setting.ComboBox?.Options;
            if (options == null || options.Count == 0)
                return -1;

            var registrySetting = SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting);
            if (registrySetting == null)
                return -1;

            if (!TryOpenSubkey(registrySetting.KeyPath, out var subkey))
                return -1;

            using (subkey)
            {
                var currentValue = subkey?.GetValue(registrySetting.ValueName);

                var mappingKey = registrySetting.ValueName ?? "KeyExists";
                for (int i = 0; i < options.Count; i++)
                {
                    var mappings = options[i].ValueMappings;
                    if (mappings != null
                        && mappings.TryGetValue(mappingKey, out var mappedValue)
                        && ValuesEqual(currentValue, mappedValue))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }
        catch
        {
            return -1;
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
