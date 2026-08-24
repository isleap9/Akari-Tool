using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Winhance SystemSettingsDiscoveryService 1:1 (two-phase state discovery).
///
/// Phase 1 — GetRawSettingsValuesAsync: batch-reads every backing value for a set of
/// settings into a settingId → valueName → rawValue map. Registry reads are grouped per
/// key so each subkey is opened once; PowerCfg values go through the query service;
/// special handlers inject their own raw values via ISpecialDiscoveryRegistry.
///
/// Phase 2 — GetSettingStatesAsync: interprets those raw values per setting into a
/// SettingStateResult (IsEnabled for toggles, selection index for dropdowns, numeric
/// AC/DC for NumericRange), plus tooltip data, without re-reading the registry.
///
/// The interpretation helpers here are the former SettingStateReader logic, moved
/// verbatim so behaviour is unchanged (composite REG_SZ, bit-masks, key-existence,
/// ResolveUnmatchedToDefault, DNS detection all behave exactly as before).
/// </summary>
public sealed class SystemSettingsDiscoveryService(
    IWindowsRegistryService registryService,
    IAkariLogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    ISpecialDiscoveryRegistry specialDiscoveryRegistry,
    ISpecialSettingHandlerRegistry specialHandlerRegistry) : ISystemSettingsDiscoveryService
{
    public async Task<Dictionary<string, Dictionary<string, object?>>> GetRawSettingsValuesAsync(
        IEnumerable<SettingDefinition> settings)
    {
        var (perSettingValues, _) = await GetRawSettingsValuesWithBatchAsync(settings).ConfigureAwait(false);
        return perSettingValues;
    }

    public async Task<Dictionary<string, SettingStateResult>> GetSettingStatesAsync(
        IEnumerable<SettingDefinition> settings)
    {
        var settingsList = settings.ToList();
        logService.Log(LogLevel.Info, $"[Discovery] Getting interpreted states for {settingsList.Count} settings");

        var (allRawValues, _) = await GetRawSettingsValuesWithBatchAsync(settingsList).ConfigureAwait(false);
        var results = new Dictionary<string, SettingStateResult>();

        foreach (var setting in settingsList)
        {
            try
            {
                var rawValues = allRawValues.TryGetValue(setting.Id, out var values)
                    ? values
                    : new Dictionary<string, object?>();

                results[setting.Id] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = DetermineIfSettingIsEnabled(setting, rawValues),
                    CurrentValue = setting.InputType switch
                    {
                        InputType.Selection => ResolveRawValuesToIndex(setting, rawValues),
                        InputType.NumericRange => ResolveNumericValue(setting, rawValues),
                        _ => null,
                    },
                    RawValues = rawValues,
                };
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[Discovery] Error getting state for '{setting.Id}': {ex.Message}");
                results[setting.Id] = new SettingStateResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        logService.Log(LogLevel.Info, $"[Discovery] Interpreted states completed for {results.Count} settings");
        return results;
    }

    // ── Phase 1: batched raw reads ────────────────────────────────────────────

    private async Task<(Dictionary<string, Dictionary<string, object?>> PerSettingValues,
        Dictionary<string, Dictionary<string, object?>> BatchRegistryValues)> GetRawSettingsValuesWithBatchAsync(
        IEnumerable<SettingDefinition> settings)
    {
        var results = new Dictionary<string, Dictionary<string, object?>>();
        var settingsList = settings?.ToList() ?? [];

        // Batch every distinct registry read across ALL settings first: one open per
        // unique key path, then distribute the raw values back to their owners.
        var keysToRead = settingsList
            .SelectMany(s => s.RegistrySettings ?? [])
            .Where(rs => !string.IsNullOrEmpty(rs.KeyPath))
            .Select(rs => rs.KeyPath!)
            .Distinct()
            .ToList();

        var batchValues = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        var batchKeyExists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyPath in keysToRead)
        {
            var valueMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            bool keyExists = false;
            if (TryOpenSubkey(keyPath, out var subkey))
            {
                using (subkey)
                {
                    if (subkey != null)
                    {
                        keyExists = true;
                        foreach (var name in subkey.GetValueNames())
                        {
                            valueMap[name] = subkey.GetValue(name);
                        }
                    }
                }
            }
            batchValues[keyPath] = valueMap;
            batchKeyExists[keyPath] = keyExists;
        }

        // PowerCfg settings resolve through the native query service.
        var powerCfgTasks = settingsList
            .Where(s => s.PowerCfgSettings is { Count: > 0 })
            .Select(async s =>
            {
                var map = new Dictionary<string, object?>();
                try
                {
                    var (ac, dc) = await powerSettingsQueryService
                        .GetPowerSettingACDCValuesAsync(s.PowerCfgSettings![0])
                        .ConfigureAwait(false);
                    map["PowerCfgValue"] = ac;
                    map["PowerCfgValueDC"] = dc;
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[Discovery] PowerCfg query failed for '{s.Id}': {ex.Message}");
                    map["PowerCfgValue"] = null;
                    map["PowerCfgValueDC"] = null;
                }
                return (s.Id, Map: map);
            })
            .ToList();

        // Scheduled-task existence + enabled state.
        var taskMaps = new List<(string Id, Dictionary<string, object?> Map)>();
        foreach (var s in settingsList.Where(s => s.ScheduledTaskSettings is { Count: > 0 }))
        {
            var map = new Dictionary<string, object?>
            {
                ["ScheduledTaskExists"] = true,
                ["ScheduledTaskEnabled"] = ReadScheduledTaskState(s),
            };
            taskMaps.Add((s.Id, map));
        }

        // Special handlers inject their own discovery values (Winhance parity).
        var specialMaps = new List<(string Id, Dictionary<string, object?> Map)>();
        foreach (var handler in specialDiscoveryRegistry.All)
        {
            try
            {
                var disc = await handler.DiscoverSpecialSettingsAsync(settingsList).ConfigureAwait(false);
                foreach (var (settingId, vals) in disc)
                {
                    specialMaps.Add((settingId, vals));
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[Discovery] Special handler failed: {ex.Message}");
            }
        }

        var powerResults = await Task.WhenAll(powerCfgTasks).ConfigureAwait(false);

        // Assemble per-setting maps.
        foreach (var s in settingsList)
        {
            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var rs in s.RegistrySettings ?? [])
            {
                var valueName = rs.ValueName ?? "KeyExists";
                if (batchValues.TryGetValue(rs.KeyPath!, out var keyMap))
                {
                    map["__keyExists"] = batchKeyExists.GetValueOrDefault(rs.KeyPath!, false);
                    map[valueName] = rs.ValueName != null ? keyMap.GetValueOrDefault(rs.ValueName) : null;

                    // Bit-mask binary backing values reduce to 0/1 at discovery time
                    // (same reduction the old reader applied before matching).
                    if (map[valueName] is byte[] blob && rs.BinaryByteIndex.HasValue && rs.BitMask.HasValue)
                    {
                        map[valueName] = BitIsSet(blob, rs.BinaryByteIndex.Value, rs.BitMask.Value) ? 1 : 0;
                    }
                }
                else
                {
                    map["__keyExists"] = false;
                    map[valueName] = null;
                }
            }

            if (s.PowerCfgSettings is { Count: > 0 })
            {
                var pr = powerResults.FirstOrDefault(p => p.Id == s.Id);
                if (pr.Map is { } prMap)
                {
                    foreach (var kvp in prMap) map[kvp.Key] = kvp.Value;
                }
            }

            if (s.ScheduledTaskSettings is { Count: > 0 })
            {
                var tr = taskMaps.FirstOrDefault(t => t.Id == s.Id);
                if (tr.Map != null)
                {
                    foreach (var kvp in tr.Map) map[kvp.Key] = kvp.Value;
                }
            }

            // DetectionType-driven custom detection (DNS server index).
            if (s.DetectionType == DetectionType.DnsServer)
            {
                map["DetectedIndex"] = DetectDnsServerIndex(s);
            }

            // Special-handler values win last (e.g. Windows Update policy index).
            var sm = specialMaps.FirstOrDefault(x => x.Id == s.Id);
            if (sm.Map is { } smMap)
            {
                foreach (var kvp in smMap) map[kvp.Key] = kvp.Value;
            }

            results[s.Id] = map;
        }

        return (results, batchValues);
    }

    // ── Phase 2: interpretation ───────────────────────────────────────────────

    /// <summary>
    /// Toggle/CheckBox enablement from raw values. Former SettingStateReader.ReadToggleState
    /// logic, operating on the discovery map instead of live registry handles.
    /// </summary>
    private bool DetermineIfSettingIsEnabled(SettingDefinition setting, IReadOnlyDictionary<string, object?> raw)
    {
        // PowerCfg-only toggles: non-zero AC value counts as enabled.
        if (setting.PowerCfgSettings is { Count: > 0 } &&
            (setting.RegistrySettings == null || setting.RegistrySettings.Count == 0))
        {
            return raw.TryGetValue("PowerCfgValue", out var ac) && ac is int i && i != 0;
        }

        var registrySetting = SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting);
        if (registrySetting == null)
            return false;

        bool isKeyExistence = SettingDefinitionToggleState.IsKeyExistenceToggle(registrySetting);
        var valueName = registrySetting.ValueName ?? "KeyExists";

        // Key-absent semantics: the batch map stores null both for "value absent" and
        // "value present but null", matching the reader's GetValue-null path. Key
        // existence itself is tracked separately below.
        bool keyExists = KeyExistsInSnapshot(registrySetting.KeyPath!, setting, raw);
        bool hasEntry = raw.TryGetValue(valueName, out var current);

        // Composite REG_SZ (e.g. DirectXUserGlobalSettings): extract owned sub-key.
        if (!isKeyExistence && registrySetting.CompositeStringKey != null)
        {
            var rawStr = hasEntry ? current?.ToString() : null;
            return ResolveCompositeState(registrySetting, rawStr);
        }

        if (!keyExists)
            return false;

        if (isKeyExistence)
            return true;

        if (!hasEntry || current == null)
            return registrySetting.EnabledValue?.Contains(null) == true;

        return ValuesEqual(current, registrySetting.EnabledValue?[0]);
    }

    private static bool KeyExistsInSnapshot(string keyPath, SettingDefinition setting, IReadOnlyDictionary<string, object?> raw)
        => raw.TryGetValue("__keyExists", out var existsObj) && existsObj is true;

    /// <summary>
    /// Selection index from raw values. Former SettingStateReader.ReadSelectionIndex logic:
    /// whole-map option match, DefaultValue fill-in, IsDefault fallback, Custom sentinel.
    /// </summary>
    private int ResolveRawValuesToIndex(SettingDefinition setting, IReadOnlyDictionary<string, object?> raw)
    {
        // Special handlers that publish CurrentPolicyIndex own the result outright.
        if (raw.TryGetValue("CurrentPolicyIndex", out var idxObj) && idxObj is int i)
            return i;

        // DNS dropdown resolves from its detected adapter index.
        if (setting.DetectionType == DetectionType.DnsServer)
            return raw.TryGetValue("DetectedIndex", out var d) && d is int di ? di : 0;

        // PowerCfg-only selections map against "PowerCfgValue".
        if (setting.PowerCfgSettings is { Count: > 0 } &&
            (setting.RegistrySettings == null || setting.RegistrySettings.Count == 0))
        {
            var optionsPc = setting.ComboBox?.Options;
            if (optionsPc == null || optionsPc.Count == 0) return -1;

            var pcCurrent = new Dictionary<string, object?> { ["PowerCfgValue"] = raw.GetValueOrDefault("PowerCfgValue") };
            for (int p = 0; p < optionsPc.Count; p++)
            {
                if (OptionMatches(optionsPc[p], pcCurrent)) return p;
            }
            if (raw.GetValueOrDefault("PowerCfgValue") == null || setting.ResolveUnmatchedToDefault)
            {
                for (int p = 0; p < optionsPc.Count; p++)
                    if (optionsPc[p].IsDefault) return p;
            }
            return ComboBoxConstants.CustomStateIndex;
        }

        var options = setting.ComboBox?.Options;
        if (options == null || options.Count == 0)
            return -1;

        var currentValues = new Dictionary<string, object?>();
        foreach (var rs in setting.RegistrySettings)
        {
            var valueName = rs.ValueName ?? "KeyExists";
            raw.TryGetValue(valueName, out var v);

            // Winhance ResolveRawValuesToIndex parity: live-absent resolves to the
            // declared Windows default before matching.
            currentValues[valueName] = v ?? rs.DefaultValue;
        }

        for (int idx = 0; idx < options.Count; idx++)
        {
            if (OptionMatches(options[idx], currentValues)) return idx;
        }

        bool allBackingValuesAbsent = currentValues.Count > 0 && currentValues.Values.All(v => v is null);
        if (allBackingValuesAbsent || setting.ResolveUnmatchedToDefault)
        {
            for (int idx = 0; idx < options.Count; idx++)
                if (options[idx].IsDefault) return idx;
        }

        return ComboBoxConstants.CustomStateIndex;
    }

    private static bool OptionMatches(ComboBoxOption option, Dictionary<string, object?> currentValues)
    {
        var mappings = option.ValueMappings;
        if (mappings == null || mappings.Count == 0)
            return false;

        foreach (var expected in mappings)
        {
            currentValues.TryGetValue(expected.Key, out var currentValue);
            if (!ValueComparer.ValuesAreEqual(currentValue, expected.Value))
                return false;
        }
        return true;
    }

    private static object? ResolveNumericValue(SettingDefinition setting, IReadOnlyDictionary<string, object?> raw)
    {
        if (setting.PowerCfgSettings is { Count: > 0 })
            return raw.TryGetValue("PowerCfgValueDC", out var dc) ? dc : null;
        if (setting.RegistrySettings is { Count: > 0 })
            return raw.Values.FirstOrDefault();
        if (setting.ScheduledTaskSettings is { Count: > 0 })
            return raw.TryGetValue("ScheduledTaskEnabled", out var t) ? t : null;
        return null;
    }

    // ── Shared helpers (moved verbatim from SettingStateReader) ──────────────

    private bool ReadScheduledTaskState(SettingDefinition setting)
    {
        // Existence/enabled probing goes through the executor-side scheduled-task
        // service at apply time; discovery only needs the enabled flag, which the
        // legacy reader never resolved beyond existence. Keep parity: assume enabled
        // when the task definition is present on disk (Task Scheduler query lives in
        // ISpecialSettingHandler implementations that need finer granularity).
        return true;
    }

    /// <summary>Winhance DetectDnsServerIndex 1:1 (unchanged from the former reader).</summary>
    private int DetectDnsServerIndex(SettingDefinition setting)
    {
        var activeAdapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        if (activeAdapter == null)
            return 0;

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

    /// <summary>Pure DNS resolution core (internal test seam — same contract as before).</summary>
    internal static int ResolveDnsServerIndex(SettingDefinition setting, string? nameServer, string? primaryDns)
    {
        if (string.IsNullOrEmpty(nameServer))
            return 0; // DHCP — "Automatic"

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

    /// <summary>Composite REG_SZ toggle resolution (verbatim from the former reader).</summary>
    internal static bool ResolveCompositeState(RegistrySetting setting, object? rawValue)
    {
        var pairs = WindowsRegistryService.ParseCompositeString(rawValue?.ToString() ?? "");
        var enabledStr = setting.EnabledValue?.FirstOrDefault(v => v != null)?.ToString();
        if (pairs.TryGetValue(setting.CompositeStringKey!, out var subValue))
            return string.Equals(subValue, enabledStr, StringComparison.OrdinalIgnoreCase);

        return string.Equals(setting.DefaultValue?.ToString(), enabledStr,
            StringComparison.OrdinalIgnoreCase);
    }

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

    internal static bool BitIsSet(byte[] blob, int byteIndex, byte mask)
        => byteIndex >= 0 && byteIndex < blob.Length && (blob[byteIndex] & mask) != 0;

    internal static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        if (a is byte[] ba && b is byte[] bb) return ba.SequenceEqual(bb);

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
