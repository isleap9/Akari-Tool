using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Update;
// The Update Policy dropdown now lives on the declarative SettingDefinition stack
    // (UpdateOptimizations catalog + WindowsUpdatePolicyHandler).
    public static partial class UpdateTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // DELIVERY & STORE
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] DeliveryAndStore(Action<string> Log)
        {
            const string DOSubHKLM  = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string DOKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string DOKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string StoreHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\WindowsStore";
            const string StoreHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore";
            const string StoreSubHKCU = @"SOFTWARE\Policies\Microsoft\WindowsStore";

            return new[]
            {
                new TweakDefinition
                {
                    Id          = "updates-delivery-optimization",
                    Name        = "Delivery Optimization",
                    Description = "Share downloaded updates with other PCs on your network or the internet to reduce bandwidth usage",
                    IsPreference = true,
                    InputKind   = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Windows Default",               0, IsDefault: true),
                        new TweakDropdownOption("Devices on LAN Only",           1),
                        new TweakDropdownOption("Devices on LAN and Internet",   3),
                        new TweakDropdownOption("Disabled (Recommended)",        99, IsRecommended: true),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, DOSubHKLM, "DODownloadMode");
                        if (!v.HasValue) return 0;
                        return v.Value switch { 1 => 1, 3 => 2, 99 => 3, _ => 0 };
                    },
                    ApplyIndex = idx =>
                    {
                        int? mode = idx switch { 0 => (int?)null, 1 => 1, 2 => 3, 3 => 99, _ => null };
                        if (mode == null)
                        {
                            foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser })
                                h.OpenSubKey(DOSubHKLM, true)?.DeleteValue("DODownloadMode", false);
                            Log("Delivery Optimization: Windows default restored.");
                        }
                        else
                        {
                            Registry.SetValue(DOKeyHKLM, "DODownloadMode", mode.Value, RegistryValueKind.DWord);
                            Registry.SetValue(DOKeyHKCU, "DODownloadMode", mode.Value, RegistryValueKind.DWord);
                            Log($"Delivery Optimization set to mode {mode.Value}.");
                        }
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-store-auto-download",
                    Name             = "Auto Update Microsoft Store Apps",
                    Description      = "Automatically download and install updates for apps from the Microsoft Store",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[4,null], DisabledValue=[2]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, StoreSubHKCU, "AutoDownload");
                        return v.HasValue ? v != 2 : true;
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine })
                                h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\WindowsStore", true)
                                 ?.DeleteValue("AutoDownload", false);
                        }
                        else
                        {
                            Registry.SetValue(StoreHKCU, "AutoDownload", 2, RegistryValueKind.DWord);
                            Registry.SetValue(StoreHKLM, "AutoDownload", 2, RegistryValueKind.DWord);
                        }
                        Log($"Store Auto Update {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE BEHAVIOR
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] UpdateBehavior(Action<string> Log)
        {
            const string UXSub  = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            const string UXKey  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            const string WUSub  = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string AUSub  = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string WUKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string WUKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string AUKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string AUKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

            return new[]
            {
                new TweakDefinition
                {
                    Id               = "updates-latest-updates",
                    Name             = "Get the latest updates as soon as they're available",
                    Description      = "Be among the first to get the latest non-security updates, fixes, and improvements as they roll out",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1,null], DisabledValue=[0]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, UXSub, "IsContinuousInnovationOptedIn");
                        return v.HasValue ? v != 0 : true;
                    },
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.LocalMachine.OpenSubKey(UXSub, true); k?.DeleteValue("IsContinuousInnovationOptedIn", false); }
                        else Registry.SetValue(UXKey, "IsContinuousInnovationOptedIn", 0, RegistryValueKind.DWord);
                        Log($"Get latest updates early {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-other-products",
                    Name             = "Receive updates for other Microsoft products",
                    Description      = "Get Microsoft Office and other updates together with Windows updates",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, UXSub, "AllowMUUpdateService");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(UXKey, "AllowMUUpdateService", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Updates for other Microsoft products {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-restart-asap",
                    Name             = "Get me up to date (restart ASAP)",
                    Description      = "Restart as soon as possible, even during active hours, to finish updating",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, UXSub, "IsExpedited");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(UXKey, "IsExpedited", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Restart ASAP after updates {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-restart-options",
                    Name             = "Automatic Restart After Updates",
                    Description      = "Allow Windows to automatically restart your PC after installing updates when you're logged in",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[null], DisabledValue=[1]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, AUSub, "NoAutoRebootWithLoggedOnUsers");
                        return v.HasValue ? v != 1 : true;
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser })
                                h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", true)
                                 ?.DeleteValue("NoAutoRebootWithLoggedOnUsers", false);
                        }
                        else
                        {
                            Registry.SetValue(AUKeyHKLM, "NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKeyHKCU, "NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);
                        }
                        Log($"Auto restart after updates {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-notification-level",
                    Name             = "Update Notifications",
                    Description      = "Show or hide notifications about available updates and update progress",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[2], DisabledValue=[null] → ON=value 2, OFF=key absent
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, WUSub, "SetUpdateNotificationLevel");
                        return v.HasValue ? v == 2 : true; // absent = notifications on (default)
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser })
                                h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", true)
                                 ?.DeleteValue("SetUpdateNotificationLevel", false);
                        }
                        else
                        {
                            // Disable = set to value that hides notifications
                            foreach (var h in new[] { WUKeyHKLM, WUKeyHKCU })
                                Registry.SetValue(h, "SetUpdateNotificationLevel", 2, RegistryValueKind.DWord);
                        }
                        Log($"Update Notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-restart-notification",
                    Name             = "Notify me when a restart is required to finish updating",
                    Description      = "Show a notification when your device needs to restart to complete an update",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, UXSub, "RestartNotificationsAllowed2");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(UXKey, "RestartNotificationsAllowed2", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Restart required notification {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-metered-connection",
                    Name             = "Download updates over metered connections",
                    Description      = "Allow Windows to download updates when using mobile hotspots or data-limited connections",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, UXSub, "AllowAutoWindowsUpdateDownloadOverMeteredNetwork");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(UXKey, "AllowAutoWindowsUpdateDownloadOverMeteredNetwork", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Updates over metered connection {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-driver-controls",
                    Name             = "Driver Updates via Windows Update",
                    Description      = "Include hardware driver updates when downloading and installing Windows Updates",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[null], DisabledValue=[1]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, WUSub, "ExcludeWUDriversInQualityUpdate");
                        return v.HasValue ? v != 1 : true;
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser })
                                h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", true)
                                 ?.DeleteValue("ExcludeWUDriversInQualityUpdate", false);
                        }
                        else
                        {
                            Registry.SetValue(WUKeyHKLM, "ExcludeWUDriversInQualityUpdate", 1, RegistryValueKind.DWord);
                            Registry.SetValue(WUKeyHKCU, "ExcludeWUDriversInQualityUpdate", 1, RegistryValueKind.DWord);
                        }
                        Log($"Driver updates via Windows Update {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "updates-driver-coinstallers",
                    Name             = "Driver Co-Installers",
                    Description      = "Allows hardware vendors to install companion software alongside device drivers. Disabling prevents bloatware like Razer Synapse and printer utilities from auto-installing when you plug in devices",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[null], DisabledValue=[1]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer", "DisableCoInstallers");
                        return v.HasValue ? v != 1 : true;
                    },
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer";
                        const string sub  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer";
                        if (on) { using var k = Registry.LocalMachine.OpenSubKey(sub, true); k?.DeleteValue("DisableCoInstallers", false); }
                        else Registry.SetValue(path, "DisableCoInstallers", 1, RegistryValueKind.DWord);
                        Log($"Driver Co-Installers {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ── Registry helpers ──────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }
    }
