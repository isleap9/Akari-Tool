namespace AkariTool.ViewModels.Software;

/// <summary>How the Software &amp; Apps catalog renders each app (Winhance view-mode toggle).</summary>
public enum SoftwareViewMode
{
    Card,
    Table,
    Compact,
}

/// <summary>Sort order for the Software &amp; Apps catalog (shared toolbar Sort dropdown).</summary>
public enum SoftwareSortMode
{
    NameAsc,
    NameDesc,
    InstalledFirst,
    NotInstalledFirst,
}
