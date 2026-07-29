using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        private static bool? ReadConsentStore(string subKey)
        {
            try { using var k = Registry.LocalMachine.OpenSubKey(subKey); var v = k?.GetValue("Value") as string; return v == null ? (bool?)true : v != "Deny"; }
            catch { return true; }
        }

        private static void WriteConsentStore(string subKey, bool allow)
            => Registry.SetValue($@"HKEY_LOCAL_MACHINE\{subKey}", "Value", allow ? "Allow" : "Deny", RegistryValueKind.String);

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) as string; }
            catch { return null; }
        }
    }
}
