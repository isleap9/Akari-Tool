// Service layer for the Software tab — ported from Winhance's
// AppStatusDiscoveryService / BloatRemovalService / WinGet CLI runner,
// condensed into AkariTool's single-service code-behind architecture.
//
// Responsibilities:
//   • Installed-status discovery (AppX, registry uninstall keys, DISM
//     capabilities/optional features, detection paths)
//   • Windows-app removal via generated BloatRemoval.ps1 (+ dedicated
//     Edge/OneDrive scripts), persisted with a startup scheduled task
//   • External-app install/uninstall via the winget CLI

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AkariTool.Tabs;

/// <summary>Literal paths embedded in generated PowerShell scripts.</summary>
public static class AkariPaths
{
    public static readonly string ScriptsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AkariTool", "Scripts");

    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\AkariTool\Scripts";
    public const string LogsDirectoryLiteral = @"C:\ProgramData\AkariTool\Logs";
    public const string PowerShellExePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
}

/// <summary>Snapshot of system install state, gathered once per refresh.</summary>
public sealed class InstallSnapshot
{
    public HashSet<string> AppxPackages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InstalledCapabilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> EnabledFeatures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(string SubKeyName, string? DisplayName)> UninstallEntries { get; } = [];

    /// <summary>
    /// Winhance parity: installed winget package ids (COM composite catalog, CLI
    /// export fallback), fetched lazily by the apply layer when any app still
    /// needs winget detection. Null until fetched; null result means unavailable.
    /// </summary>
    public HashSet<string>? WinGetPackageIds { get; internal set; }
}

public static class SoftwareAppService
{
    // ═════════════════════════════════════════════════════════════════════
    // STATUS DISCOVERY
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gathers the full install snapshot: one AppX query, one capability query,
    /// one optional-feature query (single PowerShell process), plus an in-process
    /// registry uninstall-key scan. Runs off the UI thread.
    /// </summary>
    public static async Task<InstallSnapshot> GetInstallSnapshotAsync()
    {
        var snapshot = new InstallSnapshot();

        // Single PowerShell round-trip for AppX + capabilities + features.
        // Sections are delimited so one parse pass fills all three sets.
        const string ps =
            "$ErrorActionPreference='SilentlyContinue';" +
            "'###APPX###';" +
            "Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name;" +
            "'###CAPS###';" +
            "Get-WindowsCapability -Online | Where-Object State -eq 'Installed' | Select-Object -ExpandProperty Name;" +
            "'###FEATURES###';" +
            "Get-WindowsOptionalFeature -Online | Where-Object State -eq 'Enabled' | Select-Object -ExpandProperty FeatureName;";

        var output = await RunHiddenAsync(AkariPaths.PowerShellExePath,
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"", timeoutMs: 120_000);

        var section = "";
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line is "###APPX###" or "###CAPS###" or "###FEATURES###") { section = line; continue; }

