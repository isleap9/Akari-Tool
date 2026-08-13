using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Desktop.System.cs.
    // Sections "Startup", "Devices", "Lock Screen".
    public static partial class CustomizeTweaks
    {
        // ── Startup ────────────────────────────────────────────────────────────
        public static TweakDefinition[] DesktopStartup(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-desktop-show-auto-login-option",
                Name        = "Show Auto-Login Option",
                Description = "Re-enables the 'Users must enter a user name and password' checkbox in netplwiz",
                Group       = "Startup",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadAutoLoginOptionShown,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device";
                    Registry.SetValue(path, "DevicePasswordLessBuildVersion", enable ? 0 : 2, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Auto-login option {(enable ? "shown" : "hidden")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-desktop-numlock-at-startup",
                Name        = "NumLock On at Startup",
                Description = "Enables NumLock automatically when Windows starts",
                Group       = "Startup",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadNumLockAtStartup,
                Apply       = enable =>
                {
                    // InitialKeyboardIndicators: 0=off, 2=on
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard",
                        "InitialKeyboardIndicators", enable ? "2" : "0", RegistryValueKind.String);
                    Log($"[DESKTOP] NumLock at startup {(enable ? "on" : "off")}.");
                },
            },
        };

        // ── Devices ────────────────────────────────────────────────────────────
        public static TweakDefinition[] DesktopDevices(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-desktop-dynamic-lighting",
                Name        = "Dynamic Lighting",
                Description = "Let Windows control RGB lighting on compatible devices (keyboards, mice, strips)",
                Group       = "Devices",
                DefaultState = true,
                ReadState   = SystemStateReader.ReadDynamicLighting,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                        "AmbientLightingEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Dynamic Lighting {(enable ? "enabled" : "disabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-desktop-foreground-lighting-control",
                Name        = "Foreground Apps Control Lighting",
                Description = "Let the app in the foreground take over RGB lighting from Windows",
                Group       = "Devices",
                DefaultState = true,
                ReadState   = SystemStateReader.ReadForegroundLightingControl,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                        "ControlledByForegroundApp", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Foreground lighting control {(enable ? "enabled" : "disabled")}.");
                },
            },
        };

        // ── Lock Screen ──────────────────────────────────────────────────────────
        public static TweakDefinition[] DesktopLockScreen(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-desktop-disable-spotlight",
                Name        = "Disable Windows Spotlight",
                Description = "Stops Windows from changing the lock screen image via Spotlight (online)",
                Group       = "Lock Screen",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadSpotlightDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
                    Registry.SetValue(path, "DisableWindowsSpotlightFeatures", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Windows Spotlight {(enable ? "disabled" : "enabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-desktop-disable-lock-screen",
                Name        = "Disable Lock Screen",
                Description = "Skips the lock screen and goes straight to the sign-in screen",
                Group       = "Lock Screen",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadLockScreenDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Personalization";
                    Registry.SetValue(path, "NoLockScreen", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Lock screen {(enable ? "disabled" : "enabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-desktop-disable-lock-screen-tips",
                Name        = "Disable Lock Screen Tips & Tricks",
                Description = "Removes fun facts, tips, and Spotlight info on the lock screen",
                Group       = "Lock Screen",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadLockScreenTipsDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
                    Registry.SetValue(path, "RotatingLockScreenEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Registry.SetValue(path, "RotatingLockScreenOverlayEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[DESKTOP] Lock screen tips {(enable ? "disabled" : "enabled")}.");
                },
            },
        };
    }
}
