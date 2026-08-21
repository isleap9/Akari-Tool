using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Declarative registry apply path — ported from Winhance's WindowsRegistryService.
/// OTS elevation removed (not used in Akari). LockKeyAccess deferred.
/// </summary>
public sealed class WindowsRegistryService : IWindowsRegistryService
{
    private readonly IAkariLogService _log;

    public WindowsRegistryService(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ── Interface overloads ────────────────────────────────────────────────

    public bool ApplySetting(RegistrySetting setting, bool enable)
        => ApplySettingCore(setting, enable, specificValue: null, useDefaultValue: false);

    public bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue)
        => ApplySettingCore(setting, enable, specificValue, useDefaultValue: false);

    public bool ApplySetting(RegistrySetting setting, bool enable, bool useDefaultValue)
        => ApplySettingCore(setting, enable, specificValue: null, useDefaultValue);

    // ── Core apply logic (ported from Winhance) ───────────────────────────

    private bool ApplySettingCore(RegistrySetting setting, bool isEnabled, object? specificValue, bool useDefaultValue)
    {
        if (setting == null) return false;

        try
        {
            // Per-network-interface expansion
            if (setting.ApplyPerNetworkInterface)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                {
                    _log.Log(LogLevel.Warning, $"[Registry] No subkeys found under '{setting.KeyPath}' for per-interface setting");
                    return false;
                }
                var allSucceeded = true;
                foreach (var subKey in subKeys)
                {
                    var expanded = setting with { KeyPath = $@"{setting.KeyPath}\{subKey}", ApplyPerNetworkInterface = false };
                    if (!ApplySettingCore(expanded, isEnabled, specificValue, useDefaultValue))
                        allSucceeded = false;
                }
                return allSucceeded;
            }

            // Per-monitor expansion
            if (setting.ApplyPerMonitor)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                {
                    _log.Log(LogLevel.Warning, $"[Registry] No subkeys found under '{setting.KeyPath}' for per-monitor setting");
                    return false;
                }
                var allSucceeded = true;
                foreach (var subKey in subKeys)
                {
                    var expanded = setting with { KeyPath = $@"{setting.KeyPath}\{subKey}", ApplyPerMonitor = false };
                    if (!ApplySettingCore(expanded, isEnabled, specificValue, useDefaultValue))
                        allSucceeded = false;
                }
                return allSucceeded;
            }

            _log.Log(LogLevel.Info, $"[Registry] Applying: {setting.KeyPath}\\{setting.ValueName} enable={isEnabled}");

            // Key-existence toggle (ValueName == null)
            if (setting.ValueName == null)
                return isEnabled ? CreateKey(setting.KeyPath) : DeleteKey(setting.KeyPath);

            // Composite REG_SZ (e.g. DirectXUserGlobalSettings)
            if (setting.CompositeStringKey != null)
            {
                if (!CreateKey(setting.KeyPath)) return false;
                var current = ValueExists(setting.KeyPath, setting.ValueName)
                    ? (GetValue(setting.KeyPath, setting.ValueName)?.ToString() ?? "")
                    : "";
                var pairs = ParseCompositeString(current);
                var subValue = specificValue?.ToString()
                    ?? (isEnabled ? GetWriteValue(setting.EnabledValue)?.ToString()
                                  : GetWriteValue(setting.DisabledValue)?.ToString());
                if (subValue != null) pairs[setting.CompositeStringKey] = subValue;
                else pairs.Remove(setting.CompositeStringKey);
                var merged = BuildCompositeString(pairs);
                return SetValue(setting.KeyPath, setting.ValueName, merged, RegistryValueKind.String);
            }

            // REG_BINARY bit-mask (BinaryByteIndex + BitMask)
            if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
            {
                if (!CreateKey(setting.KeyPath)) return false;
                var setBit = specificValue switch
                {
                    bool b => b,
                    int  i => i != 0,
                    byte b => b != 0,
                    _      => isEnabled
                };
                return ModifyBinaryBit(setting.KeyPath, setting.ValueName!,
                    setting.BinaryByteIndex.Value, setting.BitMask.Value, setBit);
            }

            // REG_BINARY byte-only modify
            if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
            {
                var byteValue = specificValue switch
                {
                    byte b => b,
                    int  i => (byte)i,
                    _ when isEnabled => GetWriteValue(setting.EnabledValue) switch
                    {
                        byte b => b,
                        int  i => (byte)i,
                        _      => (byte)0
                    },
                    _ => GetWriteValue(setting.DisabledValue) switch
                    {
                        byte b => b,
                        int  i => (byte)i,
                        _      => (byte)0
                    }
                };
                if (!CreateKey(setting.KeyPath)) return false;
                return ModifyBinaryByte(setting.KeyPath, setting.ValueName!,
                    setting.BinaryByteIndex.Value, byteValue);
            }

