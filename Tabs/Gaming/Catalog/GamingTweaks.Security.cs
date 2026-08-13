using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

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
                        // RE-ARMED (isleap approved, 2026-08-01) together with the
                        // App.xaml.cs --defender-phase2 handoff. Body restored VERBATIM from
                        // the WPF build — SAME DefenderService.SetAsync entry point, which
                        // schedules the byte-identical DefenderPhase2Scheduler.ScheduleRunOnce
                        // post-reboot trigger.
                        // on = true  → user wants Defender DISABLED → SetAsync(disable: true)
                        // on = false → user wants Defender ENABLED  → SetAsync(disable: false)
                        // SetAsync is async; fire-and-forget exactly like the AkariOS card does.
                        _ = AkariTool.Services.DefenderService.SetAsync(on, ToolService.Current!);
                        Log($"Windows Defender {(on ? "disable" : "re-enable")} requested — restart required.");
                    }
                },
            };
    }
}