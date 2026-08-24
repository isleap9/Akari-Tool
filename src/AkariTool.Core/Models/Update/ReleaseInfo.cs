namespace AkariTool.Core.Models.Update
{
    public sealed class ReleaseInfo
    {
        public string Tag { get; init; } = "";         // "v2.0.0"
        public string Name { get; init; } = "";
        public string Body { get; init; } = "";        // release notes (markdown)
        public DateTime PublishedUtc { get; init; }
        public bool IsCurrent { get; init; }
    }
}
