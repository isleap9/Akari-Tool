using AkariTool.Core.Interfaces;
using AkariTool.Core.Models.ShaderCache;
using AkariTool.Services;

namespace AkariTool.Infrastructure.Services
{
    public sealed class ShaderCacheServiceWrapper : IShaderCacheService
    {
        public IReadOnlyList<ShaderCacheTarget> GetTargets() => ShaderCacheService.GetTargets();

        public bool IsSteamInstalled() => ShaderCacheService.IsSteamInstalled();
        public bool IsSteamRunning() => ShaderCacheService.IsSteamRunning();

        public Task<IReadOnlyList<ShaderCacheScanResult>> ScanAsync(
            IEnumerable<ShaderCacheTarget> targets, CancellationToken ct = default)
            => ShaderCacheService.ScanAsync(targets, ct);

        public Task<IReadOnlyList<ShaderCacheCleanResult>> CleanAsync(
            IEnumerable<ShaderCacheTarget> targets,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
            => ShaderCacheService.CleanAsync(targets, progress, ct);

        public string FormatBytes(long bytes) => ShaderCacheService.FormatBytes(bytes);
    }
}
