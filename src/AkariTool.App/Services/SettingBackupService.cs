using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Utilities;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.Services;

/// <summary>
/// SettingDefinition-native backup/restore + global search (Phase 3b-ii).
///
/// TweakRegistry holds zero entries since Session C (every tweak page moved to the
/// <see cref="SettingPageViewModel"/> marker), so the legacy registry-backed export/import
/// and global search silently produce nothing. This service replaces that path by reading
/// and writing live <see cref="SettingItemViewModel"/> instances registered from
/// <see cref="SettingPageWarmUp"/>.
///
/// JSON envelope is byte-identical to <see cref="TweakRegistry"/> (format
/// "akari-tool-settings", version 1, "tweaks" object) so old backup files remain
/// importable. Per-item payloads are Winhance-equivalent (enriched v1):
///   - Toggle/CheckBox  → { type:"toggle",  name, value }
///   - Selection        → { type:"dropdown", name, value:<index>, label }
///   - Power Plan       → { type:"dropdown", name, powerPlanGuid, powerPlanName }
///   - NumericRange     → { type:"numeric", name, powerSettings:{ ACValue, DCValue } | { Value } }
///     Numeric values are stored in SYSTEM units (raw powercfg seconds) and converted to
///     display units on import — Winhance's ConfigExportService/ConfigurationApplicationBridgeService
///     contract, NOT Akari's display-unit VM surface.
///
/// Import applies via <see cref="ISettingOperationExecutor"/> directly (silent, no
/// confirmation dialogs) — Winhance's ConfigApplicationExecutionService path, NOT the
/// user-click ViewModel setters — then refreshes the affected rows from the system.
/// </summary>
public sealed class SettingBackupService
{
    private readonly ISettingOperationExecutor _executor;
    private readonly List<SettingPageViewModel> _pages = new();

    public SettingBackupService(ISettingOperationExecutor executor)
    {
        _executor = executor;
    }

    /// <summary>Registers a declarative page after its Build() so its rows are covered.</summary>
    public void Register(SettingPageViewModel vm)
    {
        if (vm == null) return;
        if (!_pages.Contains(vm)) _pages.Add(vm);
    }

    /// <summary>Total exportable rows across all registered pages (excludes Action rows —
    /// toggles, dropdowns, numeric ranges and the Power Plan row all serialize).</summary>
    public int Count => EnumerateItems().Count(IsExportable);

    // ── Item enumeration ────────────────────────────────────────────────────────

    private IEnumerable<SettingItemViewModel> EnumerateItems()
    {
        foreach (var page in _pages)
            foreach (var section in page.Sections)
                foreach (var row in section.Items)
                    if (row is SettingItemViewModel item)
                        yield return item;
    }

    private static bool IsExportable(SettingItemViewModel item) =>
        item.InputType != InputType.Action;

    private static bool IsToggleLike(SettingItemViewModel item) =>
        item.InputType is InputType.Toggle or InputType.CheckBox;

    private static string GetDisplayUnits(SettingItemViewModel item) => item.Units;

