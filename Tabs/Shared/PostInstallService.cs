using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Downloads the entire AkariOS PostInstall folder from GitHub
    /// and mirrors it to C:\PostInstall\ on the local machine.
    ///
    /// Source: https://github.com/isleap9/PostInstall (main branch)
    /// Local root: C:\PostInstall\
    ///
    /// On AkariOS all files are already present — no downloads occur.
    /// On stock Windows the entire folder is downloaded on first use (~30 MB).
    ///
    /// Usage:
    ///   bool ok = await PostInstallService.EnsurePostInstallAsync(log);
    ///   // All PostInstall files are now at C:\PostInstall\
    /// </summary>
    public static class PostInstallService
    {
        // ── GitHub raw base ───────────────────────────────────────────────────
        private const string RawBase =
            "https://raw.githubusercontent.com/isleap9/PostInstall/main/PostInstall/";

        // ── Local root (mirrors AkariOS layout exactly) ───────────────────────
        public static readonly string LocalRoot = @"C:\PostInstall";

        // ── Commonly referenced paths (derived from LocalRoot) ────────────────
        public static string MinSudoPath => Path.Combine(LocalRoot, @"Tweaks\MinSudo.exe");
        public static string NSudoPath => Path.Combine(LocalRoot, @"Tweaks\NSudo.exe");
        public static string PowerRunPath => Path.Combine(LocalRoot, @"Tweaks\PowerRun.exe");
        public static string NoDefenderPath => Path.Combine(LocalRoot, @"Defender\NoDefender.cab");

        // ── Complete file list (relative paths under PostInstall/) ────────────
        // Generated from: github.com/isleap9/PostInstall tree (148 files, ~30 MB)
        private static readonly string[] AllFiles =
        {
            "AntiCheat/Disable DEP NX (default).bat",
            "AntiCheat/Enable DEP NX (needed for faceit).bat",
            "AntiCheat/Set DEP NX to optin (should work for valorant).bat",
            "Change Username/Change Name.bat",
            "Defender/DisableDefender.ps1",
            "Defender/DisableDefenderServices.bat",
            "Defender/EnableDefender.ps1",
            "Defender/EnableDefenderServices.bat",
            "Defender/NoDefender.cab",
            "GPU/AMD/AMD Dwords by imribiy.bat",
            "GPU/AMD/Mpo_disable.reg",
            "GPU/AMD/disable_dx11navi.exe",
            "GPU/AMD/radeon software slimmer/7-Zip/7z.dll",
            "GPU/AMD/radeon software slimmer/7-Zip/7z.exe",
            "GPU/AMD/radeon software slimmer/7-Zip/License.txt",
            "GPU/AMD/radeon software slimmer/ControlzEx.dll",
            "GPU/AMD/radeon software slimmer/MahApps.Metro.IconPacks.Core.dll",
            "GPU/AMD/radeon software slimmer/MahApps.Metro.IconPacks.FontAwesome.dll",
            "GPU/AMD/radeon software slimmer/MahApps.Metro.dll",
            "GPU/AMD/radeon software slimmer/Microsoft.Win32.Registry.dll",
            "GPU/AMD/radeon software slimmer/Microsoft.Win32.TaskScheduler.dll",
            "GPU/AMD/radeon software slimmer/Microsoft.Xaml.Behaviors.dll",
            "GPU/AMD/radeon software slimmer/Newtonsoft.Json.dll",
            "GPU/AMD/radeon software slimmer/RadeonSoftwareSlimmer.exe",
            "GPU/AMD/radeon software slimmer/RadeonSoftwareSlimmer.exe.config",
            "GPU/AMD/radeon software slimmer/RadeonSoftwareSlimmer.pdb",
            "GPU/AMD/radeon software slimmer/System.Diagnostics.EventLog.dll",
            "GPU/AMD/radeon software slimmer/System.IO.Abstractions.dll",
            "GPU/AMD/radeon software slimmer/System.Security.AccessControl.dll",
            "GPU/AMD/radeon software slimmer/System.Security.Principal.Windows.dll",
            "GPU/AMD/radeon software slimmer/System.ServiceProcess.ServiceController.dll",
            "GPU/AMD/radeon software slimmer/TestableIO.System.IO.Abstractions.Wrappers.dll",
            "GPU/AMD/radeon software slimmer/TestableIO.System.IO.Abstractions.dll",
            "GPU/AMD/radeon software slimmer/de/MahApps.Metro.resources.dll",
            "GPU/AMD/radeon software slimmer/de/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/es/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/fr/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/it/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/pl/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/ru/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/zh-CN/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/AMD/radeon software slimmer/zh-Hant/Microsoft.Win32.TaskScheduler.resources.dll",
            "GPU/Nvidia/!Disable HDCP.bat",
            "GPU/Nvidia/!Disable telemetry (Breaks Geforce).bat",
            "GPU/Nvidia/!No ECC.bat",
            "GPU/Nvidia/!P-State 0.bat",
            "GPU/Nvidia/!Unrestricted Clock Policy by Cancerogeno.bat",
            "GPU/Nvidia/NIP/Reference.xml",
            "GPU/Nvidia/NIP/Settings.nip",
            "GPU/Nvidia/NIP/nvidiaProfileInspector.exe",
            "GPU/Nvidia/NVCleanstall_1.18.0.exe",
            "GPU/Nvidia/Nvidia Debloating Guide by AMIT.url",
            "GPU/Nvidia/mpo disable.bat",
            "GPU/Nvidia/mpo enable.bat",
            "Mitigations/InSpectre.exe",
            "Others/Action Center/Disable_Action_Center(default).reg",
            "Others/Action Center/Enable_Action_Center.reg",
            "Others/Alt-Tab/Classic-AltTab(Default).bat",
            "Others/Alt-Tab/Immersive-AltTab.bat",
            "Others/Boot Configuration/bootmenupolicy legacy(Default).bat",
            "Others/Boot Configuration/bootmenupolicy-standard.bat",
            "Others/DMA remapping/Disable DMA Remapping (can apply to more stuff than done by script).bat",
            "Others/DMA remapping/Enable DMA Remapping.bat",
            "Others/Drives/support windows on ssd(Default).reg",
            "Others/Drives/support-windows-on-hdd.reg",
            "Others/Network/AkariOS Default Network Settings.bat",
            "Others/Network/Revert Network Tweaks.bat",
            "Others/Network/Run this if you had to install a network driver.bat",
            "Others/SerializeTimerExpiration/Disable SerializeTimerExpiration.reg",
            "Others/SerializeTimerExpiration/Enable SerializeTimerExpiration.reg",
            "Others/Startmenu/disable startmenu 10 and server.bat",
            "Others/Startmenu/disable startmenu 11.bat",
            "Others/Startmenu/enable startmenu 11.bat",
            "Others/Startmenu/enable startmenu.bat",
            "Others/SvcHostSplitThresholdInKB/Set SvcHostSplitThresholdInKB To Default Value.bat",
            "Others/SvcHostSplitThresholdInKB/Set SvcHostSplitThresholdInKB To ffffffff.bat",
            "Resync/resync.bat",
            "Services/AkariOS-Default-services.reg",
            "Services/Windows-Default-services.reg",
            "Services/AkariOS-Daily-services.reg",
            "Services/exes enable.bat",
            "Services/exes.bat",
            "Services/minimal-services.reg",
            "Tweaks/!Bufferbloat Test.url",
            "Tweaks/!Discord Debloat.url",
            "Tweaks/!Network Adapter settings.url",
            "Tweaks/Auto DSCP & FSE.bat",
            "Tweaks/Autoruns.exe",
            "Tweaks/CRU/CRU.exe",
            "Tweaks/CRU/reset-all.exe",
            "Tweaks/CRU/restart.exe",
            "Tweaks/CRU/restart64.exe",
            "Tweaks/Dism++10.1.1002.1B/Dism++x64.exe",
            "Tweaks/Dism++10.1.1002.1B/Config/Languages/en.zip",
            "Tweaks/Dism++10.1.1002.1B/Config/Plugins/FrogPlugins_Pcn7FMvReAsVWfCQBfRJCw/Config.ini",
            "Tweaks/Dism++10.1.1002.1B/Config/Plugins/FrogPlugins_Pcn7FMvReAsVWfCQBfRJCw/Plugin.amd64.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/Plugins/FrogPlugins_Pcn7FMvReAsVWfCQBfRJCw/Plugin.arm64.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/Plugins/FrogPlugins_Pcn7FMvReAsVWfCQBfRJCw/Plugin.x86.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/Plugins/FrogPlugins_Pcn7FMvReAsVWfCQBfRJCw/PluginRes.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/amd64/CBSHost.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/amd64/NCleaner.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/amd64/bcdboot.exe",
            "Tweaks/Dism++10.1.1002.1B/Config/amd64/wimgapi.dll",
            "Tweaks/Dism++10.1.1002.1B/Config/amd64/wofadk.sys",
            "Tweaks/Dism++10.1.1002.1B/Config/Config.ini",
            "Tweaks/Dism++10.1.1002.1B/Config/Data.zip",
            "Tweaks/Dism++10.1.1002.1B/Config/Dism++.log",
            "Tweaks/Dism++10.1.1002.1B/Config/default.ui.zip",
            "Tweaks/Change resolution without immersive cp.lnk",
            "Tweaks/DevManView.exe",
            "Tweaks/DeviceCleanup.exe",
            "Tweaks/DeviceCleanupCmd.exe",
            "Tweaks/Interrupt Affinity Policy Tool.exe",
            "Tweaks/MSI Mode Utility.exe",
            "Tweaks/MeasureSleep.exe",
            "Tweaks/MinSudo.exe",
            "Tweaks/Mouse Movement Recorder.exe",
            "Tweaks/Mouse Polling Test/MouseTester.exe",
            "Tweaks/NSudo.exe",
            "Tweaks/PowerRun.exe",
            "Tweaks/PowerSettingsExplorer.exe",
            "Tweaks/Process explorer/Process Explorer.exe",
            "Tweaks/ReservedCpuSets.dll",
            "Tweaks/ReservedCpuSets.exe",
            "Tweaks/SCEWIN/5.05.01.0002/Export.bat",
            "Tweaks/SCEWIN/5.05.01.0002/Import.bat",
            "Tweaks/SCEWIN/5.05.01.0002/SCEWIN_64.exe",
            "Tweaks/SCEWIN/5.05.01.0002/amifldrv64.sys",
            "Tweaks/SCEWIN/5.05.01.0002/amigendrv64.sys",
            "Tweaks/hidusbf/DRIVER/1kHz.cmd",
            "Tweaks/hidusbf/DRIVER/2kHz-4kHz.cmd",
            "Tweaks/hidusbf/DRIVER/4kHz-8kHz.cmd",
            "Tweaks/hidusbf/DRIVER/98ME/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64/1khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64/2khz-4khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64/4khz-8khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64/nopatch/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64_AS/1khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64_AS/2khz-4khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64_AS/4khz-8khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64_AS/NoPatch/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/AMD64_AS/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/HIDUSBF.INF",
            "Tweaks/hidusbf/DRIVER/HIDUSBFU.INF",
            "Tweaks/hidusbf/DRIVER/HIDUSBF_AS.INF",
            "Tweaks/hidusbf/DRIVER/NTX86/1khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTX86/2khz-4khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTX86/4khz-8khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTX86/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTX86/nopatch/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTx86_AS/1khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTx86_AS/2khz-4khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTx86_AS/4khz-8khz/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTx86_AS/NoPatch/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/NTx86_AS/hidusbf.sys",
            "Tweaks/hidusbf/DRIVER/Setup.exe",
            "Tweaks/hidusbf/DRIVER/nopatch.cmd",
            "Tweaks/hidusbf/DRIVER/sx64.exe",
            "Tweaks/hidusbf/README.2kHz-8kHz.ENG.TXT",
            "Tweaks/hidusbf/README.ENG.TXT",
            "Tweaks/hidusbf/README.RUS.TXT",
            "Tweaks/hidusbf/SweetLow.CER",
            "Tweaks/serviwin.exe",
        };

        private static HttpClient? _http;
        private static HttpClient Http => _http ??= new HttpClient(new HttpClientHandler
        {
            UseProxy = false,          // skip system proxy (important on VMs)
            AllowAutoRedirect = true,
        })
        {
            DefaultRequestHeaders = { { "User-Agent", "AkariTool" } },
            Timeout = TimeSpan.FromMinutes(10),
        };

        // ── Public API ────────────────────────────────────────────────────────

        public static bool MinSudoPresent => File.Exists(MinSudoPath);
        public static bool NSudoPresent => File.Exists(NSudoPath);
        public static bool PowerRunPresent => File.Exists(PowerRunPath);
        public static bool NoDefenderPresent => File.Exists(NoDefenderPath);

        /// <summary>
        /// True when the entire PostInstall folder is present locally.
        /// Checks every file in AllFiles against C:\PostInstall\.
        /// </summary>
        public static bool IsFullyInstalled =>
            AllFiles.All(f => File.Exists(Path.Combine(LocalRoot, f.Replace('/', '\\'))));

        /// <summary>
        /// Ensures all Defender-related files are present.
        /// Downloads the full PostInstall folder if anything is missing.
        /// AkariOS users skip this entirely.
        /// </summary>
        public static async Task<bool> EnsureDefenderFilesAsync(ToolService log)
        {
            // Check the files the Defender toggle specifically needs
            bool defenderReady =
                MinSudoPresent &&
                PowerRunPresent &&
                File.Exists(Path.Combine(LocalRoot, @"Defender\NoDefender.cab")) &&
                File.Exists(Path.Combine(LocalRoot, @"Defender\DisableDefender.ps1")) &&
                File.Exists(Path.Combine(LocalRoot, @"Defender\DisableDefenderServices.bat")) &&
                File.Exists(Path.Combine(LocalRoot, @"Defender\EnableDefender.ps1")) &&
                File.Exists(Path.Combine(LocalRoot, @"Defender\EnableDefenderServices.bat"));

            if (defenderReady)
            {
                log.Log("[POSTINSTALL] All Defender files already present.");
                return true;
            }

            return await EnsurePostInstallAsync(log);
        }

        /// <summary>
        /// Downloads the entire PostInstall folder to C:\PostInstall\.
        /// Skips files that already exist (idempotent — safe to call repeatedly).
        /// On AkariOS all files exist — returns immediately.
        /// </summary>
        public static async Task<bool> EnsurePostInstallAsync(ToolService log)
        {
            if (IsFullyInstalled)
            {
                log.Log("[POSTINSTALL] PostInstall folder already complete.");
                return true;
            }

            log.Log("[POSTINSTALL] Downloading PostInstall folder from GitHub (~30 MB)...");

            int downloaded = 0, skipped = 0, failed = 0;

            foreach (var relativePath in AllFiles)
            {
                var localPath = Path.Combine(
                    LocalRoot,
                    relativePath.Replace('/', '\\'));

                // Skip files that already exist
                if (File.Exists(localPath))
                {
                    skipped++;
                    continue;
                }

                // Encode spaces and special chars for the URL
                var urlPath = string.Join("/",
                    relativePath.Split('/').Select(Uri.EscapeDataString));
                var url = RawBase + urlPath;

                var label = Path.GetFileName(relativePath);
                bool ok = await DownloadFileAsync(log, url, localPath, label);
                if (ok) downloaded++;
                else failed++;
            }

            log.Log($"[POSTINSTALL] Done — {downloaded} downloaded, {skipped} already present, {failed} failed.");

            if (failed > 0)
                log.Log("[POSTINSTALL] Some files failed. Check your internet connection and try again.");

            return failed == 0;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static async Task<bool> DownloadFileAsync(
            ToolService log, string url, string destPath, string label)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                var bytes = await Http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(destPath, bytes);
                log.Log($"[POSTINSTALL] ✓ {label} ({bytes.Length / 1024} KB)");
                return true;
            }
            catch (Exception ex)
            {
                log.Log($"[POSTINSTALL] ✗ {label}: {ex.Message}");
                return false;
            }
        }
    }
}