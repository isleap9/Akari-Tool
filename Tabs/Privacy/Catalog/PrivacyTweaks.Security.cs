using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // SECURITY
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Security(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "security-uac-level", Name = "User Account Control Level",
                    Description = "Controls UAC notification level and secure desktop behavior",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Prompt for Credentials",                        0),
                        new TweakDropdownOption("Always notify",                                  1),
                        // Windows default is also the recommendation here. Winhance marks
                        // "Never notify" as recommended; we deliberately deviate — silently
                        // elevating every admin process is a real security downgrade.
                        new TweakDropdownOption("Notify when apps make changes (Default)",        2, IsDefault: true, IsRecommended: true),
                        new TweakDropdownOption("Notify when apps make changes (no dim)",         3,
                            Warning: "Disables the secure desktop: UAC prompts render on the normal desktop where other software can read or spoof them. Slightly weakens protection against prompt-hijacking malware."),
                        new TweakDropdownOption("Never notify",                                   4,
                            Warning: "Turns UAC prompts off entirely — anything running as your user can silently elevate to administrator. This significantly weakens system security and some apps (and the Microsoft Store) may misbehave. Not recommended."),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var consent = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin");
                        var desktop = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop");
                        if (!consent.HasValue) return 2;
                        return (consent.Value, desktop ?? 1) switch { (1,1)=>0,(2,1)=>1,(5,1)=>2,(5,0)=>3,(0,0)=>4,_=>2 };
                    },
                    ApplyIndex = idx =>
                    {
                        (int c, int d) = idx switch { 0=>(1,1),1=>(2,1),2=>(5,1),3=>(5,0),4=>(0,0),_=>(5,1) };
                        const string k = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
                        Registry.SetValue(k, "ConsentPromptBehaviorAdmin", c, RegistryValueKind.DWord);
                        Registry.SetValue(k, "PromptOnSecureDesktop", d, RegistryValueKind.DWord);
                        Log($"UAC level set to option {idx}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-workplace-join-messages", Name = "Workplace Join Message Prompts",
                    Description = "Show 'Allow my organization to manage my device' prompts throughout Windows",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin", "BlockAADWorkplaceJoin"); return v.HasValue ? v != 1 : true; },
                    Apply = on =>
                    {
                        if (on) { foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin", true)?.DeleteValue("BlockAADWorkplaceJoin", false); }
                        else { foreach (var h in new[] { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin", "BlockAADWorkplaceJoin", 1, RegistryValueKind.DWord); }
                        Log($"Workplace Join messages {(on ? "enabled" : "blocked")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-bitlocker-auto-encryption", Name = "BitLocker Auto Encryption",
                    Description = "Controls whether Windows can automatically encrypt drives with BitLocker",
                    IsPreference = true, RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption"); return v.HasValue ? v == 0 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"BitLocker Auto Encryption {(on ? "allowed" : "prevented")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-wifi-sense", Name = "WiFi-Sense",
                    Description = "Allow sharing WiFi passwords with contacts and automatically connecting to suggested open hotspots",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", "Value"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", "Value", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots", "Value", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"WiFi Sense {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-automatic-maintenance", Name = "Automatic Maintenance",
                    Description = "Choose if Windows should run automatic system maintenance tasks during idle time",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "MaintenanceDisabled"); return v.HasValue ? v == 0 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance", "MaintenanceDisabled", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Automatic Maintenance {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-error-reporting", Name = "Windows Error Reporting",
                    Description = "Choose if Windows should collect and send crash reports and error information to Microsoft",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled"); return v.HasValue ? v == 0 : true; },
                    Apply = on =>
                    {
                        foreach (var h in new[] { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER" })
                            Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Windows Error Reporting {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-remote-assistance", Name = "Remote Assistance",
                    Description = "Choose if other people can connect to your computer remotely to provide technical support",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Remote Assistance {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-smart-app-control", Name = "Smart App Control",
                    Description = "Controls the Smart App Control feature which blocks untrusted and potentially dangerous applications",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Off (Recommended)", 0, IsRecommended: true),
                        new TweakDropdownOption("On (Enforced)", 1),
                        new TweakDropdownOption("Evaluation Mode (Default)", 2, IsDefault: true),
                    },
                    ReadCurrentIndex = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState"); return v switch { 0=>0,1=>1,_=>2 }; },
                    ApplyIndex = idx =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState", idx, RegistryValueKind.DWord);
                        Log($"Smart App Control set to option {idx}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-developer-mode", Name = "Developer Mode",
                    Description = "Allows the installation of apps from any source, including loose files",
                    IsPreference = true, RecommendedState = false, DefaultState = false,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense"); return v.HasValue ? v == 1 : false; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Developer Mode {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "security-powershell-execution-policy", Name = "PowerShell Execution Policy",
                    Description = "Controls whether PowerShell scripts are allowed to run and under what conditions",
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Restricted (Default)", "Restricted", IsDefault: true),
                        new TweakDropdownOption("AllSigned", "AllSigned"),
                        new TweakDropdownOption("RemoteSigned (Recommended)", "RemoteSigned", IsRecommended: true),
                        new TweakDropdownOption("Unrestricted", "Unrestricted"),
                        new TweakDropdownOption("Bypass", "Bypass"),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser, @"Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy")
                             ?? ReadString(RegistryHive.LocalMachine, @"Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy");
                        return v switch { "Restricted"=>0,"AllSigned"=>1,"RemoteSigned"=>2,"Unrestricted"=>3,"Bypass"=>4,_=>0 };
                    },
                    ApplyIndex = idx =>
                    {
                        string[] p = { "Restricted","AllSigned","RemoteSigned","Unrestricted","Bypass" };
                        if (idx<0||idx>=p.Length) return;
                        foreach (var h in new[] { "HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE" })
                            Registry.SetValue($@"{h}\Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", "ExecutionPolicy", p[idx], RegistryValueKind.String);
                        Log($"PowerShell Execution Policy set to {p[idx]}.");
                    }
                },
            };
        }

    }
}