    // ── Export ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes one exportable row to its v1-compatible payload node.
    /// Shared by v2 export; kept instance-private so numeric unit conversion
    /// helpers stay in one place. Returns null when the row's state is
    /// unreadable (caller counts it as skipped).
    /// </summary>
    private JsonObject? BuildItemPayload(SettingItemViewModel item)
    {
        if (IsToggleLike(item))
        {
            return new JsonObject
            {
                ["type"]  = "toggle",
                ["name"]  = item.Name,
                ["value"] = item.IsOn,
            };
        }

        if (item.InputType == InputType.NumericRange)
        {
            var node = new JsonObject
            {
                ["type"] = "numeric",
                ["name"] = item.Name,
            };
            var power = new JsonObject();
            if (item.SupportsSeparateACDC)
            {
                power["ACValue"] = NumericConversionHelper.ConvertToSystemUnits(item.AcNumericValue, GetDisplayUnits(item));
                power["DCValue"] = NumericConversionHelper.ConvertToSystemUnits(item.DcNumericValue, GetDisplayUnits(item));
            }
            else
            {
                power["Value"] = NumericConversionHelper.ConvertToSystemUnits(item.NumericValue, GetDisplayUnits(item));
            }
            node["powerSettings"] = power;
            return node;
        }

        if (item.IsPowerPlanSetting)
        {
            var plan = item.PlanOptions.ElementAtOrDefault(item.SelectedIndex);
            string? guid = plan?.SystemPlan?.Guid ?? plan?.PredefinedPlan?.Guid;
            if (string.IsNullOrEmpty(guid)) return null;

            return new JsonObject
            {
                ["type"]          = "dropdown",
                ["name"]          = item.Name,
                ["powerPlanGuid"] = guid,
                ["powerPlanName"] = plan?.DisplayName ?? "",
            };
        }

        int idx = item.SelectedIndex;
        if (idx < 0 || idx >= item.Options.Length) return null;

        return new JsonObject
        {
            ["type"]  = "dropdown",
            ["name"]  = item.Name,
            ["value"] = idx,
            ["label"] = item.Options[idx] ?? "",
        };
    }

