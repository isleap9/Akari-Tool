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

    private static IReadOnlyList<SettingGroup> BuildGraphics()
    {
        // TODO: Graphics — GamingTweaks.Graphics.cs
        return [];
    }

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

    private static IReadOnlyList<SettingGroup> BuildXbox()
    {
        // TODO: Xbox — GamingTweaks.Xbox.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildSecurity()
    {
        // TODO: Security — GamingTweaks.Security.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildSystemServices()
    {
        // TODO: System Services — GamingTweaks.SystemServices.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildScheduledTasks()
    {
        // TODO: Scheduled Tasks — GamingTweaks.ScheduledTasks.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildSystemRestore()
    {
        // TODO: System Restore — GamingTweaks.SystemRestore.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildAccessibility()
    {
        // TODO: Accessibility — GamingTweaks.Accessibility.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildVisualEffects()
    {
        // TODO: Visual Effects — GamingTweaks.VisualEffects.cs
        return [];
    }
}
