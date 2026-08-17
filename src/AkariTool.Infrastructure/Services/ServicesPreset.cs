using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Service startup presets.
    ///
    /// StockDefault  — True Windows 11 stock values. Derived from the AkariOS
    ///                 Windows-Default-services.reg with 9 corrections applied
    ///                 (Defender, WSearch, WER were wrongly disabled on AkariOS).
    ///
    /// AkariOsDefault — The AkariOS aggressive preset, cleaned of Defender
    ///                  tampering. All 5 Defender-related services are restored
    ///                  to their true stock values before applying.
    ///                  166 services differ from stock — telemetry, Bluetooth,
    ///                  Xbox, remote access, Hyper-V guests, search, and more.
    /// </summary>
    public static partial class ServicesPreset
    {
        // ── Defender services — never touched by any preset ───────────────────
        // Services never touched by any preset
        // Defender services: tampering breaks AV/security
        // Boot-critical drivers (stock Start=0 or 1): disabling causes INACCESSIBLE_BOOT_DEVICE
        private static readonly HashSet<string> _defenderServices = new()
        {
            // Defender
            "WdBoot", "WdFilter", "WdNisDrv", "WdNisSvc", "WinDefend",
            "SecurityHealthService", "Sense", "WdmCompanionFilter",
            "webthreatdefsvc", "webthreatdefusersvc", "webthreatdefusersvc_56a94",
            // Boot-critical storage/kernel drivers (BSOD if disabled)
            "fvevol",      // BitLocker volume filter — boot driver
            "storflt",     // Storage filter driver — boot driver
            "vdrvroot",    // Virtual Drive Root — boot driver (critical in VMs)
            "vpci",        // Virtual PCI — boot driver (critical in VMs)
            "spaceport",   // Storage Spaces port — boot driver
            "Dfsc",        // DFS client — system driver
            "acpiex",      // ACPI extensions — boot driver
            "bam",         // Background Activity Moderator — system driver
            "bttflt",      // Bluetooth filter — boot driver
            "dam",         // Desktop Activity Moderator — system driver
            "rdyboost",    // ReadyBoost — boot driver
            "arcsas",      // Adaptec SAS driver — boot driver
            "EhStorClass", // Enhanced Storage — boot driver
            "EhStorTcgDrv",// Enhanced Storage TCG — boot driver
            "SiSRaid2",    // SiS RAID 2 — boot driver
            "SiSRaid4",    // SiS RAID 4 — boot driver
            "VSTXRAID",    // VST RAID — boot driver
            "NdisCap",     // NDIS capture — system driver
            "NetBIOS",     // NetBIOS over TCP — system driver
            "NetBT",       // NetBT — system driver
            "Beep",        // Beep — system driver
            // Boot-critical SERVICES — ACL-locked by Windows precisely because
            // breaking them makes the machine unbootable. Every preset already
            // assigns them their stock value (2), so writing them can only ever
            // be a no-op or a catastrophe. Never attempt it.
            "DcomLaunch",   // DCOM Server Process Launcher
            "RpcSs",        // Remote Procedure Call
            "RpcEptMapper", // RPC Endpoint Mapper
            "SamSs",        // Security Accounts Manager
        };

        // ── Public apply methods ──────────────────────────────────────────────

        public static async Task ApplyAkariGaming(ToolService log, bool akariOsContext = false)
        {
            log.Log("[SERVICES] Applying AkariOS Gaming preset (Defender protected)...");
            await Task.Run(() => Apply(_akariOsPreset, log, akariOsContext));
            await ApplyLockedServicesAsync(_akariOsPreset, log, akariOsContext);
            StampPreset(akariOsContext ? "AkariGaming" : "Gaming", log);
            log.Log("[SERVICES] AkariOS Gaming preset applied. Restart to take full effect.");
        }

        public static async Task ApplyAkariDaily(ToolService log, bool akariOsContext = false)
        {
            log.Log("[SERVICES] Applying AkariOS Daily preset (Windows Update + ISO mount enabled)...");
            await Task.Run(() => Apply(_dailyPreset, log, akariOsContext));
            await ApplyLockedServicesAsync(_dailyPreset, log, akariOsContext);

            // Data Usage task — disabled by the AkariOS image; DusmSvc alone is not
            // enough to restore the Settings connectivity status. Best-effort.
            await Task.Run(() => AkariTool.Tabs.TweakHelpers.RunCommand(
                "schtasks.exe", @"/Change /TN ""Microsoft\Windows\DUSM\dusmtask"" /Enable"));
            log.Log("[SERVICES] Re-enabled DUSM data usage task (network status in Settings).");

            StampPreset(akariOsContext ? "AkariDaily" : "Daily", log);
            log.Log("[SERVICES] AkariOS Daily preset applied. Restart to take full effect.");
        }

        public static async Task ApplyMinimal(ToolService log, bool akariOsContext = false)
        {
            log.Log("[SERVICES] Applying AkariOS Minimal preset (most aggressive — fully revertable)...");
            log.Log("[SERVICES] NOTE: ISO mount via Explorer disabled (use PowerShell: Mount-DiskImage).");
            log.Log("[SERVICES] NOTE: HWiNFO/AIDA64 sensor readout disabled (WmiAcpi=4).");
            await Task.Run(() => Apply(_minimalPreset, log, akariOsContext));
            await ApplyLockedServicesAsync(_minimalPreset, log, akariOsContext);
            StampPreset(akariOsContext ? "AkariMinimal" : "Minimal", log);
            log.Log("[SERVICES] Minimal preset applied. Restart to take full effect.");
        }

        public static async Task ApplyStockDefault(ToolService log, bool akariOsContext = false)
        {
            log.Log("[SERVICES] Restoring Windows 11 stock defaults...");
            await Task.Run(() => Apply(_stockDefault, log, akariOsContext));
            await ApplyLockedServicesAsync(_stockDefault, log, akariOsContext);
            StampPreset(akariOsContext ? "AkariStock" : "Stock", log);
            log.Log("[SERVICES] Stock defaults restored. Restart to take full effect.");
        }

        /// <summary>
        /// Writes the ACL-locked service keys that Administrator cannot write directly,
        /// natively under SYSTEM impersonation (ElevationService.RunAsSystem — no MinSudo
        /// download). Defender services are never included.
        /// </summary>
        private static Task ApplyLockedServicesAsync(Dictionary<string, int> preset, ToolService log, bool akariOsContext = false)
        {
            var targets = new List<(string svc, int start)>();
            foreach (var svc in _aclLockedServices)
            {
                if (!preset.TryGetValue(svc, out var startVal)) continue;
                if (_defenderServices.Contains(svc)) continue;
                if (akariOsContext && _akariOsPreserve.Contains(svc)) continue;
                targets.Add((svc, startVal));
            }
            if (targets.Count == 0) return Task.CompletedTask;

            log.Log($"[SERVICES] Applying {targets.Count} ACL-locked service(s) via SYSTEM (native)...");
            return Task.Run(() =>
            {
                bool ok = AkariTool.Services.ElevationService.RunAsSystem(() =>
                {
                    foreach (var (svc, start) in targets)
                    {
                        try
                        {
                            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                                $@"SYSTEM\CurrentControlSet\Services\{svc}", writable: true);
                            if (k == null) continue;
                            k.SetValue("Start", start, Microsoft.Win32.RegistryValueKind.DWord);
                        }
                        catch (Exception ex) { log.Log($"[SERVICES] locked {svc}: {ex.Message}"); }
                    }
                }, log.Log);
                log.Log(ok ? "[SERVICES] Locked services applied via SYSTEM."
                           : "[SERVICES] SYSTEM elevation failed for locked services.");
            });
        }

        // ── Core apply logic ──────────────────────────────────────────────────

        // Services whose registry keys are ACL-locked even to Administrator.
        // Windows intentionally denies write access to these — skip silently.
        private static readonly HashSet<string> _aclLockedServices = new()
        {
            "TrustedInstaller", "gpsvc", "wscsvc", "WdiServiceHost",
            "WdiSystemHost", "TrkWks", "Schedule", "lsass",
            "DPS", // Diagnostic Policy Service — same ACL family as WdiServiceHost/
                   // WdiSystemHost. Daily restores it to 2 for network status, so it
                   // must go through SYSTEM or that write silently fails.
        };

        // Services skipped ONLY when applying from the AkariOS-gated section, so the
        // playbook's value is preserved instead of being overwritten. PRM is a boot-start
        // driver: we must never write 4 on a live system, and writing 0 would undo the
        // playbook — so we leave it untouched.
        private static readonly HashSet<string> _akariOsPreserve = new() { "PRM" };

        // ── Applied-preset stamp ──────────────────────────────────────────────
        // "AkariOS Daily" and stock "Apply Daily" write identical service values
        // (akariOsContext only guards PRM), so registry state alone can never tell
        // them apart. Record which button ran; DetectServicePreset() still decides
        // whether that stamp is still valid.
        //
        // Stamp values: AkariGaming | Gaming | AkariDaily | Daily
        //               AkariMinimal | Minimal | AkariStock | Stock
        //
        // HKLM (not HKCU) because service startup is machine-scoped state — a second
        // user account must see the same answer. Writing needs elevation, which the
        // preset writes already require; a failed stamp degrades to the old unnamed
        // behaviour rather than throwing.
        private const string StampKeyPath = @"SOFTWARE\AkariTool";

        private static void StampPreset(string value, ToolService log)
        {
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(StampKeyPath);
                k?.SetValue("ServicePreset", value, RegistryValueKind.String);
                k?.SetValue("ServicePresetAppliedUtc",
                            DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                log.Log($"[SERVICES] Could not stamp applied preset: {ex.Message}");
            }
        }

        /// <summary>Which preset button was last run, or null if never stamped.</summary>
        public static string? ReadPresetStamp()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(StampKeyPath);
                return k?.GetValue("ServicePreset") as string;
            }
            catch { return null; }
        }

        private static void Apply(Dictionary<string, int> preset, ToolService log, bool akariOsContext = false)
        {
            int ok = 0, skip = 0, fail = 0;

            foreach (var (service, startValue) in preset)
            {
                // AkariOS context: preserve the playbook's value for these (e.g. PRM boot driver)
                if (akariOsContext && _akariOsPreserve.Contains(service)) { skip++; continue; }

                // Never touch Defender or boot-critical drivers
                if (_defenderServices.Contains(service)) { skip++; continue; }

                // Skip ACL-locked services silently — Windows protects these
                if (_aclLockedServices.Contains(service)) { skip++; continue; }

                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(
                        $@"SYSTEM\CurrentControlSet\Services\{service}", writable: true);

                    // Key doesn't exist on this machine — skip silently
                    if (key is null) { skip++; continue; }

                    key.SetValue("Start", startValue, RegistryValueKind.DWord);
                    ok++;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException
                                              or System.Security.SecurityException)
                {
                    // ACL-locked key not in our known list — skip silently.
                    // OpenSubKey(writable: true) raises SecurityException
                    // ("Requested registry access is not allowed."), NOT
                    // UnauthorizedAccessException, so both must be caught or the
                    // key lands in the generic handler and is logged as a failure.
                    skip++;
                }
                catch (Exception ex)
                {
                    log.Log($"[SERVICES] WARNING {service}: {ex.Message}");
                    fail++;
                }
            }

            log.Log($"[SERVICES] Done — {ok} written, {skip} skipped, {fail} failed.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // AKAIOS DEFAULT PRESET
        // Source: AkariOS-Default-services.reg
        // Defender services (WdBoot, WdFilter, WdNisDrv, WdNisSvc, WinDefend,
        // ═════════════════════════════════════════════════════════════════════
        // AKAIROS MINIMAL PRESET
        // Most aggressive runtime-safe service configuration.
        // Extends AkariOS preset with 25 additional kills.
        // Every change is revertable via ApplyStockDefault().
        //
        // Known tradeoffs vs AkariOS:
        //   - ISO mount via Explorer broken (use PowerShell: Mount-DiskImage)
        //   - HWiNFO / AIDA64 sensor readout broken (WmiAcpi=4)
        //   - Wireshark / packet capture broken (NdisCap=4)
        //   - Network usage stats in Settings broken (NDU=4)
        //   - LUA file virtualization broken (luafv=4) — some app compat affected
        //   - Legacy file sharing broken (NetBIOS=4, NetBT=4)
        //
        // Boot drivers (fvevol, storflt, bam, dam, vdrvroot, etc.) are NOT
        // included — they require offline modification to disable safely.
        // ═════════════════════════════════════════════════════════════════════
    }
}