using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// Resolves Steam's install root and every library folder it knows about.
    /// Shared by ShaderCacheService (shadercache directories) and
    /// CompetitiveService (installed-game detection) so the vdf parsing lives
    /// in exactly one place.
    /// </summary>
    public static class SteamLibrary
    {
        private const string SteamKey = @"HKEY_CURRENT_USER\Software\Valve\Steam";

        // "path"    "D:\\SteamLibrary"   — libraryfolders.vdf entries.
        private static readonly Regex VdfPathRegex =
            new("\"path\"\\s+\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Steam's install root, normalized to backslashes. Null when Steam is not
        /// installed. Read from the registry on every call, never cached, so an
        /// install during the session is picked up without a restart.
        /// </summary>
        public static string? GetSteamPath()
        {
            try
            {
                if (Registry.GetValue(SteamKey, "SteamPath", null) is not string raw ||
                    string.IsNullOrWhiteSpace(raw))
                    return null;

                // Steam writes forward slashes; GetFullPath normalizes them.
                return Path.GetFullPath(raw);
            }
            catch { return null; }
        }

        public static bool IsInstalled() => GetSteamPath() is not null;

        /// <summary>
        /// Every library root Steam knows about: the install root plus each "path"
        /// entry in libraryfolders.vdf. Deduplicated case-insensitively, because
        /// Windows paths are and the same library can appear in both sources with
        /// different casing. Empty when Steam is not installed.
        /// </summary>
        public static IReadOnlyList<string> GetLibraryRoots()
        {
            string? steamPath = GetSteamPath();
            if (steamPath is null) return Array.Empty<string>();

            var roots = new List<string>();
            var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string root)
            {
                try
                {
                    string full = Path.GetFullPath(root);
                    if (seen.Add(full)) roots.Add(full);
                }
                catch { /* malformed vdf entry — skip it */ }
            }

            Add(steamPath);

            try
            {
                string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in VdfPathRegex.Matches(File.ReadAllText(vdf)))
                        Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
            catch { /* unreadable vdf — the install root above is still covered */ }

            return roots;
        }

        // ── Installed app manifests ───────────────────────────────────────────

        /// <summary>One installed Steam app, as described by its appmanifest_*.acf.</summary>
        public sealed record SteamApp(uint AppId, string Name, string InstallDir, string LibraryRoot);

        // Generic VDF key/value pair: "installdir"    "Half-Life"
        private static readonly Regex VdfPairRegex =
            new("\"([^\"]+)\"\\s+\"([^\"]*)\"", RegexOptions.Compiled);

        /// <summary>
        /// Every installed app across every library, keyed by installdir (the folder
        /// name under steamapps\common). Case-insensitive, because that is how the
        /// filesystem compares and how the .acf casing drifts from the real folder.
        ///
        /// Rebuilt on each call — a game installed mid-session would otherwise never
        /// resolve, and the cost is a few dozen small file reads.
        /// </summary>
        public static IReadOnlyDictionary<string, SteamApp> GetInstalledApps()
        {
            var map = new Dictionary<string, SteamApp>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in GetLibraryRoots())
            {
                string steamapps = Path.Combine(root, "steamapps");

                List<string> manifests;
                try
                {
                    if (!Directory.Exists(steamapps)) continue;
                    manifests = Directory.EnumerateFiles(steamapps, "appmanifest_*.acf").ToList();
                }
                catch { continue; }

                foreach (string manifest in manifests)
                {
                    try
                    {
                        var app = ParseManifest(manifest, root);
                        if (app is null) continue;

                        // First library wins on a duplicate installdir; a second copy
                        // of the same folder name elsewhere is a broken install and
                        // guessing between them would be worse than being consistent.
                        if (!map.ContainsKey(app.InstallDir)) map[app.InstallDir] = app;
                    }
                    catch { /* one malformed manifest must not lose the library */ }
                }
            }

            return map;
        }

        private static SteamApp? ParseManifest(string manifestPath, string libraryRoot)
        {
            string text;
            try { text = File.ReadAllText(manifestPath); }
            catch { return null; }

            uint appId = 0;
            string installDir = "", name = "";

            foreach (Match m in VdfPairRegex.Matches(text))
            {
                string key   = m.Groups[1].Value;
                string value = m.Groups[2].Value;

                if (key.Equals("appid", StringComparison.OrdinalIgnoreCase))
                    uint.TryParse(value, out appId);
                else if (key.Equals("installdir", StringComparison.OrdinalIgnoreCase))
                    installDir = value;
                else if (key.Equals("name", StringComparison.OrdinalIgnoreCase) && name.Length == 0)
                    name = value;   // first "name" only — depot entries carry their own
            }

            if (appId == 0 || installDir.Length == 0) return null;
            return new SteamApp(appId, name, installDir, libraryRoot);
        }

        /// <summary>
        /// Resolves the Steam AppID owning <paramref name="gameExePath"/>, by finding
        /// the path segment directly beneath a "steamapps\common" component and
        /// looking it up among the installed manifests.
        ///
        /// Returns false for anything not under a Steam library — a Browse-selected
        /// exe, a non-Steam game, or a Steam folder with no manifest.
        /// </summary>
        public static bool TryGetSteamAppId(string gameExePath, out uint appId)
        {
            appId = 0;
            try
            {
                string? installDir = FindInstallDirSegment(gameExePath);
                if (installDir is null) return false;

                if (!GetInstalledApps().TryGetValue(installDir, out var app)) return false;

                appId = app.AppId;
                return appId != 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// The folder name directly under ...\steamapps\common\, however deep the exe
        /// sits below it — "…\common\Game\bin\x64\game.exe" yields "Game".
        /// </summary>
        private static string? FindInstallDirSegment(string exePath)
        {
            string[] parts;
            try
            {
                parts = Path.GetFullPath(exePath)
                            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                                   StringSplitOptions.RemoveEmptyEntries);
            }
            catch { return null; }

            // Need steamapps / common / <installdir> with at least one more segment
            // after it (the exe itself, or a subfolder on the way down).
            for (int i = 0; i + 2 < parts.Length; i++)
            {
                if (!parts[i].Equals("steamapps", StringComparison.OrdinalIgnoreCase)) continue;
                if (!parts[i + 1].Equals("common", StringComparison.OrdinalIgnoreCase)) continue;
                return parts[i + 2];
            }

            return null;
        }

        /// <summary>
        /// &lt;root&gt;\steamapps\&lt;leaf&gt; for every known library root.
        /// Directories are not checked for existence here.
        /// </summary>
        public static IReadOnlyList<string> GetSteamAppsSubdirectories(string leaf)
        {
            var paths = new List<string>();
            foreach (string root in GetLibraryRoots())
            {
                try { paths.Add(Path.Combine(root, "steamapps", leaf)); }
                catch { }
            }
            return paths;
        }
    }
}
