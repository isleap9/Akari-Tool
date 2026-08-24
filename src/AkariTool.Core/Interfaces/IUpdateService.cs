using AkariTool.Core.Models.Update;

namespace AkariTool.Core.Interfaces
{
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckAsync();

        Task<string> DownloadInstallerAsync(
            string url, IProgress<double>? progress = null, CancellationToken ct = default);

        Task<List<ReleaseInfo>?> GetReleasesAsync(int max = 10);

        // Read directly by SettingsViewModel.
        string ReleasesPageUrl { get; }
        string CurrentVersionDisplay { get; }
    }
}
