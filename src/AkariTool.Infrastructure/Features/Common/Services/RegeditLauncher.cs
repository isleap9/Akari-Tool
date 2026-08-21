using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Implementation of IRegeditLauncher for checking registry key existence
/// and launching regedit at a specific path.
/// </summary>
public class RegeditLauncher : IRegeditLauncher
{
    private readonly IAkariLogService _log;

    public RegeditLauncher(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool KeyExists(string registryPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(registryPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key != null;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warning, $"[RegeditLauncher] KeyExists failed for '{registryPath}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public void OpenAtPath(string registryPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(registryPath))
                return;

            // Normalize the path: ensure it starts with a valid hive name
            var normalized = NormalizeRegistryPath(registryPath);
            if (string.IsNullOrEmpty(normalized))
            {
                _log.Log(LogLevel.Warning, $"[RegeditLauncher] Invalid registry path: '{registryPath}'");
                return;
            }

            // Write the path to HKCU\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit\LastKey
            // This makes regedit open at that key on next launch
            const string regeditLastKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";
            const string lastKeyValue = "LastKey";

            using var baseKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regeditLastKeyPath, true);
            if (baseKey != null)
            {
                baseKey.SetValue(lastKeyValue, normalized, Microsoft.Win32.RegistryValueKind.String);
            }

            // Launch regedit
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "regedit.exe",
                UseShellExecute = true
            });

            if (process == null)
            {
                _log.Log(LogLevel.Warning, "[RegeditLauncher] Failed to start regedit.exe");
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[RegeditLauncher] OpenAtPath failed for '{registryPath}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string NormalizeRegistryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Handle common hive abbreviations
        var hiveMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "HKCU", "HKEY_CURRENT_USER" },
            { "HKLM", "HKEY_LOCAL_MACHINE" },
            { "HKCR", "HKEY_CLASSES_ROOT" },
            { "HKU", "HKEY_USERS" },
            { "HKCC", "HKEY_CURRENT_CONFIG" }
        };

        // Find the first backslash
        var idx = path.IndexOf('\\');
        if (idx < 0)
            return string.Empty;

        var hive = path[..idx].ToUpperInvariant();
        var subPath = path[(idx + 1)..];

        if (hiveMap.TryGetValue(hive, out var fullHive))
        {
            return $@"{fullHive}\{subPath}";
        }

        // Check if it's already a full hive name
        var validHives = new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKEY_CURRENT_CONFIG" };
        if (validHives.Contains(hive, StringComparer.OrdinalIgnoreCase))
        {
            return path;
        }

        return string.Empty;
    }

    private static (Microsoft.Win32.RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
    {
        var idx = keyPath.IndexOf('\\');
        if (idx < 0) throw new ArgumentException($"Invalid registry key path: {keyPath}");
        var hive = keyPath[..idx].ToUpperInvariant();
        var subPath = keyPath[(idx + 1)..];
        var rootKey = hive switch
        {
            "HKEY_CURRENT_USER" => Microsoft.Win32.Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" => Microsoft.Win32.Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" => Microsoft.Win32.Registry.ClassesRoot,
            "HKEY_USERS" => Microsoft.Win32.Registry.Users,
            "HKEY_CURRENT_CONFIG" => Microsoft.Win32.Registry.CurrentConfig,
            _ => throw new ArgumentException($"Invalid registry hive: {hive}")
        };
        return (rootKey, subPath);
    }
}