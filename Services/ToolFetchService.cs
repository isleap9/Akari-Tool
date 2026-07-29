using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AkariTool.Services
{
    /// <summary>
    /// Downloads tools on demand from the PostInstall repo, caches them under
    /// %LOCALAPPDATA%\AkariTool\Tools\ and launches them. Second launch is offline.
    /// </summary>
    public static class ToolFetchService
    {
        private const string Base =
            "https://raw.githubusercontent.com/isleap9/PostInstall/main/PostInstall/";

        /// <summary>Name = cache folder name, Remote = path under Base, ExePath = file to launch inside the extracted zip.</summary>
        public readonly record struct ToolEntry(string Name, string Remote, string ExePath);

        public static readonly Dictionary<string, ToolEntry> Tools = new()
        {
            ["Autoruns"]          = new("Autoruns",          "_bundles/Autoruns.zip",                       "Autoruns.exe"),
            ["CRU"]               = new("CRU",               "_bundles/CRU.zip",                            @"CRU\CRU.exe"),
            ["DevManView"]        = new("DevManView",        "_bundles/DevManView.zip",                     "DevManView.exe"),
            ["DeviceCleanup"]     = new("DeviceCleanup",     "_bundles/DeviceCleanup.zip",                  "DeviceCleanup.exe"),
            ["InSpectre"]         = new("InSpectre",         "_bundles/InSpectre.zip",                      "InSpectre.exe"),
            ["MeasureSleep"]      = new("MeasureSleep",      "_bundles/MeasureSleep.zip",                   "MeasureSleep.exe"),
            ["ReservedCpuSets"]   = new("ReservedCpuSets",   "_bundles/ReservedCpuSets.zip",                "ReservedCpuSets.exe"),
            ["ServiWin"]          = new("ServiWin",          "_bundles/serviwin.zip",                       "serviwin.exe"),
            ["HidUsbF"]           = new("HidUsbF",           "_bundles/hidusbf.zip",                        @"hidusbf\DRIVER\Setup.exe"),
            ["MouseTester"]       = new("MouseTester",       "_bundles/Mouse Polling Test.zip",             @"Mouse Polling Test\MouseTester.exe"),
            ["ProcessExplorer"]   = new("ProcessExplorer",   "_bundles/Process explorer.zip",               @"Process explorer\Process Explorer.exe"),
            ["InterruptAffinity"] = new("InterruptAffinity", "_bundles/Interrupt Affinity Policy Tool.zip", "Interrupt Affinity Policy Tool.exe"),
            ["DismPP"]            = new("DismPP",            "_bundles/Dism++10.1.1002.1B.zip",             @"Dism++10.1.1002.1B\Dism++x64.exe"),
            ["AutoDSCP"]          = new("AutoDSCP",          "Tweaks/Auto DSCP & FSE.bat",                  "Auto DSCP & FSE.bat"),
            ["NVCleanstall"]      = new("NVCleanstall",      "_bundles/NVCleanstall.zip",                   "NVCleanstall*.exe"),
            ["RadeonSlimmer"]     = new("RadeonSlimmer",     "_bundles/RadeonSoftwareSlimmer.zip",          @"radeon software slimmer\RadeonSoftwareSlimmer.exe"),
            ["DisableDx11Navi"]   = new("DisableDx11Navi",   "_bundles/DisableDx11Navi.zip",                "disable_dx11navi.exe"),
        };

        private static string CacheRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "AkariTool", "Tools");

        /// <summary>Ensures the tool is cached (downloading + extracting if needed), then launches it. Safe to call repeatedly.</summary>
        public static async Task LaunchAsync(string key, ToolService log)
        {
            if (!Tools.TryGetValue(key, out var tool))
            {
                log?.Log($"Unknown tool '{key}'.");
                return;
            }

            var cacheDir  = Path.Combine(CacheRoot, tool.Name);
            var targetExe = Path.Combine(cacheDir, tool.ExePath);

            var cached = Resolve(cacheDir, tool.ExePath);
            if (cached != null)
            {
                if (Launch(cached, log))
                    log?.Log($"Launching {tool.Name} (cached).");
                return;
            }

            string? temp = null;
            try
            {
                Directory.CreateDirectory(cacheDir);

                var url = Base + string.Join("/", tool.Remote.Split('/').Select(Uri.EscapeDataString));
                temp = Path.Combine(Path.GetTempPath(), $"akari_{Guid.NewGuid():N}.tmp");

                log?.Log($"Downloading {tool.Name}…");
                try
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("AkariTool");
                    using var resp = await http.GetAsync(url).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    await using var fs = File.Create(temp);
                    await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log?.Log($"Could not download {tool.Name} — check your internet connection and try again. ({ex.Message})");
                    return;
                }

                if (tool.Remote.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    ZipFile.ExtractToDirectory(temp, cacheDir, overwriteFiles: true);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetExe)!);
                    File.Copy(temp, targetExe, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                log?.Log($"ERROR preparing {tool.Name}: {ex.Message}");
                return;
            }
            finally
            {
                try { if (temp != null && File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
            }

            var resolved = Resolve(cacheDir, tool.ExePath);
            if (resolved == null)
            {
                log?.Log($"Downloaded {tool.Name} but could not find {tool.ExePath} inside it.");
                return;
            }

            if (Launch(resolved, log))
                log?.Log($"Downloaded and launched {tool.Name}.");
        }

        /// <summary>
        /// Resolves <paramref name="exePath"/> inside the cache folder, or null when absent.
        /// A '*' anywhere in the path makes it a recursive search pattern (bundles whose exe
        /// carries a version number, e.g. NVCleanstall_1.19.0.exe); otherwise it is a literal path.
        /// </summary>
        private static string? Resolve(string cacheDir, string exePath)
        {
            try
            {
                if (!exePath.Contains('*'))
                {
                    var literal = Path.Combine(cacheDir, exePath);
                    return File.Exists(literal) ? literal : null;
                }

                if (!Directory.Exists(cacheDir)) return null;
                var pattern = Path.GetFileName(exePath);
                return Directory.EnumerateFiles(cacheDir, pattern, SearchOption.AllDirectories)
                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                .FirstOrDefault();
            }
            catch { return null; }
        }

        private static bool Launch(string exe, ToolService log)
        {
            try
            {
                // UseShellExecute so UAC elevation prompts work for tools that need admin.
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute  = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)!
                });
                return true;
            }
            catch (Exception ex)
            {
                log?.Log($"ERROR launching {Path.GetFileName(exe)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Deletes the whole tool cache. For a future "clear cache" button.</summary>
        public static void ClearCache(Action<string> log)
        {
            try
            {
                if (Directory.Exists(CacheRoot))
                {
                    Directory.Delete(CacheRoot, recursive: true);
                    log?.Invoke("Tool cache cleared.");
                }
                else log?.Invoke("Tool cache is already empty.");
            }
            catch (Exception ex) { log?.Invoke($"ERROR clearing tool cache: {ex.Message}"); }
        }
    }
}
