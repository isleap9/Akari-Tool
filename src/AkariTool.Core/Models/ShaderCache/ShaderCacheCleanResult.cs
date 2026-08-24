namespace AkariTool.Core.Models.ShaderCache
{
    /// <summary>Result of clearing a target. <see cref="Error"/> is null on success.</summary>
    public sealed record ShaderCacheCleanResult(
        string TargetId,
        long BytesFreed,
        int FilesDeleted,
        int FilesSkipped,
        string? Error);
}
