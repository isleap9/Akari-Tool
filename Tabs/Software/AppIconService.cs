// AppIconService — real app icons for the Software tab cards.
//
// Ported from Winhance's RepoIconKey + RepoIconSource + AppIconResolver,
// slimmed to the essentials:
//   • Icon paths derived mechanically from the definition (winget id /
//     AppX name / capability / feature name) — nothing stored per-app.
//   • Downloaded once from the isleap9/package-icons repo via jsDelivr CDN,
//     then served from %ProgramData%\AkariTool\IconCache.
//   • Missing icons are negative-cached for the session; callers fall back
//     to the letter avatar.
//
// Not ported (v1): manifest.json SHA-256 verification, AppX local logo
// extraction (WinRT imaging dependency).

using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace AkariTool.Tabs;

public static class AppIconService
{
    private const string BaseUrl = "https://cdn.jsdelivr.net/gh/isleap9/package-icons@main/";
    private const long MaxIconBytes = 10L * 1024 * 1024;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AkariTool", "IconCache");

    private static readonly HttpClient Http = CreateClient();

    // Session caches: loaded bitmaps by app id, and known-missing ids so the
    // CDN isn't re-queried every refresh for apps that have no icon.
    private static readonly Dictionary<string, BitmapImage> Loaded = [];
    private static readonly HashSet<string> Missing = [];
    private static readonly SemaphoreSlim Gate = new(6); // max parallel downloads

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "AkariTool/1.0 (+https://github.com/isleap9)");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/*");
        return client;
    }

    /// <summary>
    /// Returns the app's icon as a frozen (thread-safe) BitmapImage, or null
    /// when no icon exists. Safe to fire-and-forget from card construction;
    /// assign the result to an Image.Source on the dispatcher.
    /// </summary>
    public static async Task<BitmapImage?> GetIconAsync(AppDefinition app)
    {
        lock (Loaded)
        {
            if (Loaded.TryGetValue(app.Id, out var cached)) return cached;
            if (Missing.Contains(app.Id)) return null;
        }

        try
        {
            // 1) Disk cache
            var cachePath = Path.Combine(CacheRoot, Sanitize(app.Id) + ".png");
            if (File.Exists(cachePath))
                return Remember(app.Id, LoadFrozen(cachePath));

            // 2) CDN — try each candidate repo path in order
            await Gate.WaitAsync();
            try
            {
                foreach (var repoPath in CandidatePaths(app))
                {
                    var bytes = await DownloadAsync(repoPath);
                    if (bytes == null) continue;

                    Directory.CreateDirectory(CacheRoot);
                    await File.WriteAllBytesAsync(cachePath, bytes);
                    return Remember(app.Id, LoadFrozen(cachePath));
                }
            }
            finally
            {
                Gate.Release();
            }
        }
        catch
        {
            // Icons are cosmetic — never let them break the tab.
        }

        lock (Loaded) Missing.Add(app.Id);
        return null;
    }

    /// <summary>
    /// Candidate icon paths in the package-icons repo, in priority order
    /// (Winhance RepoIconKey logic, adapted to AppDefinition).
    /// </summary>
    private static IEnumerable<string> CandidatePaths(AppDefinition app)
    {
        if (app.Id.StartsWith("external-app-", StringComparison.Ordinal))
        {
            var key = (app.WinGetPackageId is { Length: > 0 } w && !string.IsNullOrEmpty(w[0]))
                ? w[0]
                : !string.IsNullOrEmpty(app.ChocoPackageId)
                    ? app.ChocoPackageId
                    : app.Id["external-app-".Length..];
            yield return $"icons/external/{key.ToLowerInvariant()}.png";
            yield break;
        }

        if (app.Id.StartsWith("windows-app-", StringComparison.Ordinal))
        {
            // Multiple AppX identities possible (e.g. GamingApp + XboxApp)
            if (app.AppxPackageName != null)
                foreach (var name in app.AppxPackageName)
                    if (!string.IsNullOrEmpty(name))
                        yield return $"icons/windows/{name.ToLowerInvariant()}.png";
            yield break;
        }

        if (app.Id.StartsWith("capability-", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(app.CapabilityName))
        {
            yield return $"icons/windows/{app.CapabilityName.ToLowerInvariant()}.png";
            yield break;
        }

        if (app.Id.StartsWith("feature-", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(app.OptionalFeatureName))
        {
            yield return $"icons/windows/{app.OptionalFeatureName.ToLowerInvariant()}.png";
        }
    }

    private static async Task<byte[]?> DownloadAsync(string repoPath)
    {
        try
        {
            using var cts = new CancellationTokenSource(FetchTimeout);
            using var resp = await Http.GetAsync(BaseUrl + repoPath.TrimStart('/'),
                HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;
            if (resp.Content.Headers.ContentLength is long len && len > MaxIconBytes) return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
            if (bytes.Length == 0 || bytes.Length > MaxIconBytes) return null;

            // Sanity check: accept PNG magic only (repo stores PNGs; anything
            // else — e.g. an HTML error page — is rejected).
            if (bytes.Length < 8 ||
                bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                return null;

            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Loads a BitmapImage fully into memory and freezes it for cross-thread use.</summary>
    private static BitmapImage LoadFrozen(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // release the file handle
        bmp.DecodePixelWidth = 80;                    // cards render at 40px; 2x for HiDPI
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static BitmapImage Remember(string id, BitmapImage bmp)
    {
        lock (Loaded) Loaded[id] = bmp;
        return bmp;
    }

    private static string Sanitize(string id) =>
        string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_'));
}
