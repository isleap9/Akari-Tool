using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── EXPLORER ▸ BEHAVIOR ──
        private void BuildExplorerBehavior(StackPanel panel)
        {
            // ── Behavior ─────────────────────────────────────────────────────
            var behaviorSection = TweakHelpers.BuildSection(panel, "Behavior");

            var behaviorToggles = new TweakDefinition[]
            {
                new()
                {
                    Id          = "customize-explorer-disable-folder-auto-discovery",
                    Name        = "Disable Folder Auto-Discovery",
                    Description = "Stops Explorer from auto-changing folder view templates based on content",
                    Group       = "Behavior",
                    // Not modelled by Winhance. Both values are absent out of the box → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadFolderAutoDiscoveryDisabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                        if (enable) { key?.SetValue("FolderContentsInfoTip", 0, RegistryValueKind.DWord); key?.SetValue("ClassicViewState", 1, RegistryValueKind.DWord); }
                        else        { key?.DeleteValue("FolderContentsInfoTip", throwOnMissingValue: false); key?.DeleteValue("ClassicViewState", throwOnMissingValue: false); }
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-disable-quick-access",
                    Name        = "Disable Quick Access",
                    Description = "Stops Explorer from showing recent files and frequent folders in Quick Access",
                    Group       = "Behavior",
                    // Winhance splits this into explorer-customization-show-recent-files and
                    // -show-frequent-folders; both carry Recommended=0 (hidden), Default=1 (shown).
                    // This row drives both values at once and inverts the polarity (Disable).
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadQuickAccessDisabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                        key?.SetValue("ShowRecent",   enable ? 0 : 1, RegistryValueKind.DWord);
                        key?.SetValue("ShowFrequent", enable ? 0 : 1, RegistryValueKind.DWord);
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-hide-home-delegate-folder",
                    Name        = "Hide Home from Sidebar",
                    Description = "Removes the Home entry from the Explorer navigation pane",
                    Group       = "Behavior",
                    // Not modelled by Winhance. Key ships present (Home shown) → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadHomeDelegateFolderHidden,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        const string homeGuid = "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}";
                        if (enable)
                            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders", true)
                                ?.DeleteSubKey(homeGuid, throwOnMissingSubKey: false);
                        else
                            Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{homeGuid}");
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-hide-gallery-sidebar",
                    Name        = "Hide Gallery from Sidebar",
                    Description = "Removes the Gallery entry from the Explorer navigation pane",
                    Group       = "Behavior",
                    // Not modelled by Winhance. Key ships present (Gallery shown) → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadGallerySidebarHidden,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        const string galleryGuid = "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
                        if (enable)
                            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace", true)
                                ?.DeleteSubKey(galleryGuid, throwOnMissingSubKey: false);
                        else
                            Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{galleryGuid}");
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-disable-sharing-wizard",
                    Name        = "Disable Sharing Wizard",
                    Description = "Disables the simplified sharing wizard — enables advanced sharing dialog",
                    Group       = "Behavior",
                    // Winhance explorer-customization-sharing-wizard (SharingWizardOn):
                    // Recommended=0 → wizard off; Default=key-absent → wizard on.
                    // This row inverts the polarity (Disable), so both states flip.
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadSharingWizardDisabled,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SharingWizardOn", enable ? 0 : 1)),
                },
                new()
                {
                    Id          = "customize-explorer-show-drive-letters-first",
                    Name        = "Show Drive Letters First",
                    Description = "Displays drive letters before the drive name (e.g. C: Windows instead of Windows (C:))",
                    Group       = "Behavior",
                    // Winhance models ShowDriveLettersFirst with EnabledValue=[null]/DisabledValue=[2],
                    // which does not line up with the 4/0 values this row writes → needs-confirm.
                    ReadState   = SystemStateReader.ReadDriveLettersFirst,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "ShowDriveLettersFirst", enable ? 4 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-disable-autoplay",
                    Name        = "Disable AutoPlay",
                    Description = "Stops the AutoPlay dialog when inserting USB drives, DVDs, or SD cards, and blocks autorun on all drive types. Off restores the Windows default",
                    Group       = "Behavior",
                    // Winhance explorer-autoplay carries no recommended value; its default
                    // resolves to AutoPlay enabled → this inverted row defaults OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadAutoplayDisabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        // Per-user AutoPlay suppression (Windows default: value absent)
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", enable ? 1 : 0);
                        // Machine autorun policy: 255 = block all drive types; delete restores
                        // the built-in 0x91 default. Never write 0 (allows autorun everywhere).
                        try
                        {
                            using var pol = Registry.LocalMachine.CreateSubKey(
                                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer");
                            if (enable) pol?.SetValue("NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
                            else        pol?.DeleteValue("NoDriveTypeAutoRun", throwOnMissingValue: false);
                        }
                        catch { /* HKLM policy needs elevation — HKCU part still applies */ }
                    }),
                },
            };

            foreach (var def in behaviorToggles)
                TweakHelpers.AddTweakRow(behaviorSection, def);
        }
    }
}
