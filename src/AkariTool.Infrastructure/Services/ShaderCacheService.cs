using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using AkariTool.Core.Models.ShaderCache;

namespace AkariTool.Services
{
    /// <summary>
    /// Clears GPU/driver and Steam per-game shader caches. Every method is
    /// best-effort: an unreadable subtree or a locked file is skipped, never
    /// fatal, so one bad path cannot abort a target.
    /// </summary>
    public static class ShaderCacheService
    {
        // ── Targets ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the target list. Environment variables and the Steam registry are
        /// read on every call, never cached in a static initializer, so a Steam
        /// install or a new library folder is picked up without a restart.
        /// </summary>
        public static IReadOnlyList<ShaderCacheTarget> GetTargets()
        {
            string local  = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            return new List<ShaderCacheTarget>
            {
                new("directx", "DirectX", new[]
                {
                    Path.Combine(local, "D3DSCache"),
                }),
                new("nvidia", "NVIDIA", new[]
                {
                    Path.Combine(local,  "NVIDIA", "DXCache"),
                    Path.Combine(local,  "NVIDIA", "GLCache"),
                    Path.Combine(local,  "NVIDIA Corporation", "NV_Cache"),
                    Path.Combine(common, "NVIDIA Corporation", "NV_Cache"),
                }),
                new("amd", "AMD", new[]
                {
                    Path.Combine(local, "AMD", "DxCache"),
                    Path.Combine(local, "AMD", "DxcCache"),
                    Path.Combine(local, "AMD", "GLCache"),
                    Path.Combine(local, "AMD", "VkCache"),
                    Path.Combine(local, "AMD", "OglCache"),
                }),
                new("intel", "Intel", new[]
                {
                    Path.Combine(local, "Intel", "ShaderCache"),
                }),
                new("steam", "Steam", GetSteamShaderCachePaths()),
            };
        }

        /// <summary>
        /// Every &lt;library&gt;\steamapps\shadercache directory Steam knows about.
        /// Empty when Steam is not installed. Library resolution lives in
        /// <see cref="SteamLibrary"/> — CompetitiveService needs the same roots.
        /// </summary>
        private static IReadOnlyList<string> GetSteamShaderCachePaths() =>
            SteamLibrary.GetSteamAppsSubdirectories("shadercache");

        public static bool IsSteamInstalled() => SteamLibrary.IsInstalled();

        public static bool IsSteamRunning()
        {
            try { return Process.GetProcessesByName("steam").Any(); }
            catch { return false; }
        }

        // ── Scan ──────────────────────────────────────────────────────────────

        /// <summary>Measures each target without modifying anything. Never throws.</summary>
        public static Task<IReadOnlyList<ShaderCacheScanResult>> ScanAsync(
            IEnumerable<ShaderCacheTarget> targets, CancellationToken ct = default)
        {
            var list = targets.ToList();
            return Task.Run<IReadOnlyList<ShaderCacheScanResult>>(() =>
            {
                var results = new List<ShaderCacheScanResult>(list.Count);
                foreach (var target in list)
                {
                    ct.ThrowIfCancellationRequested();

                    long bytes = 0;
                    int  files = 0;
                    bool exists = false;

                    foreach (string dir in target.Paths)
                    {
                        if (!SafeDirectoryExists(dir)) continue;
                        exists = true;

                        foreach (string file in EnumerateFilesSafe(dir))
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                bytes += new FileInfo(file).Length;
                                files++;
                            }
                            catch { /* vanished or unreadable between enumerate and stat */ }
                        }
                    }

                    results.Add(new ShaderCacheScanResult(target.Id, bytes, files, exists));
                }
                return results;
            }, ct);
        }

        // ── Clean ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the CONTENTS of every directory belonging to each target. The
        /// directories themselves are always kept — drivers expect them to exist.
        /// Targets with no existing directory are a no-op, not an error.
        /// </summary>
        public static Task<IReadOnlyList<ShaderCacheCleanResult>> CleanAsync(
            IEnumerable<ShaderCacheTarget> targets,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var list = targets.ToList();
            return Task.Run<IReadOnlyList<ShaderCacheCleanResult>>(() =>
            {
                var results = new List<ShaderCacheCleanResult>(list.Count);
                foreach (var target in list)
                {
                    ct.ThrowIfCancellationRequested();
                    results.Add(CleanTarget(target, progress, ct));
                }
                return results;
            }, ct);
        }

        private static ShaderCacheCleanResult CleanTarget(
            ShaderCacheTarget target, IProgress<string>? progress, CancellationToken ct)
        {
            long bytesFreed = 0;
            int  deleted    = 0;
            int  skipped    = 0;

            try
            {
                progress?.Report($"Cleaning {target.DisplayName}…");

                foreach (string dir in target.Paths)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!SafeDirectoryExists(dir)) continue;

                    foreach (string file in EnumerateFilesSafe(dir))
                    {
                        ct.ThrowIfCancellationRequested();

                        // Size must be read before the delete, and accumulated as we
                        // go — a second pass would measure an already-empty tree.
                        long size = 0;
                        try { size = new FileInfo(file).Length; } catch { }

                        try
                        {
                            File.Delete(file);
                            bytesFreed += size;
                            deleted++;
                        }
                        catch (IOException)                   { skipped++; }
                        catch (UnauthorizedAccessException)   { skipped++; }
                        catch                                 { skipped++; }
                    }

                    DeleteEmptySubdirectories(dir);
                }

                return new ShaderCacheCleanResult(target.Id, bytesFreed, deleted, skipped, null);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new ShaderCacheCleanResult(target.Id, bytesFreed, deleted, skipped, ex.Message);
            }
        }

        /// <summary>
        /// Removes now-empty subdirectories bottom-up. The root <paramref name="dir"/>
        /// itself is never removed. Failures are ignored.
        /// </summary>
        private static void DeleteEmptySubdirectories(string dir)
        {
            List<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(p => p.Length)   // deepest first
                                   .ToList();
            }
            catch { return; }

            foreach (string sub in subdirs)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(sub).Any())
                        Directory.Delete(sub, false);
                }
                catch { /* still populated or locked — leave it */ }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool SafeDirectoryExists(string dir)
        {
            try { return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir); }
            catch { return false; }
        }

        /// <summary>
        /// Recursive file enumeration that materializes eagerly inside a try/catch,
        /// so an unreadable subtree yields nothing instead of throwing mid-iteration
        /// and aborting the whole target.
        /// </summary>
        private static IReadOnlyList<string> EnumerateFilesSafe(string dir)
        {
            try
            {
                return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
            }
            catch
            {
                // Fall back to the top level only — better than losing the target.
                try { return Directory.EnumerateFiles(dir).ToList(); }
                catch { return Array.Empty<string>(); }
            }
        }

        /// <summary>Human-readable size, e.g. "1.4 GB". Whole numbers up to KB, one decimal above.</summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return unit <= 1 ? $"{Math.Round(value)} {units[unit]}"
                             : $"{value:0.0} {units[unit]}";
        }
    }
}
