using AkariTool.Core.Models.ShaderCache;

namespace AkariTool.Core.Interfaces
{
    public interface IShaderCacheService
    {
        IReadOnlyList<ShaderCacheTarget> GetTargets();

        bool IsSteamInstalled();
        bool IsSteamRunning();

        Task<IReadOnlyList<ShaderCacheScanResult>> ScanAsync(
            IEnumerable<ShaderCacheTarget> targets, CancellationToken ct = default);

        Task<IReadOnlyList<ShaderCacheCleanResult>> CleanAsync(
            IEnumerable<ShaderCacheTarget> targets,
            IProgress<string>? progress = null,
            CancellationToken ct = default);

        string FormatBytes(long bytes);
    }
}
