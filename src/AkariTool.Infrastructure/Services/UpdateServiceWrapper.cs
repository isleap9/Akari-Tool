using AkariTool.Core.Interfaces;
using AkariTool.Core.Models.Update;
using AkariTool.Services;

namespace AkariTool.Infrastructure.Services
{
    public sealed class UpdateServiceWrapper : IUpdateService
    {
        public Task<UpdateCheckResult> CheckAsync() => UpdateService.CheckAsync();

        public Task<string> DownloadInstallerAsync(
            string url, IProgress<double>? progress = null, CancellationToken ct = default)
            => UpdateService.DownloadInstallerAsync(url, progress, ct);

        public Task<List<ReleaseInfo>?> GetReleasesAsync(int max = 10)
            => UpdateService.GetReleasesAsync(max);

        public string ReleasesPageUrl => UpdateService.ReleasesPageUrl;
        public string CurrentVersionDisplay => UpdateService.CurrentVersionDisplay;
    }
}
