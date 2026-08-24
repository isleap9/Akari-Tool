namespace AkariTool.Core.Models.Update
{
    public enum UpdateStatus
    {
        UpToDate,          // latest release <= current version
        UpdateAvailable,   // latest release > current version
        NoReleases,        // repo has no published releases yet (404)
        Error              // network / API failure
    }
}
