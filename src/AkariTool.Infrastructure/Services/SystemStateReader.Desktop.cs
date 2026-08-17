using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // DESKTOP TWEAKS
        //
        // The Desktop page had no state-restore pass before the TweakDefinition
        // migration — every row rendered Off regardless of the real system state.
        // These readers back the ReadState wiring that replaced it.
        // ═════════════════════════════════════════════════════════════════════

        private const string HideDesktopIcons =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";

        /// <summary>Desktop icon shown when its HideDesktopIcons value is 0 (absent = hidden for all but Recycle Bin)</summary>
        public static bool? ReadDesktopIconShown(string guid)
        {
            var val = ReadHkcuDword(HideDesktopIcons, guid);
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Shortcut arrow overlay removed when Shell Icons "29" points at a replacement icon</summary>
        public static bool? ReadShortcutArrowRemoved()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons");
                return key?.GetValue("29") is string s && s.Length > 0;
            }
            catch { return null; }
        }

        /// <summary>'- Shortcut' suffix removed when Explorer's "link" binary is all zero</summary>
        public static bool? ReadShortcutSuffixRemoved()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer");
                if (key?.GetValue("link") is not byte[] data || data.Length == 0) return null;
                foreach (var b in data) if (b != 0) return false;
                return true;
            }
            catch { return null; }
        }

        /// <summary>netplwiz auto-login checkbox shown when DevicePasswordLessBuildVersion == 0</summary>
        public static bool? ReadAutoLoginOptionShown()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device",
                "DevicePasswordLessBuildVersion");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>NumLock on at startup when InitialKeyboardIndicators is "2"</summary>
        public static bool? ReadNumLockAtStartup()
        {
            var val = ReadHkcuString(@"Control Panel\Keyboard", "InitialKeyboardIndicators");
            return val is null ? null : val.Trim() == "2";
        }

        /// <summary>Dynamic Lighting enabled when AmbientLightingEnabled == 1</summary>
        public static bool? ReadDynamicLighting()
        {
            var val = ReadHkcuDword(@"Software\Microsoft\Lighting", "AmbientLightingEnabled");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Foreground app controls lighting when ControlledByForegroundApp == 1</summary>
        public static bool? ReadForegroundLightingControl()
        {
            var val = ReadHkcuDword(@"Software\Microsoft\Lighting", "ControlledByForegroundApp");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Windows Spotlight disabled when DisableWindowsSpotlightFeatures == 1</summary>
        public static bool? ReadSpotlightDisabled()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableWindowsSpotlightFeatures");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Lock screen disabled when the NoLockScreen policy == 1</summary>
        public static bool? ReadLockScreenDisabled()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows\Personalization",
                "NoLockScreen");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Lock screen tips disabled when RotatingLockScreenEnabled == 0</summary>
        public static bool? ReadLockScreenTipsDisabled()
        {
            var val = ReadHkcuDword(
                @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "RotatingLockScreenEnabled");
            return val.HasValue ? val == 0 : null;
        }
    }
}
