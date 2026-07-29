using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // MULTIMEDIA SETTINGS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildMultimedia(StackPanel panel)
        {
            const string SG = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b";
            var section = TweakHelpers.BuildSection(panel, "Multimedia Settings");

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "multimedia-when-sharing-media", Name = "When Sharing Media",
                Description = "Control whether your PC can sleep while streaming media to other devices on your network",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Allow the computer to sleep (Default DC)", 0u, IsDefault: true),
                    new TweakDropdownOption("Prevent idling to sleep (Recommended)",    1u, IsRecommended: true),
                    new TweakDropdownOption("Allow the computer to enter Away Mode",    2u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "03680956-93bc-4294-bba6-4e0f09bb717f", ac: true); return v switch { 0 => 0, 1 => 1, 2 => 2, _ => 1 }; },
                ApplyIndex = idx => SetPowerCfg(SG, "03680956-93bc-4294-bba6-4e0f09bb717f", (uint)idx, (uint)idx, "Sharing media")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "multimedia-video-playback-quality-bias", Name = "Video Playback Quality Bias",
                Description = "Prioritize smooth video playback or battery life when watching videos",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Video playback power bias (Default DC)",        0u, IsDefault: true),
                    new TweakDropdownOption("Video playback performance bias (Recommended)", 1u, IsRecommended: true),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "10778347-1370-4ee0-8bbd-33bdacaade49", ac: true); return v == 1 ? 1 : 0; },
                ApplyIndex = idx => SetPowerCfg(SG, "10778347-1370-4ee0-8bbd-33bdacaade49", (uint)idx, (uint)idx, "Video quality bias")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "multimedia-when-playing-video", Name = "When Playing Video",
                Description = "Balance video quality and power consumption during video playback",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Optimize video quality (Recommended)", 0u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("Balanced",                             1u),
                    new TweakDropdownOption("Optimize power savings (Default DC)",  2u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4", ac: true); return v switch { 0 => 0, 1 => 1, _ => 2 }; },
                ApplyIndex = idx => SetPowerCfg(SG, "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4", (uint)idx, 2u, "Video playback")
            }));
        }

        // ══════════════════════════════════════════════════════════════════════
        // POWER BUTTONS AND LID
        // ══════════════════════════════════════════════════════════════════════

        private void BuildPowerButtons(StackPanel panel)
        {
            const string SG = "4f971e89-eebd-4455-a8de-9e59040e7347";
            var section = TweakHelpers.BuildSection(panel, "Power Buttons and Lid");

            TweakDropdownOption[] ButtonActions() => new[]
            {
                new TweakDropdownOption("Do nothing (Recommended)", 0u, IsRecommended: true),
                new TweakDropdownOption("Sleep",                    1u),
                new TweakDropdownOption("Hibernate",                2u),
                new TweakDropdownOption("Shut down (Default)",      3u, IsDefault: true),
                new TweakDropdownOption("Turn off the display",     4u),
            };

            // Power button action
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "power-button-action", Name = "Power button action",
                Description = "Choose what happens when you press the physical power button on your computer",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = ButtonActions(),
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "7648efa3-dd9c-4e3e-b566-50f929386280", ac: true); return (int)(v ?? 3u) switch { 0=>0,1=>1,2=>2,3=>3,4=>4,_=>3 }; },
                ApplyIndex = idx => { uint[] vals={0u,1u,2u,3u,4u}; SetPowerCfg(SG,"7648efa3-dd9c-4e3e-b566-50f929386280",vals[Math.Min(idx,4)],vals[Math.Min(idx,4)],"Power button"); }
            }));

            // Sleep button action
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "sleep-button-action", Name = "Sleep button action",
                Description = "Choose what happens when you press the dedicated sleep button on your keyboard or computer",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Do nothing (Recommended)", 0u, IsRecommended: true),
                    new TweakDropdownOption("Sleep (Default)",          1u, IsDefault: true),
                    new TweakDropdownOption("Hibernate",                2u),
                    new TweakDropdownOption("Shut down",                3u),
                    new TweakDropdownOption("Turn off the display",     4u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "96996bc0-ad50-47ec-923b-6f41874dd9eb", ac: true); return (int)(v ?? 1u) switch { 0=>0,1=>1,2=>2,3=>3,4=>4,_=>1 }; },
                ApplyIndex = idx => { uint[] vals={0u,1u,2u,3u,4u}; SetPowerCfg(SG,"96996bc0-ad50-47ec-923b-6f41874dd9eb",vals[Math.Min(idx,4)],vals[Math.Min(idx,4)],"Sleep button"); }
            }));

            // Lid close action
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "lid-close-action", Name = "Lid close action",
                Description = "Choose what happens when you close your laptop lid",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Do nothing",          0u),
                    new TweakDropdownOption("Sleep (Recommended & Default)", 1u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("Hibernate",           2u),
                    new TweakDropdownOption("Shut down",           3u),
                    new TweakDropdownOption("Turn off the display",4u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "5ca83367-6e45-459f-a27b-476b1d01c936", ac: true); return (int)(v ?? 1u) switch { 0=>0,1=>1,2=>2,3=>3,4=>4,_=>1 }; },
                ApplyIndex = idx => { uint[] vals={0u,1u,2u,3u,4u}; SetPowerCfg(SG,"5ca83367-6e45-459f-a27b-476b1d01c936",vals[Math.Min(idx,4)],vals[Math.Min(idx,4)],"Lid close"); }
            }));
        }

        // ══════════════════════════════════════════════════════════════════════
        // START MENU POWER OPTIONS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildStartMenuPower(StackPanel panel)
        {
            const string FlyoutKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings";
            const string FlyoutSub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings";
            var section = TweakHelpers.BuildSection(panel, "Start Menu Power Options");

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "start-power-lock-option", Name = "Show Lock Option",
                Description = "Display the Lock option in the Start Menu power button menu",
                IsPreference = true, RecommendedState = false, DefaultState = true,
                ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, FlyoutSub, "ShowLockOption"); return v.HasValue ? v != 0 : true; },
                Apply = on =>
                {
                    if (on) { using var k = Registry.LocalMachine.OpenSubKey(FlyoutSub, true); k?.DeleteValue("ShowLockOption", false); }
                    else Registry.SetValue(FlyoutKey, "ShowLockOption", 0, RegistryValueKind.DWord);
                    Service?.Log($"Start Menu Lock option {(on ? "shown" : "hidden")}.");
                }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "start-power-sleep-option", Name = "Show Sleep Option",
                Description = "Display the Sleep option in the Start Menu power button menu",
                IsPreference = true, RecommendedState = false, DefaultState = true,
                ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, FlyoutSub, "ShowSleepOption"); return v.HasValue ? v != 0 : true; },
                Apply = on =>
                {
                    if (on) { using var k = Registry.LocalMachine.OpenSubKey(FlyoutSub, true); k?.DeleteValue("ShowSleepOption", false); }
                    else Registry.SetValue(FlyoutKey, "ShowSleepOption", 0, RegistryValueKind.DWord);
                    Service?.Log($"Start Menu Sleep option {(on ? "shown" : "hidden")}.");
                }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "start-power-hibernate-option", Name = "Show Hibernate Option",
                Description = "Display the Hibernate option in the Start Menu power button menu",
                IsPreference = true, RecommendedState = false, DefaultState = false,
                ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, FlyoutSub, "ShowHibernateOption"); return v.HasValue ? v == 1 : false; },
                Apply = on =>
                {
                    Registry.SetValue(FlyoutKey, "ShowHibernateOption", on ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"Start Menu Hibernate option {(on ? "shown" : "hidden")}.");
                }
            }));
        }

    }
}
