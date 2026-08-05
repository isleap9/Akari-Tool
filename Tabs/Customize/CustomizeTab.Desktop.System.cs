using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── DESKTOP ▸ STARTUP, DEVICES, LOCK SCREEN ──
        private void BuildDesktopSystem(StackPanel panel)
        {
            // ── Startup ───────────────────────────────────────────────────────
            var startupSection = TweakHelpers.BuildSection(panel, "Startup");

            TweakHelpers.AddTweakRow(startupSection, new TweakDefinition
            {
                Id          = "customize-desktop-show-auto-login-option",
                Name        = "Show Auto-Login Option",
                Description = "Re-enables the 'Users must enter a user name and password' checkbox in netplwiz",
                Group       = "Startup",
                // Not modelled by Winhance. Windows ships DevicePasswordLessBuildVersion=2
                // (checkbox hidden) → OFF is the factory state.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadAutoLoginOptionShown,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device";
                    Registry.SetValue(path, "DevicePasswordLessBuildVersion", enable ? 0 : 2, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Auto-login option {(enable ? "shown" : "hidden")}.");
                },
            });

            TweakHelpers.AddTweakRow(startupSection, new TweakDefinition
            {
                Id          = "customize-desktop-numlock-at-startup",
                Name        = "NumLock On at Startup",
                Description = "Enables NumLock automatically when Windows starts",
                Group       = "Startup",
                // Not modelled by Winhance. Windows ships InitialKeyboardIndicators=0 → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadNumLockAtStartup,
                Apply       = enable =>
                {
                    // InitialKeyboardIndicators: 0=off, 2=on
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard",
                        "InitialKeyboardIndicators", enable ? "2" : "0", RegistryValueKind.String);
                    Service?.Log($"[DESKTOP] NumLock at startup {(enable ? "on" : "off")}.");
                },
            });

            // "Verbose Boot/Shutdown Messages" lives in Taskbar ▸ Behavior (with
            // state restore) — duplicate row removed from here to avoid a second
            // toggle that always displayed Off.

            // ── Devices ───────────────────────────────────────────────────────
            var devicesSection = TweakHelpers.BuildSection(panel, "Devices");

            TweakHelpers.AddTweakRow(devicesSection, new TweakDefinition
            {
                Id          = "customize-desktop-dynamic-lighting",
                Name        = "Dynamic Lighting",
                Description = "Let Windows control RGB lighting on compatible devices (keyboards, mice, strips)",
                Group       = "Devices",
                // Not modelled by Winhance. Windows ships Dynamic Lighting on for
                // compatible devices → ON is the factory state.
                DefaultState = true,
                ReadState   = SystemStateReader.ReadDynamicLighting,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                        "AmbientLightingEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Dynamic Lighting {(enable ? "enabled" : "disabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(devicesSection, new TweakDefinition
            {
                Id          = "customize-desktop-foreground-lighting-control",
                Name        = "Foreground Apps Control Lighting",
                Description = "Let the app in the foreground take over RGB lighting from Windows",
                Group       = "Devices",
                // Not modelled by Winhance. Windows ships this on alongside Dynamic Lighting → ON.
                DefaultState = true,
                ReadState   = SystemStateReader.ReadForegroundLightingControl,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                        "ControlledByForegroundApp", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Foreground lighting control {(enable ? "enabled" : "disabled")}.");
                },
            });

            // ── Lock Screen ───────────────────────────────────────────────────
            var lockSection = TweakHelpers.BuildSection(panel, "Lock Screen");

            TweakHelpers.AddTweakRow(lockSection, new TweakDefinition
            {
                Id          = "customize-desktop-disable-spotlight",
                Name        = "Disable Windows Spotlight",
                Description = "Stops Windows from changing the lock screen image via Spotlight (online)",
                Group       = "Lock Screen",
                // Not modelled by Winhance as a lock-screen setting. Spotlight ships on → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadSpotlightDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
                    Registry.SetValue(path, "DisableWindowsSpotlightFeatures", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Windows Spotlight {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(lockSection, new TweakDefinition
            {
                Id          = "customize-desktop-disable-lock-screen",
                Name        = "Disable Lock Screen",
                Description = "Skips the lock screen and goes straight to the sign-in screen",
                Group       = "Lock Screen",
                // Not modelled by Winhance. The NoLockScreen policy is absent by default → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadLockScreenDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Personalization";
                    Registry.SetValue(path, "NoLockScreen", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Lock screen {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(lockSection, new TweakDefinition
            {
                Id          = "customize-desktop-disable-lock-screen-tips",
                Name        = "Disable Lock Screen Tips & Tricks",
                Description = "Removes fun facts, tips, and Spotlight info on the lock screen",
                Group       = "Lock Screen",
                // Not modelled by Winhance. Rotating lock screen content ships on → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadLockScreenTipsDisabled,
                Apply       = enable =>
                {
                    const string path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
                    Registry.SetValue(path, "RotatingLockScreenEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Registry.SetValue(path, "RotatingLockScreenOverlayEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[DESKTOP] Lock screen tips {(enable ? "disabled" : "enabled")}.");
                },
            });
        }
    }
}
