using System.Diagnostics;
using System.IO;
using System.Text;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    public enum DriftKind
    {
        /// <summary>Now sitting on the Windows factory value — the Windows Update signature.</summary>
        RevertedToWindowsDefault,
        /// <summary>Changed to something that is neither what Akari wrote nor the Windows default.</summary>
        ChangedToOther,
    }

    public sealed record DriftItem(
        string Id,
        string Name,
        DriftKind Kind,
        string RecordedDisplay,
        string CurrentDisplay,
        DateTime RecordedAt,
        string RecordedOsBuild,
        bool AcrossOsUpdate,
        bool? RecordedToggle,   // toggle tweaks — the value Akari wrote
        int? RecordedIndex);    // dropdown tweaks — the option index Akari wrote

    public sealed record DriftScanResult(
        IReadOnlyList<DriftItem> Drifted,
        int Tracked,
        int StillMatching,
        int Unreadable,
        int Orphaned,
        string CurrentOsBuild,
        TimeSpan Duration,
        DateTime ScannedAt)
    {
        public bool HasDrift => Drifted.Count > 0;
        public int RevertedCount => Drifted.Count(d => d.Kind == DriftKind.RevertedToWindowsDefault);
        public int ChangedCount  => Drifted.Count(d => d.Kind == DriftKind.ChangedToOther);
    }

    /// <summary>
    /// Compares the drift baseline against live system state.
    ///
    /// Runs synchronously on the caller's thread by design. The baseline only
    /// holds tweaks the user actually applied — a few dozen at most — and the
    /// ReadState delegates are registry reads. Going async would mean calling
    /// arbitrary registered delegates off the UI thread, which is a much larger
    /// promise than this feature needs. Duration is measured and logged; if it
    /// ever becomes a problem the fix is to audit slow readers, not to Task.Run
    /// blindly.
    /// </summary>
    public static class DriftScanner
    {
        /// <summary>Result of the most recent scan this session, or null.</summary>
        public static DriftScanResult? Last { get; private set; }

        public static DriftScanResult Scan()
        {
            var sw = Stopwatch.StartNew();
            var baseline = DriftBaseline.Snapshot();
            string osNow = DriftBaseline.CurrentOsBuild;

            var drifted = new List<DriftItem>();
            int matching = 0, unreadable = 0, orphaned = 0;

            foreach (var e in baseline)
            {
                if (!TweakRegistry.TryGetDefinition(e.Id, out var def)) { orphaned++; continue; }

                try
                {
                    bool acrossUpdate = !string.Equals(e.OsBuild, osNow, StringComparison.OrdinalIgnoreCase)
                                        && e.OsBuild != "unknown" && osNow != "unknown";

                    if (e.Type == "toggle" && def.InputKind == TweakInputKind.Toggle)
                    {
                        var current = def.ReadState?.Invoke();
                        if (current == null) { unreadable++; continue; }

                        bool recorded = e.ToggleValue ?? false;
                        if (current.Value == recorded) { matching++; continue; }

                        var kind = def.DefaultState.HasValue && current.Value == def.DefaultState.Value
                            ? DriftKind.RevertedToWindowsDefault
                            : DriftKind.ChangedToOther;

                        drifted.Add(new DriftItem(
                            e.Id, def.Name, kind,
                            recorded ? "On" : "Off",
                            current.Value ? "On" : "Off",
                            e.AppliedAt, e.OsBuild, acrossUpdate,
                            recorded, null));
                    }
                    else if (e.Type == "dropdown" && def.InputKind == TweakInputKind.Dropdown
                             && def.Options is { Length: > 0 })
                    {
                        int recordedIdx = TweakRegistry.ResolveOptionIndex(def, e.OptionValue, e.OptionLabel);
                        if (recordedIdx < 0 && e.OptionIndex is int fallback
                            && fallback >= 0 && fallback < def.Options.Length)
                            recordedIdx = fallback;

                        // The recorded option no longer exists in this build of the app.
                        if (recordedIdx < 0) { orphaned++; continue; }

                        var current = def.ReadCurrentIndex?.Invoke();
                        if (current == null) { unreadable++; continue; }
                        if (current.Value == recordedIdx) { matching++; continue; }

                        // -1 = live value matches no listed option. That is drift, and it
                        // is never "the Windows default" because the default is a listed option.
                        bool isDefaultNow = current.Value >= 0
                                            && current.Value < def.Options.Length
                                            && def.Options[current.Value].IsDefault;

                        drifted.Add(new DriftItem(
                            e.Id, def.Name,
                            isDefaultNow ? DriftKind.RevertedToWindowsDefault : DriftKind.ChangedToOther,
                            def.Options[recordedIdx].Label,
                            current.Value >= 0 && current.Value < def.Options.Length
                                ? def.Options[current.Value].Label
                                : "(unlisted value)",
                            e.AppliedAt, e.OsBuild, acrossUpdate,
                            null, recordedIdx));
                    }
                    else
                    {
                        // Type changed between versions (toggle became dropdown or vice versa).
                        orphaned++;
                    }
                }
                catch { unreadable++; }
            }

            sw.Stop();
            var result = new DriftScanResult(
                drifted, baseline.Count, matching, unreadable, orphaned,
                osNow, sw.Elapsed, DateTime.Now);

            Last = result;
            WriteLog(result);
            return result;
        }

        // ── Diagnostic log ────────────────────────────────────────────────
        // Pass 2 has no UI. This file is how you observe drift over the next
        // few weeks of real Windows Updates before committing to pass 4.

        private static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AkariTool", "drift-scan.log");

        private const long MaxLogBytes = 256 * 1024;

        private static void WriteLog(DriftScanResult r)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== {r.ScannedAt:yyyy-MM-dd HH:mm:ss}  build {r.CurrentOsBuild}  ({r.Duration.TotalMilliseconds:F0} ms)");
                sb.AppendLine($"    tracked {r.Tracked} · matching {r.StillMatching} · drifted {r.Drifted.Count} " +
                              $"(reverted {r.RevertedCount}, changed {r.ChangedCount}) · unreadable {r.Unreadable} · orphaned {r.Orphaned}");

                foreach (var d in r.Drifted)
                {
                    string tag = d.Kind == DriftKind.RevertedToWindowsDefault ? "REVERTED" : "CHANGED ";
                    sb.AppendLine($"    {tag} {d.Id}: {d.RecordedDisplay} -> {d.CurrentDisplay}" +
                                  $"  (set {d.RecordedAt.ToLocalTime():yyyy-MM-dd} on {d.RecordedOsBuild}" +
                                  $"{(d.AcrossOsUpdate ? ", ACROSS UPDATE" : "")})");
                }

                var dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);

                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                {
                    var lines = File.ReadAllLines(LogPath);
                    File.WriteAllLines(LogPath, lines.Skip(lines.Length / 2));
                }

                File.AppendAllText(LogPath, sb.ToString());
            }
            catch { /* diagnostics must never break startup */ }
        }
    }
}
