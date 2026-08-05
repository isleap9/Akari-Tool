using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Update
{
    public partial class UpdateTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();

        public UpdateTab() => InitializeComponent();

        public override string NavTag   => "Update";
        public override string NavLabel => "Windows Updates";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.MaxWidth = 860;
            RootPanel.HorizontalAlignment = HorizontalAlignment.Center;

            RootPanel.Children.Add(PageHeader("Windows Updates",
                "Update policy, delivery optimisation, and update behaviour controls.",
                withActions: true, RootPanel));

            BuildUpdatePolicy(RootPanel);
            BuildDeliveryAndStore(RootPanel);
            BuildUpdateBehavior(RootPanel);

            foreach (var r in _refreshActions)
                try { r(); } catch { }
        }

        private void AddSection(StackPanel panel, string title, TweakDefinition[] tweaks)
        {
            var section = TweakHelpers.BuildSection(panel, title);
            foreach (var def in tweaks)
                _refreshActions.Add(TweakHelpers.AddTweakRow(section, def));
            TweakHelpers.AttachBulkActions(section);
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE POLICY
        // ══════════════════════════════════════════════════════════════════════

        private void BuildUpdatePolicy(StackPanel panel)
        {
            const string AUSubHKLM = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string UXSub     = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            const string AUKEYHLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string AUKEYHKCU = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string UXKEY     = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

            AddSection(panel, "Update Policy", new[]
            {
                new TweakDefinition
                {
                    Id          = "updates-policy-mode",
                    Name        = "Windows Update Policy",
                    Description = "Control how Windows updates are installed on your system",
                    IsPreference = true,
                    InputKind   = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Normal (Windows Default)",                       0, IsDefault: true),
                        new TweakDropdownOption("Security Updates Only (Recommended)",            1, IsRecommended: true),
                        new TweakDropdownOption("Paused for a long time (Unpause in Settings)",  2),
                        new TweakDropdownOption("Disabled (NOT Recommended, Security Risk)",      3),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var noAuto  = ReadDword(RegistryHive.LocalMachine, AUSubHKLM, "NoAutoUpdate");
                        var auOpts  = ReadDword(RegistryHive.LocalMachine, AUSubHKLM, "AUOptions");
                        var deferF  = ReadDword(RegistryHive.LocalMachine, UXSub, "DeferFeatureUpdates");
                        var pauseF  = ReadString(RegistryHive.LocalMachine, UXSub, "PauseFeatureUpdatesEndTime");

                        // Paused until 2051
                        if (pauseF != null && pauseF.StartsWith("2051")) return 2;
                        // Disabled: NoAutoUpdate=1, AUOptions=1, no defer
                        if (noAuto == 1 && auOpts == 1 && deferF == null) return 3;
                        // Security only: AUOptions=2, DeferFeatureUpdates=1
                        if (auOpts == 2 && deferF == 1) return 1;
                        // Normal: nothing set
                        return 0;
                    },
                    ApplyIndex = idx =>
                    {
                        // Clear all first
                        void ClearAU()
                        {
                            foreach (var (hive, hkey) in new[] {
                                (RegistryHive.LocalMachine, AUKEYHLM),
                                (RegistryHive.CurrentUser,  AUKEYHKCU) })
                            {
                                var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default)
                                                   .OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", true);
                                if (k != null)
                                    foreach (var v in new[] { "NoAutoUpdate","AUOptions","NoAUShutdownOption","AlwaysAutoRebootAtScheduledTime","AutoInstallMinorUpdates","UseWUServer" })
                                        k.DeleteValue(v, false);
                            }
                            var ux = Registry.LocalMachine.OpenSubKey(UXSub, true);
                            if (ux != null)
                                foreach (var v in new[] { "BranchReadinessLevel","DeferFeatureUpdates","DeferFeatureUpdatesPeriodInDays","DeferQualityUpdates","DeferQualityUpdatesPeriodInDays",
                                    "PauseFeatureUpdatesStartTime","PauseFeatureUpdatesEndTime","PauseQualityUpdatesStartTime","PauseQualityUpdatesEndTime",
                                    "PauseUpdatesStartTime","PauseUpdatesExpiryTime","PausedQualityDate","PausedFeatureDate","FlightSettingsMaxPauseDays",
                                    "PausedFeatureStatus","PausedQualityStatus" })
                                    ux.DeleteValue(v, false);
                        }

                        ClearAU();

                        if (idx == 0) // Normal — already cleared
                        {
                            Log("Windows Update: restored to Normal (Windows default).");
                        }
                        else if (idx == 1) // Security only
                        {
                            Registry.SetValue(AUKEYHLM,  "AUOptions", 2, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "AUOptions", 2, RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "BranchReadinessLevel",           20,  RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "DeferFeatureUpdates",             1,  RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "DeferFeatureUpdatesPeriodInDays", 365, RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "DeferQualityUpdates",             1,  RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "DeferQualityUpdatesPeriodInDays", 7,  RegistryValueKind.DWord);
                            Log("Windows Update: Security Updates Only — feature updates deferred 365 days, quality 7 days.");
                        }
                        else if (idx == 2) // Paused until 2051
                        {
                            Registry.SetValue(AUKEYHLM,  "NoAutoUpdate", 1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "NoAutoUpdate", 1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "AUOptions",    1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "AUOptions",    1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "NoAUShutdownOption",          1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "NoAUShutdownOption",          1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "AlwaysAutoRebootAtScheduledTime", 0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "AlwaysAutoRebootAtScheduledTime", 0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "AutoInstallMinorUpdates",    0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "AutoInstallMinorUpdates",    0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "UseWUServer",                0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "UseWUServer",                0, RegistryValueKind.DWord);
                            foreach (var (name, val) in new[] {
                                ("PauseFeatureUpdatesStartTime",  "2025-01-01T00:00:00Z"),
                                ("PauseFeatureUpdatesEndTime",    "2051-12-31T00:00:00Z"),
                                ("PauseQualityUpdatesStartTime",  "2025-01-01T00:00:00Z"),
                                ("PauseQualityUpdatesEndTime",    "2051-12-31T00:00:00Z"),
                                ("PauseUpdatesStartTime",         "2025-01-01T00:00:00Z"),
                                ("PauseUpdatesExpiryTime",        "2051-12-31T00:00:00Z"),
                                ("PausedQualityDate",             "2025-01-01T00:00:00Z"),
                                ("PausedFeatureDate",             "2025-01-01T00:00:00Z"),
                            })
                                Registry.SetValue(UXKEY, name, val, RegistryValueKind.String);
                            Registry.SetValue(UXKEY, "FlightSettingsMaxPauseDays", 10023, RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "PausedFeatureStatus",            1, RegistryValueKind.DWord);
                            Registry.SetValue(UXKEY, "PausedQualityStatus",            1, RegistryValueKind.DWord);
                            Log("WARNING: Windows Update paused until 2051. Unpause manually in Settings > Windows Update.");
                        }
                        else if (idx == 3) // Disabled
                        {
                            Registry.SetValue(AUKEYHLM,  "NoAutoUpdate", 1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "NoAutoUpdate", 1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "AUOptions",    1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "AUOptions",    1, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHLM,  "UseWUServer",  0, RegistryValueKind.DWord);
                            Registry.SetValue(AUKEYHKCU, "UseWUServer",  0, RegistryValueKind.DWord);
                            Log("WARNING: Windows Update disabled. Your system is now vulnerable to security threats.");
                        }
                    }
                },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // DELIVERY & STORE
        // ══════════════════════════════════════════════════════════════════════

        private void BuildDeliveryAndStore(StackPanel panel)
        {
            const string DOSubHKLM  = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string DOKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string DOKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string StoreHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\WindowsStore";
            const string StoreHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore";
            const string StoreSubHKCU = @"SOFTWARE\Policies\Microsoft\WindowsStore";

            AddSection(panel, "Delivery & Store", new[]
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
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE BEHAVIOR
        // ══════════════════════════════════════════════════════════════════════

        private void BuildUpdateBehavior(StackPanel panel)
        {
            const string UXSub  = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            const string UXKey  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            const string WUSub  = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string AUSub  = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string WUKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string WUKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            const string AUKeyHKLM  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
            const string AUKeyHKCU  = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

            AddSection(panel, "Update Behavior", new[]
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
            });
        }

        // ── Registry helpers ──────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) as string; }
            catch { return null; }
        }

        private void Log(string msg) => Service?.Log(msg);
    }
}