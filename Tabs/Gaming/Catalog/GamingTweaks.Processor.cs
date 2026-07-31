using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Processor(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-win32-priority",
                    Name             = "Win32 Priority Separation",
                    Description      = "Controls how much CPU time is given to foreground vs background processes",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("2A (hex) — recommended", 0, IsRecommended: true),
                        new TweakDropdownOption("26 (hex)",                1),
                        new TweakDropdownOption("28 (hex)",                2),
                        new TweakDropdownOption("16 (hex)",                3, IsDefault: true),
                        new TweakDropdownOption("06 (hex)",                4),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"System\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation");
                        if (!v.HasValue) return 3;
                        return v.Value switch
                        {
                            42 => 0,  // 0x2A
                            38 => 1,  // 0x26
                            40 => 2,  // 0x28
                            22 => 3,  // 0x16
                             6 => 4,  // 0x06
                             2 => 0,  // 0x02 alias
                             _ => 3,
                        };
                    },
                    ApplyIndex = idx =>
                    {
                        int[] values = { 42, 38, 40, 22, 6 };
                        string[] labels = { "2A (hex)", "26 (hex)", "28 (hex)", "16 (hex)", "06 (hex)" };
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl",
                            "Win32PrioritySeparation", values[idx], RegistryValueKind.DWord);
                        Log($"Win32PrioritySeparation set to {labels[idx]}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-system-responsiveness",
                    Name             = "System Responsiveness for Games",
                    Description      = "Minimize background task interference by allocating more CPU time to your active game or multimedia application",
                    // EnabledValue=[10], DisabledValue=[20], DefaultValue=20
                    RecommendedState = true,   // recommended=ON (value=10)
                    DefaultState     = false,  // default=OFF (value=20)
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            "SystemResponsiveness");
                        return v.HasValue ? v == 10 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            "SystemResponsiveness", on ? 10 : 20, RegistryValueKind.DWord);
                        Log($"System Responsiveness {(on ? "10 (game-optimized)" : "20 (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-cpu-priority",
                    Name             = "CPU Priority for Gaming",
                    Description      = "Give games higher CPU scheduling priority to dedicate more processor time to your game",
                    // EnabledValue=[6], DisabledValue=[2], DefaultValue=2
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Priority");
                        return v.HasValue ? v == 6 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Priority", on ? 6 : 2, RegistryValueKind.DWord);
                        Log($"CPU Priority {(on ? "6 (high)" : "2 (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-gpu-priority",
                    Name             = "GPU Priority for Gaming",
                    Description      = "Give games higher GPU scheduling priority to improve graphics performance and frame rates",
                    // EnabledValue=[8], DisabledValue=[2], DefaultValue=2
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "GPU Priority");
                        return v.HasValue ? v == 8 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "GPU Priority", on ? 8 : 2, RegistryValueKind.DWord);
                        Log($"GPU Priority {(on ? "8 (high)" : "2 (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-scheduling-category",
                    Name             = "High Scheduling Category for Gaming",
                    Description      = "Assign high-priority scheduling category to ensure games receive preferential system resource allocation",
                    // EnabledValue=["High"], DisabledValue=["Medium"], DefaultValue="Medium"
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Scheduling Category");
                        return v != null ? v == "High" : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Scheduling Category", on ? "High" : "Medium", RegistryValueKind.String);
                        Log($"Scheduling Category {(on ? "High" : "Medium (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-svchost-split-threshold",
                    Name             = "SvcHost Split Threshold",
                    Description      = "Set the memory threshold that determines when Windows splits services into separate svchost.exe processes. Select the value matching your system RAM",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Default (3670016 KB)", 3670016, IsRecommended: true, IsDefault: true),
                        new TweakDropdownOption("4 GB",   4194304),
                        new TweakDropdownOption("6 GB",   6291456),
                        new TweakDropdownOption("8 GB",   8388608),
                        new TweakDropdownOption("12 GB", 12582912),
                        new TweakDropdownOption("16 GB", 16777216),
                        new TweakDropdownOption("24 GB", 25165824),
                        new TweakDropdownOption("32 GB", 33554432),
                        new TweakDropdownOption("64 GB", 67108864),
                        new TweakDropdownOption("128 GB",134217728),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB");
                        int[] vals = { 3670016, 4194304, 6291456, 8388608, 12582912, 16777216, 25165824, 33554432, 67108864, 134217728 };
                        if (!v.HasValue) return 0;
                        int idx = Array.IndexOf(vals, (int)v.Value);
                        return idx >= 0 ? idx : 0;
                    },
                    ApplyIndex = idx =>
                    {
                        int[] vals = { 3670016, 4194304, 6291456, 8388608, 12582912, 16777216, 25165824, 33554432, 67108864, 134217728 };
                        if (idx < 0 || idx >= vals.Length) return;
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                            "SvcHostSplitThresholdInKB", vals[idx], RegistryValueKind.DWord);
                        Log($"SvcHost threshold set to {vals[idx]} KB. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-background-services",
                    Name             = "Optimize Background Services",
                    Description      = "Reduce the startup timeout for Windows services from 60 to 30 seconds. This can speed up boot time slightly",
                    // EnabledValue=[30000], DisabledValue=[60000], DefaultValue=60000
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control", "ServicesPipeTimeout");
                        return v.HasValue ? v == 30000 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                            "ServicesPipeTimeout", on ? 30000 : 60000, RegistryValueKind.DWord);
                        Log($"Background Services timeout {(on ? "30s (optimized)" : "60s (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-prefetch",
                    Name             = "Prefetch Feature",
                    Description      = "Preload frequently used applications and boot files into memory to speed up launches. Generally recommended for HDDs not SSDs",
                    IsPreference     = true,
                    // EnabledValue=[3], DisabledValue=[0], DefaultValue=3 → toggle ON = prefetch enabled
                    RecommendedState = false, // Winhance RecommendedValue=0 → recommended=OFF
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                            "EnablePrefetcher");
                        return v.HasValue ? v == 3 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                            "EnablePrefetcher", on ? 3 : 0, RegistryValueKind.DWord);
                        Log($"Prefetch {(on ? "enabled (3)" : "disabled (0)")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "processor-intel-tsx",
                    Name             = "Enable Intel TSX",
                    Description      = "Enable Intel Transactional Synchronization Extensions. Improves some multi-threaded workloads on supported Intel CPUs. Disabled by default as a security mitigation (TAA); no effect on AMD.",
                    RecommendedState = false,   // Windows/security default: TSX disabled
                    DefaultState     = false,
                    RequiresRestart  = true,
                    Warning          = "Intel TSX is disabled by default as a security mitigation (TAA/Zombieload-class). Enabling it trades security for a niche performance gain and only affects supported Intel CPUs. Continue?",
                    WarningState     = true,    // warn only when switching ON
                    ReadState        = () => SystemStateReader.ReadTsx() ?? false,
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
                        Registry.SetValue(path, "DisableTsx", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Intel TSX {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "processor-large-system-cache",
                    Name             = "Large System Cache",
                    Description      = "Prioritize the system file cache over application working set. Can help RAM-heavy file I/O workloads; may reduce memory available to games. Situational.",
                    IsPreference     = true,    // no objectively correct answer
                    DefaultState     = false,
                    RequiresRestart  = true,
                    ReadState        = () => SystemStateReader.ReadLargeSystemCache() ?? false,
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
                        Registry.SetValue(path, "LargeSystemCache", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"LargeSystemCache {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "processor-no-lazy-mode",
                    Name             = "MMCSS No Lazy Mode",
                    Description      = "Disable the multimedia scheduler's lazy mode for lower, more consistent audio latency and scheduler timing.",
                    RecommendedState = true,
                    DefaultState     = false,
                    RequiresRestart  = false,
                    ReadState        = () => SystemStateReader.ReadNoLazyMode() ?? false,
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                        Registry.SetValue(path, "NoLazyMode", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(path, "AlwaysOn",   on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"MMCSS NoLazyMode {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-mmcss-sfio-priority",
                    Name             = "High SFIO Priority for Gaming",
                    Description      = "Raise the scheduled I/O priority for games so file and storage requests from game processes are serviced ahead of background I/O",
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "SFIO Priority");
                        return v != null ? v == "High" : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "SFIO Priority", on ? "High" : "Normal", RegistryValueKind.String);
                        Log($"SFIO Priority {(on ? "High" : "Normal (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-mmcss-background-only",
                    Name             = "Disable Background-Only for Gaming",
                    Description      = "Mark the Games MMCSS task as foreground so game threads are not treated as background work and deprioritised by the scheduler",
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Background Only");
                        return v != null && v.Equals("False", StringComparison.OrdinalIgnoreCase);
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Background Only", on ? "False" : "True", RegistryValueKind.String);
                        Log($"Background Only set to {(on ? "False" : "True (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-mmcss-clock-rate",
                    Name             = "MMCSS Clock Rate",
                    Description      = "Multimedia scheduler clock granularity for the Games task. Lower values increase scheduler resolution at the cost of slightly higher CPU overhead",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Default (10000)", 10000, IsRecommended: true, IsDefault: true),
                        new TweakDropdownOption("5000",  5000),
                        new TweakDropdownOption("2500",  2500),
                        new TweakDropdownOption("1000",  1000),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Clock Rate");
                        int[] vals = { 10000, 5000, 2500, 1000 };
                        if (!v.HasValue) return 0;
                        int i = Array.IndexOf(vals, v.Value);
                        return i >= 0 ? i : 0;
                    },
                    ApplyIndex = idx =>
                    {
                        int[] vals = { 10000, 5000, 2500, 1000 };
                        int val = vals[Math.Clamp(idx, 0, vals.Length - 1)];
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            "Clock Rate", val, RegistryValueKind.DWord);
                        Log($"MMCSS Clock Rate set to {val}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-mmcss-pro-audio-profile",
                    Name             = "Optimize Pro Audio MMCSS Task",
                    Description      = "Raise the Pro Audio multimedia task priority to reduce audio latency and dropouts for DAWs, voice chat, and audio interfaces",
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var p = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio",
                            "Priority");
                        return p.HasValue && p.Value == 8;
                    },
                    Apply = on =>
                    {
                        const string pro = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio";
                        Registry.SetValue(pro, "Priority", on ? 8 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(pro, "Scheduling Category", "High", RegistryValueKind.String);
                        Log($"Pro Audio MMCSS task {(on ? "optimized" : "reset to defaults")}.");
                    }
                },
            };
    }
}
