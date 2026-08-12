using Microsoft.Win32;

namespace AkariTool.Tabs.Power
{
    // MVVM PORT: extracted verbatim from net8 PowerTab.Peripherals.cs.
    // Sections "Display", "Hard Disk", "Internet Explorer", "Desktop Background Settings",
    // "Wireless Adapter Settings", "Sleep", "USB Settings", "PCI Express".
    public static partial class PowerTweaks
    {
        // ── Display ────────────────────────────────────────────────────────────
        public static TweakDefinition[] Display(Action<string> Log)
        {
            const string SG  = "7516b95f-f776-4464-8c53-06167f40cc99";
            const string SET = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "power-display-timeout", Name = "Turn off the display",
                    Description = "Period of inactivity before Windows turns off the display",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = TimeIntervalOptions(recAC: 0, recDC: 300, defAC: 300, defDC: 180),
                    ReadCurrentIndex = () => FindTimeIndex(QueryPowerCfg(SG, SET, ac: true), TimeIntervalSeconds()),
                    ApplyIndex = idx => { var (ac, dc) = TimeIntervalPair(idx); SetPowerCfg(SG, SET, ac, dc, "Display timeout"); }
                },
            };
        }

        // ── Hard Disk ──────────────────────────────────────────────────────────
        public static TweakDefinition[] HardDisk(Action<string> Log)
        {
            const string SG  = "0012ee47-9041-4b5d-9b77-535fba8b1442";
            const string SET = "6738e2c4-e8a5-4a42-b16a-e040e769756e";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "power-harddisk-timeout", Name = "Turn off hard disk after",
                    Description = "Period of inactivity before Windows turns off the hard disk (0 = Never)",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Never (Recommended AC)", 0u,    IsRecommended: true),
                        new TweakDropdownOption("10 minutes (Default DC)",600u,   IsDefault: true),
                        new TweakDropdownOption("20 minutes",             1200u),
                        new TweakDropdownOption("30 minutes",             1800u),
                        new TweakDropdownOption("1 hour",                 3600u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, SET, ac: true); return v switch { 0 => 0, 600 => 1, 1200 => 2, 1800 => 3, 3600 => 4, _ => 1 }; },
                    ApplyIndex = idx => { uint[] vals = { 0u, 600u, 1200u, 1800u, 3600u }; SetPowerCfg(SG, SET, vals[Math.Min(idx,4)], vals[Math.Min(idx,4)], "Hard disk timeout"); }
                },
            };
        }

        // ── Internet Explorer (JavaScript Timer Frequency) ─────────────────────
        public static TweakDefinition[] InternetExplorer(Action<string> Log)
        {
            const string SG  = "02f815b5-a5cf-4c84-bf20-649d1f75d3d8";
            const string SET = "4c793e7d-a264-42e1-87d3-7a0d2f523ccd";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "internet-explorer-javascript-timer", Name = "JavaScript Timer Frequency",
                    Description = "Specifies the frequency of JavaScript timers. Maximum Performance reduces timer throttling for better performance in Edge and WebView2 apps",
                    InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Maximum Performance (Recommended)", 0u, IsRecommended: true, IsDefault: true),
                        new TweakDropdownOption("Maximum Power Savings",              1u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, SET, ac: true); return v == 1 ? 1 : 0; },
                    ApplyIndex = idx => SetPowerCfg(SG, SET, (uint)idx, (uint)idx, "JavaScript Timer Frequency")
                },
            };
        }

        // ── Desktop Background ─────────────────────────────────────────────────
        public static TweakDefinition[] DesktopBackground(Action<string> Log)
        {
            const string SG  = "0d7dbae2-4294-402a-ba8e-26777e8488cd";
            const string SET = "309dce9b-bef4-4119-9921-a851fb12f0f4";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "desktop-slideshow", Name = "Desktop Background Slide Show",
                    Description = "Allow or prevent Windows from rotating through multiple wallpaper images",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Available (Default AC)", 0u, IsDefault: true),
                        new TweakDropdownOption("Paused (Recommended)",   1u, IsRecommended: true),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, SET, ac: true); return v == 1 ? 1 : 0; },
                    ApplyIndex = idx => SetPowerCfg(SG, SET, (uint)idx, (uint)idx, "Desktop slideshow")
                },
            };
        }

        // ── Wireless Adapter ───────────────────────────────────────────────────
        public static TweakDefinition[] WirelessAdapter(Action<string> Log)
        {
            const string SG  = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1";
            const string SET = "12bbebe6-58d6-4636-95bb-3217ef867c1a";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "wireless-power-mode", Name = "Power Saving Mode",
                    Description = "Balance wireless network performance with battery life by adjusting adapter power usage",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Maximum Performance (Recommended AC & Default AC)", 0u, IsRecommended: true, IsDefault: true),
                        new TweakDropdownOption("Minimum Power Saving",                              1u),
                        new TweakDropdownOption("Medium Power Saving (Default DC)",                  2u),
                        new TweakDropdownOption("Maximum Power Saving",                              3u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, SET, ac: true); return v switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 0 }; },
                    ApplyIndex = idx => SetPowerCfg(SG, SET, (uint)idx, idx == 0 ? 2u : (uint)idx, "Wireless power mode")
                },
            };
        }

        // ── Sleep ──────────────────────────────────────────────────────────────
        public static TweakDefinition[] Sleep(Action<string> Log)
        {
            const string SG_SLEEP = "238c9fa8-0aad-41ed-83f4-97be242c8f20";
            return new[]
            {
                // Sleep timeout
                new TweakDefinition
                {
                    Id = "power-sleep-timeout", Name = "Put the computer to sleep",
                    Description = "Period of inactivity before Windows puts the computer to sleep",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = TimeIntervalOptions(recAC: 0, recDC: 900, defAC: 900, defDC: 600),
                    ReadCurrentIndex = () => FindTimeIndex(QueryPowerCfg(SG_SLEEP, "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", ac: true), TimeIntervalSeconds()),
                    ApplyIndex = idx => { var (ac, dc) = TimeIntervalPair(idx); SetPowerCfg(SG_SLEEP, "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", ac, dc, "Sleep timeout"); }
                },
                // Wake timers
                new TweakDefinition
                {
                    Id = "power-wake-timers", Name = "Allow wake timers",
                    Description = "Allow scheduled tasks and applications to wake your computer from sleep",
                    InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Disable (Recommended)", 0u, IsRecommended: true),
                        new TweakDropdownOption("Enable (Default)",       1u, IsDefault: true),
                        new TweakDropdownOption("Important Wake Timers Only", 2u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG_SLEEP, "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d", ac: true); return v switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 }; },
                    ApplyIndex = idx => { uint val = idx switch { 0 => 0u, 2 => 2u, _ => 1u }; SetPowerCfg(SG_SLEEP, "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d", val, val, "Wake timers"); }
                },
                // Hibernation
                new TweakDefinition
                {
                    Id = "power-hibernation-enable", Name = "Hibernation",
                    Description = "Save your session to disk and power down completely, using no battery while preserving your work",
                    IsPreference = true, RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        RunPowerCfg(on ? "/hibernate on" : "/hibernate off");
                        Log($"Hibernation {(on ? "enabled" : "disabled")}.");
                    }
                },
                // Hibernate after
                new TweakDefinition
                {
                    Id = "power-hibernate-timeout", Name = "Hibernate after",
                    Description = "Period of inactivity before Windows hibernates the computer",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = TimeIntervalOptions(recAC: 0, recDC: 0, defAC: 0, defDC: 10800),
                    ReadCurrentIndex = () => FindTimeIndex(QueryPowerCfg(SG_SLEEP, "9d7815a6-7ee4-497e-8888-515a05f02364", ac: true), TimeIntervalSeconds()),
                    ApplyIndex = idx => { var (ac, dc) = TimeIntervalPair(idx); SetPowerCfg(SG_SLEEP, "9d7815a6-7ee4-497e-8888-515a05f02364", ac, dc, "Hibernate timeout"); }
                },
                // Hybrid sleep
                new TweakDefinition
                {
                    Id = "power-hybrid-sleep", Name = "Allow hybrid sleep",
                    Description = "Combines sleep and hibernate by saving your session to disk while staying in low-power mode for faster wake",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Off (Recommended)", 0u, IsRecommended: true),
                        new TweakDropdownOption("On (Default)",      1u, IsDefault: true),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG_SLEEP, "94ac6d29-73ce-41a6-809f-6363ba21b47e", ac: true); return v == 0 ? 0 : 1; },
                    ApplyIndex = idx => SetPowerCfg(SG_SLEEP, "94ac6d29-73ce-41a6-809f-6363ba21b47e", (uint)idx, (uint)idx, "Hybrid sleep")
                },
                // Fast Startup
                new TweakDefinition
                {
                    Id = "power-fast-startup", Name = "Fast Startup",
                    Description = "Hibernate system state during shutdown for faster boot times (does not affect restart)",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\ControlSet001\Control\Session Manager\Power", "HiberbootEnabled"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        const string sub = @"SYSTEM\ControlSet001\Control\Session Manager\Power";
                        if (on) { using var k = Registry.LocalMachine.OpenSubKey(sub, true); k?.DeleteValue("HiberbootEnabled", false); }
                        else Registry.SetValue(@"HKEY_LOCAL_MACHINE\" + sub, "HiberbootEnabled", 0, RegistryValueKind.DWord);
                        Log($"Fast Startup {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ── USB Settings ───────────────────────────────────────────────────────
        public static TweakDefinition[] USB(Action<string> Log)
        {
            const string SG_USB = "2a737441-1930-4402-8d77-b2bebba308a3";
            return new[]
            {
                // USB Hub Selective Suspend Timeout
                new TweakDefinition
                {
                    Id = "usb-hub-selective-suspend-timeout", Name = "USB Hub Selective Suspend Timeout",
                    Description = "Set how long USB hubs wait idle before powering down to save energy",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("0ms — Never suspend (Recommended AC)", 0u,    IsRecommended: true),
                        new TweakDropdownOption("50ms (Default)",                        50u,   IsDefault: true),
                        new TweakDropdownOption("500ms",                                 500u),
                        new TweakDropdownOption("1000ms (Recommended DC)",               1000u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG_USB, "0853a681-27c8-4100-a2fd-82013e970683", ac: true); return v switch { 0 => 0, 50 => 1, 500 => 2, 1000 => 3, _ => 1 }; },
                    ApplyIndex = idx => { uint[] vals = { 0u, 50u, 500u, 1000u }; SetPowerCfg(SG_USB, "0853a681-27c8-4100-a2fd-82013e970683", vals[Math.Min(idx,3)], vals[Math.Min(idx,3)], "USB Hub Suspend Timeout"); }
                },
                // USB Selective Suspend
                new TweakDefinition
                {
                    Id = "usb-selective-suspend", Name = "USB selective suspend",
                    Description = "Allow Windows to power down individual USB ports when devices are idle to save energy",
                    InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Disabled (Recommended AC)", 0u, IsRecommended: true),
                        new TweakDropdownOption("Enabled (Default)",          1u, IsDefault: true),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG_USB, "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", ac: true); return v == 0 ? 0 : 1; },
                    ApplyIndex = idx => SetPowerCfg(SG_USB, "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", (uint)idx, 1u, "USB Selective Suspend")
                },
                // USB 3 Link Power Management
                new TweakDefinition
                {
                    Id = "usb3-link-power-management", Name = "USB 3 Link Power Management",
                    Description = "Control how aggressively USB 3.0 ports enter low-power states when devices are idle",
                    InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Off (Recommended AC)",           0u, IsRecommended: true),
                        new TweakDropdownOption("Minimum power savings",           1u),
                        new TweakDropdownOption("Moderate power savings (Default)",2u, IsDefault: true),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG_USB, "d4e98f31-5ffe-4ce1-be31-1b38b384c009", ac: true); return v switch { 0 => 0, 1 => 1, _ => 2 }; },
                    ApplyIndex = idx => SetPowerCfg(SG_USB, "d4e98f31-5ffe-4ce1-be31-1b38b384c009", (uint)idx, 2u, "USB 3 Link Power")
                },
            };
        }

        // ── PCI Express ────────────────────────────────────────────────────────
        public static TweakDefinition[] PciExpress(Action<string> Log)
        {
            const string SG  = "501a4d13-42af-4429-9fd1-a8218c268e20";
            const string SET = "ee12f906-d277-404b-b6da-e5fa1a576df5";
            return new[]
            {
                new TweakDefinition
                {
                    Id = "pci-link-state-power-management", Name = "Link State Power Management",
                    Description = "Control power savings for PCIe devices like graphics cards, SSDs, and expansion cards",
                    InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Off (Recommended AC)",           0u, IsRecommended: true),
                        new TweakDropdownOption("Moderate power savings (Default)",1u, IsDefault: true),
                        new TweakDropdownOption("Maximum power savings",           2u),
                    },
                    ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, SET, ac: true); return v switch { 0 => 0, 2 => 2, _ => 1 }; },
                    ApplyIndex = idx => SetPowerCfg(SG, SET, (uint)idx, 2u, "PCIe ASPM")
                },
            };
        }
    }
}
