using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Central registry of every TweakDefinition rendered through
    /// TweakHelpers.AddTweakRow(). Powers the Advanced Tools ▸ Settings
    /// Backup page — the equivalent of Winhance's config export/import.
    ///
    /// Export: reads the *current* state of each registered tweak
    ///         (ReadState / ReadCurrentIndex) and serializes it to JSON.
    /// Import: matches entries by tweak Id, applies via Apply / ApplyIndex,
    ///         then invokes the row's refresh Action so the UI updates live.
    /// </summary>
    public static class TweakRegistry
    {
        public const string FormatName = "akari-tool-settings";
        public const int FormatVersion = 1;

        /// <summary>
        /// Raised after an import applies tweaks, so the rendering layer can refresh
        /// aggregate UI (the net8 build's per-section "pending" pills). MVVM PORT: this
        /// replaces a direct TweakHelpers.RefreshAllSectionPills() call, keeping this
        /// logic type free of any view dependency. Null while headless.
        /// </summary>
        public static event Action? SectionsNeedRefresh;

        /// <summary>
        /// Raises <see cref="SectionsNeedRefresh"/> from outside an import — used by the
        /// Verify page after a re-apply so every tab's section "pending" pills + quick-action
        /// counts refresh (the MVVM seam net8 covered with TweakHelpers.RefreshAllSectionPills()).
        /// Additive only; the event's existing import raise is unchanged.
        /// </summary>
        public static void NotifySectionsNeedRefresh() => SectionsNeedRefresh?.Invoke();

        private static readonly List<(TweakDefinition Def, Action Refresh)> _entries = new();

        /// <summary>Called by TweakHelpers.AddTweakRow for every rendered tweak.</summary>
        public static void Register(TweakDefinition def, Action refresh) =>
            _entries.Add((def, refresh));

        public static int Count => _entries.Count;

        // ═════════════════════════════════════════════════════════════════
        //  Tab attribution + global search
        // ═════════════════════════════════════════════════════════════════

        // Entries are appended in Build() order, so each tab owns a contiguous index range.
        private static readonly List<(string Tag, string Label, int Start, int End)> _tabRanges = new();

        /// <summary>Called before a tab builds, to bracket the rows it is about to produce.</summary>
        public static int Mark() => _entries.Count;

        /// <summary>Called after a tab builds, claiming everything appended since <paramref name="start"/>.</summary>
        public static void ClaimRange(string tag, string label, int start)
        {
            if (_entries.Count > start) _tabRanges.Add((tag, label, start, _entries.Count));
        }

        /// <summary>One tab's claimed [Start, End) slice of the entries list.</summary>
        public readonly record struct TabRange(string Tag, string Label, int Start, int End)
        {
            public int Count => End - Start;
        }

        /// <summary>
        /// A read-only snapshot of every claimed tab range, in Build() order.
        /// Diagnostics only (the startup warm-up guard) — Mark/ClaimRange are the
        /// only writers and their semantics are unchanged. Note ClaimRange never
        /// records an empty range, so a tab that registered zero rows is ABSENT
        /// here rather than present-and-empty — the guard cross-checks tags to
        /// catch that case.
        /// </summary>
        public static IReadOnlyList<TabRange> TabRanges =>
            _tabRanges.Select(r => new TabRange(r.Tag, r.Label, r.Start, r.End)).ToList();

        public readonly record struct SearchHit(string Id, string Name, string Description, string TabTag, string TabLabel);

        /// <summary>Case-insensitive match on Name, Description and Id. Returns at most <paramref name="max"/> hits.</summary>
        public static List<SearchHit> Search(string query, int max = 12)
        {
            var hits = new List<SearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;
            var q = query.Trim();

            string TagFor(int i)
            {
                foreach (var r in _tabRanges) if (i >= r.Start && i < r.End) return r.Tag;
                return "";
            }
            string LabelFor(int i)
            {
                foreach (var r in _tabRanges) if (i >= r.Start && i < r.End) return r.Label;
                return "";
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count && hits.Count < max; i++)
            {
                var d = _entries[i].Def;
                bool m = (d.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (d.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (d.Id?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!m) continue;

                var tag = TagFor(i);
                if (string.IsNullOrEmpty(tag)) continue;          // unattributed row — skip
                if (!seen.Add(d.Id + "|" + tag)) continue;        // dedupe same tweak in same tab

                var id = d.Id ?? "";
                hits.Add(new SearchHit(id, d.Name ?? id, d.Description ?? "", tag, LabelFor(i)));
            }

            // Name matches first — they're the most relevant.
            return hits.OrderByDescending(h => h.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Finds the definition registered under <paramref name="id"/>.
        /// The same Id can render in multiple rows; the first wins, matching
        /// the behaviour of export and import.
        /// </summary>
        public static bool TryGetDefinition(string id, out TweakDefinition def)
        {
            foreach (var (d, _) in _entries)
            {
                if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    def = d;
                    return true;
                }
            }
            def = null!;
            return false;
        }

        /// <summary>Invokes the refresh delegate of every row rendered for this Id.</summary>
        public static void RefreshRows(string id)
        {
            foreach (var (d, refresh) in _entries)
                if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
                    try { refresh(); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Export
        // ═════════════════════════════════════════════════════════════════

        public sealed record ExportResult(int Exported, int Skipped);

        /// <summary>
        /// Serializes the current state of every registered tweak to a JSON
        /// file. Tweaks whose state can't be read (null) are skipped.
        /// </summary>
        public static ExportResult ExportToFile(string path)
        {
            var tweaks = new JsonObject();
            int exported = 0, skipped = 0;
            var seen = new HashSet<string>();

            foreach (var (def, _) in _entries)
            {
                if (!seen.Add(def.Id)) continue; // duplicate Id → first wins

                try
                {
                    if (def.InputKind == TweakInputKind.Toggle)
                    {
                        var state = def.ReadState?.Invoke();
                        if (state == null || def.Apply == null) { skipped++; continue; }

                        tweaks[def.Id] = new JsonObject
                        {
                            ["type"]  = "toggle",
                            ["name"]  = def.Name,
                            ["value"] = state.Value,
                        };
                        exported++;
                    }
                    else if (def.InputKind == TweakInputKind.Dropdown && def.Options is { Length: > 0 })
                    {
                        var idx = def.ReadCurrentIndex?.Invoke();
                        if (idx == null || idx < 0 || idx >= def.Options.Length || def.ApplyIndex == null)
                        { skipped++; continue; }

                        var opt = def.Options[idx.Value];
                        tweaks[def.Id] = new JsonObject
                        {
                            ["type"]  = "dropdown",
                            ["name"]  = def.Name,
                            ["value"] = JsonValue.Create(opt.Value?.ToString()),
                            ["label"] = opt.Label,
                        };
                        exported++;
                    }
                    else skipped++;
                }
                catch { skipped++; }
            }

            var root = new JsonObject
            {
                ["format"]     = FormatName,
                ["version"]    = FormatVersion,
                ["exportedAt"] = DateTime.UtcNow.ToString("o"),
                ["machine"]    = Environment.MachineName,
                ["tweaks"]     = tweaks,
            };

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return new ExportResult(exported, skipped);
        }

        // ═════════════════════════════════════════════════════════════════
        //  Preview
        // ═════════════════════════════════════════════════════════════════

        public sealed record PreviewEntry(
            string Id, string Name, string CurrentDisplay, string ImportedDisplay, bool Differs);

        public sealed record PreviewResult(List<PreviewEntry> Entries, int Unknown);

        /// <summary>
        /// Reads a settings file and compares each entry against the current
        /// system state without applying anything. Unknown Ids are counted.
        /// Throws InvalidDataException like ImportFromFile for bad files.
        /// </summary>
        public static PreviewResult PreviewImport(string path)
        {
            var tweaks = ParseSettingsFile(path);
            var byId = GroupEntriesById();

            var list = new List<PreviewEntry>();
            int unknown = 0;

            foreach (var (id, node) in tweaks)
            {
                if (node is not JsonObject entry) { unknown++; continue; }
                if (!byId.TryGetValue(id, out var targets)) { unknown++; continue; }

                var def = targets[0].Def;

                try
                {
                    if (def.InputKind == TweakInputKind.Toggle)
                    {
                        bool value = entry["value"]?.GetValue<bool>() ?? false;
                        var current = def.ReadState?.Invoke();

                        string cur = current == null ? "Unknown" : (current.Value ? "On" : "Off");
                        string imp = value ? "On" : "Off";
                        list.Add(new PreviewEntry(id, def.Name, cur, imp, current != value));
                    }
                    else if (def.InputKind == TweakInputKind.Dropdown && def.Options is { Length: > 0 })
                    {
                        int idx = ResolveDropdownIndex(def, entry);
                        if (idx < 0) { unknown++; continue; }

                        var current = def.ReadCurrentIndex?.Invoke();
                        string cur = current != null && current >= 0 && current < def.Options.Length
                            ? def.Options[current.Value].Label : "Unknown";
                        list.Add(new PreviewEntry(id, def.Name, cur, def.Options[idx].Label, current != idx));
                    }
                    else unknown++;
                }
                catch { unknown++; }
            }

            return new PreviewResult(list, unknown);
        }

        // ═════════════════════════════════════════════════════════════════
        //  Import
        // ═════════════════════════════════════════════════════════════════

        public sealed record ImportResult(int Applied, int AlreadySet, int Unknown, int Failed)
        {
            public int Total => Applied + AlreadySet + Unknown + Failed;
        }

        /// <summary>
        /// Reads a settings JSON file and applies every recognized tweak.
        /// Tweaks already in the target state are counted but not re-applied.
        /// Throws InvalidDataException if the file isn't an Akari settings export.
        /// </summary>
        public static ImportResult ImportFromFile(string path) => ImportFromFile(path, onlyIds: null);

        /// <summary>
        /// Same as <see cref="ImportFromFile(string)"/> but only applies entries
        /// whose Id is in <paramref name="onlyIds"/> (null = apply everything).
        /// Entries outside the set are silently skipped (not counted).
        /// </summary>
        public static ImportResult ImportFromFile(string path, ISet<string>? onlyIds)
        {
            var tweaks = ParseSettingsFile(path);
            var byId = GroupEntriesById();

            int applied = 0, alreadySet = 0, unknown = 0, failed = 0;

            DriftBaseline.BeginBatch();     // one file write for the whole import
            try
            {
            foreach (var (id, node) in tweaks)
            {
                if (onlyIds != null && !onlyIds.Contains(id)) continue;
                if (node is not JsonObject entry) { failed++; continue; }
                if (!byId.TryGetValue(id, out var targets)) { unknown++; continue; }

                var def = targets[0].Def;

                try
                {
                    if (def.InputKind == TweakInputKind.Toggle)
                    {
                        bool value = entry["value"]?.GetValue<bool>() ?? false;
                        var current = def.ReadState?.Invoke();

                        if (current == value) { alreadySet++; }
                        else if (def.Apply != null) { TweakHelpers.ApplyToggle(def, value); applied++; }
                        else { failed++; continue; }
                    }
                    else if (def.InputKind == TweakInputKind.Dropdown && def.Options is { Length: > 0 })
                    {
                        int idx = ResolveDropdownIndex(def, entry);
                        if (idx < 0) { failed++; continue; }

                        var current = def.ReadCurrentIndex?.Invoke();
                        if (current == idx) { alreadySet++; }
                        else if (def.ApplyIndex != null) { TweakHelpers.ApplyOption(def, idx); applied++; }
                        else { failed++; continue; }
                    }
                    else { failed++; continue; }

                    // refresh every rendered row for this Id so the UI reflects the new state
                    foreach (var t in targets)
                        try { t.Refresh(); } catch { }
                }
                catch { failed++; }
            }
            }
            finally { DriftBaseline.EndBatch(); }

            // Update every section's pending pill to reflect imported state.
            // MVVM PORT SEAM: the net8 build called TweakHelpers.RefreshAllSectionPills()
            // directly — a rendering call inside the import (logic) path. Replaced with a
            // hook so the logic layer stays view-free; the rendering layer subscribes to
            // it when it is rebuilt. No subscriber = no-op (correct while headless).
            SectionsNeedRefresh?.Invoke();

            return new ImportResult(applied, alreadySet, unknown, failed);
        }

        // ── shared parsing helpers ────────────────────────────────────────

        private static JsonObject ParseSettingsFile(string path)
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidDataException("Not a valid JSON file.");

            if ((string?)root["format"] != FormatName)
                throw new InvalidDataException("This file is not an Akari Tool settings export.");

            if (root["tweaks"] is not JsonObject tweaks)
                throw new InvalidDataException("Settings file contains no tweaks.");

            return tweaks;
        }

        // group registered entries by Id (same Id can render in multiple rows)
        private static Dictionary<string, List<(TweakDefinition Def, Action Refresh)>> GroupEntriesById()
        {
            var byId = new Dictionary<string, List<(TweakDefinition Def, Action Refresh)>>();
            foreach (var e in _entries)
            {
                if (!byId.TryGetValue(e.Def.Id, out var list))
                    byId[e.Def.Id] = list = new();
                list.Add(e);
            }
            return byId;
        }

        /// <summary>
        /// Finds the option index matching a raw value or label.
        /// Matches by raw value first, then by label — robust against option
        /// reordering between versions. Returns -1 when neither matches.
        /// </summary>
        public static int ResolveOptionIndex(TweakDefinition def, string? value, string? label)
        {
            if (def.Options is not { Length: > 0 }) return -1;

            if (value != null)
            {
                int i = Array.FindIndex(def.Options, o => string.Equals(o.Value?.ToString(), value, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            if (label != null)
            {
                int i = Array.FindIndex(def.Options, o => string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            return -1;
        }

        private static int ResolveDropdownIndex(TweakDefinition def, JsonObject entry) =>
            ResolveOptionIndex(def, entry["value"]?.ToString(), entry["label"]?.ToString());
    }
}
