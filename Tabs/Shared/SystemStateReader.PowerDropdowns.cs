using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // POWER TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the active power plan's friendly name and GUID by reading
        /// HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes.
        /// </summary>
        public static (string? Name, string? Guid) ReadActivePowerPlan()
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                if (key is null) return (null, null);

                var guid = key.GetValue("ActivePowerScheme") as string;
                if (guid is null) return (null, null);

                using var planKey = key.OpenSubKey(guid);
                var name = planKey?.GetValue("FriendlyName") as string;

                // FriendlyName may be a MUI string like @%SystemRoot%\...,1234
                // If so, fall back to a known name map
                if (name is not null && name.StartsWith('@'))
                    name = KnownPlanName(guid);

                return (name ?? KnownPlanName(guid) ?? guid, guid);
            }
            catch { return (null, null); }
        }

        private static string? KnownPlanName(string guid) => guid.ToLower() switch
        {
            "381b4222-f694-41f0-9685-ff5bb260df2e" => "Balanced",
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "High Performance",
            "a1841308-3541-4fab-bc81-f71556f20b4a" => "Power Saver",
            "e9a42b02-d5df-448d-aa00-03f14749eb61" => "Ultimate Performance",
            _ => null
        };

        /// <summary>Core parking disabled when min cores % == 100</summary>
        public static bool? ReadCoreParking()
        {
            // Check the park min cores setting for the current scheme (AC)
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                "ValueIndex");
            return val.HasValue ? val == 100 : null;
        }

        /// <summary>CPU idle disabled when processor idle disable setting == 1</summary>
        public static bool? ReadCpuIdle()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\68f262a7-f621-4069-b9a5-4804249fa22a",
                "ValueIndex");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Throttle states disabled when PowerThrottlingOff == 1</summary>
        public static bool? ReadThrottleStates()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                "PowerThrottlingOff");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>USB selective suspend disabled when AC value index == 0</summary>
        public static bool? ReadUsbSelectiveSuspend()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
                "ValueIndex");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Hibernate disabled when HibernateEnabled == 0</summary>
        public static bool? ReadHibernate()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power",
                "HibernateEnabled");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>PCIe ASPM disabled when Link State Power Management == 0</summary>
        public static bool? ReadPcieAspm()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5",
                "ValueIndex");
            return val.HasValue ? val == 0 : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // DROPDOWNS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the current SvcHostSplitThresholdInKB value from registry.
        /// Callers map this to a dropdown index.
        /// </summary>
        public static int? ReadSvcHostThreshold()
        {
            return ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control",
                "SvcHostSplitThresholdInKB");
        }

        /// <summary>
        /// Returns the current Win32PrioritySeparation value from registry.
        /// </summary>
        public static int? ReadWin32Priority()
        {
            return ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation");
        }

    }
}
