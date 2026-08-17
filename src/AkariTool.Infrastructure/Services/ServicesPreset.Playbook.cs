using System.Diagnostics;
using System.Globalization;
using System.IO;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// AkariOS Playbook Services — apply a service-startup list shipped by the AME
    /// Playbook (<c>C:\PostInstall\Services\*.reg</c>).
    ///
    /// The apply mechanism (<see cref="ApplyPlaybookFileAsync"/>) replicates the old AkariOS
    /// Companion's PROVEN sequence byte-for-byte: a temp <c>.bat</c> run under
    /// <c>MinSudo.exe --System --Privileged --NoLogo</c> that restores the three renamed
    /// System32 files (via bundled <c>NSudo.exe -U:S -P:E … ren</c>) then imports the whole
    /// file with a single native <c>regedit /s</c>. Earlier native attempts
    /// (<c>ElevationService.RunAsSystem</c> + per-key <c>Registry.SetValue</c>, even with
    /// SeBackup/SeRestore enabled) did NOT reproduce the Companion's result on real AkariOS
    /// hardware — ISO mounting via Daily / Windows Default still failed — so that path was
    /// removed in favour of MinSudo + NSudo + regedit /s. <see cref="StampPreset"/> is still
    /// reused (same partial class); <see cref="ParseRegFile"/> stays available but is no
    /// longer used by the apply path (regedit /s imports the real file directly).
    ///
    /// This is ADDITIVE and independent of the built-in Service Preset buttons
    /// (Apply Gaming / Daily / Restore Stock) — it neither changes nor depends on them.
    ///
    /// GATING is the caller's job via <see cref="IsAkariOsVolume"/>: the whole feature is
    /// only active when the C: volume label is exactly "AkariOS" (the AME Playbook stamps
    /// this at image-build time). Nothing here writes anything until the caller has gated.
    /// </summary>
    public static partial class ServicesPreset
    {
        /// <summary>Where the AME Playbook drops its service-list .reg files.</summary>
        public const string PlaybookServicesDir = @"C:\PostInstall\Services";

        /// <summary>The volume label the AME Playbook stamps on the system drive.</summary>
        private const string AkariOsVolumeLabel = "AkariOS";

        /// <summary>
        /// Curated, friendly dropdown labels for KNOWN playbook files, keyed on the filename
        /// minus ".reg". Presentation only — the on-disk filename is still what gets parsed and
        /// applied. Files NOT listed here fall back to their raw filename (discovery stays
        /// authoritative), so a new playbook file added later still appears, just unlabelled.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> PlaybookDisplayNameOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AkariOS-Daily-services"]   = "AkariOS Daily",
                ["AkariOS-Default-services"] = "AkariOS Gaming",
                ["Windows-Default-services"] = "Windows Default",
            };

        /// <summary>
        /// Playbook files hidden from the dropdown entirely (keyed on filename minus ".reg").
        /// Skipped during discovery so they never appear.
        /// </summary>
        public static readonly IReadOnlySet<string> PlaybookExcludedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "minimal-services" };

        /// <summary>
        /// Friendly label for a discovered file: the curated override when known, otherwise the
        /// raw filename-minus-extension. Cosmetic only — never used to construct a path.
        /// </summary>
        public static string PlaybookDisplayName(string rawNameWithoutExtension) =>
            PlaybookDisplayNameOverrides.TryGetValue(rawNameWithoutExtension, out var friendly)
                ? friendly
                : rawNameWithoutExtension;

        /// <summary>
        /// True only on an actual AkariOS-built machine — C: volume label == "AkariOS"
        /// (case-insensitive). Deliberately STRONGER than "the Services folder exists":
        /// a stock Windows user could have downloaded PostInstall via the app's own button,
        /// and that must not enable this feature. Any error (inaccessible/unlabeled drive)
        /// is treated as "not AkariOS" — never throws.
        /// </summary>
        public static bool IsAkariOsVolume()
        {
            try
            {
                var label = new DriveInfo("C:").VolumeLabel;
                return string.Equals(label, AkariOsVolumeLabel, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Full paths of every <c>*.reg</c> in <see cref="PlaybookServicesDir"/>, sorted by
        /// name. Empty if the folder is missing or unreadable — callers gate on
        /// <see cref="IsAkariOsVolume"/> FIRST; folder/file presence only matters after that.
        /// The only names filtered out are <see cref="PlaybookExcludedFiles"/> (hidden entirely);
        /// everything else is returned regardless of whether it has a curated display name.
        /// </summary>
        public static IReadOnlyList<string> DiscoverPlaybookServiceFiles()
        {
            try
            {
                if (!Directory.Exists(PlaybookServicesDir)) return Array.Empty<string>();
                return Directory
                    .GetFiles(PlaybookServicesDir, "*.reg", SearchOption.TopDirectoryOnly)
                    .Where(p => !PlaybookExcludedFiles.Contains(Path.GetFileNameWithoutExtension(p)))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Parses a standard <c>.reg</c> (v5.00) service file into
        /// <c>{ serviceName -> Start value }</c>.
        ///
        /// Tracks the current service from each
        /// <c>[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\NAME]</c> key line and
        /// reads the following <c>"Start"=dword:XXXXXXXX</c>. A service block with no
        /// <c>Start</c> line is skipped. Subkey blocks (…\Services\NAME\Parameters) are not
        /// treated as services. Tolerant of the <c>\r\n</c> + trailing-space formatting the
        /// real files use (every line is trimmed before matching).
        /// </summary>
        public static Dictionary<string, int> ParseRegFile(string path)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            const string servicesMarker = @"\Services\";
            const string dwordMarker = "dword:";

            string? current = null;

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                // Key line: [ ...\Services\NAME ]  — sets/clears the current service.
                if (line[0] == '[')
                {
                    current = null;

                    int idx = line.IndexOf(servicesMarker, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;

                    var tail = line.Substring(idx + servicesMarker.Length).TrimEnd(']').Trim();

                    // Only the direct service key counts — not a subkey like ...\NAME\Parameters.
                    if (tail.Length == 0 || tail.Contains('\\')) continue;

                    current = tail;
                    continue;
                }

                if (current is null) continue;

                // "Start"=dword:00000002  (tolerate case + spacing around the value).
                if (line.StartsWith("\"Start\"", StringComparison.OrdinalIgnoreCase))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    var val = line.Substring(eq + 1).Trim();
                    int d = val.IndexOf(dwordMarker, StringComparison.OrdinalIgnoreCase);
                    if (d < 0) continue;

                    var hex = val.Substring(d + dwordMarker.Length).Trim();
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var start))
                        result[current] = start;
                }
            }

            return result;
        }

        /// <summary>
        /// Applies a playbook service list by replicating the old AkariOS Companion's PROVEN
        /// sequence byte-for-byte: a temp <c>.bat</c> run under
        /// <c>MinSudo.exe --System --Privileged --NoLogo cmd /c "temp.bat"</c>, where the batch
        /// restores the three renamed System32 files (each via <c>NSudo.exe -U:S -P:E … ren</c>)
        /// then does the whole import with a single native <c>regedit /s "&lt;file&gt;.reg"</c>.
        ///
        /// This deliberately REPLACES the earlier native <c>ElevationService.RunAsSystem</c> +
        /// per-key <c>Registry.SetValue</c> path, which — even with SeBackup/SeRestore enabled —
        /// did NOT reproduce the Companion's result on real AkariOS hardware (ISO mounting via
        /// Daily / Windows Default still failed). <c>regedit /s</c> on the real file is the one
        /// native Windows operation that does; we no longer parse the file here at all
        /// (<see cref="ParseRegFile"/> stays for other uses).
        ///
        /// Fails loudly (returns false + logs) if MinSudo/NSudo are missing or the run errors —
        /// never a silent no-op. Returns true when the MinSudo process launched and ran to exit.
        /// </summary>
        public static async Task<bool> ApplyPlaybookFileAsync(string path, ToolService log)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            log.Log($"[SERVICES] Applying AkariOS Playbook service list: {name}...");

            // Both external tools must be present — fail loudly, never silently no-op.
            if (!PostInstallService.MinSudoPresent || !PostInstallService.NSudoPresent)
            {
                log.Log("[SERVICES] MinSudo.exe/NSudo.exe not found — cannot apply playbook service list.");
                return false;
            }

            if (!File.Exists(path))
            {
                log.Log($"[SERVICES] Service list not found on disk: {path}");
                return false;
            }

            // The exact Companion batch: exes-enable (NSudo renames) → regedit /s. %WINDIR%
            // resolved to the real path; NSudo from PostInstall; the actual selected .reg file
            // as the final import target (regedit /s imports the whole file natively).
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string nsudo  = PostInstallService.NSudoPath;

            string batch =
                "@echo off\r\n" +
                "taskkill /f /im ctfmon.exe\r\n" +
                $"\"{nsudo}\" -U:S -P:E cmd.exe /c ren \"{windir}\\System32\\ctfmon.exee\" \"ctfmon.exe\"\r\n" +
                "taskkill /f /im TextInputHost.exe\r\n" +
                $"\"{nsudo}\" -U:S -P:E cmd.exe /c ren \"{windir}\\SystemApps\\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\\TextInputHost.exee\" \"TextInputHost.exe\"\r\n" +
                "taskkill /f /im backgroundTaskHost.exe\r\n" +
                $"\"{nsudo}\" -U:S -P:E cmd.exe /c ren \"{windir}\\System32\\backgroundTaskHost.exee\" \"backgroundTaskHost.exe\"\r\n" +
                $"regedit /s \"{path}\"\r\n";

            // Explicit .bat extension — cmd /c will not execute a Path.GetTempFileName() .tmp as a script.
            string tempBat = Path.Combine(Path.GetTempPath(), $"AkariPlaybook-{Guid.NewGuid():N}.bat");

            try
            {
                await File.WriteAllTextAsync(tempBat, batch);
                log.Log($"[SERVICES] temp batch file at: {tempBat}");
                log.Log($"[SERVICES] batch content:\r\n{batch}");

                string minsudo = PostInstallService.MinSudoPath;
                string args = $"--System --Privileged --NoLogo cmd /c \"{tempBat}\"";
                log.Log($"[SERVICES] argv: [{minsudo} {args}]");

                var psi = new ProcessStartInfo
                {
                    FileName               = minsudo,
                    Arguments              = args,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };

                using var proc = Process.Start(psi);
                if (proc is null)
                {
                    log.Log("[SERVICES] Failed to start MinSudo.exe — Process.Start returned null.");
                    return false;
                }

                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                int exit = proc.ExitCode;
                log.Log($"[SERVICES] exit code: {exit}");
                if (!string.IsNullOrWhiteSpace(stdout)) log.Log($"[SERVICES] stdout: {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr)) log.Log($"[SERVICES] stderr: {stderr.Trim()}");
                if (exit != 0)
                    log.Log($"[SERVICES] MinSudo returned non-zero exit code {exit} — check the output above.");

                StampPreset($"Playbook:{name}", log);
                log.Log($"[SERVICES] Playbook service list '{name}' applied via regedit /s. Restart to take full effect.");
                return true;
            }
            catch (Exception ex)
            {
                log.Log($"[SERVICES] Playbook apply failed for {name}: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (File.Exists(tempBat)) File.Delete(tempBat); }
                catch (Exception ex) { log.Log($"[SERVICES] Could not delete temp batch {tempBat}: {ex.Message}"); }
            }
        }
    }
}
