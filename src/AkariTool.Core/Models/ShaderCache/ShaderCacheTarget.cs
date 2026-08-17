namespace AkariTool.Core.Models.ShaderCache
{
    /// <summary>One cleanable shader cache vendor and every directory it owns.</summary>
    public sealed record ShaderCacheTarget(
        string Id,
        string DisplayName,
        IReadOnlyList<string> Paths);
}
