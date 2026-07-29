using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AkariTool.Services
{
    public enum UpdateStatus
    {
        UpToDate,          // latest release <= current version
        UpdateAvailable,   // latest release > current version
        NoReleases,        // repo has no published releases yet (404)
        Error              // network / API failure
    }

    public sealed class UpdateCheckResult
    {
        public UpdateStatus Status { get; init; }
        public string? LatestTag { get; init; }        // e.g. "v2.1.0"
        public string? ReleaseName { get; init; }      // release title
        public string? ReleaseNotes { get; init; }     // markdown body
        public string? ReleasePageUrl { get; init; }   // html_url — always safe to open
        public string? InstallerUrl { get; init; }     // AkariTool-Setup-*.exe asset, if present
        public string? ErrorMessage { get; init; }
    }

    public sealed class ReleaseInfo
    {
        public string Tag { get; init; } = "";         // "v2.0.0"
        public string Name { get; init; } = "";
        public string Body { get; init; } = "";        // release notes (markdown)
        public DateTime PublishedUtc { get; init; }
        public bool IsCurrent { get; init; }
    }

    /// <summary>
    /// GitHub Releases integration for isleap9/Akari-Tool:
    ///   CheckAsync()             — is a newer version published?
    ///   DownloadInstallerAsync() — pull the Setup exe to %TEMP% with progress
    ///   GetReleasesAsync()       — release history for the live changelog card
    /// Unauthenticated API (60 req/hour per IP) — fine for manual checks.
    /// </summary>
    public static class UpdateService
    {
        private const string Owner = "isleap9";
        private const string Repo  = "Akari-Tool";
        private const string ApiBase = $"https://api.github.com/repos/{Owner}/{Repo}";

        public static string ReleasesPageUrl => $"https://github.com/{Owner}/{Repo}/releases";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; // long enough for installer download
            c.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("AkariTool", CurrentVersion.ToString()));
            c.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return c;
        }

        /// <summary>App version from the assembly — set &lt;Version&gt; in the .csproj.</summary>
        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        /// <summary>"v2.0" / "v2.0.1" style display string.</summary>
        public static string CurrentVersionDisplay
        {
            get
            {
                var v = CurrentVersion;
                return $"v{v.Major}.{v.Minor}" + (v.Build > 0 ? $".{v.Build}" : "");
            }
        }

        // ── Check ────────────────────────────────────────────────────────────────
        public static async Task<UpdateCheckResult> CheckAsync()
        {
            try
            {
                using var resp = await Http.GetAsync($"{ApiBase}/releases/latest").ConfigureAwait(false);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new UpdateCheckResult { Status = UpdateStatus.NoReleases };

                if (!resp.IsSuccessStatusCode)
                    return new UpdateCheckResult
                    {
                        Status = UpdateStatus.Error,
                        ErrorMessage = $"GitHub API returned {(int)resp.StatusCode}"
                    };

                using var doc = JsonDocument.Parse(
                    await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
                var root = doc.RootElement;

                string tag   = root.GetProperty("tag_name").GetString() ?? "";
                string html  = root.GetProperty("html_url").GetString() ?? ReleasesPageUrl;
                string name  = root.TryGetProperty("name", out var n) ? n.GetString() ?? tag : tag;
                string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                // Look specifically for the installer asset the build script produces.
                string? installer = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        var an = a.GetProperty("name").GetString() ?? "";
                        if (an.StartsWith("AkariTool-Setup", StringComparison.OrdinalIgnoreCase) &&
                            an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installer = a.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                var latest = ParseTag(tag);
                bool newer = latest is not null && latest > Normalize(CurrentVersion);

                return new UpdateCheckResult
                {
                    Status         = newer ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate,
                    LatestTag      = tag,
                    ReleaseName    = name,
                    ReleaseNotes   = notes,
                    ReleasePageUrl = html,
                    InstallerUrl   = installer
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Error,
                    ErrorMessage = ex is TaskCanceledException
                        ? "Request timed out — check your connection."
                        : ex.Message
                };
            }
        }

        // ── Download installer with progress ────────────────────────────────────
        /// <summary>
        /// Downloads the installer to %TEMP% and returns its path.
        /// Progress reports 0.0–1.0 (or stays at 0 if length is unknown).
        /// </summary>
        public static async Task<string> DownloadInstallerAsync(
            string url, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            string dest = Path.Combine(Path.GetTempPath(),
                $"AkariTool-Setup-{Guid.NewGuid():N}.exe");

            using var resp = await Http.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = File.Create(dest);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }
            return dest;
        }

        // ── Release history (live changelog) ────────────────────────────────────
        /// <summary>Returns published releases, newest first. Null on any failure.</summary>
        public static async Task<List<ReleaseInfo>?> GetReleasesAsync(int max = 10)
        {
            try
            {
                using var resp = await Http.GetAsync($"{ApiBase}/releases?per_page={max}")
                                           .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(
                    await resp.Content.ReadAsStringAsync().ConfigureAwait(false));

                var current = Normalize(CurrentVersion);
                var list = new List<ReleaseInfo>();
                foreach (var r in doc.RootElement.EnumerateArray())
                {
                    if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;

                    string tag = r.GetProperty("tag_name").GetString() ?? "";
                    var v = ParseTag(tag);
                    list.Add(new ReleaseInfo
                    {
                        Tag  = tag,
                        Name = r.TryGetProperty("name", out var nm) ? nm.GetString() ?? tag : tag,
                        Body = r.TryGetProperty("body", out var bd) ? bd.GetString() ?? "" : "",
                        PublishedUtc = r.TryGetProperty("published_at", out var p) &&
                                       p.ValueKind == JsonValueKind.String &&
                                       DateTime.TryParse(p.GetString(), out var dt)
                                       ? dt.ToUniversalTime() : DateTime.MinValue,
                        IsCurrent = v is not null && v == current
                    });
                }
                return list.Count > 0 ? list : null;
            }
            catch
            {
                return null; // caller falls back to the static changelog
            }
        }

        // ── Tag parsing ─────────────────────────────────────────────────────────
        /// <summary>Parses "v2.1", "2.1.0", "v2.1.0-beta" into a comparable Version.</summary>
        internal static Version? ParseTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var s = tag.Trim().TrimStart('v', 'V');
            int dash = s.IndexOfAny(new[] { '-', '+' });     // strip pre-release suffix
            if (dash >= 0) s = s[..dash];
            if (!s.Contains('.')) s += ".0";                 // "v2" → "2.0"
            return Version.TryParse(s, out var v) ? Normalize(v) : null;
        }

        // Version(2,0) vs Version(2,0,0) compare unequal — normalize to 3 parts.
        private static Version Normalize(Version v) =>
            new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
    }
}
