using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace AkariTool.Services
{
    /// <summary>
    /// Applies the AkariOS NVIDIA profile using the LATEST nvidiaProfileInspector,
    /// fetched on demand from GitHub.
    ///
    /// The .nip profile is EMBEDDED (it is AkariOS's own tuning); only the tool is
    /// downloaded, so the user always gets the current release without re-bundling.
    ///
    /// nvidiaProfileInspector is MIT — Copyright (c) 2016 Orbmu2k. We no longer
    /// redistribute the binary, so only the credit is required.
    /// </summary>
    public static class NvidiaProfileService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/Orbmu2k/nvidiaProfileInspector/releases/latest";

        private const string ExeName = "nvidiaProfileInspector.exe";

        // GitHub's API rejects requests without a User-Agent.
        private static HttpClient? _http;
        private static HttpClient Http => _http ??= new HttpClient(new HttpClientHandler
        {
            UseProxy = false,          // skip system proxy (important on VMs)
            AllowAutoRedirect = true,
        })
        {
            DefaultRequestHeaders = { { "User-Agent", "AkariTool" } },
            Timeout = TimeSpan.FromMinutes(5),
        };

        /// <summary>
        /// Downloads the latest nvidiaProfileInspector release, extracts it to a temp folder,
        /// imports the embedded AkariOS profile silently, then cleans up.
        /// </summary>
        public static async Task ApplyAkariProfileAsync(ToolService log)
        {
            var workDir = Path.Combine(Path.GetTempPath(), "AkariNvidia");

            try
            {
                // 1. Clean work directory
                TryDeleteDir(workDir);
                Directory.CreateDirectory(workDir);

                // 2. Extract the embedded AkariOS profile
                var nipPath = Path.Combine(workDir, "Settings.nip");
                if (!await TryExtractEmbeddedAsync(".Settings.nip", nipPath))
                {
                    log.Log("The AkariOS NVIDIA profile is missing from this build — cannot apply.");
                    return;
                }

                // 3. Resolve the latest release
                log.Log("Fetching the latest nvidiaProfileInspector release...");
                string? zipUrl = null, version = null;
                try
                {
                    var json = await Http.GetStringAsync(LatestReleaseApi);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    version = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;

                    if (root.TryGetProperty("assets", out var assets))
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                            if (name is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                zipUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                                break;
                            }
                        }
                }
                catch (Exception ex)
                {
                    log.Log($"Could not download nvidiaProfileInspector — check your internet connection and try again. ({ex.Message})");
                    return;
                }

                if (string.IsNullOrWhiteSpace(zipUrl))
                {
                    log.Log("No .zip asset found on the latest nvidiaProfileInspector release — aborting.");
                    return;
                }

                // 4. Download the zip
                var zipPath = Path.Combine(workDir, "npi.zip");
                try
                {
                    await using var src = await Http.GetStreamAsync(zipUrl);
                    await using var dst = File.Create(zipPath);
                    await src.CopyToAsync(dst);
                }
                catch (Exception ex)
                {
                    log.Log($"Could not download nvidiaProfileInspector — check your internet connection and try again. ({ex.Message})");
                    return;
                }

                // 5. Extract
                ZipFile.ExtractToDirectory(zipPath, workDir);

                // The exe may sit at the zip root or inside a subfolder — search for it.
                var exe = Directory.GetFiles(workDir, ExeName, SearchOption.AllDirectories).FirstOrDefault();
                if (exe is null)
                {
                    log.Log($"{ExeName} was not found in the downloaded archive — aborting.");
                    return;
                }

                // 6. Apply the profile silently
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = exe,
                    Arguments       = $"/s \"{nipPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                };

                using var p = System.Diagnostics.Process.Start(psi)!;
                await p.WaitForExitAsync();

                if (p.ExitCode != 0)
                {
                    log.Log($"nvidiaProfileInspector exited with code {p.ExitCode} — the profile may not have applied.");
                    return;
                }

                log.Log($"AkariOS NVIDIA profile applied (nvidiaProfileInspector {version ?? "latest"}). Restart games to take effect.");
            }
            catch (Exception ex)
            {
                log.Log($"ERROR applying the NVIDIA profile: {ex.Message}");
            }
            finally
            {
                // 7. Always clean up
                TryDeleteDir(workDir);
            }
        }

        /// <summary>Writes the embedded resource ending with <paramref name="endsWith"/> to disk. False when absent.</summary>
        private static async Task<bool> TryExtractEmbeddedAsync(string endsWith, string destPath)
        {
            try
            {
                var asm = typeof(NvidiaProfileService).Assembly;
                var name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));
                if (name is null) return false;

                await using var rs = asm.GetManifestResourceStream(name)!;
                await using var fs = File.Create(destPath);
                await rs.CopyToAsync(fs);
                return true;
            }
            catch { return false; }
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
