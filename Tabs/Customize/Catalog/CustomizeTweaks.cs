using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    // MVVM PORT: base partial for the extracted Customize catalog. Holds the shared
    // registry helpers that the net8 CustomizeTab code-behind kept in CustomizeTab.xaml.cs
    // (ReadDwordCu, the ShellState blob helpers, SetHkcu). Lifted verbatim; the only
    // adaptations are: instance → static, and Service?.Log → ToolService.Current?.Log
    // (the Phase-1 static-service accessor). Each section method takes an Action<string> Log.
    //
    // _suppressRestart carries over as a static flag: several Customize Apply delegates skip
    // the per-row Explorer restart when a bulk operation is batching them. The (future)
    // bulk-apply/rendering layer sets it exactly as the net8 tab did.
    public static partial class CustomizeTweaks
    {
        public static bool _suppressRestart = false;

        private static int? ReadDwordCu(string subKey, string valueName)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : (int?)null;
            }
            catch { return null; }
        }

        // ShellState blob (Explorer) — carries the single/double-click flag in bit 5
        // of byte 4. Winhance-default template used when the value is absent.
        private static readonly byte[] _shellStateDefault = {
            0x24,0x00,0x00,0x00, 0x3E,0x28,0x00,0x00, 0x00,0x00,0x00,0x00,
            0x01,0x00,0x00,0x00, 0x01,0x00,0x00,0x00, 0x00,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00, 0x80,0x00,0x00,0x00, 0x00,0x00,0x00,0x00 };

        private static byte[] ReadShellState()
        {
            try {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer");
                if (k?.GetValue("ShellState") is byte[] d && d.Length >= 5) return (byte[])d.Clone();
            } catch { }
            return (byte[])_shellStateDefault.Clone();
        }
        private static bool ShellStateDoubleClick() { var s = ReadShellState(); return (s[4] & 0x20) != 0; }
        private static void WriteShellStateDoubleClick(bool dbl)
        {
            var s = ReadShellState();
            if (dbl) s[4] |= 0x20; else s[4] = (byte)(s[4] & ~0x20);
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer");
            k?.SetValue("ShellState", s, RegistryValueKind.Binary);
        }

        // Shared by every Customize taskbar/explorer tweak. A denied write degrades
        // gracefully with a logged message (net8 logged via the shared Service; here via
        // the static ToolService.Current) instead of escaping as an unhandled exception.
        private static bool SetHkcu(string subKey, string valueName, int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(subKey,
                    RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key == null)
                {
                    ToolService.Current?.Log($"[TASKBAR] Could not open HKCU\\{subKey} for writing.");
                    return false;
                }
                key.SetValue(valueName, value, RegistryValueKind.DWord);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ToolService.Current?.Log($"[TASKBAR] Access denied writing {valueName} to HKCU\\{subKey}. " +
                             "This value may be locked by policy; try running as administrator.");
                return false;
            }
            catch (Exception ex)
            {
                ToolService.Current?.Log($"[TASKBAR] Failed to write {valueName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs an Explorer registry write, then restarts Explorer so the change shows
        /// immediately. Inside a bulk run the restart coalesces to one via ExplorerRestart
        /// batching. (net8 logged via the shared Service; here via ToolService.Current.)
        /// </summary>
        private static void WriteExplorer(Action write)
        {
            try { write(); }
            catch (Exception ex) { ToolService.Current?.Log($"[EXPLORER] ERROR: {ex.Message}"); }
            if (!_suppressRestart) ExplorerRestart.Request();
        }
    }
}