    public TweakRegistry.ExportResult ExportToFile(string path)
    {
        int exported = 0, skipped = 0;
        var seen = new HashSet<string>();

        // v2 Akari Profile: sections keyed by page NavTag, groups keyed by
        // SettingGroup.Name — Winhance UnifiedConfigurationFile shape with
        // include flags at both levels.
        var profile = new AkariProfile();
        var navTagByPage = _pages.ToDictionary(p => p, p => p.NavTag);

        foreach (var (page, navTag) in navTagByPage)
        {
            var sectionNode = new AkariProfileSection();
            foreach (var section in page.Sections)
            {
                var groupNode = new AkariProfileGroup();
                foreach (var row in section.Items)
                {
                    if (row is not SettingItemViewModel item) continue;
                    if (!seen.Add(item.Id)) continue; // duplicate Id → first wins

                    try
                    {
                        if (!IsExportable(item)) { skipped++; continue; }
                        var payload = BuildItemPayload(item);
                        if (payload == null) { skipped++; continue; }
                        groupNode.Items[item.Id] = payload;
                        exported++;
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                if (groupNode.Items.Count > 0)
                    sectionNode.Groups[section.Title] = groupNode;
            }

            if (sectionNode.Groups.Count > 0)
                profile.Sections[navTag] = sectionNode;
        }

        var root = new JsonObject
        {
            ["format"]     = TweakRegistry.FormatName,
            ["version"]    = AkariProfile.CurrentVersion,
            ["exportedAt"] = DateTime.UtcNow.ToString("o"),
            ["machine"]    = Environment.MachineName,
            ["sections"]   = SerializeSections(profile.Sections),
        };

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return new TweakRegistry.ExportResult(exported, skipped);
    }

    private static JsonObject SerializeSections(Dictionary<string, AkariProfileSection> sections)
    {
        var node = new JsonObject();
        foreach (var (name, section) in sections)
        {
            var groups = new JsonObject();
            foreach (var (gname, group) in section.Groups)
            {
                var items = new JsonObject();
                foreach (var (iid, payload) in group.Items)
                    items[iid] = payload.DeepClone();
                groups[gname] = new JsonObject
                {
                    ["included"] = group.Included,
                    ["items"]    = items,
                };
            }
            node[name] = new JsonObject
            {
                ["included"] = section.Included,
                ["groups"]   = groups,
            };
        }
        return node;
    }

    // ── Preview ─────────────────────────────────────────────────────────────────

    public TweakRegistry.PreviewResult PreviewImport(string path)
        => PreviewImport(path, scope: null);

    public TweakRegistry.PreviewResult PreviewImport(string path, AkariProfileScope? scope)
    {
        var profile = ParseProfileFile(path);
        var tweaks = FilterByScope(profile, scope);
        var byId = BuildItemLookup();

        var list = new List<TweakRegistry.PreviewEntry>();
        int unknown = 0;

        foreach (var (id, node) in tweaks)
        {
            if (node is not JsonObject entry) { unknown++; continue; }
            if (!byId.TryGetValue(id, out var item)) { unknown++; continue; }

            try
            {
                if (IsToggleLike(item))
                {
                    bool value = entry["value"]?.GetValue<bool>() ?? false;
                    string cur = item.IsOn ? "On" : "Off";
                    list.Add(new TweakRegistry.PreviewEntry(item.Id, item.Name, cur, value ? "On" : "Off", item.IsOn != value));
                }
                else if (item.IsPowerPlanSetting)
                {
                    string? exportedGuid = entry["powerPlanGuid"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(exportedGuid)) { unknown++; continue; }

                    var current = item.PlanOptions.FirstOrDefault(o => o.IsActive);
                    string cur = current?.DisplayName ?? "Unknown";
                    string imp = entry["powerPlanName"]?.GetValue<string>() ?? exportedGuid;
                    bool differs = !string.Equals(
                        current?.SystemPlan?.Guid ?? current?.PredefinedPlan?.Guid,
                        exportedGuid, StringComparison.OrdinalIgnoreCase);
                    list.Add(new TweakRegistry.PreviewEntry(item.Id, item.Name, cur, imp, differs));
                }
                else if (item.InputType == InputType.NumericRange)
                {
                    var power = entry["powerSettings"] as JsonObject;
                    if (power == null) { unknown++; continue; }

                    if (item.SupportsSeparateACDC)
                    {
                        int? acSystem = power["ACValue"]?.GetValue<int>();
                        int? dcSystem = power["DCValue"]?.GetValue<int>();
                        if (acSystem == null && dcSystem == null) { unknown++; continue; }

                        string cur = FormatNumericPair(item.AcNumericValue, item.DcNumericValue);
                        string imp = FormatNumericPair(
                            acSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(acSystem.Value, GetDisplayUnits(item)) : item.AcNumericValue,
                            dcSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(dcSystem.Value, GetDisplayUnits(item)) : item.DcNumericValue);
                        bool differs = item.AcNumericValue != (acSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(acSystem.Value, GetDisplayUnits(item)) : item.AcNumericValue)
                                    || item.DcNumericValue != (dcSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(dcSystem.Value, GetDisplayUnits(item)) : item.DcNumericValue);
                        list.Add(new TweakRegistry.PreviewEntry(item.Id, item.Name, cur, imp, differs));
                    }
                    else
                    {
                        int? sysValue = power["Value"]?.GetValue<int>();
                        if (sysValue == null) { unknown++; continue; }

                        int impValue = NumericConversionHelper.ConvertFromSystemUnits(sysValue.Value, GetDisplayUnits(item));
                        list.Add(new TweakRegistry.PreviewEntry(
                            item.Id, item.Name, item.NumericValue.ToString(), impValue.ToString(), item.NumericValue != impValue));
                    }
                }
                else
                {
                    int idx = entry["value"]?.GetValue<int>() ?? -1;
                    if (idx < 0 || idx >= item.Options.Length) { unknown++; continue; }

                    string cur = item.SelectedIndex >= 0 && item.SelectedIndex < item.Options.Length
                        ? item.Options[item.SelectedIndex] ?? ""
                        : "Unknown";
                    list.Add(new TweakRegistry.PreviewEntry(item.Id, item.Name, cur, item.Options[idx] ?? "", item.SelectedIndex != idx));
                }
            }
            catch
            {
                unknown++;
            }
        }

        return new TweakRegistry.PreviewResult(list, unknown);
    }

    private static string FormatNumericPair(int ac, int dc) => $"AC {ac} / DC {dc}";

    // ── Import ──────────────────────────────────────────────────────────────────

    public async Task<TweakRegistry.ImportResult> ImportFromFile(string path, ISet<string>? onlyIds)
        => await ImportFromFile(path, onlyIds, scope: null);

    public async Task<TweakRegistry.ImportResult> ImportFromFile(string path, ISet<string>? onlyIds, AkariProfileScope? scope)
    {
        var profile = ParseProfileFile(path);
        var tweaks = FilterByScope(profile, scope);
        var byId = BuildItemLookup();

        // Winhance ConfigApplicationExecutionService parity: config imports run a
        // Group Policy cleanup first (LGPO/AD leftovers silently override HKCU
        // tweaks), and every applied change lands in the change-history receipt
        // inside one batch.
        var policyCleanup = WinUI.Framework.IoC.ServiceLocator
            .GetService<AkariTool.Core.Features.Common.Interfaces.IPolicyCleanupService>();
        var changeHistory = WinUI.Framework.IoC.ServiceLocator
            .GetService<AkariTool.Core.Features.Common.Interfaces.IChangeHistoryService>();
        if (policyCleanup != null)
            await Task.Run(policyCleanup.CleanupPolicyKeys).ConfigureAwait(false);
        IDisposable? historyBatch = changeHistory?.BeginBatch($"Imported Akari Profile ({DateTime.Now:yyyy-MM-dd HH:mm})");

        int applied = 0, alreadySet = 0, unknown = 0, failed = 0;

        foreach (var (id, node) in tweaks)
        {
            if (onlyIds != null && !onlyIds.Contains(id)) continue;
            if (node is not JsonObject entry) { failed++; continue; }
            if (!byId.TryGetValue(id, out var item)) { unknown++; continue; }

            try
            {
                if (IsToggleLike(item))
                {
                    bool value = entry["value"]?.GetValue<bool>() ?? false;
                    if (item.IsOn == value)
                    {
                        alreadySet++;
                    }
                    else
                    {
                        var result = await _executor.ApplySettingOperationsAsync(item.Definition, value, null);
                        if (!result.Success) { failed++; continue; }
                        item.RefreshFromSystem();
                        applied++;
                    }
                }
                else if (item.IsPowerPlanSetting)
                {
                    string? exportedGuid = entry["powerPlanGuid"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(exportedGuid)) { failed++; continue; }

                    var current = item.PlanOptions.FirstOrDefault(o => o.IsActive);
                    if (string.Equals(
                            current?.SystemPlan?.Guid ?? current?.PredefinedPlan?.Guid,
                            exportedGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadySet++;
                    }
                    else if (await item.ApplyPowerPlanByGuidAsync(exportedGuid))
                    {
                        applied++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                else if (item.InputType == InputType.NumericRange)
                {
                    var power = entry["powerSettings"] as JsonObject;
                    if (power == null) { failed++; continue; }

                    if (item.SupportsSeparateACDC)
                    {
                        int? acSystem = power["ACValue"]?.GetValue<int>();
                        int? dcSystem = power["DCValue"]?.GetValue<int>();
                        if (acSystem == null && dcSystem == null) { failed++; continue; }

                        int acTarget = acSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(acSystem.Value, GetDisplayUnits(item)) : item.AcNumericValue;
                        int dcTarget = dcSystem.HasValue ? NumericConversionHelper.ConvertFromSystemUnits(dcSystem.Value, GetDisplayUnits(item)) : item.DcNumericValue;

                        if (item.AcNumericValue == acTarget && item.DcNumericValue == dcTarget)
                        {
                            alreadySet++;
                        }
                        else
                        {
                            var dict = new Dictionary<string, object?>
                            {
                                ["ACValue"] = acTarget,
                                ["DCValue"] = dcTarget,
                            };
                            var result = await _executor.ApplySettingOperationsAsync(item.Definition, true, dict);
                            if (!result.Success) { failed++; continue; }
                            item.RefreshFromSystem();
                            applied++;
                        }
                    }
                    else
                    {
                        int? sysValue = power["Value"]?.GetValue<int>();
                        if (sysValue == null) { failed++; continue; }

                        int target = NumericConversionHelper.ConvertFromSystemUnits(sysValue.Value, GetDisplayUnits(item));
                        if (item.NumericValue == target)
                        {
                            alreadySet++;
                        }
                        else
                        {
                            var result = await _executor.ApplySettingOperationsAsync(item.Definition, true, target);
                            if (!result.Success) { failed++; continue; }
                            item.RefreshFromSystem();
                            applied++;
                        }
                    }
                }
                else
                {
                    int idx = entry["value"]?.GetValue<int>() ?? -1;
                    if (idx < 0 || idx >= item.Options.Length) { failed++; continue; }

                    if (item.SelectedIndex == idx)
                    {
                        alreadySet++;
                    }
                    else
                    {
                        var result = await _executor.ApplySettingOperationsAsync(item.Definition, true, idx);
                        if (!result.Success) { failed++; continue; }
                        item.RefreshFromSystem();
                        applied++;
                    }
                }
            }
            catch
            {
                failed++;
            }
        }

        // After all entries: end the history batch, refresh aggregate UI + quick counts.
        historyBatch?.Dispose();
        TweakRegistry.NotifySectionsNeedRefresh();
        foreach (var page in _pages)
            page.RefreshQuickActionCounts();

        return new TweakRegistry.ImportResult(applied, alreadySet, unknown, failed);
    }

    // ── Global search ───────────────────────────────────────────────────────────

    public List<TweakRegistry.SearchHit> Search(string query, int max = 12)
    {
        var hits = new List<TweakRegistry.SearchHit>();
        if (string.IsNullOrWhiteSpace(query)) return hits;
        var q = query.Trim();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in _pages)
        {
            foreach (var item in EnumerateItems(page))
            {
                bool m = (item.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (item.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (item.Id?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!m) continue;
                if (!seen.Add(item.Id + "|" + page.NavTag)) continue;

                var id = item.Id ?? "";
                hits.Add(new TweakRegistry.SearchHit(id, item.Name ?? id, item.Description ?? "", page.NavTag, page.NavLabel));
                if (hits.Count >= max) break;
            }
            if (hits.Count >= max) break;
        }

        // Name matches first — they're the most relevant.
        return hits.OrderByDescending(h => h.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static IEnumerable<SettingItemViewModel> EnumerateItems(SettingPageViewModel page)
    {
        foreach (var section in page.Sections)
            foreach (var row in section.Items)
                if (row is SettingItemViewModel item)
                    yield return item;
    }

    // ── Shared parsing helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Parsed file contents: flat id → payload map plus per-section / per-group
    /// include flags. v2 files carry sections/groups; v1 files come back as one
    /// implicit section with everything included (compat path).
    /// </summary>
    public sealed record ParsedProfile(
        Dictionary<string, JsonObject> Items,
        IReadOnlyDictionary<string, bool> SectionIncluded,
        IReadOnlyDictionary<string, bool> GroupIncluded,
        int Version);

    private ParsedProfile ParseProfileFile(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException("Not a valid JSON file.");

        if ((string?)root["format"] != TweakRegistry.FormatName)
            throw new InvalidDataException("This file is not an Akari Tool settings export.");

        int version = root["version"]?.GetValue<int>() ?? TweakRegistry.FormatVersion;

        var items = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var sectionFlags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var groupFlags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        _itemOwners.Clear();

        if (root["sections"] is JsonObject sections)
        {
            // ── v2: sectioned envelope ──
            foreach (var (sectionName, sectionNode) in sections)
            {
                if (sectionNode is not JsonObject sObj) continue;
                sectionFlags[sectionName] = sObj["included"]?.GetValue<bool>() ?? true;

                if (sObj["groups"] is not JsonObject groups) continue;
                foreach (var (groupName, groupNode) in groups)
                {
                    if (groupNode is not JsonObject gObj) continue;
                    // Group flag keyed "section|group" so identical group titles
                    // across pages can't collide.
                    groupFlags[$"{sectionName}|{groupName}"] = gObj["included"]?.GetValue<bool>() ?? true;

                    if (gObj["items"] is not JsonObject groupItems) continue;
                    foreach (var (id, payload) in groupItems)
                    {
                        if (payload is not JsonObject p) continue;
                        // First occurrence wins; owner recorded for the surviving entry.
                        if (!items.ContainsKey(id))
                        {
                            items[id] = p;
                            _itemOwners[id] = (sectionName, groupName);
                        }
                    }
                }
            }
        }
        else if (root["tweaks"] is JsonObject tweaks)
        {
            // ── v1 compat: flat tweaks object — everything included ──
            foreach (var (id, payload) in tweaks)
                if (payload is JsonObject p)
                    items[id] = p;
        }
        else
        {
            throw new InvalidDataException("Settings file contains no tweaks.");
        }

        return new ParsedProfile(items, sectionFlags, groupFlags, version);
    }

    /// <summary>
    /// Import scope: which sections (and optionally groups) to include when
    /// previewing/applying a profile. Null = everything included.
    /// </summary>
    public sealed record AkariProfileScope(
        ISet<string>? IncludedSections = null,
        IReadOnlyDictionary<string, bool>? GroupOverrides = null);

    /// <summary>
    /// Filters a parsed profile by scope + file-embedded include flags. A setting
    /// survives when: scope allows its section (or scope is null), the section's
    /// file flag is true, and its group's file flag is true. Scope group overrides
    /// win over file flags (keyed "section|group").
    /// </summary>
    private Dictionary<string, JsonObject> FilterByScope(ParsedProfile profile, AkariProfileScope? scope)
    {
        if (scope is null && profile.SectionIncluded.Count == 0)
            return profile.Items;

        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        // Rebuild id→(section|group) membership from the file's own structure.
        // ParseProfileFile flattens, so re-walk flags via item ownership recorded
        // during parse — simplest correct approach: keep a second map there. For
        // now derive from the raw file again (cheap; files are small).
        foreach (var (id, payload) in profile.Items)
        {
            var owner = ItemOwner(id);
            bool include = true;

            if (profile.SectionIncluded.TryGetValue(owner.section, out var secFlag))
                include &= secFlag;
            var groupKey = $"{owner.section}|{owner.group}";
            if (profile.GroupIncluded.TryGetValue(groupKey, out var grpFlag))
                include &= grpFlag;

            if (scope?.GroupOverrides != null
                && scope.GroupOverrides.TryGetValue(groupKey, out var overrideFlag))
                include &= overrideFlag;

            if (scope?.IncludedSections != null
                && !scope.IncludedSections.Contains(owner.section))
                include = false;

            if (include)
                result[id] = payload;
        }
        return result;
    }

    /// <summary>Section/group an id belongs to, from the last parse (v1: "","").</summary>
    private readonly Dictionary<string, (string section, string group)> _itemOwners =
        new(StringComparer.OrdinalIgnoreCase);

    private (string section, string group) ItemOwner(string id)
        => _itemOwners.TryGetValue(id, out var owner) ? owner : ("", "");

    /// <summary>
    /// Public read-only view of the last parsed profile's section/group ownership,
    /// for UI grouping (import review dialog). v1 files yield empty owners.
    /// </summary>
    public IReadOnlyDictionary<string, (string Section, string Group)> ItemOwners => _itemOwners;

    // group registered rows by Id (same Id can render in multiple rows) — first wins
    private Dictionary<string, SettingItemViewModel> BuildItemLookup()
    {
        var byId = new Dictionary<string, SettingItemViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in EnumerateItems())
            if (!byId.ContainsKey(item.Id))
                byId[item.Id] = item;
        return byId;
    }
}