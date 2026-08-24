namespace AkariTool.Core.Models.Update
{
    public sealed class UpdateCheckResult
    {
        public UpdateStatus Status { get; init; }
        public string? LatestTag { get; init; }        // e.g. "v2.1.0"
        public string? ReleaseName { get; init; }      // release title
        public string? ReleaseNotes { get; init; }     // markdown body
        public string? ReleasePageUrl { get; init; }   // html_url — always safe to open
        public string? InstallerUrl { get; init; }     // AkariTool-Setup-*.exe asset, if present
        public string? ErrorMessage { get; init; }
    }
}