            // Standard value write / delete
            var valueToSet = useDefaultValue
                ? GetParentDisableValue(setting.DisabledValue)
                : specificValue ?? (isEnabled
                    ? GetWriteValue(setting.EnabledValue)
                    : GetWriteValue(setting.DisabledValue));

            if (valueToSet == null)
                return DeleteValue(setting.KeyPath, setting.ValueName);

            if (!CreateKey(setting.KeyPath)) return false;
            return SetValue(setting.KeyPath, setting.ValueName, valueToSet, setting.ValueType);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] Error applying '{setting.KeyPath}\\{setting.ValueName}': {ex.Message}");
            return false;
        }
    }

    // ── Registry helpers ──────────────────────────────────────────────────

    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    private static object? GetParentDisableValue(object?[]? disabledValues) => GetWriteValue(disabledValues);

    public bool CreateKey(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.CreateSubKey(subKeyPath, true);
            return key != null;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] Failed to create key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.CreateSubKey(subKeyPath, true);
            if (key == null) return false;
            key.SetValue(valueName, value, valueKind);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] Failed to set '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    public object? GetValue(string keyPath, string? valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key?.GetValue(valueName);
        }
        catch { return null; }
    }

    public bool DeleteValue(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, true);
            if (key == null) return false;
            key.DeleteValue(valueName, false);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] Failed to delete value '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    private const int MinDeleteDepth = 2;
    private static readonly HashSet<string> ProtectedSubKeyRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        @"SOFTWARE\Microsoft\Windows",
        @"SOFTWARE\Microsoft\Windows NT",
        @"SOFTWARE\Policies",
        @"SYSTEM\CurrentControlSet",
        @"SYSTEM\CurrentControlSet\Services",
    };

    public bool DeleteKey(string keyPath)
    {
        try
        {
            if (!KeyExists(keyPath)) return true;
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            var segments = subKeyPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < MinDeleteDepth)
            {
                _log.Log(LogLevel.Warning, $"[Registry] Refusing to delete shallow key '{keyPath}'");
                return false;
            }
            foreach (var protectedRoot in ProtectedSubKeyRoots)
            {
                if (subKeyPath.Equals(protectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Log(LogLevel.Warning, $"[Registry] Refusing to delete protected key '{keyPath}'");
                    return false;
                }
            }
            rootKey.DeleteSubKeyTree(subKeyPath, false);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] Failed to delete key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    public bool KeyExists(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key != null;
        }
        catch { return false; }
    }

    public bool ValueExists(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key?.GetValue(valueName) != null;
        }
        catch { return false; }
    }

    public string[] GetSubKeyNames(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    // ── Binary helpers ────────────────────────────────────────────────────

    private bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.CreateSubKey(subKeyPath, true);
            if (key == null) return false;
            var data = key.GetValue(valueName) as byte[] ?? Array.Empty<byte>();
            if (byteIndex >= data.Length)
                Array.Resize(ref data, byteIndex + 1);
            if (setBit) data[byteIndex] |= bitMask;
            else        data[byteIndex] = (byte)(data[byteIndex] & ~bitMask);
            key.SetValue(valueName, data, RegistryValueKind.Binary);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] ModifyBinaryBit failed '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    private bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte byteValue)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.CreateSubKey(subKeyPath, true);
            if (key == null) return false;
            var data = key.GetValue(valueName) as byte[] ?? Array.Empty<byte>();
            if (byteIndex >= data.Length)
                Array.Resize(ref data, byteIndex + 1);
            data[byteIndex] = byteValue;
            key.SetValue(valueName, data, RegistryValueKind.Binary);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[Registry] ModifyBinaryByte failed '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    // ── Composite string helpers ──────────────────────────────────────────

    internal static Dictionary<string, string> ParseCompositeString(string value)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(value)) return pairs;
        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = entry.IndexOf('=');
            if (eq > 0) pairs[entry[..eq]] = entry[(eq + 1)..];
        }
        return pairs;
    }

    private static string BuildCompositeString(Dictionary<string, string> pairs)
    {
        if (pairs.Count == 0) return "";
        return string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}")) + ";";
    }

    // ── Key path parsing ──────────────────────────────────────────────────

    private static (RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
    {
        var idx = keyPath.IndexOf('\\');
        if (idx < 0) throw new ArgumentException($"Invalid registry key path: {keyPath}");
        var hive = keyPath[..idx].ToUpperInvariant();
        var subPath = keyPath[(idx + 1)..];
        var rootKey = hive switch
        {
            "HKEY_CURRENT_USER"  or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT"  or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS"         or "HKU"  => Registry.Users,
            "HKEY_CURRENT_CONFIG"or "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Invalid registry hive: {hive}")
        };
        return (rootKey, subPath);
    }
}
