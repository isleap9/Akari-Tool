using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] GameMode(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-game-mode",
                    Name             = "Game Mode",
                    Description      = "Optimize your PC for play by turning things off in the background",
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=null → default=key absent=OFF
                    // Winhance: RecommendedValue=1, DefaultValue=null → recommended=ON, default=OFF
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
                        // EnabledValue=[1, null] → enabled if value is 1 OR key is absent
                        return v.HasValue ? v == 1 : true; // null = key absent = enabled
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            "AutoGameModeEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Game Mode {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-autostart-delay",
                    Name             = "Startup Delay for Apps",
                    Description      = "Delay startup applications by 10 seconds after boot to improve initial system responsiveness. Windows becomes usable faster, but your startup apps take longer to load",
                    IsPreference     = true,
                    // EnabledValue=[10000], DisabledValue=[0], DefaultValue=0 → enabled=ON means delay IS active
                    RecommendedState = false,  // recommended = OFF (no delay)
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                            "StartupDelayInMSec");
                        return v.HasValue ? v == 10000 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                            "StartupDelayInMSec", on ? 10000 : 0, RegistryValueKind.DWord);
                        Log($"Startup Delay {(on ? "enabled (10s)" : "disabled (0s)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-storage-sense",
                    Name             = "Storage Sense",
                    Description      = "Automatically free up disk space by removing temporary files, emptying the recycle bin, and managing downloads",
                    IsPreference     = true,
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=null → enabled=key absent
                    RecommendedState = false,  // recommended=OFF (disable storage sense)
                    DefaultState     = true,   // default = key absent = enabled
                    ReadState        = () =>
                    {
                        // Check HKCU policy first, then HKLM
                        var hkcu = ReadDword(RegistryHive.CurrentUser,
                            @"SOFTWARE\Policies\Microsoft\Windows\StorageSense", "AllowStorageSenseGlobal");
                        if (hkcu.HasValue) return hkcu == 1;
                        var hklm = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Policies\Microsoft\Windows\StorageSense", "AllowStorageSenseGlobal");
                        return hklm.HasValue ? hklm == 1 : true; // null = enabled (default)
                    },
                    Apply = on =>
                    {
                        foreach (var hive in new[] { @"HKEY_CURRENT_USER", @"HKEY_LOCAL_MACHINE" })
                            Registry.SetValue($@"{hive}\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                                "AllowStorageSenseGlobal", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Storage Sense {(on ? "enabled" : "disabled via policy")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-explorer-search",
                    Name             = "Search Entire File System",
                    Description      = "Search your entire file system instead of only indexed locations. This provides more complete results but is significantly slower than indexed search and increases disk activity",
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=0
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences",
                            "WholeFileSystem");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences",
                            "WholeFileSystem", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Search Entire File System {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-search-webview2",
                    Name             = "WebView2 in Windows Search",
                    Description      = "Allow Windows Search to use WebView2 (Edge) for rendering search results. Disabling removes Edge processes spawned by SearchHost.exe",
                    // EnabledValue=[2], DisabledValue=[1], DefaultValue=2 → enabled=ON when EnabledState=2
                    RecommendedState = false,  // recommended = OFF (disable webview2)
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260",
                            "EnabledState");
                        return v.HasValue ? v == 2 : true; // null/absent = default = enabled
                    },
                    Apply = on =>
                    {
                        const string key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260";
                        if (on)
                        {
                            // Restore defaults: remove the override values
                            using var k = Registry.LocalMachine.OpenSubKey(
                                @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260", true);
                            if (k != null)
                            {
                                foreach (var v in new[] { "EnabledState", "EnabledStateOptions", "Variant", "VariantPayload", "VariantPayloadKind" })
                                    k.DeleteValue(v, false);
                            }
                        }
                        else
                        {
                            Registry.SetValue(key, "EnabledState",        1, RegistryValueKind.DWord);
                            Registry.SetValue(key, "EnabledStateOptions", 0, RegistryValueKind.DWord);
                            Registry.SetValue(key, "Variant",             0, RegistryValueKind.DWord);
                            Registry.SetValue(key, "VariantPayload",      0, RegistryValueKind.DWord);
                            Registry.SetValue(key, "VariantPayloadKind",  0, RegistryValueKind.DWord);
                        }
                        Log($"WebView2 in Search {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-wallpaper-compression",
                    Name             = "Allow Desktop Wallpaper Compression",
                    Description      = "Allow Windows to compress wallpapers to save disk space and improve performance. Only affects images in JPEG format",
                    // EnabledValue=[0,null], DisabledValue=[100], DefaultValue=null → enabled=key absent (Winhance toggle ON = compression allowed)
                    RecommendedState = false,  // recommended = OFF (disable compression → JPEGImportQuality=100)
                    DefaultState     = true,   // default = key absent = compression allowed
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Control Panel\Desktop", "JPEGImportQuality");
                        return v.HasValue ? v != 100 : true; // absent = compression on = toggle ON
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                            k?.DeleteValue("JPEGImportQuality", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop",
                                "JPEGImportQuality", 100, RegistryValueKind.DWord);
                        Log($"Wallpaper Compression {(on ? "allowed (default)" : "disabled (quality=100)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-explorer-menu-show-delay",
                    Name             = "Menu Show Delay",
                    Description      = "Add a brief delay before displaying menus (400ms = Windows default), or show them instantly (0ms) for faster navigation",
                    IsPreference     = true,
                    RecommendedState = false,  // recommended = OFF (0ms instant)
                    DefaultState     = true,   // default = 400ms
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Desktop", "MenuShowDelay");
                        return v != null ? v == "400" : true; // absent = default = 400ms
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop",
                            "MenuShowDelay", on ? "400" : "0", RegistryValueKind.String);
                        Log($"Menu Show Delay {(on ? "400ms (default)" : "0ms (instant)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-explorer-alt-tab-filter",
                    Name             = "Alt+Tab Filter",
                    Description      = "Show only traditional open windows in Alt+Tab instead of including Microsoft Edge tabs and other Windows suggestions",
                    IsPreference     = true,
                    // EnabledValue=[3], DisabledValue=[0], DefaultValue=3 → toggle ON = filter active
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "MultiTaskingAltTabFilter");
                        return v.HasValue ? v == 3 : true; // absent = default = 3 = filter ON
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "MultiTaskingAltTabFilter", on ? 3 : 0, RegistryValueKind.DWord);
                        Log($"Alt+Tab Filter {(on ? "enabled (windows only)" : "disabled (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-mouse-hover-time",
                    Name             = "Mouse Hover Time",
                    Description      = "Controls how long you hover before tooltips and menus appear. Lower = faster response",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("1ms — Instant (Recommended)", "1",    IsRecommended: true),
                        new TweakDropdownOption("100ms",                        "100"),
                        new TweakDropdownOption("200ms",                        "200"),
                        new TweakDropdownOption("400ms (Default)",              "400",  IsDefault: true),
                        new TweakDropdownOption("600ms",                        "600"),
                        new TweakDropdownOption("1000ms",                       "1000"),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseHoverTime");
                        return v switch { "1"=>"0", "100"=>"1", "200"=>"2", "400"=>"3", "600"=>"4", "1000"=>"5", _ =>"3" } is string s ? int.Parse(s) : 3;
                    },
                    ApplyIndex = idx =>
                    {
                        string[] vals = { "1", "100", "200", "400", "600", "1000" };
                        if (idx < 0 || idx >= vals.Length) return;
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseHoverTime", vals[idx], RegistryValueKind.String);
                        Log($"Mouse Hover Time set to {vals[idx]}ms. Sign out to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-background-apps",
                    Name             = "Background App Permissions",
                    Description      = "Control whether apps can run in the background. Force Deny blocks all background apps — avoid if you use Teams, Zoom, or WhatsApp",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("User in Control (Default)", -1, IsDefault: true),
                        new TweakDropdownOption("Force Allow",               1),
                        new TweakDropdownOption("Force Deny (Recommended)",  2, IsRecommended: true),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground");
                        return v.HasValue ? v.Value switch { 1 => 1, 2 => 2, _ => 0 } : 0;
                    },
                    ApplyIndex = idx =>
                    {
                        if (idx == 0)
                        {
                            foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine })
                                h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", true)
                                 ?.DeleteValue("LetAppsRunInBackground", false);
                            Log("Background App Permissions: User in Control (default).");
                        }
                        else
                        {
                            foreach (var h in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
                                Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                                    "LetAppsRunInBackground", idx == 1 ? 1 : 2, RegistryValueKind.DWord);
                            Log($"Background App Permissions: {(idx == 1 ? "Force Allow" : "Force Deny")}.");
                        }
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-performance-explorer-mouse-precision",
                    Name             = "Enhance Pointer Precision",
                    Description      = "Adjust cursor speed based on movement velocity (mouse acceleration). Most competitive gamers disable this for consistent aiming in FPS games",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=["1"], DisabledValue=["0"], DefaultValue="1" → toggle ON = acceleration ON
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Mouse", "MouseSpeed");
                        return v != null ? v == "1" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse",
                            "MouseSpeed",      on ? "1" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse",
                            "MouseThreshold1", on ? "6" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse",
                            "MouseThreshold2", on ? "10" : "0", RegistryValueKind.String);
                        Log($"Enhance Pointer Precision {(on ? "enabled" : "disabled")}. Sign out to apply.");
                    }
                },
            };
    }
}
