using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // CUSTOMIZE — APPEARANCE
        // ═════════════════════════════════════════════════════════════════════

        private const string PersonalizePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        // ═════════════════════════════════════════════════════════════════════
        // CUSTOMIZE — REGIONAL SETTINGS
        // ═════════════════════════════════════════════════════════════════════

        private const string InternationalPath = @"Control Panel\International";

        /// <summary>Raw REG_SZ under HKCU\Control Panel\International, or null when absent.</summary>
        public static string? ReadInternationalValue(string valueName) =>
            ReadHkcuString(InternationalPath, valueName);

        public static bool? ReadDarkModeApps()
        {
            var val = ReadHkcuDword(PersonalizePath, "AppsUseLightTheme");
            return val.HasValue ? val == 0 : null;
        }

        public static bool? ReadDarkModeSystem()
        {
            var val = ReadHkcuDword(PersonalizePath, "SystemUsesLightTheme");
            return val.HasValue ? val == 0 : null;
        }

        public static bool? ReadOledTaskbarTransparency()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "UseOLEDTaskbarTransparency");
            return val.HasValue ? val == 1 : null;
        }

        public static bool? ReadDwmForceEffectMode()
        {
            var val = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\Dwm", "ForceEffectMode");
            return val.HasValue ? val == 2 : null;
        }

        public static bool? ReadTitleBarAccentColor()
        {
            var val = ReadHkcuDword(@"Software\Microsoft\Windows\DWM", "ColorPrevalence");
            return val.HasValue ? val == 1 : null;
        }

        public static bool? ReadTaskbarAccentColor()
        {
            var val = ReadHkcuDword(PersonalizePath, "ColorPrevalence");
            return val.HasValue ? val == 1 : null;
        }

        public static bool? ReadSquareWindowCorners()
        {
            var val = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DWM", "DisableRoundedCorners");
            return val.HasValue ? val == 1 : null;
        }

        public static bool? ReadDragFullWindows()
        {
            var val = ReadHkcuString(@"Control Panel\Desktop", "DragFullWindows");
            return val is null ? null : val == "1";
        }

        public static bool? ReadMinAnimate()
        {
            var val = ReadHkcuString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate");
            return val is null ? null : val == "1";
        }

        // ═════════════════════════════════════════════════════════════════════
        // CUSTOMIZE — START MENU
        // ═════════════════════════════════════════════════════════════════════

        public static bool? ReadStartMorePins()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_Layout");
            return val.HasValue ? val == 1 : null;
        }

        public static bool? ReadStartRecommendationsHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations");
            return val.HasValue ? val == 0 : null;
        }

        public static bool? ReadBingSearchDisabled()
        {
            var policy = ReadHkcuDword(@"Software\Policies\Microsoft\Windows", "DisableSearchBoxSuggestions");
            if (policy == 1) return true;
            var bing = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled");
            return bing.HasValue ? bing == 0 : (policy.HasValue ? false : null);
        }

        public static bool? ReadAccountNotificationsDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_AccountNotifications");
            return val.HasValue ? val == 0 : null;
        }

        public static bool? ReadWebSuggestionsDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Bluetooth disabled when BTHPORT Start == 4</summary>
        public static bool? ReadBluetooth()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\BTHPORT", "Start");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>WiFi disabled when WlanSvc Start == 4</summary>
        public static bool? ReadWifi()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WlanSvc", "Start");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>VPN services disabled when RasMan Start == 4</summary>
        public static bool? ReadVpn()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\RasMan", "Start");
            return val.HasValue ? val == 4 : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PRIVACY TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Notifications disabled when WpnService Start == 4</summary>
        public static bool? ReadNotifications()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\WpnService", "Start");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>Action Center disabled when DisableNotificationCenter == 1</summary>
        public static bool? ReadActionCenter()
        {
            var val = ReadHkcuDword(
                @"Software\Policies\Microsoft\Windows\Explorer",
                "DisableNotificationCenter");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>NTFS encryption disabled when NtfsDisableEncryption == 1</summary>
        public static bool? ReadNtfsEncryption()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Policies",
                "NtfsDisableEncryption");
            return val.HasValue ? val == 1 : null;
        }


        /// <summary>Network optimization applied when NetworkThrottlingIndex == 0xffffffff</summary>
        public static bool? ReadNetworkOptimization()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "NetworkThrottlingIndex");
            return val.HasValue ? val == unchecked((int)0xffffffff) : null;
        }

                /// <summary>UTC clock enabled when RealTimeIsUniversal == 1</summary>
        public static bool? ReadUtc()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation",
                "RealTimeIsUniversal");
            return val.HasValue ? val == 1 : null;
        }


    }
}
