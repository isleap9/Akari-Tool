using System;
using System.Collections.Generic;

namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// Akari Profile v2 envelope (Winhance UnifiedConfigurationFile shape, adapted):
/// one versioned JSON file covering every tweak area, organized by feature
/// section, each section/subsection include-toggled so imports can be scoped.
///
/// Layout:
/// {
///   "format": "akari-tool-settings",
///   "version": 2,
///   "exportedAt": "...", "machine": "...",
///   "sections": {
///     "Gaming": { "included": true, "groups": {
///        "Game Mode": { "included": true,
///          "items": { "&lt;settingId&gt;": { ...same item payload as v1... } } } } },
///     ...
///   }
/// }
///
/// Item payloads are byte-identical to v1 ({type,name,value} / dropdown /
/// numeric powerSettings), so per-item import logic is shared unchanged.
/// Version-1 files (flat "tweaks" object) are imported via the compat path.
/// </summary>
public sealed class AkariProfile
{
    public const int CurrentVersion = 2;

    public string Format { get; set; } = "akari-tool-settings";
    public int Version { get; set; } = CurrentVersion;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string Machine { get; set; } = Environment.MachineName;

    /// <summary>Feature-area → section. Key is the page NavTag (e.g. "Gaming").</summary>
    public Dictionary<string, AkariProfileSection> Sections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One feature area (a declarative tab) within an Akari Profile.</summary>
public sealed class AkariProfileSection
{
    public bool Included { get; set; } = true;

    /// <summary>Group title → subsection. Key is SettingGroup.Name (e.g. "Layout").</summary>
    public Dictionary<string, AkariProfileGroup> Groups { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Convenience count for UI summaries.</summary>
    public int CountItems()
    {
        int n = 0;
        foreach (var g in Groups.Values)
            n += g.Items.Count;
        return n;
    }
}

/// <summary>One titled group inside a section, itself include-togglable.</summary>
public sealed class AkariProfileGroup
{
    public bool Included { get; set; } = true;

    /// <summary>Setting id → v1-compatible item payload.</summary>
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> Items { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