            switch (section)
            {
                case "###APPX###": snapshot.AppxPackages.Add(line); break;
                case "###CAPS###": snapshot.InstalledCapabilities.Add(line); break;
                case "###FEATURES###": snapshot.EnabledFeatures.Add(line); break;
            }
        }

        // Registry uninstall keys — fast in-process scan (covers winget/choco/MSI installs).
        await Task.Run(() =>
        {
            void Scan(RegistryKey root, string path)
            {
                try
                {
                    using var key = root.OpenSubKey(path);
                    if (key is null) return;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(sub);
                            var displayName = subKey?.GetValue("DisplayName") as string;
                            snapshot.UninstallEntries.Add((sub, displayName));
                        }
                        catch { /* ignore unreadable keys */ }
                    }
                }
                catch { /* ignore unreadable hives */ }
            }

            Scan(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            Scan(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            Scan(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        });

        return snapshot;
    }

    /// <summary>Applies snapshot data to a set of app definitions (Winhance layered detection).</summary>
    public static void ApplyInstallStatus(IEnumerable<AppDefinition> apps, InstallSnapshot snapshot)
    {
        foreach (var app in apps)
        {
            app.DetectedVia = AppDetectionSource.None;
            app.IsInstalled = false;

            // Layer 1 — capability / optional feature state (exact domain)
            if (app.CapabilityName != null)
            {
                app.IsInstalled = snapshot.InstalledCapabilities.Any(c =>
                    c.StartsWith(app.CapabilityName, StringComparison.OrdinalIgnoreCase));
                if (app.IsInstalled) app.DetectedVia = AppDetectionSource.Capability;
                continue;
            }
            if (app.OptionalFeatureName != null)
            {
                app.IsInstalled = snapshot.EnabledFeatures.Contains(app.OptionalFeatureName);
                if (app.IsInstalled) app.DetectedVia = AppDetectionSource.OptionalFeature;
                continue;
            }

            // Layer 2 — AppX package names
            if (app.AppxPackageName != null &&
                app.AppxPackageName.Any(p => snapshot.AppxPackages.Contains(p)))
            {
                app.IsInstalled = true;
                app.DetectedVia = AppDetectionSource.AppX;
                continue;
            }

            // Layer 3 — registry uninstall keys ({version}/{arch}/{locale} wildcards)
            if (app.RegistrySubKeyName != null || app.RegistryDisplayName != null)
            {
                bool match = snapshot.UninstallEntries.Any(e =>
                    (app.RegistrySubKeyName != null && MatchesPattern(e.SubKeyName, app.RegistrySubKeyName)) ||
                    (app.RegistryDisplayName != null && e.DisplayName != null && MatchesPattern(e.DisplayName, app.RegistryDisplayName)));
                if (match)
                {
                    app.IsInstalled = true;
                    app.DetectedVia = AppDetectionSource.Registry;
                    continue;
                }
            }

            // Layer 3b — winget package id / app name against registry entries.
            // Winget writes ARP entries whose DisplayName usually matches the app
            // name; SubKeyName often contains the winget package id.
            if (!app.IsInstalled && app.WinGetPackageId != null)
            {
                bool match = snapshot.UninstallEntries.Any(e =>
                    app.WinGetPackageId.Any(id =>
                        e.SubKeyName.Contains(id, StringComparison.OrdinalIgnoreCase)) ||
                    (e.DisplayName != null &&
                     e.DisplayName.Equals(app.Name, StringComparison.OrdinalIgnoreCase)));
                if (match)
                {
                    app.IsInstalled = true;
                    app.DetectedVia = AppDetectionSource.Registry;
                    continue;
                }
            }

            // Layer 3c — WinGet installed-package ids (Winhance parity: exact-id
            // match against the COM composite catalog / winget export inventory).
            // Lazy: the ids are fetched once per snapshot, only when an app with
            // winget ids is still undetected. Null ids = WinGet unavailable.
            if (!app.IsInstalled && (app.WinGetPackageId != null || app.MsStoreId != null))
            {
                var wingetIds = GetOrFetchWinGetPackageIdsAsync(snapshot).GetAwaiter().GetResult();
                if (wingetIds != null)
                {
                    bool matchedById = app.WinGetPackageId?.Any(pkgId => wingetIds.Contains(pkgId)) == true;
                    bool matchedByStoreId = !string.IsNullOrEmpty(app.MsStoreId) && wingetIds.Contains(app.MsStoreId);
                    if (matchedById || matchedByStoreId)
                    {
                        app.IsInstalled = true;
                        app.DetectedVia = AppDetectionSource.WinGet;
                    }
                }
            }

            // Layer 4 — detection paths
            if (app.DetectionPaths != null)
            {
                foreach (var path in app.DetectionPaths)
                {
                    var expanded = Environment.ExpandEnvironmentVariables(path);
                    if (File.Exists(expanded) || Directory.Exists(expanded))
                    {
                        app.IsInstalled = true;
                        app.DetectedVia = AppDetectionSource.FileSystem;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Winhance GetOrFetchWinGetPackageIdsAsync parity: bootstrap winget, then
    /// fetch installed package ids once per snapshot. Returns null when WinGet
    /// is unavailable (callers skip the layer). Result is cached on the snapshot.
    /// </summary>
    private static async Task<HashSet<string>?> GetOrFetchWinGetPackageIdsAsync(InstallSnapshot snapshot)
    {
        if (snapshot.WinGetPackageIds != null)
            return snapshot.WinGetPackageIds;

        try
        {
            var bootstrapper = WinUI.Framework.IoC.ServiceLocator
                .GetService<AkariTool.Core.Features.Apps.Interfaces.IWingetBootstrapper>();
            var detection = WinUI.Framework.IoC.ServiceLocator
                .GetService<AkariTool.Core.Features.Apps.Interfaces.IWingetInstalledDetectionService>();

            if (bootstrapper == null || detection == null)
                return null;

            bool winGetReady = await bootstrapper.EnsureWinGetReadyAsync().ConfigureAwait(false);
            if (!winGetReady)
                return null;

            snapshot.WinGetPackageIds = await detection.GetInstalledPackageIdsAsync().ConfigureAwait(false);
            return snapshot.WinGetPackageIds;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tests whether input matches a pattern containing {version}, {arch}, {locale}
    /// placeholders. Each placeholder becomes a non-greedy wildcard (Winhance 1:1).
    /// </summary>
    public static bool MatchesPattern(string input, string pattern)
    {
        var regexPattern = Regex.Escape(pattern)
            .Replace(@"\{version}", ".+?")
            .Replace(@"\{arch}", ".+?")
            .Replace(@"\{locale}", ".+?");
        return Regex.IsMatch(input, $"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    // ═════════════════════════════════════════════════════════════════════
    // WINDOWS APP REMOVAL (BloatRemoval pipeline)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Removes the given Windows apps. Apps with dedicated scripts (Edge,
    /// OneDrive) run their own script; everything else goes through one
    /// generated BloatRemoval.ps1. Afterwards the merged BloatRemoval.ps1 is
    /// saved and a startup scheduled task registered so removals persist
    /// across Windows updates (Winhance behaviour).
    /// </summary>
    public static async Task RemoveWindowsAppsAsync(
        IReadOnlyList<AppDefinition> apps,
        Action<string> log,
        Action<string> status)
    {
        var scriptApps = apps.Where(a => a.RemovalScript != null).ToList();
        var regularApps = apps.Where(a => a.RemovalScript == null).ToList();

        // 1) Dedicated scripts (Edge / OneDrive)
        foreach (var app in scriptApps)
        {
            status($"Removing {app.Name}…");
            log($"[REMOVE] {app.Name} (dedicated script)");
            var script = app.RemovalScript!();
            var name = app.Id.Contains("edge") ? "EdgeRemoval.ps1"
                     : app.Id.Contains("onedrive") ? "OneDriveRemoval.ps1"
                     : $"{Sanitize(app.Name)}Removal.ps1";
            await RunScriptTextAsync(script, name, log);
        }

        // 2) Regular apps — one generated BloatRemoval run
        if (regularApps.Count > 0)
        {
            status($"Removing {regularApps.Count} item(s)…");
            var (packages, capabilities, features, specialApps) = Categorize(regularApps);

            bool xboxFix = packages.Any(p => p is "Microsoft.GamingApp" or "Microsoft.XboxGamingOverlay" or "Microsoft.XboxGameOverlay");
            bool teamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

            var script = BloatRemovalScriptGenerator.GenerateScript(
                packages, capabilities, features, specialApps, xboxFix, teamsKill);

            log($"[REMOVE] {regularApps.Count} item(s) via BloatRemoval script");
            await RunScriptTextAsync(script, "BloatRemoval-Run.ps1", log);
        }

        // 3) Persist: merge into the saved BloatRemoval.ps1 + register startup task
        status("Saving removal script…");
        await SaveAndRegisterBloatRemovalAsync(apps, log);
    }

    private static (List<string> packages, List<string> capabilities, List<string> features, List<string> specialApps)
        Categorize(IEnumerable<AppDefinition> apps)
    {
        var packages = new List<string>();
        var capabilities = new List<string>();
        var features = new List<string>();
        var specialApps = new List<string>();

        foreach (var app in apps)
        {
            if (app.CapabilityName != null) capabilities.Add(app.CapabilityName);
            else if (app.OptionalFeatureName != null) features.Add(app.OptionalFeatureName);
            else if (app.RegistrySubKeyName != null && app.Id == "windows-app-onenote") specialApps.Add("OneNote");
            if (app.AppxPackageName != null) packages.AddRange(app.AppxPackageName);
        }

        return (packages.Distinct().ToList(), capabilities.Distinct().ToList(),
                features.Distinct().ToList(), specialApps.Distinct().ToList());
    }

    /// <summary>
    /// Merges the newly removed items into C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1
    /// and registers a startup scheduled task so removed apps stay removed
    /// after Windows updates (Winhance's keep-removed mechanism).
    /// </summary>
    private static async Task SaveAndRegisterBloatRemovalAsync(IEnumerable<AppDefinition> apps, Action<string> log)
    {
        try
        {
            Directory.CreateDirectory(AkariPaths.ScriptsDirectory);
            var scriptPath = Path.Combine(AkariPaths.ScriptsDirectory, "BloatRemoval.ps1");

            // Existing arrays (if the script already exists) + new items, deduplicated
            List<string> packages = [], capabilities = [], features = [], specialApps = [];
            if (File.Exists(scriptPath))
            {
                var existing = await File.ReadAllTextAsync(scriptPath);
                packages = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "packages");
                capabilities = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "capabilities");
                features = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "optionalFeatures");
                specialApps = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "specialApps");
            }

            var (newPackages, newCapabilities, newFeatures, newSpecialApps) =
                Categorize(apps.Where(a => a.RemovalScript == null));

            packages = packages.Union(newPackages, StringComparer.OrdinalIgnoreCase).ToList();
            capabilities = capabilities.Union(newCapabilities, StringComparer.OrdinalIgnoreCase).ToList();
            features = features.Union(newFeatures, StringComparer.OrdinalIgnoreCase).ToList();
            specialApps = specialApps.Union(newSpecialApps, StringComparer.OrdinalIgnoreCase).ToList();

            if (packages.Count == 0 && capabilities.Count == 0 && features.Count == 0 && specialApps.Count == 0)
                return;

            bool xboxFix = packages.Any(p => p is "Microsoft.GamingApp" or "Microsoft.XboxGamingOverlay" or "Microsoft.XboxGameOverlay");
            bool teamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

            var merged = BloatRemovalScriptGenerator.GenerateScript(
                packages, capabilities, features, specialApps, xboxFix, teamsKill);
            await File.WriteAllTextAsync(scriptPath, merged, Encoding.UTF8);
            log($"[SAVED] {scriptPath}");

            // Startup scheduled task (SYSTEM, highest privileges) — mirrors Winhance
            var taskCmd =
                "schtasks /Create /F /TN \"AkariTool\\BloatRemoval\" /SC ONSTART /RU SYSTEM /RL HIGHEST " +
                $"/TR \"'{AkariPaths.PowerShellExePath}' -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File '{scriptPath}'\"";
            await RunHiddenAsync("cmd.exe", $"/c {taskCmd}", timeoutMs: 30_000);
            log("[TASK] BloatRemoval startup task registered");
        }
        catch (Exception ex)
        {
            log($"[ERROR] Failed to persist BloatRemoval script: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes items from the saved BloatRemoval.ps1 (called after reinstalling
    /// an app so the startup task doesn't remove it again). Deletes the script
    /// and task when nothing remains.
    /// </summary>
    public static async Task RemoveFromSavedScriptAsync(IEnumerable<AppDefinition> apps, Action<string> log)
    {
        try
        {
            var scriptPath = Path.Combine(AkariPaths.ScriptsDirectory, "BloatRemoval.ps1");
            if (!File.Exists(scriptPath)) return;

            var existing = await File.ReadAllTextAsync(scriptPath);
            var packages = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "packages");
            var capabilities = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "capabilities");
            var features = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "optionalFeatures");
            var specialApps = BloatRemovalScriptGenerator.ExtractArrayFromScript(existing, "specialApps");

            var (rmPackages, rmCapabilities, rmFeatures, rmSpecialApps) = Categorize(apps);

            packages.RemoveAll(p => rmPackages.Contains(p, StringComparer.OrdinalIgnoreCase));
            capabilities.RemoveAll(c => rmCapabilities.Contains(c, StringComparer.OrdinalIgnoreCase));
            features.RemoveAll(f => rmFeatures.Contains(f, StringComparer.OrdinalIgnoreCase));
            specialApps.RemoveAll(s => rmSpecialApps.Contains(s, StringComparer.OrdinalIgnoreCase));

            if (packages.Count == 0 && capabilities.Count == 0 && features.Count == 0 && specialApps.Count == 0)
            {
                File.Delete(scriptPath);
                await RunHiddenAsync("cmd.exe", "/c schtasks /Delete /F /TN \"AkariTool\\BloatRemoval\"", timeoutMs: 30_000);
                log("[TASK] BloatRemoval script and task removed (nothing left to keep removed)");
                return;
            }

            bool xboxFix = packages.Any(p => p is "Microsoft.GamingApp" or "Microsoft.XboxGamingOverlay" or "Microsoft.XboxGameOverlay");
            bool teamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));
            var merged = BloatRemovalScriptGenerator.GenerateScript(packages, capabilities, features, specialApps, xboxFix, teamsKill);
            await File.WriteAllTextAsync(scriptPath, merged, Encoding.UTF8);
            log("[SAVED] BloatRemoval.ps1 updated (reinstalled items excluded)");
        }
        catch (Exception ex)
        {
            log($"[ERROR] Failed to update saved BloatRemoval script: {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // INSTALL (winget CLI / DISM)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Installs a single app definition. Capabilities/features go through DISM;
    /// everything else through winget (package id fallbacks, then msstore id).
    /// Returns true on success.
    /// </summary>
    public static async Task<bool> InstallAppAsync(AppDefinition app, Action<string> log)
    {
        // Capabilities / optional features → DISM via PowerShell
        if (app.CapabilityName != null)
        {
            log($"[INSTALL] Capability: {app.CapabilityName}");
            var output = await RunHiddenAsync(AkariPaths.PowerShellExePath,
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                $"\"Get-WindowsCapability -Online -Name '{app.CapabilityName}*' | Add-WindowsCapability -Online\"",
                timeoutMs: 600_000);
            log(output.Trim());
            return !output.Contains("Error", StringComparison.OrdinalIgnoreCase);
        }
        if (app.OptionalFeatureName != null)
        {
            log($"[INSTALL] Optional feature: {app.OptionalFeatureName}");
            var output = await RunHiddenAsync(AkariPaths.PowerShellExePath,
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                $"\"Enable-WindowsOptionalFeature -Online -FeatureName '{app.OptionalFeatureName}' -All -NoRestart\"",
                timeoutMs: 600_000);
            log(output.Trim());
            return !output.Contains("Error", StringComparison.OrdinalIgnoreCase);
        }

        // winget package ids, in declared order (Winhance 1:1: installs run via the
        // CLI — Winhance's WinGetPackageInstaller is CLI-based; COM is used for
        // installed-state detection only).
        if (app.WinGetPackageId != null)
        {
            foreach (var id in app.WinGetPackageId)
            {
                var overrideArg = app.WinGetInstallerOverride != null
                    ? $" --override \"{app.WinGetInstallerOverride}\"" : "";
                log($"[INSTALL] winget: {id}");
                var exit = await RunWingetAsync(
                    $"install -e --id {id} --silent --accept-source-agreements --accept-package-agreements" +
                    $" --disable-interactivity{overrideArg}", log);
                if (exit == 0) return true;
                log($"[WARN] winget install {id} exited with 0x{exit:X}");
            }
        }

        // MS Store fallback
        if (app.MsStoreId != null)
        {
            log($"[INSTALL] winget (msstore): {app.MsStoreId}");
            var exit = await RunWingetAsync(
                $"install --id {app.MsStoreId} --source msstore --silent " +
                "--accept-source-agreements --accept-package-agreements --disable-interactivity", log);
            if (exit == 0) return true;
        }

        log($"[ERROR] No install source succeeded for {app.Name}");
        return false;
    }

    /// <summary>Uninstalls an external app via winget (with AppX fallback).</summary>
    public static async Task<bool> UninstallExternalAppAsync(AppDefinition app, Action<string> log)
    {
        if (app.ProcessesToStop != null)
        {
            foreach (var proc in app.ProcessesToStop)
            {
                try { foreach (var p in Process.GetProcessesByName(proc)) p.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
            }
        }

        if (app.WinGetPackageId != null)
        {
            foreach (var id in app.WinGetPackageId)
            {
                log($"[UNINSTALL] winget: {id}");
                var exit = await RunWingetAsync(
                    $"uninstall -e --id {id} --silent --disable-interactivity --accept-source-agreements", log);
                if (exit == 0) return true;
            }
        }

        if (app.AppxPackageName != null)
        {
            log($"[UNINSTALL] AppX: {string.Join(", ", app.AppxPackageName)}");
            var names = string.Join("','", app.AppxPackageName);
            var output = await RunHiddenAsync(AkariPaths.PowerShellExePath,
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                $"\"foreach($n in @('{names}')){{ Get-AppxPackage -AllUsers -Name $n | Remove-AppxPackage -AllUsers }}\"",
                timeoutMs: 300_000);
            log(output.Trim());
            return true;
        }

        log($"[ERROR] No uninstall method available for {app.Name}");
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════
    // PROCESS HELPERS
    // ═════════════════════════════════════════════════════════════════════

    private static async Task<int> RunWingetAsync(string arguments, Action<string> log)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data.Trim()); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log($"[ERROR] {e.Data.Trim()}"); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            log($"[ERROR] winget not available: {ex.Message}");
            return -1;
        }
    }

    /// <summary>Writes script text to a temp file and runs it hidden, streaming output to the log.</summary>
    private static async Task RunScriptTextAsync(string scriptText, string fileName, Action<string> log)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"AkariTool-{Guid.NewGuid():N}-{fileName}");
        try
        {
            await File.WriteAllTextAsync(tempPath, scriptText, Encoding.UTF8);
            var psi = new ProcessStartInfo
            {
                FileName = AkariPaths.PowerShellExePath,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data.Trim()); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log($"[ERROR] {e.Data.Trim()}"); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* temp cleanup best effort */ }
        }
    }

    private static async Task<string> RunHiddenAsync(string fileName, string arguments, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var process = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitTask = process.WaitForExitAsync();
        if (await Task.WhenAny(waitTask, Task.Delay(timeoutMs)) != waitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        }
        else
        {
            await waitTask;
        }
        lock (sb) return sb.ToString();
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit));
}
