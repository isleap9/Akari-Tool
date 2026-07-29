using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// Native replacements for the GPU batch files that used to live in C:\PostInstall.
    /// Every method catches everything, logs, and never throws.
    /// </summary>
    public static class GpuTweaks
    {
        private const string VideoClass =
            @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";

        // ── NVIDIA ────────────────────────────────────────────────────────

        /// <summary>
        /// NVIDIA P-State 0 — writes DisableDynamicPstate=1 on every video controller's driver key.
        /// Replaces the WMI/reg-query batch loop with native enumeration.
        /// </summary>
        public static void SetPState0(Action<string> log)
        {
            try
            {
                var driverKeys = new List<string>();

                using (var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        using (mo)
                        {
                            var pnp = mo["PNPDeviceID"] as string;
                            if (string.IsNullOrEmpty(pnp) ||
                                !pnp.StartsWith(@"PCI\VEN_", StringComparison.OrdinalIgnoreCase))
                                continue;

                            // The Enum entry points at the adapter's Control\Class subkey ("{GUID}\0000").
                            using var enumKey = Registry.LocalMachine.OpenSubKey(
                                @"SYSTEM\ControlSet001\Enum\" + pnp);
                            if (enumKey?.GetValue("Driver") is string driver && driver.Length > 0)
                                driverKeys.Add(driver);
                        }
                    }
                }

                if (driverKeys.Count == 0)
                {
                    log("P-State 0: no PCI video adapters with a driver key were found.");
                    return;
                }

                int written = 0;
                ElevationService.RunAsSystem(() =>
                {
                    foreach (var driver in driverKeys)
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\" + driver,
                            "DisableDynamicPstate", 1, RegistryValueKind.DWord);
                        written++;
                    }
                }, log);

                log($"P-State 0 applied to {written} adapter(s). Restart required.");
            }
            catch (Exception ex) { log($"ERROR SetPState0: {ex.Message}"); }
        }

        /// <summary>
        /// Every PCI video adapter's Control\Class driver key ("{GUID}\NNNN").
        /// Shared by the tweaks that must hit every adapter rather than guessing an
        /// index — the enumeration the batch files did with wmic + reg query.
        /// </summary>
        public static IReadOnlyList<string> EnumerateAdapterDriverKeys()
        {
            var driverKeys = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
                using var results = searcher.Get();

                foreach (ManagementObject mo in results)
                {
                    using (mo)
                    {
                        var pnp = mo["PNPDeviceID"] as string;
                        if (string.IsNullOrEmpty(pnp) ||
                            !pnp.StartsWith(@"PCI\VEN_", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // CurrentControlSet, not the batch's hardcoded ControlSet001 —
                        // the running set is the one whose values take effect.
                        using var enumKey = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Enum\" + pnp);
                        if (enumKey?.GetValue("Driver") is string driver && driver.Length > 0)
                            driverKeys.Add(driver);
                    }
                }
            }
            catch { /* enumeration failure yields an empty list, never throws */ }

            return driverKeys;
        }

        /// <summary>
        /// HDCP on/off across every PCI video adapter — the native replacement for
        /// "!Disable HDCP.bat", which was a wmic + reg query + Reg.exe add loop
        /// writing a single value.
        ///
        /// Disable writes RMHdcpKeyglobZero=1; enable writes 0 to the SAME enumerated
        /// keys, so revert is a true inverse rather than the old hardcoded \0001 write.
        /// Plain Administrator writes are attempted first; SYSTEM impersonation is used
        /// only if the Class key denies access.
        /// </summary>
        /// <returns>Number of adapters written, or -1 on failure.</returns>
        public static int SetHdcpDisabled(bool disable, Action<string> log)
        {
            try
            {
                var driverKeys = EnumerateAdapterDriverKeys();
                if (driverKeys.Count == 0)
                {
                    log("HDCP: no PCI video adapters with a driver key were found.");
                    return -1;
                }

                int value = disable ? 1 : 0;
                int written = 0;
                bool needsElevation = false;

                foreach (var driver in driverKeys)
                {
                    try
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\" + driver,
                            "RMHdcpKeyglobZero", value, RegistryValueKind.DWord);
                        written++;
                    }
                    catch (UnauthorizedAccessException) { needsElevation = true; }
                    catch (System.Security.SecurityException) { needsElevation = true; }
                }

                if (needsElevation)
                {
                    // The action must stay fully synchronous — impersonation is
                    // per-thread and an await could resume unelevated.
                    log("HDCP: Class key denied Administrator access, retrying as SYSTEM…");
                    ElevationService.RunAsSystem(() =>
                    {
                        foreach (var driver in driverKeys)
                        {
                            try
                            {
                                Registry.SetValue(
                                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\" + driver,
                                    "RMHdcpKeyglobZero", value, RegistryValueKind.DWord);
                                written++;
                            }
                            catch { }
                        }
                    }, log);
                }

                return written;
            }
            catch (Exception ex)
            {
                log($"ERROR SetHdcpDisabled: {ex.Message}");
                return -1;
            }
        }

        /// <summary>NVIDIA telemetry off — 2 registry values.</summary>
        public static void DisableNvidiaTelemetry(Action<string> log)
        {
            try
            {
                ElevationService.RunAsSystem(() =>
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\NVIDIA Corporation\NvControlPanel2\Client",
                        "OptInOrOutPreference", 0, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\Startup",
                        "SendTelemetryData", 0, RegistryValueKind.DWord);
                }, log);

                log("NVIDIA telemetry disabled (2 values written).");
            }
            catch (Exception ex) { log($"ERROR DisableNvidiaTelemetry: {ex.Message}"); }
        }

        /// <summary>Runs nvidia-smi with the given args from the NVIDIA driver's own NVSMI folder.</summary>
        private static void RunNvidiaSmi(string args, string label, Action<string> log)
        {
            try
            {
                const string smi = @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe";
                if (!File.Exists(smi))
                {
                    // NVSMI ships only with the full driver package, not with every release.
                    log("nvidia-smi not found — this requires the full NVIDIA driver package (NVSMI component).");
                    return;
                }

                using var p = Process.Start(new ProcessStartInfo(smi, args)
                {
                    UseShellExecute = true,
                    Verb            = "runas",
                    WindowStyle     = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetDirectoryName(smi)!
                });
                p?.WaitForExit();
                log($"{label}: nvidia-smi {args} exited with code {p?.ExitCode ?? -1}.");
            }
            catch (Exception ex) { log($"ERROR {label}: {ex.Message}"); }
        }

        public static void DisableEcc(Action<string> log) => RunNvidiaSmi("-e 0", "Disable ECC", log);

        public static void UnrestrictClockPolicy(Action<string> log) =>
            RunNvidiaSmi("-acp 0", "Unrestricted clock policy", log);

        // ── AMD ───────────────────────────────────────────────────────────

        /// <summary>
        /// AMD Adrenalin tuning — ports the HKCU AMD\CN, AMD\DVR, ATI\ACE and HKLM values
        /// from "AMD Dwords by imribiy.bat".
        /// </summary>
        public static void ApplyAmdDwords(Action<string> log)
        {
            int written = 0;
            try
            {
                // The tool runs elevated, so HKCU here is the invoking user's hive — same as
                // the other HKCU tweaks in this codebase.
                const string cn  = @"HKEY_CURRENT_USER\Software\AMD\CN";
                const string dvr = @"HKEY_CURRENT_USER\Software\AMD\DVR";

                void Set(string key, string name, object data, RegistryValueKind kind)
                {
                    Registry.SetValue(key, name, data, kind);
                    written++;
                }

                Set(cn, "AutoUpdateTriggered",        0,                 RegistryValueKind.DWord);
                Set(cn, "PowerSaverAutoEnable_CUR",   0,                 RegistryValueKind.DWord);
                Set(cn, "BuildType",                  0,                 RegistryValueKind.DWord);
                Set(cn, "WizardProfile",              "PROFILE_CUSTOM",  RegistryValueKind.String);
                Set(cn, "UserTypeWizardShown",        1,                 RegistryValueKind.DWord);
                Set(cn, "AutoUpdate",                 0,                 RegistryValueKind.DWord);
                Set(cn, "RSXBrowserUnavailable",      "true",            RegistryValueKind.String);
                Set(cn, "SystemTray",                 "false",           RegistryValueKind.String);
                Set(cn, "AllowWebContent",            "false",           RegistryValueKind.String);
                Set(cn, "CN_Hide_Toast_Notification", "true",            RegistryValueKind.String);
                Set(cn, "AnimationEffect",            "false",           RegistryValueKind.String);
                Set(cn + @"\OverlayNotification",     "AlreadyNotified", 1, RegistryValueKind.DWord);
                Set(cn + @"\VirtualSuperResolution",  "AlreadyNotified", 1, RegistryValueKind.DWord);

                Set(dvr, "PerformanceMonitorOpacityWA", 0,      RegistryValueKind.DWord);
                Set(dvr, "DvrEnabled",                  1,      RegistryValueKind.DWord);
                Set(dvr, "ActiveSceneId",               "0",    RegistryValueKind.String);
                Set(dvr, "PrevInstantReplayEnable",     0,      RegistryValueKind.DWord);
                Set(dvr, "PrevInGameReplayEnabled",     0,      RegistryValueKind.DWord);
                Set(dvr, "PrevInstantGifEnabled",       0,      RegistryValueKind.DWord);
                Set(dvr, "RemoteServerStatus",          0,      RegistryValueKind.DWord);
                Set(dvr, "ShowRSOverlay",               "false", RegistryValueKind.String);

                Set(@"HKEY_CURRENT_USER\Software\ATI\ACE\Settings\ADL\AppProfiles",
                    "AplReloadCounter", 0, RegistryValueKind.DWord);

                ElevationService.RunAsSystem(() =>
                {
                    Set(@"HKEY_LOCAL_MACHINE\Software\AMD\Install", "AUEP",            1, RegistryValueKind.DWord);
                    Set(@"HKEY_LOCAL_MACHINE\Software\AUEP",        "RSX_AUEPStatus",  2, RegistryValueKind.DWord);
                    Set(VideoClass, "NotifySubscription",  new byte[] { 0x30, 0x00 },             RegistryValueKind.Binary);
                    Set(VideoClass, "IsComponentControl",  new byte[] { 0x00, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                }, log);

                log($"AMD DWORDS applied — {written} value(s) written. Restart Adrenalin to see the changes.");
            }
            catch (Exception ex) { log($"ERROR ApplyAmdDwords: {ex.Message} ({written} value(s) written before the failure)"); }
        }
    }
}
