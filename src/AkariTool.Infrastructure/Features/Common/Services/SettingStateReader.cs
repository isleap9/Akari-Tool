using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class SettingStateReader(
    IPowerSettingsQueryService powerSettingsQueryService,
    ISpecialSettingHandlerRegistry specialHandlerRegistry) : ISettingStateReader
{
    private readonly ISpecialSettingHandlerRegistry _specialHandlerRegistry = specialHandlerRegistry;

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

                // Composite REG_SZ (e.g. DirectXUserGlobalSettings): extract the owned sub-key
                // before comparing. Runs before the key/value-absent checks because a missing
                // sub-key resolves its implied state via DefaultValue (Winhance
                // IsRegistryValueInEnabledState parity).
                if (!isKeyExistence && registrySetting.CompositeStringKey != null)
                    return ResolveCompositeState(registrySetting, subkey?.GetValue(registrySetting.ValueName));

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
            // Special-setting handlers own their own composite detection (e.g. the
            // Windows Update policy dropdown, whose Paused/Disabled states collide
            // under single-value matching). Delegate before any generic read.
            if (_specialHandlerRegistry.TryGet(setting.Id) is { } handler)
            {
                var disc = handler.DiscoverSpecialSettingsAsync(new[] { setting }).GetAwaiter().GetResult();
                if (disc.TryGetValue(setting.Id, out var vals) && vals.TryGetValue("CurrentPolicyIndex", out var raw) && raw is int i)
                    return i;
            }

            // Custom-detection rows (Winhance DetectedIndex parity): the DNS Server
            // dropdown resolves from the live adapter configuration, not from any
            // backing registry/powercfg value. Without this the reader returns -1
            // and the ComboBox renders blank on every launch (Winhance
            // SystemSettingsDiscoveryService.DetectDnsServerIndex 1:1).
            if (setting.DetectionType == DetectionType.DnsServer)
            {
                return DetectDnsServerIndex(setting);
            }

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

                // Winhance ResolveRawValuesToIndex parity: a live-absent value resolves to
                // the row's declared Windows default before matching; only entries without
                // a DefaultValue stay null.
                currentValues[registrySetting.ValueName ?? "KeyExists"] =
                    readValue ?? registrySetting.DefaultValue;
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

            // No option matched. Fall back to the IsDefault option when either:
            //  - every backing registry value is absent (a pristine system is the Windows default), or
            //  - the setting opts in via ResolveUnmatchedToDefault (its default state isn't a
            //    single enumerable value, so any unrecognised state is treated as the default).
            // (Winhance ResolveRawValuesToIndex parity — Akari previously honoured only the
            // pristine case, leaving ResolveUnmatchedToDefault rows blank/unmatched.)
            bool allBackingValuesAbsent = currentValues.Count > 0 && currentValues.Values.All(v => v is null);
            if (allBackingValuesAbsent || setting.ResolveUnmatchedToDefault)
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

        // Query failed to produce a value, or the setting opts in via
        // ResolveUnmatchedToDefault (Winhance unified-fallback parity): resolve to
        // the IsDefault option.
        if (acValue == null || setting.ResolveUnmatchedToDefault)
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
    /// Winhance DetectDnsServerIndex 1:1: resolves the DNS dropdown index from the
    /// live adapter configuration. Reads the active adapter's NameServer registry
    /// value (empty = DHCP) and its primary IPv4 DNS address, then delegates to
    /// <see cref="ResolveDnsServerIndex"/>.
    /// </summary>
    private int DetectDnsServerIndex(SettingDefinition setting)
    {
        var activeAdapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        if (activeAdapter == null)
            return 0;

        // Check if DNS is configured manually by reading the NameServer registry value.
        // When DNS is obtained via DHCP, NameServer is empty.
        string? nameServer = null;
        if (TryOpenSubkey(
                $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{activeAdapter.Id}",
                out var subKey))
        {
            using (subKey)
            {
                nameServer = subKey?.GetValue("NameServer") as string;
            }
        }

        var primaryDns = activeAdapter.GetIPProperties().DnsAddresses
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?
            .ToString();

        return ResolveDnsServerIndex(setting, nameServer, primaryDns);
    }

    /// <summary>
    /// Pure core of <see cref="DetectDnsServerIndex"/> (internal test seam): DHCP/empty
    /// NameServer → index 0 ("Automatic"); otherwise match the adapter's primary IPv4
    /// DNS against each option's ScriptVariables["primary"]; unknown manual server →
    /// Custom.
    /// </summary>
    internal static int ResolveDnsServerIndex(SettingDefinition setting, string? nameServer, string? primaryDns)
    {
        if (string.IsNullOrEmpty(nameServer))
            return 0; // DHCP — return "Automatic" index

        var dnsOptions = setting.ComboBox?.Options;
        if (string.IsNullOrEmpty(primaryDns) || dnsOptions == null)
            return 0;

        for (int i = 0; i < dnsOptions.Count; i++)
        {
            if (dnsOptions[i].ScriptVariables is { } variables
                && variables.TryGetValue("primary", out var primary)
                && primary == primaryDns)
            {
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
    /// Resolves a composite REG_SZ toggle state from its raw string value.
    /// Winhance IsRegistryValueInEnabledState parity: the owned sub-key is extracted
    /// and compared against EnabledValue; when absent, the implied state comes from
    /// DefaultValue. Internal + pure so it can be tested without a real registry.
    /// </summary>
    internal static bool ResolveCompositeState(RegistrySetting setting, object? rawValue)
    {
        var pairs = WindowsRegistryService.ParseCompositeString(rawValue?.ToString() ?? "");
        var enabledStr = setting.EnabledValue?.FirstOrDefault(v => v != null)?.ToString();
        if (pairs.TryGetValue(setting.CompositeStringKey!, out var subValue))
            return string.Equals(subValue, enabledStr, StringComparison.OrdinalIgnoreCase);

        return string.Equals(setting.DefaultValue?.ToString(), enabledStr,
            StringComparison.OrdinalIgnoreCase);
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
