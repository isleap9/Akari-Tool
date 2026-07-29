using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AkariTool.Services
{
    public sealed record DetectedGame(string Name, string ExePath);

    /// <summary>
    /// Installed-game discovery. v1 covers Steam only: every folder under
    /// &lt;library&gt;\steamapps\common, represented by its best-guess game binary.
    ///
    /// Selection is deliberately NOT "largest .exe" — game folders routinely ship a
    /// dedicated server, a crash handler, an uninstaller and redist installers that
    /// are all larger than the game. Half-Life is the canonical failure: hlds.exe
    /// (dedicated server) outweighs hl.exe and crashes on launch with a Visual C++
    /// assertion when started with no server arguments.
    ///
    /// The ranking is: hard exclusions first (absolute), then an exact
    /// folder-name match, then version-resource evidence, then size as a last
    /// resort. Browse still exists for everything this gets wrong.
    /// </summary>
    public static class GameDetection
    {
        // Whole game folders that never contain a playable title.
        private static readonly string[] IgnoredFolderNames =
        {
            "steamworks shared", "steamvr", "proton", "_commonredist",
        };

        /// <summary>
        /// SET A — substring match. These tokens do not occur incidentally inside
        /// real game names, and they routinely appear glued to other text
        /// ("unins000", "EasyAntiCheat_Setup"), so a substring test is both safe
        /// and necessary.
        /// </summary>
        private static readonly string[] ExcludedSubstrings =
        {
            "hlds", "srcds", "vcredist", "vc_redist", "dxsetup", "dxwebsetup",
            "unins", "crashhandler", "crashreport", "crashpad", "bugreport",
            "unrealcefsubprocess", "easyanticheat", "battleye",
            "steamerrorreporter", "oalinst", "prereq", "touchup", "bsdiff",
            "dotnet", "redist", "directx", "sdk", "activation", "eula",
        };

        /// <summary>
        /// SET B — whole-token match only. These are ordinary English words that
        /// appear legitimately inside game titles, so substring matching would
        /// reject "Observer" (server), "Tooltip" (tool) and "Contest" (test).
        /// Matched against the tokens produced by <see cref="TokenizeName"/>, with
        /// an optional trailing "s" so "Servers" is caught too.
        /// </summary>
        private static readonly HashSet<string> ExcludedWords =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "server", "dedicated", "launcher", "uninstall", "setup", "install",
                "patcher", "updater", "update", "config", "cfg", "editor", "tool",
                "benchmark", "debug", "test", "helper", "service",
            };

        /// <summary>
        /// Folder names that disqualify everything beneath them. Matched
        /// case-insensitively against each path segment below the game root.
        /// </summary>
        private static readonly HashSet<string> ExcludedFolderNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "_CommonRedist", "Redist", "Redistributables", "DirectX", "dotnet",
                "vcredist", "Tools", "Editor", "Engine", "Extras", "SDK",
                "Dedicated Server", "Server", "__Installer", "Installers",
                "support", "Docs", "Manual",
            };

        /// <summary>
        /// Build-variant suffixes stripped before comparing a filename to its game
        /// folder name, so "Witcher3-Win64-Shipping" still matches "Witcher3".
        /// Longest first — "-win64-shipping" must be tried before "64".
        /// </summary>
        private static readonly string[] VariantSuffixes =
        {
            "-win64-shipping", "-shipping", "_x64", "_release", "64",
        };

        // ── Public API ────────────────────────────────────────────────────────

        public static IReadOnlyList<DetectedGame> DetectSteamGames()
        {
            var games = new List<DetectedGame>();
            var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string commonDir in SteamLibrary.GetSteamAppsSubdirectories("common"))
            {
                List<string> gameDirs;
                try
                {
                    if (!Directory.Exists(commonDir)) continue;
                    gameDirs = Directory.EnumerateDirectories(commonDir).ToList();
                }
                catch { continue; }

                foreach (string gameDir in gameDirs)
                {
                    try
                    {
                        string folderName = Path.GetFileName(gameDir);
                        if (IgnoredFolderNames.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                            continue;

                        string? exe = SelectGameExe(gameDir, folderName);
                        if (exe is null) continue;
                        if (!seen.Add(exe)) continue;

                        games.Add(new DetectedGame(folderName, exe));
                    }
                    catch { /* one unreadable game folder must not lose the rest */ }
                }
            }

            return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private sealed record Candidate(string Path, long Size, int Depth, string NormalizedName);

        /// <summary>
        /// Picks the most likely game binary in <paramref name="gameDir"/>, or null
        /// when every .exe is excluded (an excluded exe is never selected, even as
        /// the only remaining candidate).
        /// </summary>
        private static string? SelectGameExe(string gameDir, string folderName)
        {
            var candidates = EnumerateCandidates(gameDir);
            if (candidates.Count == 0) return null;

            string normalizedFolder = Normalize(folderName);

            // ── Tier 1: filename matches the game folder name ─────────────────
            // The strongest available signal. Shallowest wins, then largest.
            var exact = candidates
                .Where(c => c.NormalizedName == normalizedFolder)
                .OrderBy(c => c.Depth).ThenByDescending(c => c.Size)
                .FirstOrDefault();
            if (exact is not null) return exact.Path;

            // ── Tier 2: a version resource naming the product ─────────────────
            // Shipping game binaries carry FileDescription/ProductName; the small
            // unnamed helpers that survived Tier-1 exclusion usually do not.
            // Reading version info is why this runs only on the survivors.
            var described = candidates
                .Select(c => (Cand: c, Info: ReadVersionStrings(c.Path)))
                .Where(t => t.Info is not null)
                .ToList();

            // A version string that itself matches the folder name beats one that
            // merely exists — this is what rescues folders whose exe is abbreviated.
            var describedMatch = described
                .Where(t => Normalize(t.Info!.Value.Description) == normalizedFolder
                         || Normalize(t.Info!.Value.Product)     == normalizedFolder)
                .OrderBy(t => t.Cand.Depth).ThenByDescending(t => t.Cand.Size)
                .Select(t => t.Cand)
                .FirstOrDefault();
            if (describedMatch is not null) return describedMatch.Path;

            if (described.Count > 0)
                return described
                    .OrderBy(t => t.Cand.Depth).ThenByDescending(t => t.Cand.Size)
                    .First().Cand.Path;

            // ── Tier 3: fall back to size ─────────────────────────────────────
            // Only reached when nothing matched and nothing carries version info.
            // Safe now that the exclusion list has already removed the servers,
            // installers and crash handlers that made raw size dangerous.
            return candidates
                .OrderBy(c => c.Depth).ThenByDescending(c => c.Size)
                .First().Path;
        }

        private static List<Candidate> EnumerateCandidates(string gameDir)
        {
            var result = new List<Candidate>();

            foreach (string file in EnumerateExesSafe(gameDir))
            {
                try
                {
                    if (IsExcluded(gameDir, file)) continue;

                    long size = 0;
                    try { size = new FileInfo(file).Length; } catch { }

                    result.Add(new Candidate(
                        Path: file,
                        Size: size,
                        Depth: DepthBelow(gameDir, file),
                        NormalizedName: Normalize(Path.GetFileNameWithoutExtension(file))));
                }
                catch { /* skip this file, keep the rest */ }
            }

            return result;
        }

        /// <summary>
        /// Recursive .exe enumeration that cannot throw mid-iteration — a junction
        /// or a permission-denied subtree would otherwise lose the whole folder.
        /// </summary>
        private static IReadOnlyList<string> EnumerateExesSafe(string dir)
        {
            try { return Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories).ToList(); }
            catch
            {
                try { return Directory.EnumerateFiles(dir, "*.exe").ToList(); }
                catch { return Array.Empty<string>(); }
            }
        }

        // ── Exclusion ─────────────────────────────────────────────────────────

        /// <summary>
        /// Absolute veto: filename fragment or any excluded folder on the path
        /// between the game root and the file. Checked before any ranking, so an
        /// excluded exe can never be selected.
        /// </summary>
        private static bool IsExcluded(string gameDir, string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);

            // Set A: substring.
            foreach (string fragment in ExcludedSubstrings)
                if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;

            // Set B: discrete tokens only.
            foreach (string token in TokenizeName(name))
            {
                if (ExcludedWords.Contains(token)) return true;

                // Optional trailing plural: "Servers" is as disqualifying as "Server".
                if (token.Length > 1 && (token[^1] is 's' or 'S') &&
                    ExcludedWords.Contains(token[..^1]))
                    return true;
            }

            foreach (string segment in SegmentsBelow(gameDir, filePath))
                if (ExcludedFolderNames.Contains(segment))
                    return true;

            return false;
        }

        /// <summary>
        /// Splits a filename into discrete words. Boundaries are: space, hyphen,
        /// underscore and period; lowercase→uppercase transitions; and transitions
        /// between letters and digits in either direction.
        ///
        /// Both letter/digit directions are needed, not just digit→letter: without
        /// the letter→digit split "GameServer64" tokenizes to "Game" + "Server64"
        /// and the "server" token is missed.
        ///
        /// So: "GameServer", "Game_Server", "Game-Server 2" and "GameServer64" all
        /// yield "server", while "Observer" and "Tooltip" stay whole.
        /// </summary>
        private static IEnumerable<string> TokenizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) yield break;

            var token = new System.Text.StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (c is ' ' or '-' or '_' or '.')
                {
                    if (token.Length > 0) { yield return token.ToString(); token.Clear(); }
                    continue;
                }

                if (token.Length > 0)
                {
                    char prev = name[i - 1];
                    bool caseBreak  = char.IsLower(prev) && char.IsUpper(c);
                    bool digitBreak = char.IsDigit(prev) != char.IsDigit(c);

                    if (caseBreak || digitBreak)
                    {
                        yield return token.ToString();
                        token.Clear();
                    }
                }

                token.Append(c);
            }

            if (token.Length > 0) yield return token.ToString();
        }

        /// <summary>Directory names between the game root and the file, exclusive of both.</summary>
        private static IEnumerable<string> SegmentsBelow(string gameDir, string filePath)
        {
            string? dir;
            try
            {
                dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                gameDir = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch { yield break; }

            while (dir is not null &&
                   !dir.TrimEnd(Path.DirectorySeparatorChar).Equals(gameDir, StringComparison.OrdinalIgnoreCase))
            {
                string segment = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(segment)) yield break;   // hit the drive root
                yield return segment;

                dir = Path.GetDirectoryName(dir);
            }
        }

        private static int DepthBelow(string gameDir, string filePath)
        {
            try { return SegmentsBelow(gameDir, filePath).Count(); }
            catch { return int.MaxValue; }
        }

        // ── Version resource ──────────────────────────────────────────────────

        /// <summary>
        /// FileDescription / ProductName from the version resource, or null when the
        /// binary carries neither (both blank is the common case for tiny helper
        /// executables).
        /// </summary>
        private static (string Description, string Product)? ReadVersionStrings(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                string description = info.FileDescription ?? "";
                string product     = info.ProductName ?? "";

                if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(product))
                    return null;

                return (description, product);
            }
            catch { return null; }
        }

        // ── Normalization ─────────────────────────────────────────────────────

        /// <summary>
        /// Lowercases, strips a trailing build-variant suffix, then removes spaces,
        /// hyphens and underscores — so "Half-Life", "half life" and "HalfLife" all
        /// compare equal.
        /// </summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string s = value.Trim().ToLowerInvariant();

            // Longest-first so "-win64-shipping" is not partially eaten by "64".
            foreach (string suffix in VariantSuffixes)
                if (s.Length > suffix.Length && s.EndsWith(suffix, StringComparison.Ordinal))
                {
                    s = s[..^suffix.Length];
                    break;
                }

            Span<char> buffer = stackalloc char[s.Length];
            int n = 0;
            foreach (char c in s)
                if (c is not (' ' or '-' or '_')) buffer[n++] = c;

            return new string(buffer[..n]);
        }
    }
}
