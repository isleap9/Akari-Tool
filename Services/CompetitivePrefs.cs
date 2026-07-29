using System;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// Persists the Competitive Mode option set and the last chosen game under
    /// HKCU\Software\AkariTool, alongside the other UI preferences.
    ///
    /// Kept out of the tweak-state mechanism deliberately: these are UI choices,
    /// not applied system tweaks, and must not show up in drift verification.
    /// </summary>
    public static class CompetitivePrefs
    {
        private const string KeyPath = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string Prefix  = "Competitive";

        public static CompetitiveOptions LoadOptions()
        {
            var d = CompetitiveOptions.Default;
            try
            {
                return new CompetitiveOptions(
                    BoostGamePriority:         ReadBool("BoostGamePriority",         d.BoostGamePriority),
                    PriorityLevel:             ReadEnum("PriorityLevel",             d.PriorityLevel),
                    IoPriority:                ReadEnum("IoPriority",                d.IoPriority),
                    CpuSets:                   ReadEnum("CpuSets",                   d.CpuSets),
                    GameFocus:                 ReadBool("GameFocus",                 d.GameFocus),
                    PauseNonEssentialServices: ReadBool("PauseServices",             d.PauseNonEssentialServices),
                    ConsistentPerformance:     ReadBool("ConsistentPerformance",     d.ConsistentPerformance),
                    CloseAfterLaunch:          ReadBool("CloseAfterLaunch",          d.CloseAfterLaunch),
                    ClearStandbyMemory:        ReadBool("ClearStandbyMemory",        d.ClearStandbyMemory),
                    LaunchThroughSteam:        ReadBool("LaunchThroughSteam",        d.LaunchThroughSteam));
            }
            catch { return d; }
        }

        public static void SaveOptions(CompetitiveOptions o)
        {
            WriteBool("BoostGamePriority",     o.BoostGamePriority);
            WriteEnum("PriorityLevel",         o.PriorityLevel);
            WriteEnum("IoPriority",            o.IoPriority);
            WriteEnum("CpuSets",               o.CpuSets);
            WriteBool("GameFocus",             o.GameFocus);
            WriteBool("PauseServices",         o.PauseNonEssentialServices);
            WriteBool("ConsistentPerformance", o.ConsistentPerformance);
            WriteBool("CloseAfterLaunch",      o.CloseAfterLaunch);
            WriteBool("ClearStandbyMemory",    o.ClearStandbyMemory);
            WriteBool("LaunchThroughSteam",    o.LaunchThroughSteam);
        }

        public static string? LoadLastGamePath()
        {
            try
            {
                return Registry.GetValue(KeyPath, Prefix + "LastGame", null) as string is { Length: > 0 } s
                    ? s : null;
            }
            catch { return null; }
        }

        public static void SaveLastGamePath(string path)
        {
            try { Registry.SetValue(KeyPath, Prefix + "LastGame", path, RegistryValueKind.String); }
            catch { }
        }

        // ── Primitives ────────────────────────────────────────────────────────

        private static bool ReadBool(string name, bool fallback)
        {
            try
            {
                return Registry.GetValue(KeyPath, Prefix + name, null) is int i ? i != 0 : fallback;
            }
            catch { return fallback; }
        }

        private static void WriteBool(string name, bool value)
        {
            try { Registry.SetValue(KeyPath, Prefix + name, value ? 1 : 0, RegistryValueKind.DWord); }
            catch { }
        }

        private static T ReadEnum<T>(string name, T fallback) where T : struct, Enum
        {
            try
            {
                if (Registry.GetValue(KeyPath, Prefix + name, null) is string s &&
                    Enum.TryParse<T>(s, ignoreCase: true, out var parsed) &&
                    Enum.IsDefined(parsed))
                    return parsed;
            }
            catch { }
            return fallback;
        }

        private static void WriteEnum<T>(string name, T value) where T : struct, Enum
        {
            try { Registry.SetValue(KeyPath, Prefix + name, value.ToString()!, RegistryValueKind.String); }
            catch { }
        }
    }
}
