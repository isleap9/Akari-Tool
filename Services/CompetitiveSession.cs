using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AkariTool.Services
{
    public sealed record SuspendedProcess(int Pid, string Name);

    public sealed record StoppedService(
        string Name, string PriorStartType, bool WasRunning);

    /// <summary>
    /// Everything needed to undo a Competitive Mode session, including from a
    /// different process after a crash. Every mutation the session performs must
    /// be represented here before it is performed.
    /// </summary>
    public sealed record CompetitiveSessionState(
        string GameExePath,
        string GameProcessName,
        DateTime StartedUtc,
        string? PreviousPowerSchemeGuid,
        IReadOnlyList<SuspendedProcess> SuspendedProcesses,
        IReadOnlyList<StoppedService> StoppedServices,
        IReadOnlyList<string> TuningFailures);

    /// <summary>
    /// On-disk record of the active session at
    /// %APPDATA%\AkariTool\competitive-session.json.
    ///
    /// The file is written BEFORE each class of mutation and updated incrementally
    /// afterwards, so a crash — or a hard power loss — mid-apply still leaves a
    /// record that covers everything already changed. A record listing a change
    /// that never happened is harmless (resume of a live PID, restore of an
    /// unchanged service); a change with no record is not recoverable.
    /// </summary>
    public static class CompetitiveSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AkariTool", "competitive-session.json");

        public static bool TryLoad(out CompetitiveSessionState state)
        {
            state = null!;
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return false;

                var loaded = JsonSerializer.Deserialize<CompetitiveSessionState>(
                    File.ReadAllText(path), JsonOptions);
                if (loaded is null) return false;

                state = loaded;
                return true;
            }
            catch
            {
                // A truncated or hand-edited file must not block startup. Treat it
                // as "no session" — the alternative is nagging on every launch with
                // a prompt whose Restore does nothing.
                return false;
            }
        }

        public static void Save(CompetitiveSessionState state)
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
            }
            catch { /* best-effort — an unwritable record must not abort the session */ }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }
    }
}
