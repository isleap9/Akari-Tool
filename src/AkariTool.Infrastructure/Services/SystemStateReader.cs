using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Reads the actual current registry state of every tweak so the UI
    /// reflects reality on startup — not just our saved flags.
    ///
    /// Each Read* method returns true if the tweak is currently active/applied.
    /// Returns null if the registry key doesn't exist (can't determine state).
    /// </summary>
    public static partial class SystemStateReader
    {
        // ── P/Invoke for real HKCU when running elevated ──────────────────────

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        private static RegistryKey? OpenRealHkcu(string subKey)
        {
            try
            {
                var explorer = System.Diagnostics.Process
                    .GetProcessesByName("explorer")
                    .FirstOrDefault();
                if (explorer is null) return null;

                if (!OpenProcessToken(explorer.Handle, 8, out var token)) return null;
                using var identity = new System.Security.Principal.WindowsIdentity(token);
                var sid = identity.User!.Value;
                var hku = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                return hku.OpenSubKey($@"{sid}\{subKey}");
            }
            catch { return null; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(subKey);
                if (key?.GetValue(valueName) is int val) return val;
                return null;
            }
            catch { return null; }
        }

        private static int? ReadHkcuDword(string subKey, string valueName)
        {
            try
            {
                using var key = OpenRealHkcu(subKey);
                if (key?.GetValue(valueName) is int val) return val;
                return null;
            }
            catch { return null; }
        }

    }
}
