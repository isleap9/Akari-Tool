using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        // ── Composite string helpers (DirectXUserGlobalSettings) ──────────────
        // Format: "Key1=Value1;Key2=Value2;"

        private static Dictionary<string, string> ParseCompositeString(string raw)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq > 0)
                    result[part[..eq]] = part[(eq + 1)..];
            }
            return result;
        }

        private static string SetCompositeKey(string raw, string key, string value)
        {
            var dict = ParseCompositeString(raw);
            dict[key] = value;
            return string.Join("", dict.Select(kv => $"{kv.Key}={kv.Value};"));
        }

        // ── Registry read helpers ─────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : null;
            }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) as string;
            }
            catch { return null; }
        }

    }
}
