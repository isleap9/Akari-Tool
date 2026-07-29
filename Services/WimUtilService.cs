using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace AkariTool.Services
{
    // ── Models ─────────────────────────────────────────────────────────────

    public enum WimImageFormat { Wim, Esd }

    public sealed record WimImageInfo(
        WimImageFormat Format,
        string FilePath,
        long FileSizeBytes,
        int ImageCount,
        IReadOnlyList<string> EditionNames)
    {
        public string SizeText => $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public sealed record WimDetectionResult(WimImageInfo? WimInfo, WimImageInfo? EsdInfo)
    {
        public bool BothExist     => WimInfo != null && EsdInfo != null;
        public bool NeitherExists => WimInfo == null && EsdInfo == null;
        public WimImageInfo? Single => BothExist || NeitherExists ? null : WimInfo ?? EsdInfo;
    }

    /// <summary>
    /// Windows Installation Media Utility backend, ported from Winhance's
    /// IsoService / WimImageService / WimCustomizationService / DriverCategorizer /
    /// OscdimgToolManager into a single dependency-free service in the AkariTool style.
    ///
    /// All long operations report:
    ///   - status(text)   → short status line for the wizard step
    ///   - percent(0-100) → parsed from DISM / oscdimg output when available (-1 = indeterminate)
    /// and log details through the shared ToolService log box.
    /// </summary>
    public class WimUtilService
    {
        private readonly ToolService _service;
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

        private static readonly Regex DriveLetterRegex = new(@"\b[A-Z]\b", RegexOptions.Compiled);
        private static readonly Regex PercentRegex     = new(@"(\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);

        public const string AkariAutounattendXmlUrl =
            "https://raw.githubusercontent.com/isleap9/Akari-Tool-Autounattend/main/autounattend.xml";

        public WimUtilService(ToolService service) => _service = service;

        private void Log(string msg) => _service.Log($"[WIM] {msg}");

        // ── Process helpers ────────────────────────────────────────────────

        private async Task<(int ExitCode, string StdOut, string StdErr)> RunCaptureAsync(
            string fileName, string arguments, CancellationToken ct = default)
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = fileName,
                    Arguments              = arguments,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8,
                }
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception ex) { ToolService.Current?.Log($"[WimUtilService] Process kill failed: {ex.Message}"); } });
            await p.WaitForExitAsync(ct);

            return (p.ExitCode, stdout.ToString(), stderr.ToString());
        }

        /// <summary>
        /// Runs a console tool (dism.exe / oscdimg.exe / installers) streaming its
        /// output into the log and parsing "NN.N%" progress into the percent callback.
        /// </summary>
        private async Task<int> RunStreamingAsync(
            string fileName, string arguments,
            Action<double>? percent, CancellationToken ct = default)
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = fileName,
                    Arguments              = arguments,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                }
            };

            void HandleLine(string? line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;

                var m = PercentRegex.Match(line);
                if (m.Success &&
                    double.TryParse(m.Groups[1].Value.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    percent?.Invoke(Math.Clamp(pct, 0, 100));
                }
                else
                {
                    _service.Log(line.Trim());
                }
            }

            p.OutputDataReceived += (_, e) => HandleLine(e.Data);
            p.ErrorDataReceived  += (_, e) => HandleLine(e.Data);

            Log($"run: {Path.GetFileName(fileName)} {arguments}");
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using var reg = ct.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception ex) { ToolService.Current?.Log($"[WimUtilService] Process kill failed: {ex.Message}"); }
            });

            await p.WaitForExitAsync(ct);
            return p.ExitCode;
        }

        private static void CheckDiskSpace(string pathOnTargetDrive, long requiredBytes, string operation)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(pathOnTargetDrive));
                if (string.IsNullOrEmpty(root)) return;
                var drive = new DriveInfo(root);
                if (drive.AvailableFreeSpace < requiredBytes)
                    throw new IOException(
                        $"{operation} needs ~{requiredBytes / (1024.0 * 1024 * 1024):F1} GB free on {root} " +
                        $"but only {drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB is available.");
            }
            catch (IOException) { throw; }
            catch (Exception ex) { /* drive query failed (UNC etc.) — let the operation try */ 
                ToolService.Current?.Log($"[WimUtilService] Disk space check failed: {ex.Message}"); }
        }

        // ── Step 1: ISO validation / extraction ────────────────────────────

        public bool ValidateIsoFile(string isoPath)
        {
            if (!File.Exists(isoPath)) { Log($"ISO not found: {isoPath}"); return false; }
            if (!string.Equals(Path.GetExtension(isoPath), ".iso", StringComparison.OrdinalIgnoreCase))
            { Log($"Not an .iso file: {isoPath}"); return false; }
            if (new FileInfo(isoPath).Length < 1024 * 1024)
            { Log("ISO file is too small to be valid."); return false; }
            return true;
        }

        /// <summary>Checks that a folder already contains extracted install media.</summary>
        public bool LooksLikeExtractedMedia(string directory) =>
            Directory.Exists(Path.Combine(directory, "sources")) &&
            Directory.Exists(Path.Combine(directory, "boot"));

        public async Task<bool> ExtractIsoAsync(
            string isoPath, string workingDirectory,
            Action<string> status, Action<double>? percent = null,
            CancellationToken ct = default)
        {
            var mounted = false;
            try
            {
                if (!ValidateIsoFile(isoPath)) return false;

                CheckDiskSpace(workingDirectory,
                    new FileInfo(isoPath).Length + 2L * 1024 * 1024 * 1024, "ISO extraction");

                if (Directory.Exists(workingDirectory))
                {
                    status("Clearing existing working directory…");
                    Log($"Clearing working directory: {workingDirectory}");
                    try
                    {
                        await Task.Run(() =>
                        {
                            foreach (var f in Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories))
                                File.SetAttributes(f, FileAttributes.Normal);
                            Directory.Delete(workingDirectory, recursive: true);
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Could not delete the existing working directory '{workingDirectory}'. " +
                            "It may be open in Explorer or in use by another process. " +
                            $"Close it or delete it manually and try again. ({ex.Message})");
                    }
                }
                Directory.CreateDirectory(workingDirectory);

                status("Mounting ISO…");
                Log($"Mounting ISO: {isoPath}");
                var mount = await RunCaptureAsync("powershell.exe",
                    $"-NoProfile -Command \"(Mount-DiskImage -ImagePath '{isoPath}' -PassThru | Get-Volume).DriveLetter\"", ct);

                var letterMatch = DriveLetterRegex.Match(mount.StdOut);
                if (mount.ExitCode != 0 || !letterMatch.Success)
                { Log("Failed to mount ISO or resolve drive letter."); return false; }

                mounted = true;
                var mountedPath = $"{letterMatch.Value}:\\";
                Log($"ISO mounted at {mountedPath}");

                status("Copying ISO contents…");
                await Task.Run(() => CopyDirectory(mountedPath, workingDirectory, status, ct), ct);

                status("Dismounting ISO…");
                await RunCaptureAsync("powershell.exe",
                    $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"", CancellationToken.None);
                mounted = false;

                if (!LooksLikeExtractedMedia(workingDirectory))
                {
                    Log("Extraction verification failed — 'sources' and 'boot' folders not found.");
                    return false;
                }

                status("ISO extracted.");
                Log($"ISO extracted to {workingDirectory}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("ISO extraction cancelled.");
                if (mounted)
                    await RunCaptureAsync("powershell.exe",
                        $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"", CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                Log($"ISO extraction failed: {ex.Message}");
                if (mounted)
                    await RunCaptureAsync("powershell.exe",
                        $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"", CancellationToken.None);
                status($"Extraction failed: {ex.Message}");
                return false;
            }
        }

        private void CopyDirectory(string sourceDir, string destDir, Action<string> status, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file);
                status($"Copying {name}…");
                File.Copy(file, Path.Combine(destDir, name), overwrite: true);
            }
            foreach (var sub in Directory.GetDirectories(sourceDir))
            {
                ct.ThrowIfCancellationRequested();
                CopyDirectory(sub, Path.Combine(destDir, Path.GetFileName(sub)), status, ct);
            }
        }

        // ── Image format detection / conversion (install.wim ⇄ install.esd) ─

        public async Task<WimDetectionResult> DetectImagesAsync(string workingDirectory)
        {
            WimImageInfo? wim = null, esd = null;
            var sources = Path.Combine(workingDirectory, "sources");
            if (!Directory.Exists(sources)) return new WimDetectionResult(null, null);

            var wimPath = Path.Combine(sources, "install.wim");
            if (File.Exists(wimPath)) wim = await GetImageInfoAsync(wimPath, WimImageFormat.Wim);

            var esdPath = Path.Combine(sources, "install.esd");
            if (File.Exists(esdPath)) esd = await GetImageInfoAsync(esdPath, WimImageFormat.Esd);

            if (wim != null && esd != null) Log("Both install.wim and install.esd found — only one should exist.");
            return new WimDetectionResult(wim, esd);
        }

        private async Task<WimImageInfo> GetImageInfoAsync(string imagePath, WimImageFormat format)
        {
            long size = new FileInfo(imagePath).Length;
            int count = 1;
            var names = new List<string>();
            try
            {
                var r = await RunCaptureAsync("dism.exe", $"/Get-ImageInfo /ImageFile:\"{imagePath}\"");
                if (r.ExitCode == 0)
                {
                    int parsed = 0;
                    foreach (var raw in r.StdOut.Split('\n'))
                    {
                        var line = raw.Trim();
                        if (line.StartsWith("Index", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                            parsed++;
                        else if (line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                        {
                            var name = line[(line.IndexOf(':') + 1)..].Trim();
                            if (name.Length > 0) names.Add(name);
                        }
                    }
                    if (parsed > 0) count = parsed;
                }
            }
            catch (Exception ex) { Log($"Could not read image info: {ex.Message}"); }

            return new WimImageInfo(format, imagePath, size, count, names);
        }

        public async Task<bool> ConvertImageAsync(
            string workingDirectory, WimImageFormat targetFormat,
            Action<string> status, Action<double>? percent = null,
            CancellationToken ct = default)
        {
            string targetFile = string.Empty;
            try
            {
                var detection = await DetectImagesAsync(workingDirectory);
                var current = detection.Single;
                if (current == null)
                { Log("Could not detect a single current image format."); return false; }
                if (current.Format == targetFormat) return true;

                var sources = Path.Combine(workingDirectory, "sources");
                targetFile = Path.Combine(sources,
                    targetFormat == WimImageFormat.Wim ? "install.wim" : "install.esd");

                CheckDiskSpace(workingDirectory, current.FileSizeBytes * 2, "Image conversion");

                var compression = targetFormat == WimImageFormat.Esd ? "recovery" : "max";
                Log($"Converting {current.Format} → {targetFormat} ({current.ImageCount} edition(s)). This can take 10-20 minutes.");

                for (int i = 1; i <= current.ImageCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var editionName = current.EditionNames.Count >= i ? current.EditionNames[i - 1] : $"Index {i}";
                    status($"Converting edition {i}/{current.ImageCount}: {editionName}");

                    var args = $"/Export-Image /SourceImageFile:\"{current.FilePath}\" /SourceIndex:{i} " +
                               $"/DestinationImageFile:\"{targetFile}\" /Compress:{compression} /CheckIntegrity";
                    var exit = await RunStreamingAsync("dism.exe", args, percent, ct);
                    if (exit != 0) throw new Exception($"DISM Export-Image failed with exit code {exit}");
                }

                await Task.Delay(2000, ct);

                if (!File.Exists(targetFile)) { Log("Converted file not found."); return false; }

                // Delete the source image (retry: DISM may still hold a handle briefly)
                status("Removing old image file…");
                var deleted = await TryDeleteWithRetryAsync(current.FilePath, ct);
                var newSize = new FileInfo(targetFile).Length;
                if (!deleted)
                {
                    status($"Converted ({newSize / (1024.0 * 1024 * 1024):F2} GB) — delete the old " +
                           $"{Path.GetFileName(current.FilePath)} manually, it is still in use.");
                    return true;
                }

                var diff = current.FileSizeBytes - newSize;
                status($"Conversion complete — new size {newSize / (1024.0 * 1024 * 1024):F2} GB " +
                       (diff > 0 ? $"(saved {diff / (1024.0 * 1024 * 1024):F2} GB)"
                                 : $"(used {Math.Abs(diff) / (1024.0 * 1024 * 1024):F2} GB more)"));
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("Conversion cancelled — cleaning up incomplete file.");
                TryDeleteQuiet(targetFile);
                throw;
            }
            catch (Exception ex)
            {
                Log($"Conversion failed: {ex.Message}");
                TryDeleteQuiet(targetFile);
                status($"Conversion failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteImageFileAsync(string workingDirectory, WimImageFormat format, Action<string> status, CancellationToken ct = default)
        {
            var file = Path.Combine(workingDirectory, "sources",
                format == WimImageFormat.Wim ? "install.wim" : "install.esd");
            if (!File.Exists(file)) { Log($"File not found: {file}"); return false; }

            status($"Deleting {Path.GetFileName(file)}…");
            var ok = await TryDeleteWithRetryAsync(file, ct);
            status(ok ? $"{Path.GetFileName(file)} deleted." : $"Could not delete {Path.GetFileName(file)} — file in use.");
            return ok;
        }

        private async Task<bool> TryDeleteWithRetryAsync(string file, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    if (!File.Exists(file)) return true;
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    Log($"Deleted {file}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"Delete attempt {attempt}/5 failed: {ex.Message}");
                    if (attempt < 5) await Task.Delay(2000, ct);
                }
            }
            return false;
        }

        private static void TryDeleteQuiet(string file)
        {
            try { if (!string.IsNullOrEmpty(file) && File.Exists(file)) File.Delete(file); } catch (Exception ex) { ToolService.Current?.Log($"[WimUtilService] File deletion failed: {ex.Message}"); }
        }

        // ── Step 2: autounattend.xml ────────────────────────────────────────

        public async Task<bool> AddXmlToImageAsync(string xmlPath, string workingDirectory)
        {
            try
            {
                if (!File.Exists(xmlPath)) { Log($"XML not found: {xmlPath}"); return false; }
                if (!Directory.Exists(workingDirectory)) { Log($"Working directory not found: {workingDirectory}"); return false; }

                var dest = Path.Combine(workingDirectory, "autounattend.xml");
                File.Copy(xmlPath, dest, overwrite: true);
                Log($"autounattend.xml added: {dest}");
                return true;
            }
            catch (Exception ex) { Log($"Failed to add XML: {ex.Message}"); return false; }
        }

        public async Task<bool> DownloadAkariAutounattendXmlAsync(string workingDirectory, Action<string> status, CancellationToken ct = default)
        {
            try
            {
                status("Downloading Akari autounattend.xml…");
                var xml = await Http.GetStringAsync(AkariAutounattendXmlUrl, ct);
                Directory.CreateDirectory(workingDirectory);
                var dest = Path.Combine(workingDirectory, "autounattend.xml");
                await File.WriteAllTextAsync(dest, xml, ct);
                Log($"Downloaded Akari autounattend.xml → {dest}");
                status("autounattend.xml added (Akari).");
                return true;
            }
            catch (Exception ex)
            {
                Log($"XML download failed: {ex.Message}");
                status($"Download failed: {ex.Message}");
                return false;
            }
        }

        // ── Step 3: drivers ────────────────────────────────────────────────

        private static readonly HashSet<string> StorageClasses = new(StringComparer.OrdinalIgnoreCase)
        { "SCSIAdapter", "hdc", "HDC" };

        private static readonly string[] StorageFileNameKeywords =
        { "iaahci", "iastor", "iastorac", "iastora", "iastorv", "vmd", "irst", "rst" };

        private bool IsStorageDriver(string infPath)
        {
            try
            {
                var name = Path.GetFileName(infPath).ToLowerInvariant();
                if (StorageFileNameKeywords.Any(k => name.Contains(k))) return true;

                string content;
                try { content = File.ReadAllText(infPath, Encoding.Unicode); }
                catch { content = File.ReadAllText(infPath, Encoding.UTF8); }

                using var reader = new StringReader(content);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var t = line.Trim();
                    if (t.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
                    {
                        var parts = t.Split('=');
                        if (parts.Length >= 2 && StorageClasses.Contains(parts[1].Trim()))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log($"Could not categorize {Path.GetFileName(infPath)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Exports the current system's drivers (or uses a custom folder) and copies
        /// them into the media: storage drivers → sources\$WinpeDriver$ (loaded during
        /// setup), everything else → sources\$OEM$\$$\Drivers (installed post-setup
        /// by SetupComplete.cmd via pnputil).
        /// </summary>
        public async Task<bool> AddDriversAsync(
            string workingDirectory, string? driverSourcePath,
            Action<string> status, Action<double>? percent = null,
            CancellationToken ct = default)
        {
            string sourceDirectory;
            var usedTempExport = false;
            try
            {
                if (string.IsNullOrEmpty(driverSourcePath))
                {
                    status("Exporting system drivers (this may take several minutes)…");
                    var tempDir = Path.Combine(Path.GetTempPath(), $"AkariToolDrivers_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempDir);
                    usedTempExport = true;

                    var exit = await RunStreamingAsync("dism.exe",
                        $"/Online /Export-Driver /Destination:\"{tempDir}\"", percent, ct);
                    if (exit != 0)
                    {
                        try { Directory.Delete(tempDir, recursive: true); } catch { }
                        Log($"DISM Export-Driver failed with exit code {exit}");
                        status("Driver export failed.");
                        return false;
                    }
                    sourceDirectory = tempDir;
                }
                else
                {
                    if (!Directory.Exists(driverSourcePath))
                    { Log($"Driver folder not found: {driverSourcePath}"); return false; }
                    sourceDirectory = driverSourcePath;
                }

                status("Categorizing drivers (storage vs post-install)…");
                var winpePath = Path.Combine(workingDirectory, "sources", "$WinpeDriver$");
                var oemPath   = Path.Combine(workingDirectory, "sources", "$OEM$", "$$", "Drivers");

                int copied = await Task.Run(() =>
                    CategorizeAndCopyDrivers(sourceDirectory, winpePath, oemPath, workingDirectory), ct);

                if (usedTempExport)
                    try { Directory.Delete(sourceDirectory, recursive: true); }
                    catch (Exception ex) { Log($"Temp driver folder cleanup failed: {ex.Message}"); }

                if (copied == 0)
                {
                    Log($"No drivers found in {sourceDirectory}");
                    status("No drivers found to add.");
                    return false;
                }

                CreateSetupCompleteScript(workingDirectory);
                status($"{copied} driver package(s) added.");
                Log($"Added {copied} driver(s) — WinPE: {winpePath} · OEM: {oemPath}");
                return true;
            }
            catch (OperationCanceledException) { Log("Driver addition cancelled."); throw; }
            catch (Exception ex)
            {
                Log($"Driver addition failed: {ex.Message}");
                status($"Driver addition failed: {ex.Message}");
                return false;
            }
        }

        private int CategorizeAndCopyDrivers(string sourceDirectory, string winpePath, string oemPath, string workingDirToExclude)
        {
            var infFiles = Directory.GetFiles(sourceDirectory, "*.inf", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(workingDirToExclude, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (infFiles.Length == 0) return 0;

            int copied = 0;
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inf in infFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(inf)!;
                    if (!processed.Add(dir)) continue;

                    var targetBase = IsStorageDriver(inf) ? winpePath : oemPath;
                    var folderName = Path.GetFileName(dir);
                    var target = Path.Combine(targetBase, folderName);

                    int counter = 1;
                    while (Directory.Exists(target) && counter < 100)
                        target = Path.Combine(targetBase, $"{folderName}_{counter++}");

                    Directory.CreateDirectory(target);
                    foreach (var file in Directory.GetFiles(dir))
                        File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);

                    copied++;
                    Log($"Copied driver: {folderName}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to copy driver {Path.GetFileName(inf)}: {ex.Message}");
                }
            }
            return copied;
        }

        private void CreateSetupCompleteScript(string workingDirectory)
        {
            try
            {
                var scriptsPath = Path.Combine(workingDirectory, "sources", "$OEM$", "$$", "Setup", "Scripts");
                Directory.CreateDirectory(scriptsPath);

                const string script = @"@echo off
REM Akari Tool Automatic Driver Installation Script
REM This script is executed automatically by Windows Setup

set LOGFILE=C:\Windows\Logs\DriverInstall.log

echo ================================================== > %LOGFILE%
echo Akari Tool Driver Installation Log >> %LOGFILE%
echo Date: %DATE% %TIME% >> %LOGFILE%
echo ================================================== >> %LOGFILE%
echo. >> %LOGFILE%

echo Installing drivers from C:\Windows\Drivers... >> %LOGFILE%
pnputil /add-driver C:\Windows\Drivers\*.inf /subdirs /install >> %LOGFILE% 2>&1

echo. >> %LOGFILE%
echo Driver installation completed >> %LOGFILE%
echo Exit Code: %ERRORLEVEL% >> %LOGFILE%

exit
";
                File.WriteAllText(Path.Combine(scriptsPath, "SetupComplete.cmd"), script);
                Log("Created SetupComplete.cmd");
            }
            catch (Exception ex) { Log($"Could not create SetupComplete.cmd: {ex.Message}"); }
        }

        // ── Step 4: oscdimg + bootable ISO ─────────────────────────────────

        private static readonly string[] AdkDownloadSources =
        {
            "https://go.microsoft.com/fwlink/?linkid=2289980",
            "https://download.microsoft.com/download/2/d/9/2d9c8902-3fcd-48a6-a22a-432b08bed61e/ADK/adksetup.exe"
        };

        public string GetOscdimgPath()
        {
            var candidates = new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
                @"C:\Program Files\WinGet\Links\oscdimg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WinGet\Links\oscdimg.exe"),
            };
            foreach (var p in candidates)
                if (File.Exists(p)) return p;

            // Scan winget Packages directories for Microsoft.OSCDIMG
            var packageDirs = new[]
            {
                @"C:\Program Files\WinGet\Packages",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WinGet\Packages"),
            };
            foreach (var packagesDir in packageDirs)
            {
                if (!Directory.Exists(packagesDir)) continue;
                try
                {
                    foreach (var dir in Directory.GetDirectories(packagesDir, "Microsoft.OSCDIMG_*"))
                    {
                        var candidate = Path.Combine(dir, "oscdimg.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                catch (Exception ex) { Log($"winget package scan failed: {ex.Message}"); }
            }
            return string.Empty;
        }

        public bool IsOscdimgAvailable() => !string.IsNullOrEmpty(GetOscdimgPath());

        /// <summary>
        /// Installs oscdimg using the same fallback chain as Winhance:
        /// 1) winget Microsoft.OSCDIMG (lightweight, ~few MB)
        /// 2) ADK installer direct download (Deployment Tools feature only)
        /// 3) winget Microsoft.WindowsADK
        /// </summary>
        public async Task<bool> EnsureOscdimgAvailableAsync(
            Action<string> status, Action<double>? percent = null, CancellationToken ct = default)
        {
            if (IsOscdimgAvailable()) return true;

            status("Installing Microsoft.OSCDIMG via winget…");
            if (await InstallViaWingetAsync(
                "install Microsoft.OSCDIMG --exact --silent --scope machine --accept-package-agreements --accept-source-agreements",
                percent, ct) && IsOscdimgAvailable())
                return true;

            Log("Microsoft.OSCDIMG package failed — trying direct ADK Deployment Tools install…");
            status("Downloading Windows ADK installer…");
            if (await InstallAdkDirectAsync(status, percent, ct) && IsOscdimgAvailable())
                return true;

            Log("Direct ADK install failed — trying winget Microsoft.WindowsADK…");
            status("Installing Windows ADK via winget (several minutes)…");
            var adkLog = Path.Combine(Path.GetTempPath(), "adk_winget_install.log");
            if (await InstallViaWingetAsync(
                "install Microsoft.WindowsADK --exact --silent --accept-package-agreements --accept-source-agreements " +
                $"--override \"/quiet /norestart /features OptionId.DeploymentTools /ceip off\" --log \"{adkLog}\"",
                percent, ct) && IsOscdimgAvailable())
                return true;

            status("All installation methods for oscdimg.exe failed.");
            Log("All methods to install oscdimg.exe failed.");
            return false;
        }

        private async Task<bool> InstallViaWingetAsync(string arguments, Action<double>? percent, CancellationToken ct)
        {
            try
            {
                var exit = await RunStreamingAsync("winget", arguments, percent, ct);
                return exit == 0;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log($"winget install failed: {ex.Message}"); return false; }
        }

        private async Task<bool> InstallAdkDirectAsync(Action<string> status, Action<double>? percent, CancellationToken ct)
        {
            var setupPath = Path.Combine(Path.GetTempPath(), "adksetup.exe");
            try
            {
                var downloaded = false;
                foreach (var url in AdkDownloadSources)
                {
                    try
                    {
                        Log($"Downloading ADK installer: {url}");
                        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                        response.EnsureSuccessStatusCode();
                        await using var content = await response.Content.ReadAsStreamAsync(ct);
                        await using var file = new FileStream(setupPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                        await content.CopyToAsync(file, ct);
                        downloaded = true;
                        break;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { Log($"ADK download failed from {url}: {ex.Message}"); }
                }
                if (!downloaded) return false;

                status("Installing ADK Deployment Tools (several minutes)…");
                var logPath = Path.Combine(Path.GetTempPath(), "adk_install.log");
                var exit = await RunStreamingAsync(setupPath,
                    $"/quiet /norestart /features OptionId.DeploymentTools /ceip off /log \"{logPath}\"", percent, ct);
                return exit == 0;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log($"ADK install failed: {ex.Message}"); return false; }
            finally
            {
                TryDeleteQuiet(setupPath);
            }
        }

        public async Task<bool> CreateIsoAsync(
            string workingDirectory, string outputPath,
            Action<string> status, Action<double>? percent = null,
            CancellationToken ct = default)
        {
            try
            {
                var oscdimg = GetOscdimgPath();
                if (string.IsNullOrEmpty(oscdimg))
                { Log("oscdimg.exe not available — install it first."); return false; }

                var etfsboot = Path.Combine(workingDirectory, "boot", "etfsboot.com");
                var efisys   = Path.Combine(workingDirectory, "efi", "microsoft", "boot", "efisys.bin");
                if (!File.Exists(etfsboot)) throw new FileNotFoundException($"Boot file not found: {etfsboot}");
                if (!File.Exists(efisys))   throw new FileNotFoundException($"UEFI boot file not found: {efisys}");

                var workingSize = await Task.Run(() =>
                    Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length), ct);
                CheckDiskSpace(outputPath, workingSize + 2L * 1024 * 1024 * 1024, "ISO creation");

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
                if (File.Exists(outputPath)) { File.Delete(outputPath); Log("Removed existing output ISO."); }

                status("Creating bootable ISO (this may take several minutes)…");
                var args = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{etfsboot}\"#pEF,e,b\"{efisys}\" " +
                           $"\"{workingDirectory}\" \"{outputPath}\"";

                var exit = await RunStreamingAsync(oscdimg, args, percent, ct);
                if (exit != 0) throw new Exception($"oscdimg.exe failed with exit code {exit}");
                if (!File.Exists(outputPath)) { Log("ISO file was not created."); return false; }

                var size = new FileInfo(outputPath).Length;
                status($"ISO created — {size / (1024.0 * 1024):F0} MB");
                Log($"ISO created: {outputPath} ({size:N0} bytes)");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("ISO creation cancelled.");
                TryDeleteQuiet(outputPath);
                throw;
            }
            catch (Exception ex)
            {
                Log($"ISO creation failed: {ex.Message}");
                status($"ISO creation failed: {ex.Message}");
                return false;
            }
        }
    }
}
