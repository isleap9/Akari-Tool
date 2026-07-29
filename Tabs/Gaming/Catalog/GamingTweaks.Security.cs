using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Security(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-virtualization-based-security",
                    Name             = "Virtualization Based Security (VBS)",
                    Description      = "Isolates parts of memory to protect the system from vulnerabilities. Disabling can improve gaming performance but reduces system security",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard",
                            "EnableVirtualizationBasedSecurity", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard",
                            "RequirePlatformSecurityFeatures", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"VBS {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-memory-integrity",
                    Name             = "Memory Integrity (HVCI)",
                    Description      = "Prevents malicious code from being inserted into high-security processes. Disabling can improve gaming performance but reduces system security",
                    IsPreference     = true,
                    RequiresRestart  = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                            "Enabled");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
                        Registry.SetValue(path, "Enabled",      on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(path, "WasEnabledBy", on ? 2 : 0, RegistryValueKind.DWord);
                        Log($"Memory Integrity (HVCI) {(on ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-disable-defender",
                    Name             = "Disable Windows Defender",
                    Description      = "Fully disable Windows Defender (Antivirus, real-time protection, SmartScreen) via a reboot-based removal. Requires Tamper Protection to be OFF and a restart to complete. Re-enabling also requires a restart.",
                    IsPreference     = true,          // security tradeoff — no objectively correct answer
                    RequiresRestart  = true,
                    // Inverted row (toggle ON = Defender disabled). No Recommended pill
                    // (RecommendedState null). DefaultState=false → toggle OFF is default
                    // (= Defender ON), so the Windows Default pill highlights on a stock
                    // machine; InvertBadgeLabelWording makes its tooltip read "Windows default: On".
                    DefaultState            = false,
                    InvertBadgeLabelWording = true,
                    // Row ON = Defender DISABLED, matching the AkariOS card and the shared
                    // "DisableDefender" state key. Warn on enabling (i.e. when disabling Defender).
                    Warning          = "This fully disables Windows Defender and removes its servicing package. Tamper Protection MUST be off first (Windows Security → Virus & threat protection → Manage settings → Tamper Protection → Off). A restart is required to complete, and re-enabling also requires a restart. Continue?",
                    WarningState     = true,          // warn only when switching ON (=disabling Defender)
                    ReadState        = () => TweakHelpers.HasState("DisableDefender"),
                    Apply = on =>
                    {
                        // on = true  → user wants Defender DISABLED → SetAsync(disable: true)
                        // on = false → user wants Defender ENABLED  → SetAsync(disable: false)
                        // SetAsync is async; fire-and-forget exactly like the AkariOS card does.
                        _ = AkariTool.Services.DefenderService.SetAsync(on, ToolService.Current!);
                        Log($"Windows Defender {(on ? "disable" : "re-enable")} requested — restart required.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "boot-menu-policy-standard",
                    Name             = "Standard Boot Menu Policy",
                    Description      = "Use the modern graphical boot menu instead of the legacy text-mode menu. Standard enables the Windows recovery UI and advanced startup options",
                    RequiresRestart  = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    // bcdedit state cannot be read from a registry key, so this row keeps a
                    // HasState-based ReadState — the same intentional exception the Defender
                    // row above uses. BcdBackup covers the undo path (BCD is not in System Restore).
                    ReadState        = () => TweakHelpers.HasState("BootMenuPolicy"),
                    Apply = on =>
                    {
                        BcdBackup.EnsureBackup();
                        TweakHelpers.RunCommand("bcdedit.exe",
                            on ? "/set bootmenupolicy Standard" : "/set bootmenupolicy legacy");
                        if (on) TweakHelpers.SaveState("BootMenuPolicy");
                        else    TweakHelpers.ClearState("BootMenuPolicy");
                        Log($"Boot menu policy set to {(on ? "Standard" : "Legacy")}.");
                    }
                },
            };
    }
}
