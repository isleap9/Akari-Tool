namespace AkariTool.Core.Models.ShaderCache
{
    /// <summary>Result of measuring a target without touching anything.</summary>
    public sealed record ShaderCacheScanResult(
        string TargetId,
        long TotalBytes,
        int FileCount,
        bool Exists);
}
