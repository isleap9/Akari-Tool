using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Gaming;

public static class GamingOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildGameMode(),
        .. BuildProcessor(),
        .. BuildGraphics(),
        .. BuildStorage(),
        .. BuildNetwork(),
        .. BuildXbox(),
        .. BuildSecurity(),
        .. BuildSystemServices(),
        .. BuildScheduledTasks(),
        .. BuildSystemRestore(),
        .. BuildAccessibility(),
        .. BuildVisualEffects(),
    ];

    private static IReadOnlyList<SettingGroup> BuildGameMode() =>
    [
        new SettingGroup
        {
            Name = "Game Mode",
            FeatureId = "gaming-game-mode",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-game-mode",
                    Name = "Game Mode",
                    Description = "Optimize your PC for play by turning things off in the background",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "AutoGameModeEnabled",
                            RecommendedValue = 1,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-autostart-delay",
                    Name = "Startup Delay for Apps",
                    Description = "Delay startup applications by 10 seconds after boot to improve initial system responsiveness. Windows becomes usable faster, but your startup apps take longer to load",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                            ValueName = "StartupDelayInMSec",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 10000 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-storage-sense",
                    Name = "Storage Sense",
                    Description = "Automatically free up disk space by removing temporary files, emptying the recycle bin, and managing downloads",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                            ValueName = "AllowStorageSenseGlobal",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                            ValueName = "AllowStorageSenseGlobal",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-search",
                    Name = "Search Entire File System",
                    Description = "Search your entire file system instead of only indexed locations. This provides more complete results but is significantly slower than indexed search and increases disk activity",
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences",
                            ValueName = "WholeFileSystem",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-search-webview2",
                    Name = "WebView2 in Windows Search",
                    Description = "Allow Windows Search to use WebView2 (Edge) for rendering search results. Disabling removes Edge processes spawned by SearchHost.exe",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = restore default (delete all five override values); Disable = write override
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            ValueName = "EnabledState",
                            RecommendedValue = 1,
                            DefaultValue = 2,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            ValueName = "EnabledStateOptions",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            ValueName = "Variant",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            ValueName = "VariantPayload",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            ValueName = "VariantPayloadKind",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-wallpaper-compression",
                    Name = "Allow Desktop Wallpaper Compression",
                    Description = "Allow Windows to compress wallpapers to save disk space and improve performance. Only affects images in JPEG format",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete JPEGImportQuality (default = compression allowed); Disable = write 100
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "JPEGImportQuality",
                            RecommendedValue = 100,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 100 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-menu-show-delay",
                    Name = "Menu Show Delay",
                    Description = "Add a brief delay before displaying menus (400ms = Windows default), or show them instantly (0ms) for faster navigation",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "MenuShowDelay",
                            RecommendedValue = "0",
                            DefaultValue = "400",
                            EnabledValue = new object?[] { "400", null },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-explorer-alt-tab-filter",
                    Name = "Alt+Tab Filter",
                    Description = "Show only traditional open windows in Alt+Tab instead of including Microsoft Edge tabs and other Windows suggestions",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MultiTaskingAltTabFilter",
                            RecommendedValue = 3,
                            DefaultValue = 3,
                            EnabledValue = new object?[] { 3, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-mouse-hover-time",
                    Name = "Mouse Hover Time",
                    Description = "Controls how long you hover before tooltips and menus appear. Lower = faster response",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseHoverTime",
                            RecommendedValue = "1",
                            DefaultValue = "400",
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "1ms — Instant (Recommended)", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "1" } },
                            new ComboBoxOption { DisplayName = "100ms", ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "100" } },
                            new ComboBoxOption { DisplayName = "200ms", ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "200" } },
                            new ComboBoxOption { DisplayName = "400ms (Default)", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "400" } },
                            new ComboBoxOption { DisplayName = "600ms", ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "600" } },
                            new ComboBoxOption { DisplayName = "1000ms", ValueMappings = new Dictionary<string, object?> { ["MouseHoverTime"] = "1000" } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-background-apps",
                    Name = "Background App Permissions",
                    Description = "Control whether apps can run in the background. Force Deny blocks all background apps — avoid if you use Teams, Zoom, or WhatsApp",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsRunInBackground",
                            RecommendedValue = 2,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "User in Control (Default)", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["LetAppsRunInBackground"] = null } },
                            new ComboBoxOption { DisplayName = "Force Allow", ValueMappings = new Dictionary<string, object?> { ["LetAppsRunInBackground"] = 1 } },
                            new ComboBoxOption { DisplayName = "Force Deny (Recommended)", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["LetAppsRunInBackground"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-mouse-precision",
                    Name = "Enhance Pointer Precision",
                    Description = "Adjust cursor speed based on movement velocity (mouse acceleration). Most competitive gamers disable this for consistent aiming in FPS games",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseSpeed",
                            RecommendedValue = "0",
                            DefaultValue = "1",
                            EnabledValue = new object?[] { "1" },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseThreshold1",
                            RecommendedValue = "0",
                            DefaultValue = "6",
                            EnabledValue = new object?[] { "6" },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseThreshold2",
                            RecommendedValue = "0",
                            DefaultValue = "10",
                            EnabledValue = new object?[] { "10" },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildProcessor() =>
    [
        new SettingGroup
        {
            Name = "Processor",
            FeatureId = "gaming-processor",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-win32-priority",
                    Name = "Win32 Priority Separation",
                    Description = "Controls how much CPU time is given to foreground vs background processes",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl",
                            ValueName = "Win32PrioritySeparation",
                            RecommendedValue = 42,
                            DefaultValue = 22,
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "2A (hex) — recommended", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Win32PrioritySeparation"] = 42 } },
                            new ComboBoxOption { DisplayName = "26 (hex)", ValueMappings = new Dictionary<string, object?> { ["Win32PrioritySeparation"] = 38 } },
                            new ComboBoxOption { DisplayName = "28 (hex)", ValueMappings = new Dictionary<string, object?> { ["Win32PrioritySeparation"] = 40 } },
                            new ComboBoxOption { DisplayName = "16 (hex)", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Win32PrioritySeparation"] = 22 } },
                            new ComboBoxOption { DisplayName = "06 (hex)", ValueMappings = new Dictionary<string, object?> { ["Win32PrioritySeparation"] = 6 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-system-responsiveness",
                    Name = "System Responsiveness for Games",
                    Description = "Minimize background task interference by allocating more CPU time to your active game or multimedia application",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "SystemResponsiveness",
                            RecommendedValue = 10,
                            DefaultValue = 20,
                            EnabledValue = new object?[] { 10 },
                            DisabledValue = new object?[] { 20 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-cpu-priority",
                    Name = "CPU Priority for Gaming",
                    Description = "Give games higher CPU scheduling priority to dedicate more processor time to your game",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Priority",
                            RecommendedValue = 6,
                            DefaultValue = 2,
                            EnabledValue = new object?[] { 6 },
                            DisabledValue = new object?[] { 2 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-gpu-priority",
                    Name = "GPU Priority for Gaming",
                    Description = "Give games higher GPU scheduling priority to improve graphics performance and frame rates",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "GPU Priority",
                            RecommendedValue = 8,
                            DefaultValue = 2,
                            EnabledValue = new object?[] { 8 },
                            DisabledValue = new object?[] { 2 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-scheduling-category",
                    Name = "High Scheduling Category for Gaming",
                    Description = "Assign high-priority scheduling category to ensure games receive preferential system resource allocation",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Scheduling Category",
                            RecommendedValue = "High",
                            DefaultValue = "Medium",
                            EnabledValue = new object?[] { "High" },
                            DisabledValue = new object?[] { "Medium" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-svchost-split-threshold",
                    Name = "SvcHost Split Threshold",
                    Description = "Set the memory threshold that determines when Windows splits services into separate svchost.exe processes. Select the value matching your system RAM",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                            ValueName = "SvcHostSplitThresholdInKB",
                            RecommendedValue = 3670016,
                            DefaultValue = 3670016,
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Default (3670016 KB)", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 3670016 } },
                            new ComboBoxOption { DisplayName = "4 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 4194304 } },
                            new ComboBoxOption { DisplayName = "6 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 6291456 } },
                            new ComboBoxOption { DisplayName = "8 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 8388608 } },
                            new ComboBoxOption { DisplayName = "12 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 12582912 } },
                            new ComboBoxOption { DisplayName = "16 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 16777216 } },
                            new ComboBoxOption { DisplayName = "24 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 25165824 } },
                            new ComboBoxOption { DisplayName = "32 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 33554432 } },
                            new ComboBoxOption { DisplayName = "64 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 67108864 } },
                            new ComboBoxOption { DisplayName = "128 GB", ValueMappings = new Dictionary<string, object?> { ["SvcHostSplitThresholdInKB"] = 134217728 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-background-services",
                    Name = "Optimize Background Services",
                    Description = "Reduce the startup timeout for Windows services from 60 to 30 seconds. This can speed up boot time slightly",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                            ValueName = "ServicesPipeTimeout",
                            RecommendedValue = 30000,
                            DefaultValue = 60000,
                            EnabledValue = new object?[] { 30000 },
                            DisabledValue = new object?[] { 60000 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-prefetch",
                    Name = "Prefetch Feature",
                    Description = "Preload frequently used applications and boot files into memory to speed up launches. Generally recommended for HDDs not SSDs",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                            ValueName = "EnablePrefetcher",
                            RecommendedValue = 0,
                            DefaultValue = 3,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "processor-intel-tsx",
                    Name = "Enable Intel TSX",
                    Description = "Enable Intel Transactional Synchronization Extensions. Improves some multi-threaded workloads on supported Intel CPUs. Disabled by default as a security mitigation (TAA); no effect on AMD.",
                    RequiresRestart = true,
                    RequiresConfirmation = true,
                    EnableWarning = "Intel TSX is disabled by default as a security mitigation (TAA/Zombieload-class). Enabling it trades security for a niche performance gain and only affects supported Intel CPUs. Continue?",
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        // Inverted: toggle ON = TSX enabled = DisableTsx 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel",
                            ValueName = "DisableTsx",
                            RecommendedValue = 1,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "processor-large-system-cache",
                    Name = "Large System Cache",
                    Description = "Prioritize the system file cache over application working set. Can help RAM-heavy file I/O workloads; may reduce memory available to games. Situational.",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                            ValueName = "LargeSystemCache",
                            RecommendedValue = null,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "processor-no-lazy-mode",
                    Name = "MMCSS No Lazy Mode",
                    Description = "Disable the multimedia scheduler's lazy mode for lower, more consistent audio latency and scheduler timing.",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "NoLazyMode",
                            RecommendedValue = 1,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "AlwaysOn",
                            RecommendedValue = 1,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-mmcss-sfio-priority",
                    Name = "High SFIO Priority for Gaming",
                    Description = "Raise the scheduled I/O priority for games so file and storage requests from game processes are serviced ahead of background I/O",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "SFIO Priority",
                            RecommendedValue = "High",
                            DefaultValue = "Normal",
                            EnabledValue = new object?[] { "High" },
                            DisabledValue = new object?[] { "Normal" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-mmcss-background-only",
                    Name = "Disable Background-Only for Gaming",
                    Description = "Mark the Games MMCSS task as foreground so game threads are not treated as background work and deprioritised by the scheduler",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        // Inverted: toggle ON = "False" (foreground)
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Background Only",
                            RecommendedValue = "False",
                            DefaultValue = "True",
                            EnabledValue = new object?[] { "False" },
                            DisabledValue = new object?[] { "True" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-mmcss-clock-rate",
                    Name = "MMCSS Clock Rate",
                    Description = "Multimedia scheduler clock granularity for the Games task. Lower values increase scheduler resolution at the cost of slightly higher CPU overhead",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Clock Rate",
                            RecommendedValue = 10000,
                            DefaultValue = 10000,
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Default (10000)", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Clock Rate"] = 10000 } },
                            new ComboBoxOption { DisplayName = "5000", ValueMappings = new Dictionary<string, object?> { ["Clock Rate"] = 5000 } },
                            new ComboBoxOption { DisplayName = "2500", ValueMappings = new Dictionary<string, object?> { ["Clock Rate"] = 2500 } },
                            new ComboBoxOption { DisplayName = "1000", ValueMappings = new Dictionary<string, object?> { ["Clock Rate"] = 1000 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-mmcss-pro-audio-profile",
                    Name = "Optimize Pro Audio MMCSS Task",
                    Description = "Raise the Pro Audio multimedia task priority to reduce audio latency and dropouts for DAWs, voice chat, and audio interfaces",
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio",
                            ValueName = "Priority",
                            RecommendedValue = 8,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 8 },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        // Written to "High" on both enable and disable in source (unconditional)
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio",
                            ValueName = "Scheduling Category",
                            RecommendedValue = "High",
                            DefaultValue = "High",
                            EnabledValue = new object?[] { "High" },
                            DisabledValue = new object?[] { "High" },
                            ValueType = RegistryValueKind.String,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildGraphics() =>
    [
        new SettingGroup
        {
            Name = "Graphics",
            FeatureId = "gaming-graphics",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-gpu-scheduling",
                    Name = "Hardware-Accelerated GPU Scheduling (HAGS)",
                    Description = "Let your GPU manage its own memory and scheduling for reduced latency and improved performance",
                    RequiresRestart = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete HwSchMode (default = enabled); Disable = write 1
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers",
                            ValueName = "HwSchMode",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-flip-model",
                    Name = "Optimizations for Windowed Games",
                    Description = "Reduce latency and use advanced features in compatible games by using DirectX flip presentation model",
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            RecommendedValue = "1",
                            DefaultValue = "1",
                            EnabledValue = new object?[] { "1", null },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                            CompositeStringKey = "SwapEffectUpgradeEnable",
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-vrr-optimizations",
                    Name = "Variable Refresh Rate (G-Sync/FreeSync)",
                    Description = "Enable VRR optimizations for smoother gameplay. Requires a VRR-compatible monitor; has no effect if your monitor does not support VRR",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            RecommendedValue = "0",
                            DefaultValue = "1",
                            EnabledValue = new object?[] { "1", null },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                            CompositeStringKey = "VRROptimizeEnable",
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-auto-hdr",
                    Name = "Auto HDR",
                    Description = "Automatically convert SDR content to HDR for enhanced colors and brightness. Requires an HDR-capable display with HDR enabled",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            RecommendedValue = "0",
                            DefaultValue = "0",
                            EnabledValue = new object?[] { "1", null },
                            DisabledValue = new object?[] { "0" },
                            ValueType = RegistryValueKind.String,
                            CompositeStringKey = "AutoHDREnable",
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-nvidia-sharpening",
                    Name = "Legacy NVIDIA Image Sharpening",
                    Description = "Enable legacy NVIDIA image sharpening filter for enhanced visual clarity. Only works on older NVIDIA drivers; newer drivers should use NVIDIA Control Panel sharpening instead",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        // Inverted: EnableGR535 = 0 means sharpening enabled
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS",
                            ValueName = "EnableGR535",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-fullscreen-optimizations",
                    Name = "Fullscreen Optimizations",
                    Description = "Allow Windows to optimize games running in fullscreen mode. Disabling can fix stuttering in some older games",
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_FSEBehaviorMode",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 2 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-desktop-composition",
                    Name = "Desktop Composition Effects",
                    Description = "Enable visual effects managed by the Desktop Window Manager. Disabling may provide minor performance gains on older hardware but will break Aero effects",
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete CompositionPolicy (default = enabled); Disable = write 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "CompositionPolicy",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-auto-color-management",
                    Name = "Auto Color Management",
                    Description = "Allow Windows to automatically manage color profiles for all connected displays that support it",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore",
                            ValueName = "AutoColorManagementEnabled",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            ApplyPerMonitor = true,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-disable-mpo",
                    Name = "Multi-Plane Overlay (MPO)",
                    Description = "Composite multiple display layers in hardware using the GPU. Disabling can fix screen flickering, black screens, and stuttering on multi-monitor setups",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete OverlayTestMode (default = enabled); Disable = write 5
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                            ValueName = "OverlayTestMode",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 5 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-disable-all-overlays",
                    Name = "Hardware Overlays",
                    Description = "Allow the graphics driver to use hardware overlay surfaces. Disabling forces software composition and is known to break Steam, Discord, and RTSS in-game overlays",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete DisableOverlays (default = enabled); Disable = write 1
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                            ValueName = "DisableOverlays",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-disable-mpo-min-fps",
                    Name = "MPO Minimum Frame Rate Requirement",
                    Description = "Allow DWM to dynamically switch apps between overlay modes based on frame rate. Disabling can fix stuttering in browsers and Discord without fully disabling MPO",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete OverlayMinFPS (default = enabled); Disable = write 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                            ValueName = "OverlayMinFPS",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildStorage() =>
    [
        new SettingGroup
        {
            Name = "Storage",
            FeatureId = "gaming-storage",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "storage-nvme-tweaks",
                    Name = "NVMe Latency Tweaks",
                    Description = "Disable NVMe idle power states and diagnostic logging to reduce SSD access latency. Recommended for desktops on AC power; may increase idle power draw on laptops.",
                    RequiresRestart = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings =
                    [
                        // Enable = write values; Disable = delete all four (delete-on-disable)
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device",
                            ValueName = "ContiguousMemoryFromAnyNode",
                            RecommendedValue = 1,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { null },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device",
                            ValueName = "LogSize",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device",
                            ValueName = "IdlePowerMode",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device",
                            ValueName = "DiagnosticFlags",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildNetwork() =>
    [
        new SettingGroup
        {
            Name = "Network",
            FeatureId = "gaming-network",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-network-throttling",
                    Name = "Network Throttling",
                    Description = "Controls network packet rate limiting for multimedia applications. Keeping throttling enabled (default: 10 packets/ms) provides better DPC latency for gaming",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Disabled writes 0xFFFFFFFF (-1 as DWORD)
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "NetworkThrottlingIndex",
                            RecommendedValue = 10,
                            DefaultValue = 10,
                            EnabledValue = new object?[] { 10, null },
                            DisabledValue = new object?[] { unchecked((int)0xFFFFFFFF) },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                // [DEFERRED: gaming-nagle-algorithm — per-network-interface iteration over
                //  SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces subkeys,
                //  writing/deleting TcpAckFrequency + TCPNoDelay per adapter]
                // [DEFERRED: gaming-dns-server — netsh dns encryption table + PowerShell
                //  Set-DnsClientServerAddress per adapter with DoH templates; not a plain
                //  registry value write]
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildXbox() =>
    [
        new SettingGroup
        {
            Name = "Xbox",
            FeatureId = "gaming-xbox",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-xbox-game-dvr",
                    Name = "Xbox Game DVR",
                    Description = "Record gameplay clips and take screenshots using the Xbox Game Bar overlay. Disabling reduces CPU/GPU usage and can improve frame rates",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_Enabled",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                            ValueName = "AppCaptureEnabled",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR",
                            ValueName = "AllowGameDVR",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-game-bar-controller",
                    Name = "Game Bar Controller Access",
                    Description = "Allow your Xbox/compatible controller to open Game Bar by pressing the Xbox button. Disable to prevent accidental Game Bar activation during gaming",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete UseNexusForGameBarEnabled (default = enabled); Disable = write 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "UseNexusForGameBarEnabled",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-game-bar-tips",
                    Name = "Game Bar Tips and Hints",
                    Description = "Show tips and hints about Game Bar features when opening the overlay. Disabling reduces distractions during gameplay",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete ShowStartupPanel (default = enabled); Disable = write 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "ShowStartupPanel",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSecurity() =>
    [
        new SettingGroup
        {
            Name = "Security",
            FeatureId = "gaming-security",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-virtualization-based-security",
                    Name = "Virtualization Based Security (VBS)",
                    Description = "Isolates parts of memory to protect the system from vulnerabilities. Disabling can improve gaming performance but reduces system security",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard",
                            ValueName = "EnableVirtualizationBasedSecurity",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard",
                            ValueName = "RequirePlatformSecurityFeatures",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-memory-integrity",
                    Name = "Memory Integrity (HVCI)",
                    Description = "Prevents malicious code from being inserted into high-security processes. Disabling can improve gaming performance but reduces system security",
                    IsSubjectivePreference = true,
                    RequiresRestart = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                            ValueName = "WasEnabledBy",
                            RecommendedValue = 0,
                            DefaultValue = 2,
                            EnabledValue = new object?[] { 2 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                // [DEFERRED: gaming-disable-defender — Apply calls DefenderService.SetAsync
                //  (reboot-based servicing-package removal), not a registry value write; also
                //  covered by the Defender do-not-touch rule in CLAUDE.md]
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSystemServices() =>
    [
        new SettingGroup
        {
            Name = "System Services",
            FeatureId = "gaming-system-services",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-sysmain-service",
                    Name = "SysMain Service (Superfetch)",
                    Description = "Preload frequently used applications into RAM for faster launch times. Automatic is recommended for HDD or mixed-storage systems; Manual or Disabled for SSD-only systems",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", ValueName = "Start", RecommendedValue = 4, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, Warning = "Disabling SysMain on systems with a traditional hard drive (HDD) can noticeably reduce responsiveness and slow app launches. Recommended only for SSD-only systems.", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-windows-search-service",
                    Name = "Windows Search Indexing Service",
                    Description = "Indexes files and folders for faster search results. Disabling reduces background CPU and disk activity but breaks Outlook search and makes Start Menu and File Explorer search slow or unreliable",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", Warning = "Disabling Windows Search stops file content indexing. Outlook search, Start Menu search, and File Explorer search will become slow or return no results until re-enabled.", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-print-spooler-service",
                    Name = "Print Spooler Service",
                    Description = "Manages print jobs sent to printers. If you don't use a printer, set to Manual or Disabled to free up system resources",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-telemetry-service",
                    Name = "Connected User Experiences and Telemetry",
                    Description = "Sends usage data and diagnostics to Microsoft. Setting to Manual or Disabled reduces background network and CPU usage",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-error-reporting-service",
                    Name = "Windows Error Reporting Service",
                    Description = "Collects and sends crash data to Microsoft. Disabling prevents crash reporting and reduces network traffic",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-geolocation-service",
                    Name = "Geolocation Service",
                    Description = "Tracks your physical location for apps and services. Disabling improves privacy and prevents location tracking",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-retail-demo-service",
                    Name = "Retail Demo Service",
                    Description = "Controls device activity when in retail demo mode. Safe to disable for personal computers",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RetailDemo", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-insider-service",
                    Name = "Windows Insider Service",
                    Description = "Manages Windows Insider Program features and preview builds. Safe to disable if you're not in the Insider Program",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\wisvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-phone-service",
                    Name = "Phone Service",
                    Description = "Manages telephony state on the device. Safe to disable if you don't use phone connectivity features",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PhoneSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-wallet-service",
                    Name = "Wallet Service",
                    Description = "Provides wallet functionality for payment and NFC scenarios. Safe to disable if you don't use Microsoft Wallet",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WalletService", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-maps-broker-service",
                    Name = "Downloaded Maps Manager",
                    Description = "Provides access to downloaded maps for applications. Set to Manual to allow map access when needed",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-fax-service",
                    Name = "Fax Service",
                    Description = "Enables sending and receiving faxes. Safe to disable for most users as fax functionality is rarely used",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax", ValueName = "Start", RecommendedValue = 4, DefaultValue = 4, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-wmp-network-service",
                    Name = "Windows Media Player Network Sharing",
                    Description = "Shares Windows Media Player libraries to other networked players and media devices",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WMPNetworkSvc", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-mixed-reality-service",
                    Name = "Windows Mixed Reality OpenXR Service",
                    Description = "Runs OpenXR applications on Windows Mixed Reality devices. Safe to disable if you don't use VR or AR headsets",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MixedRealityOpenXRSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 4, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-mobile-hotspot-service",
                    Name = "Windows Mobile Hotspot Service",
                    Description = "Provides ability to share internet connection with other devices",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\icssvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sms-router-service",
                    Name = "SMS Router Service",
                    Description = "Routes SMS messages according to rules. Safe to disable if you don't use SMS features on your PC",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SmsRouter", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-parental-controls-service",
                    Name = "Parental Controls Service",
                    Description = "Enables parental controls and family safety features. Safe to disable if you don't use parental controls",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WpcMonSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-payments-nfc-service",
                    Name = "Payments and NFC/SE Manager",
                    Description = "Manages payments and Near Field Communication secure elements. Safe to disable if you don't use NFC payments",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SEMgrSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-biometric-service",
                    Name = "Windows Biometric Service",
                    Description = "Enables fingerprint and facial recognition login via Windows Hello. Safe to disable on desktop systems without biometric hardware",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WbioSrvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-access-manager",
                    Name = "Remote Access Connection Manager",
                    Description = "Manages VPN and dial-up connections. Set to Manual to reduce background activity while keeping VPN available",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasMan", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-access-auto",
                    Name = "Remote Access Auto Connection Manager",
                    Description = "Automatically connects to remote networks when programs reference remote resources",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasAuto", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-services",
                    Name = "Remote Desktop Services",
                    Description = "Allows users to connect interactively to a remote computer",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TermService", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-configuration",
                    Name = "Remote Desktop Configuration",
                    Description = "Manages Remote Desktop Services and Remote Desktop related configurations",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SessionEnv", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-compatibility-assistant-service",
                    Name = "Program Compatibility Assistant Service",
                    Description = "Monitors programs for compatibility issues and suggests fixes. Disabling prevents compatibility prompts",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-ai-fabric-service",
                    Name = "Windows AI Fabric Service",
                    Description = "Windows AI Fabric Service (WSAIFabricSvc) manages AI workloads. Disable if you don't use Windows AI features",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSAIFabricSvc", ValueName = "Start", RecommendedValue = 4, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sensor-monitoring-service",
                    Name = "Sensor Monitoring Service",
                    Description = "Monitors various sensors like ambient light and orientation. Safe to disable on desktop systems without sensor hardware",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensrSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sensor-data-service",
                    Name = "Sensor Data Service",
                    Description = "Delivers data from a variety of sensors to applications. Safe to disable on desktop systems without sensor hardware",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensorDataService", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-telephony-service",
                    Name = "Telephony Service",
                    Description = "Manages telephony (TAPI) for Phone Link audio relay, modems, fax, and VoIP softphones. Leave at Manual unless you use no telephony software",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TapiSrv", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", Warning = "Disabling Telephony breaks Phone Link audio relay, fax software, dial-up modems, and VoIP softphones (e.g. 3CX, Cisco Jabber).", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-connected-devices-platform-service",
                    Name = "Connected Devices Platform Service",
                    Description = "Enables cross-device experiences like phone linking and nearby sharing. Note: can break Windows Night Light. Use Automatic if you use Night Light.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\CDPSvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 2, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", Warning = "Disabling the Connected Devices Platform can break Windows Night Light and cross-device features (Phone Link, Nearby Sharing, clipboard sync). Manual keeps these working — it effectively auto-starts with your session.", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-smart-card-services",
                    Name = "Smart Card Services",
                    Description = "Enables smart card reader functionality. Safe to disable if you don't use physical smart cards.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCardSvr", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-spot-verifier-service",
                    Name = "Spot Verifier Service",
                    Description = "Verifies potential file system corruptions. Set to Manual to allow verification when needed.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\svsvc", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-port-redirector",
                    Name = "Remote Desktop Services UserMode Port Redirector",
                    Description = "Allows local device redirection for Remote Desktop connections. Safe to disable if you don't use Remote Desktop.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\UmRdpService", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-touch-keyboard-service",
                    Name = "Touch Keyboard and Handwriting Panel Service",
                    Description = "Manages Windows touch keyboard, pen/stylus, and handwriting panel. Safe to disable on desktop systems without touch input.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TabletInputService", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-input-app-preload",
                    Name = "Input App Preload",
                    Description = "Preload the Windows Input Experience (touch keyboard, emoji panel) at sign-in. Disable alongside the Touch Keyboard service to stop it running in the background",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\input",
                            ValueName = "IsInputAppPreloadEnabled",
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-auth-manager",
                    Name = "Xbox Live Auth Manager",
                    Description = "Provides authentication for Xbox Live. Safe to disable if you don't use Xbox Game Pass or Microsoft Store games.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, Warning = "Disabling will prevent Xbox Game Pass and Microsoft Store games from signing in or launching.", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-game-save",
                    Name = "Xbox Live Game Save",
                    Description = "Syncs game saves to Xbox Live cloud. Only needed for Xbox Game Pass and Microsoft Store games with cloud saves.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-networking",
                    Name = "Xbox Live Networking Service",
                    Description = "Supports Xbox Live multiplayer networking. Not needed for Steam or Epic games.",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc", ValueName = "Start", RecommendedValue = 4, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-midi-service",
                    Name = "Windows MIDI Service",
                    Description = "Routes MIDI data for connected musical instruments and audio interfaces. Safe to disable if you don't use MIDI hardware; set to Manual to allow it to start on demand",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings =
                    [
                        new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\midisrv", ValueName = "Start", RecommendedValue = 3, DefaultValue = 3, EnabledValue = null, DisabledValue = null, ValueType = RegistryValueKind.DWord, IsPrimary = true },
                    ],
                    ComboBox = new ComboBoxMetadata
                    {
                        Options =
                        [
                            new ComboBoxOption { DisplayName = "Disabled", ValueMappings = new Dictionary<string, object?> { ["Start"] = 4 } },
                            new ComboBoxOption { DisplayName = "Manual", IsRecommended = true, IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["Start"] = 3 } },
                            new ComboBoxOption { DisplayName = "Automatic", ValueMappings = new Dictionary<string, object?> { ["Start"] = 2 } },
                        ],
                    },
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildScheduledTasks() =>
    [
        new SettingGroup
        {
            Name = "Scheduled Tasks",
            FeatureId = "gaming-scheduled-tasks",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-task-compatibility-appraiser",
                    Name = "Microsoft Compatibility Appraiser",
                    Description = "Collects program compatibility telemetry for Windows upgrades. Disable to reduce telemetry",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-compatibility-appraiser", TaskPath = @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-program-data-updater",
                    Name = "Program Data Updater",
                    Description = "Updates the program compatibility database with information about installed applications",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-program-data-updater", TaskPath = @"\Microsoft\Windows\Application Experience\ProgramDataUpdater", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-ceip-consolidator",
                    Name = "CEIP Consolidator",
                    Description = "Consolidates and uploads usage data as part of the Customer Experience Improvement Program",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-ceip-consolidator", TaskPath = @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-usb-ceip",
                    Name = "USB CEIP",
                    Description = "Collects USB device-related telemetry for the Customer Experience Improvement Program",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-usb-ceip", TaskPath = @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-disk-diagnostic",
                    Name = "Disk Diagnostic Data Collector",
                    Description = "Collects disk diagnostic information and S.M.A.R.T. data for Microsoft",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-disk-diagnostic", TaskPath = @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-feedback-dmclient",
                    Name = "Feedback DmClient",
                    Description = "Collects feedback and diagnostic data for Microsoft",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-feedback-dmclient", TaskPath = @"\Microsoft\Windows\Feedback\Siuf\DmClient", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-feedback-dmclient-download",
                    Name = "Feedback DmClient Scenario Download",
                    Description = "Downloads feedback scenarios and configuration data from Microsoft",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-feedback-dmclient-download", TaskPath = @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-error-reporting-queue",
                    Name = "Windows Error Reporting Queue",
                    Description = "Queues crash reports and error data to send to Microsoft",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-error-reporting-queue", TaskPath = @"\Microsoft\Windows\Windows Error Reporting\QueueReporting", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-sqm",
                    Name = "Software Quality Metrics",
                    Description = "Collects software quality metrics and reliability data for Microsoft telemetry",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-sqm", TaskPath = @"\Microsoft\Windows\PI\Sqm-Tasks", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-mare-backup",
                    Name = "MAR Backup",
                    Description = "Backs up Microsoft Assisted Recovery data. Disable to reduce background system activity",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-mare-backup", TaskPath = @"\Microsoft\Windows\Application Experience\MareBackup", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-startup-app",
                    Name = "Startup App Task",
                    Description = "Tracks and monitors startup applications for telemetry and diagnostics",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-startup-app", TaskPath = @"\Microsoft\Windows\Application Experience\StartupAppTask", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-maps-update",
                    Name = "Maps Update",
                    Description = "Updates offline maps data for the Windows Maps app. Disable if you don't use the Maps app",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-maps-update", TaskPath = @"\Microsoft\Windows\Maps\MapsUpdateTask", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-autochk-proxy",
                    Name = "AutoChk Proxy",
                    Description = "Performs disk checking operations and collects diagnostic data",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-autochk-proxy", TaskPath = @"\Microsoft\Windows\Autochk\Proxy", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-power-efficiency",
                    Name = "Power Efficiency Diagnostics",
                    Description = "Analyzes system power consumption and collects energy efficiency data",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-power-efficiency", TaskPath = @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-windows-ai-recall-config",
                    Name = "Windows AI Recall Configuration",
                    Description = "Windows AI Recall configuration task. Disable to prevent Recall from being configured in the background",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-windows-ai-recall-config", TaskPath = @"\Microsoft\Windows\WindowsAI\RecallConfiguration", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-windows-ai-recall-pipeline",
                    Name = "Windows AI Recall Pipeline",
                    Description = "Windows AI Recall pipeline task. Disable to prevent Recall snapshot pipeline from running in the background",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-windows-ai-recall-pipeline", TaskPath = @"\Microsoft\Windows\WindowsAI\RecallPipeline", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-office-actions-server",
                    Name = "Office Actions Server",
                    Description = "Office AI Actions Server scheduled task. Disable to prevent Office AI from running in the background",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-office-actions-server", TaskPath = @"\Microsoft\Office\Office Actions Server", RecommendedState = false, DefaultState = true },
                    ],
                },
                new SettingDefinition
                {
                    Id = "gaming-task-family-safety",
                    Name = "Family Safety Monitor Task",
                    Description = "Monitors family safety settings and usage. Disable if you don't use family safety features",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = [],
                    ScheduledTaskSettings =
                    [
                        new ScheduledTaskSetting { Id = "gaming-task-family-safety", TaskPath = @"\Microsoft\Windows\Shell\FamilySafetyMonitor", RecommendedState = false, DefaultState = true },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSystemRestore()
    {
        // TODO: System Restore — GamingTweaks.SystemRestore.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildAccessibility() =>
    [
        new SettingGroup
        {
            Name = "Accessibility",
            FeatureId = "gaming-accessibility",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "gaming-narrator-hotkey",
                    Name = "Narrator Win+Ctrl+Enter Hotkey",
                    Description = "Enable the Win+Ctrl+Enter keyboard shortcut to quickly launch Windows Narrator screen reader",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        // Enable = delete WinEnterLaunchEnabled (default = enabled); Disable = write 0
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "WinEnterLaunchEnabled",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "accessibility-stickykeys-hotkey",
                    Name = "StickyKeys Hotkey (Shift×5)",
                    Description = "Enable the keyboard shortcut to activate StickyKeys by pressing the Shift key five times",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",
                            ValueName = "Flags",
                            RecommendedValue = "2",
                            DefaultValue = "510",
                            EnabledValue = new object?[] { "510", null },
                            DisabledValue = new object?[] { "2" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "accessibility-filterkeys-hotkey",
                    Name = "FilterKeys Hotkey (Right Shift 8s)",
                    Description = "Enable the keyboard shortcut to activate FilterKeys by holding the right Shift key for 8 seconds",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response",
                            ValueName = "Flags",
                            RecommendedValue = "2",
                            DefaultValue = "126",
                            EnabledValue = new object?[] { "126", null },
                            DisabledValue = new object?[] { "2" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "accessibility-togglekeys-hotkey",
                    Name = "ToggleKeys Hotkey (Num Lock 5s)",
                    Description = "Enable the keyboard shortcut to activate ToggleKeys by holding Num Lock for 5 seconds",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys",
                            ValueName = "Flags",
                            RecommendedValue = "34",
                            DefaultValue = "62",
                            EnabledValue = new object?[] { "62", null },
                            DisabledValue = new object?[] { "34" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "accessibility-highcontrast-hotkey",
                    Name = "High Contrast Hotkey (Alt+Shift+PrtScn)",
                    Description = "Enable the keyboard shortcut to activate High Contrast mode by pressing Left Alt + Left Shift + Print Screen",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                            ValueName = "Flags",
                            RecommendedValue = "4194",
                            DefaultValue = "126",
                            EnabledValue = new object?[] { "126", null },
                            DisabledValue = new object?[] { "4194" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "accessibility-mousekeys-hotkey",
                    Name = "MouseKeys Hotkey (Alt+Shift+NumLock)",
                    Description = "Enable the keyboard shortcut to activate MouseKeys, which lets the numeric keypad control the mouse pointer",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys",
                            ValueName = "Flags",
                            RecommendedValue = "130",
                            DefaultValue = "126",
                            EnabledValue = new object?[] { "126", null },
                            DisabledValue = new object?[] { "130" },
                            ValueType = RegistryValueKind.String,
                            IsPrimary = true,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildVisualEffects()
    {
        // TODO: Visual Effects — GamingTweaks.VisualEffects.cs
        return [];
    }
}
