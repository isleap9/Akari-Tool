using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public partial class OSTweaksTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // SECURITY
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSecuritySection(StackPanel panel)
        {
            AddSection(panel, "Security", new[]
            {
                new TweakDefinition
                {
                    Id = "os-uac", Name = "User Account Control",
                    Description = "Enable or disable User Account Control (UAC) — controls elevation prompts",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\AppInfo", "Start", enable ? 2 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"UAC {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-admin-uac", Name = "UAC Code-Signing for Admin",
                    Description = "Require apps to be code-signed before UAC grants elevation to the admin account",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ValidateAdminCodeSignatures");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\AppInfo", "Start", enable ? 2 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ValidateAdminCodeSignatures", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Admin UAC code-signing {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-ntfs-encryption", Name = "Disable NTFS Encryption",
                    Description = "Disables EFS encryption on the filesystem — removes overhead if you don't use encrypted files",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Policies", "NtfsDisableEncryption");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = disable =>
                    {
                        TweakHelpers.RunCommand("fsutil", $"behavior set disableencryption {(disable ? 1 : 0)}");
                        if (disable)
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies", "NtfsDisableEncryption", 1, RegistryValueKind.DWord);
                        else
                            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Policies", true)?.DeleteValue("NtfsDisableEncryption", throwOnMissingValue: false);
                        Log($"NTFS Encryption {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-dcom", Name = "Disable DCOM",
                    Description = "Disables Distributed COM — reduces attack surface if you don't use legacy COM automation",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Ole", "EnableDCOM");
                        return v != null ? v == "N" : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Ole", "EnableDCOM", disable ? "N" : "Y", RegistryValueKind.String);
                        Log($"DCOM {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-hyper-v", Name = "Disable Hyper-V",
                    Description = "Disables Hyper-V hypervisor via bcdedit and DISM — improves bare-metal gaming performance",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", "EnableVirtualizationBasedSecurity");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = disable =>
                    {
                        if (disable)
                        {
                            TweakHelpers.RunCommand("bcdedit", "/set hypervisorlaunchtype off");
                            TweakHelpers.RunCommand("bcdedit", "/set vsmlaunchtype Off");
                            TweakHelpers.RunCommand("bcdedit", "/set loadoptions DISABLE-LSA-ISO,DISABLE-VBS");
                            TweakHelpers.RunCommand("DISM", "/Online /Disable-Feature:Microsoft-Hyper-V-All /Quiet /NoRestart");
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeviceGuard", "EnableVirtualizationBasedSecurity", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0, RegistryValueKind.DWord);
                        }
                        else
                        {
                            TweakHelpers.RunCommand("bcdedit", "/set hypervisorlaunchtype auto");
                            TweakHelpers.RunCommand("bcdedit", "/deletevalue loadoptions");
                            TweakHelpers.RunCommand("DISM", "/Online /Enable-Feature:Microsoft-Hyper-V-All /Quiet /NoRestart");
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 1, RegistryValueKind.DWord);
                        }
                        Log($"Hyper-V {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-vbs", Name = "Enable VBS",
                    Description = "Toggle Virtualization Based Security — hardens the kernel but adds overhead",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity",  enable ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "RequirePlatformSecurityFeatures", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"VBS {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-dep-nx", Name = "Disable DEP/NX",
                    Description = "Disables Data Execution Prevention via bcdedit NX AlwaysOff — slight perf gain, reduced security",
                    IsPreference = true,
                    ReadState = () => (bool?)null,  // bcdedit — no clean registry read
                    Apply = disable =>
                    {
                        TweakHelpers.RunCommand("bcdedit", disable ? "/set NX AlwaysOff" : "/set NX OptIn");
                        Log($"DEP/NX {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-process-mitigation", Name = "Enable Process Mitigations",
                    Description = "Enables Spectre/Meltdown mitigations via FeatureSettingsOverride — more secure but slightly slower",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = enable =>
                    {
                        const string key = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
                        Registry.SetValue(key, "FeatureSettingsOverride",     enable ? 0 : 3, RegistryValueKind.DWord);
                        Registry.SetValue(key, "FeatureSettingsOverrideMask", 3, RegistryValueKind.DWord);
                        Log($"Process mitigations {(enable ? "enabled (secure)" : "disabled (performance)")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-mpo", Name = "Disable Multi-Plane Overlay",
                    Description = "Disables MPO — fixes stuttering and flickering on some GPU/driver combinations",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "DisableOverlays");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = disable =>
                    {
                        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                            .CreateSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true);
                        if (disable)
                            key?.SetValue("DisableOverlays", 1, RegistryValueKind.DWord);
                        else
                            key?.DeleteValue("DisableOverlays", throwOnMissingValue: false);
                        Log($"MPO {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-defender", Name = "Disable Windows Defender",
                    Description = "Disable or re-enable Windows Defender — requires PostInstall files and Tamper Protection OFF",
                    IsPreference = true,
                    ReadState = () => TweakHelpers.HasState("DisableDefender") ? true : false,
                    Apply = disable => SetDefenderToggle(disable)
                },
            });
        }

    }
}
