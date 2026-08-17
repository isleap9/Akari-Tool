using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;
namespace AkariTool.Tabs
{
    /// <summary>
    /// Records what Akari Tool wrote, so a later pass can detect when Windows
    /// (usually Windows Update) silently reverts it.
    ///
    /// Only tweaks Akari actually applied are tracked. A full snapshot of every
    /// registered tweak would flag changes the user made themselves in Windows
    /// Settings, which is noise, not drift.
    ///
    /// Every entry is stamped with the OS build it was written on, so a later
    /// scan can distinguish "this changed" from "this changed across an update".
    ///
    /// Nothing here is allowed to throw into a caller. A failure to record must
    /// never break the tweak that was just applied.
    /// </summary>
    public static class DriftBaseline
    {
        public const string FormatName = "akari-tool-baseline";
        public const int FormatVersion = 1;
        public sealed record Entry(
            string Id,
            string Type,        // "toggle" | "dropdown"
            string Name,
            bool? ToggleValue,  // toggle only
            string? OptionValue,// dropdown only — raw option Value as string
            string? OptionLabel,// dropdown only
            int? OptionIndex,   // dropdown only — fallback if value+label both miss
            DateTime AppliedAt,
            string OsBuild);
        private static readonly object _gate = new();
        private static Dictionary<string, Entry>? _entries;
        private static int _batchDepth;
        private static bool _dirty;
        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AkariTool");
        private static string FilePath => Path.Combine(Dir, "baseline.json");
        // ── OS build stamp ────────────────────────────────────────────────
        private static string? _osBuild;
        /// <summary>e.g. "26100.4061" — CurrentBuildNumber.UBR. "unknown" if unreadable.</summary>
        public static string CurrentOsBuild
        {
            get
            {
                if (_osBuild != null) return _osBuild;
                try
                {
                    using var k = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    var build = k?.GetValue("CurrentBuildNumber")?.ToString();
                    var ubr = k?.GetValue("UBR");
                    _osBuild = string.IsNullOrWhiteSpace(build)
                        ? "unknown"
                        : (ubr is int u ? $"{build}.{u}" : build);
                }
                catch (Exception ex) { 
                    _osBuild = "unknown";
                    ToolService.Current?.Log($"[DriftBaseline] OS build retrieval failed: {ex.Message}");
                }
                return _osBuild;
            }
        }
        // ── Batching (mirrors ExplorerRestart) ────────────────────────────
        /// <summary>Coalesce many Record() calls into one file write. Re-entrant.</summary>
        public static void BeginBatch()
        {
            lock (_gate) _batchDepth++;
        }
        /// <summary>Ends a batch; flushes once when the outermost batch closes.</summary>
        public static void EndBatch()
        {
            bool flush = false;
            lock (_gate)
            {
                if (_batchDepth > 0) _batchDepth--;
                flush = _batchDepth == 0 && _dirty;
            }
            if (flush) Flush();
        }
        // ── Record ────────────────────────────────────────────────────────
        /// <summary>Records that <paramref name="def"/> was set to <paramref name="value"/>.</summary>
        public static void Record(TweakDefinition def, bool value)
        {
            try
            {
                var e = new Entry(def.Id, "toggle", def.Name, value,
                                  null, null, null, DateTime.UtcNow, CurrentOsBuild);
                Put(e);
            }
            catch (Exception ex)
            {
                /* recording must never break an apply */
                ToolService.Current?.Log($"[DriftBaseline] Record failed: {ex.Message}");
            }
        }
        /// <summary>Records that <paramref name="def"/> was set to option <paramref name="index"/>.</summary>
        public static void Record(TweakDefinition def, int index)
        {
            try
            {
                if (def.Options is not { Length: > 0 }) return;
                if (index < 0 || index >= def.Options.Length) return;
                var opt = def.Options[index];
                var e = new Entry(def.Id, "dropdown", def.Name, null,
                                  opt.Value?.ToString(), opt.Label, index,
                                  DateTime.UtcNow, CurrentOsBuild);
                Put(e);
            }
            catch (Exception ex)
            {
                ToolService.Current?.Log($"[DriftBaseline] Record dropdown failed: {ex.Message}");
            }
        }
        private static void Put(Entry e)
        {
            bool flush;
            lock (_gate)
            {
                EnsureLoaded_NoLock();
                _entries![e.Id] = e;
                _dirty = true;
                flush = _batchDepth == 0;
            }
            if (flush) Flush();
        }
        // ── Read / maintenance (used by pass 2 and pass 4) ────────────────
        /// <summary>Immutable copy of everything currently tracked.</summary>
        public static IReadOnlyList<Entry> Snapshot()
        {
            lock (_gate)
            {
                EnsureLoaded_NoLock();
                return _entries!.Values.ToList();
            }
        }
        public static int Count
        {
            get { lock (_gate) { EnsureLoaded_NoLock(); return _entries!.Count; } }
        }
        /// <summary>Stop tracking one tweak (user dismissed it as intentional).</summary>
        public static void Forget(string id)
        {
            bool flush = false;
            lock (_gate)
            {
                EnsureLoaded_NoLock();
                if (_entries!.Remove(id)) { _dirty = true; flush = _batchDepth == 0; }
            }
            if (flush) Flush();
        }
        /// <summary>Wipe the baseline entirely.</summary>
        public static void Clear()
        {
            lock (_gate)
            {
                EnsureLoaded_NoLock();
                _entries!.Clear();
                _dirty = true;
            }
            Flush();
        }
        // ── Persistence ───────────────────────────────────────────────────
        private static void EnsureLoaded_NoLock()
        {
            if (_entries != null) return;
            _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject root) return;
                if ((string?)root["format"] != FormatName) return;
                if (root["entries"] is not JsonObject entries) return;
                foreach (var (id, node) in entries)
                {
                    if (node is not JsonObject o) continue;
                    try
                    {
                        string type = (string?)o["type"] ?? "toggle";
                        _entries[id] = new Entry(
                            id,
                            type,
                            (string?)o["name"] ?? id,
                            type == "toggle" ? o["value"]?.GetValue<bool>() : null,
                            type == "dropdown" ? o["value"]?.ToString() : null,
                            type == "dropdown" ? o["label"]?.ToString() : null,
                            type == "dropdown" ? o["index"]?.GetValue<int>() : null,
                            DateTime.TryParse((string?)o["appliedAt"], out var dt) ? dt : DateTime.UtcNow,
                            (string?)o["osBuild"] ?? "unknown");
                    }
                    catch (Exception ex) { ToolService.Current?.Log($"[DriftBaseline] Malformed entry skipped: {ex.Message}"); }
                }
            }
            catch (Exception ex) { ToolService.Current?.Log($"[DriftBaseline] Baseline load failed, starting fresh: {ex.Message}"); }
        }
        private static void Flush()
        {
            try
            {
                Dictionary<string, Entry> copy;
                lock (_gate)
                {
                    EnsureLoaded_NoLock();
                    copy = new Dictionary<string, Entry>(_entries!);
                    _dirty = false;
                }
                var entries = new JsonObject();
                foreach (var e in copy.Values)
                {
                    var o = new JsonObject
                    {
                        ["type"] = e.Type,
                        ["name"] = e.Name,
                        ["appliedAt"] = e.AppliedAt.ToString("o"),
                        ["osBuild"] = e.OsBuild,
                    };
                    if (e.Type == "toggle")
                    {
                        o["value"] = e.ToggleValue ?? false;
                    }
                    else
                    {
                        o["value"] = e.OptionValue;
                        o["label"] = e.OptionLabel;
                        o["index"] = e.OptionIndex;
                    }
                    entries[e.Id] = o;
                }
                var root = new JsonObject
                {
                    ["format"] = FormatName,
                    ["version"] = FormatVersion,
                    ["updatedAt"] = DateTime.UtcNow.ToString("o"),
                    ["osBuild"] = CurrentOsBuild,
                    ["entries"] = entries,
                };
                Directory.CreateDirectory(Dir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch (Exception ex) { 
                /* disk full, locked file, roaming profile weirdness — never surface */
                ToolService.Current?.Log($"[DriftBaseline] Flush failed: {ex.Message}");
            }
        }
    }
}
