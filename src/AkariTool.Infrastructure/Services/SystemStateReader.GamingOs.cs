using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // GAMING TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>NVIDIA preemption disabled when EnablePreemption == 0</summary>
        public static bool? ReadPreemption()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler",
                "EnablePreemption");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>HDCP disabled when RMHdcpKeyglobZero == 1</summary>
        public static bool? ReadHdcp()
        {
            // Enumerate the real adapter driver keys rather than guessing 0000/0001 —
            // the same enumeration the apply path writes to, so the read can never
            // disagree with what was written.
            foreach (var driver in AkariTool.Services.GpuTweaks.EnumerateAdapterDriverKeys())
            {
                var val = ReadDword(RegistryHive.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\Class\" + driver,
                    "RMHdcpKeyglobZero");
                if (val.HasValue) return val == 1;
            }

            // Fall back to the fixed indices when enumeration yields nothing (no WMI,
            // or a display adapter that is not on PCI).
            foreach (var idx in new[] { "0000", "0001" })
            {
                var val = ReadDword(RegistryHive.LocalMachine,
                    $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{idx}",
                    "RMHdcpKeyglobZero");
                if (val.HasValue) return val == 1;
            }
            return null;
        }

        /// <summary>FSO/Gamebar disabled when GameDVR_FSEBehaviorMode == 2</summary>
        public static bool? ReadFsoGamebar()
        {
            var val = ReadHkcuDword(@"System\GameConfigStore", "GameDVR_FSEBehaviorMode");
            return val.HasValue ? val == 2 : null;
        }

        /// <summary>System profile tweaks applied when GPU Priority == 8</summary>
        public static bool? ReadSystemProfile()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "GPU Priority");
            return val.HasValue ? val == 8 : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // OS TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Prefetch disabled when SysMain Start == 4</summary>
        public static bool? ReadPrefetch()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\SysMain", "Start");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>Print Spooler disabled when Spooler Start == 4</summary>
        public static bool? ReadPrintSpooler()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\Spooler", "Start");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>NVMe tweaks applied when IdlePowerMode == 0</summary>
        public static bool? ReadNvmeTweaks()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\ControlSet001\Services\stornvme\Parameters\Device",
                "IdlePowerMode");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Intel TSX enabled when DisableTsx == 0 (absent/1 = disabled)</summary>
        public static bool? ReadTsx()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\ControlSet001\Control\Session Manager\kernel", "DisableTsx");
            return val.HasValue ? val == 0 : null;   // null = absent = TSX disabled (default)
        }

        /// <summary>LargeSystemCache enabled when LargeSystemCache == 1</summary>
        public static bool? ReadLargeSystemCache()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>MMCSS NoLazyMode enabled when NoLazyMode == 1</summary>
        public static bool? ReadNoLazyMode()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NoLazyMode");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Animations disabled when TaskbarAnimations == 0</summary>
        public static bool? ReadAnimations()
        {
            var val = ReadHkcuDword(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "TaskbarAnimations");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Lock screen disabled when NoLockScreen == 1</summary>
        public static bool? ReadLockScreen()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows\Personalization",
                "NoLockScreen");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>MPO disabled when DisableOverlays == 1</summary>
        public static bool? ReadMpo()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                "DisableOverlays");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Wallpaper quality reduction disabled when JPEGImportQuality == 100</summary>
        public static bool? ReadWallpaperQuality()
        {
            var val = ReadHkcuDword(@"Control Panel\Desktop", "JPEGImportQuality");
            return val.HasValue ? val == 100 : null;
        }

        /// <summary>Transparency enabled when EnableTransparency == 1</summary>
        public static bool? ReadTransparency()
        {
            var val = ReadHkcuDword(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency");
            return val.HasValue ? val == 1 : null;
        }

        private static string? ReadHkcuString(string subKey, string valueName)
        {
            try
            {
                using var key = OpenRealHkcu(subKey);
                return key?.GetValue(valueName) as string;
            }
            catch { return null; }
        }

    }
}
