using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // ── Registry state persistence (logic-only partial) ───────────────────────
    // Extracted from TweakHelpers.cs during the WinUI 3 migration so the logic
    // layer (e.g. Services/DefenderService.cs, which calls SaveState/HasState/
    // ClearState) can compile without dragging in the WPF/WinUI control-building
    // partials of TweakHelpers. Behaviour is byte-identical to the original
    // definitions in TweakHelpers.cs — same HKCU\Software\AkariTool key, same
    // semantics. When TweakHelpers.cs is migrated (Phase 1) its duplicate copies
    // of these members must be removed so the single definition lives here.
    public static partial class TweakHelpers
    {
        // Stores a flag (1) for each tweak that has been applied so toggles
        // survive app restarts.
        private static RegistryKey? _stateKey;
        private static RegistryKey StateKey => _stateKey ??=
            Registry.CurrentUser.CreateSubKey(
                @"Software\AkariTool",
                RegistryKeyPermissionCheck.ReadWriteSubTree)
            ?? throw new InvalidOperationException("Failed to open AkariTool registry key.");

        public static void SaveState(string key) => StateKey.SetValue(key, 1);
        public static void ClearState(string key) => StateKey.DeleteValue(key, throwOnMissingValue: false);
        public static bool HasState(string key) => StateKey.GetValue(key) != null;
    }
}
