using Microsoft.Win32;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Small UI-preference store (section collapse state).
    ///
    /// MVVM PORT: ReadUiPref / WriteUiPref carried over verbatim from the net8
    /// factory partial <c>TweakHelpers.Controls.cs</c>, including the key path and
    /// the value naming scheme ("SectionCollapsed_&lt;TitleWithoutSpaces&gt;"), so a
    /// user's collapsed sections survive the move between builds.
    ///
    /// Deliberately NOT the framework ISettingsService: this is a UI preference that
    /// must stay byte-compatible with the net8 build's registry values. It is also
    /// distinct from TweakHelpers.SaveState/HasState/ClearState, which mark APPLIED
    /// TWEAKS rather than UI state (same key, different purpose — as in net8).
    /// </summary>
    public static class UiPreferences
    {
        private const string UiPrefKeyPath = @"HKEY_CURRENT_USER\Software\AkariTool";

        public static bool ReadUiPref(string name)
        {
            try { return Registry.GetValue(UiPrefKeyPath, name, null) is int i && i != 0; }
            catch { return false; }
        }

        public static void WriteUiPref(string name, bool value)
        {
            try { Registry.SetValue(UiPrefKeyPath, name, value ? 1 : 0, RegistryValueKind.DWord); }
            catch { /* best-effort — a lost UI preference must never break the tab */ }
        }

        /// <summary>The net8 collapse-preference value name for a section title.</summary>
        public static string SectionCollapseKey(string title) =>
            "SectionCollapsed_" + title.Replace(" ", "");
    }
}
