using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Graphics(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-gpu-scheduling",
                    Name             = "Hardware-Accelerated GPU Scheduling (HAGS)",
                    Description      = "Let your GPU manage its own memory and scheduling for reduced latency and improved performance",
                    RequiresRestart  = true,
                    // EnabledValue=[2,null], DisabledValue=[1], DefaultValue=null → default=key absent=enabled
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"System\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
                        // EnabledValue=[2, null] → enabled if value is 2 OR absent
                        return v.HasValue ? v == 2 : true;
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.LocalMachine.OpenSubKey(
                                @"System\CurrentControlSet\Control\GraphicsDrivers", true);
                            k?.DeleteValue("HwSchMode", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers",
                                "HwSchMode", 1, RegistryValueKind.DWord);
                        Log($"HAGS {(on ? "enabled (default)" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-directx-flip-model",
                    Name             = "Optimizations for Windowed Games",
                    Description      = "Reduce latency and use advanced features in compatible games by using DirectX flip presentation model",
                    // CompositeStringKey="SwapEffectUpgradeEnable", EnabledValue=["1"], DisabledValue=["0"], DefaultValue="1"
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
                        if (raw == null) return true; // absent = default = enabled
                        return ParseCompositeString(raw).TryGetValue("SwapEffectUpgradeEnable", out var v) ? v == "1" : true;
                    },
                    Apply = on =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") ?? "";
                        raw = SetCompositeKey(raw, "SwapEffectUpgradeEnable", on ? "1" : "0");
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            "DirectXUserGlobalSettings", raw, RegistryValueKind.String);
                        Log($"DirectX Flip Model {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-directx-vrr-optimizations",
                    Name             = "Variable Refresh Rate (G-Sync/FreeSync)",
                    Description      = "Enable VRR optimizations for smoother gameplay. Requires a VRR-compatible monitor; has no effect if your monitor does not support VRR",
                    IsPreference     = true,
                    // CompositeStringKey="VRROptimizeEnable", EnabledValue=["1"], DisabledValue=["0"], DefaultValue="1"
                    // Winhance recommended=OFF (RecommendedValue="0")
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
                        if (raw == null) return true;
                        return ParseCompositeString(raw).TryGetValue("VRROptimizeEnable", out var v) ? v == "1" : true;
                    },
                    Apply = on =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") ?? "";
                        raw = SetCompositeKey(raw, "VRROptimizeEnable", on ? "1" : "0");
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            "DirectXUserGlobalSettings", raw, RegistryValueKind.String);
                        Log($"VRR Optimizations {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-directx-auto-hdr",
                    Name             = "Auto HDR",
                    Description      = "Automatically convert SDR content to HDR for enhanced colors and brightness. Requires an HDR-capable display with HDR enabled",
                    IsPreference     = true,
                    // CompositeStringKey="AutoHDREnable", EnabledValue=["1"], DisabledValue=["0"], DefaultValue="0"
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
                        if (raw == null) return false;
                        return ParseCompositeString(raw).TryGetValue("AutoHDREnable", out var v) ? v == "1" : false;
                    },
                    Apply = on =>
                    {
                        var raw = ReadString(RegistryHive.CurrentUser,
                            @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings") ?? "";
                        raw = SetCompositeKey(raw, "AutoHDREnable", on ? "1" : "0");
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            "DirectXUserGlobalSettings", raw, RegistryValueKind.String);
                        Log($"Auto HDR {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-nvidia-sharpening",
                    Name             = "Legacy NVIDIA Image Sharpening",
                    Description      = "Enable legacy NVIDIA image sharpening filter for enhanced visual clarity. Only works on older NVIDIA drivers; newer drivers should use NVIDIA Control Panel sharpening instead",
                    IsPreference     = true,
                    // EnabledValue=[0], DisabledValue=[1], DefaultValue=1 → toggle ON = sharpening enabled (EnableGR535=0)
                    RecommendedState = true,   // recommended=ON (EnableGR535=0)
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\NVIDIA Corporation\Global\FTS", "EnableGR535");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS",
                            "EnableGR535", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"NVIDIA Legacy Sharpening {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-fullscreen-optimizations",
                    Name             = "Fullscreen Optimizations",
                    Description      = "Allow Windows to optimize games running in fullscreen mode. Disabling can fix stuttering in some older games",
                    // EnabledValue=[0], DisabledValue=[2], DefaultValue=0 → toggle ON = FSO enabled (GameDVR_FSEBehaviorMode=0)
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"System\GameConfigStore", "GameDVR_FSEBehaviorMode");
                        return v.HasValue ? v == 0 : true; // absent = default = enabled
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore",
                            "GameDVR_FSEBehaviorMode", on ? 0 : 2, RegistryValueKind.DWord);
                        Log($"Fullscreen Optimizations {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-desktop-composition",
                    Name             = "Desktop Composition Effects",
                    Description      = "Enable visual effects managed by the Desktop Window Manager. Disabling may provide minor performance gains on older hardware but will break Aero effects",
                    // EnabledValue=[null], DisabledValue=[0], DefaultValue=null; RecommendedToggleState=true
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\DWM", "CompositionPolicy");
                        // EnabledValue=[null] → enabled when key is absent
                        return v.HasValue ? v != 0 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM", true);
                            k?.DeleteValue("CompositionPolicy", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                                "CompositionPolicy", 0, RegistryValueKind.DWord);
                        Log($"Desktop Composition {(on ? "enabled (default)" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-auto-color-management",
                    Name             = "Auto Color Management",
                    Description      = "Allow Windows to automatically manage color profiles for all connected displays that support it",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=0; ApplyPerMonitor=true
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        // Per-monitor: check if any monitor has it enabled
                        using var k = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore");
                        if (k == null) return false;
                        foreach (var sub in k.GetSubKeyNames())
                        {
                            using var m = k.OpenSubKey(sub);
                            if (m?.GetValue("AutoColorManagementEnabled") is int v && v == 1) return true;
                        }
                        return false;
                    },
                    Apply = on =>
                    {
                        using var k = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore");
                        if (k != null)
                        {
                            foreach (var sub in k.GetSubKeyNames())
                            {
                                using var m = Registry.LocalMachine.OpenSubKey(
                                    $@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore\{sub}", true);
                                m?.SetValue("AutoColorManagementEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                            }
                        }
                        Log($"Auto Color Management {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-disable-mpo",
                    Name             = "Multi-Plane Overlay (MPO)",
                    Description      = "Composite multiple display layers in hardware using the GPU. Disabling can fix screen flickering, black screens, and stuttering on multi-monitor setups",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[null], DisabledValue=[5], DefaultValue=null; RecommendedToggleState=true
                    RecommendedState = true,   // recommended=ON (key absent = MPO enabled)
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode");
                        return v.HasValue ? v != 5 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm", true);
                            k?.DeleteValue("OverlayTestMode", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                                "OverlayTestMode", 5, RegistryValueKind.DWord);
                        Log($"MPO {(on ? "enabled (default)" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-disable-all-overlays",
                    Name             = "Hardware Overlays",
                    Description      = "Allow the graphics driver to use hardware overlay surfaces. Disabling forces software composition and is known to break Steam, Discord, and RTSS in-game overlays",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[null], DisabledValue=[1], DefaultValue=null; RecommendedToggleState=true
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "DisableOverlays");
                        return v.HasValue ? v != 1 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.LocalMachine.OpenSubKey(
                                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true);
                            k?.DeleteValue("DisableOverlays", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                                "DisableOverlays", 1, RegistryValueKind.DWord);
                        Log($"Hardware Overlays {(on ? "enabled (default)" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-disable-mpo-min-fps",
                    Name             = "MPO Minimum Frame Rate Requirement",
                    Description      = "Allow DWM to dynamically switch apps between overlay modes based on frame rate. Disabling can fix stuttering in browsers and Discord without fully disabling MPO",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[null], DisabledValue=[0], DefaultValue=null; RecommendedToggleState=true
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayMinFPS");
                        return v.HasValue ? v != 0 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm", true);
                            k?.DeleteValue("OverlayMinFPS", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                                "OverlayMinFPS", 0, RegistryValueKind.DWord);
                        Log($"MPO Min FPS {(on ? "enabled (default)" : "disabled")}. Restart to apply.");
                    }
                },
            };
    }
}
