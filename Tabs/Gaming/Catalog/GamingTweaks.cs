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

        // ── UserPreferencesMask per-bit helpers (Winhance-style) ──────────────
        // Windows appearance default mask: 9E 3E 07 80 12 00 00 00
        private static readonly byte[] _uiPrefMaskDefault =
            { 0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 };

        private static byte[] ReadUiPrefMask()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key?.GetValue("UserPreferencesMask") is byte[] data && data.Length >= 8)
                    return (byte[])data.Clone();
            }
            catch { }
            return (byte[])_uiPrefMaskDefault.Clone();
        }

        private static bool? ReadUiPrefBit(int byteIndex, byte bitMask)
        {
            var m = ReadUiPrefMask();
            if (byteIndex < 0 || byteIndex >= m.Length) return null;
            return (m[byteIndex] & bitMask) != 0;
        }

        private static void WriteUiPrefBit(int byteIndex, byte bitMask, bool on)
        {
            var m = ReadUiPrefMask();
            if (byteIndex < 0 || byteIndex >= m.Length) return;
            if (on) m[byteIndex] |= bitMask;
            else    m[byteIndex] = (byte)(m[byteIndex] & ~bitMask);
            using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
            key?.SetValue("UserPreferencesMask", m, RegistryValueKind.Binary);
        }

        private static TweakDefinition UiPrefBitTweak(
            Action<string> Log, string id, string name, string description,
            int byteIndex, byte bitMask) => new TweakDefinition
        {
            Id               = id,
            Name             = name,
            Description      = description,
            IsPreference     = true,
            RequiresRestart  = true,
            RecommendedState = false, // Winhance: mask animations recommended OFF
            DefaultState     = true,  // Windows default = bit set (ON)
            ReadState        = () => ReadUiPrefBit(byteIndex, bitMask),
            Apply            = on =>
            {
                WriteUiPrefBit(byteIndex, bitMask, on);
                Log($"{name} {(on ? "enabled" : "disabled")}.");
            }
        };

    }
}
