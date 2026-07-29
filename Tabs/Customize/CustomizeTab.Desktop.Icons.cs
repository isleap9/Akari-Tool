using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── DESKTOP ▸ DESKTOP ICONS + SHORTCUTS ──
        private void BuildDesktopIcons(StackPanel panel)
        {
            // ── Desktop Icons ─────────────────────────────────────────────────
            var iconsSection = TweakHelpers.BuildSection(panel, "Desktop Icons");

            // DefaultState: Windows 11 ships with Recycle Bin as the only desktop icon,
            // so it defaults ON and the other four default OFF. None are Winhance
            // settings and which icons you want is taste → no RecommendedState.
            var desktopIcons = new (string Id, string Title, string Desc, string Guid, bool Default)[]
            {
                ("customize-desktop-icon-this-pc",       "Show This PC Icon",      "Shows the This PC (Computer) icon on the desktop",      "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", false),
                ("customize-desktop-icon-user-folder",   "Show User Folder Icon",  "Shows your personal user folder icon on the desktop",   "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", false),
                ("customize-desktop-icon-network",       "Show Network Icon",      "Shows the Network icon on the desktop",                 "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", false),
                ("customize-desktop-icon-recycle-bin",   "Show Recycle Bin Icon",  "Shows the Recycle Bin icon on the desktop",             "{645FF040-5081-101B-9F08-00AA002F954E}", true),
                ("customize-desktop-icon-control-panel", "Show Control Panel Icon","Shows the Control Panel icon on the desktop",           "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", false),
                // Libraries is not one of Winhance's five desktop-icon settings (it only
                // models Libraries in the nav pane), but the GUID works in the same
                // HideDesktopIcons\NewStartPanel key. Windows hides it → OFF.
                ("customize-desktop-icon-libraries",     "Show Libraries Icon",    "Shows the Libraries folder icon on the desktop",        "{031E4825-7B94-4dc3-B131-E946B44C8DD5}", false),
            };

            foreach (var (id, title, desc, guid, defaultState) in desktopIcons)
            {
                var capturedGuid  = guid;
                var capturedTitle = title;
                TweakHelpers.AddTweakRow(iconsSection, new TweakDefinition
                {
                    Id           = id,
                    Name         = title,
                    Description  = desc,
                    Group        = "Desktop Icons",
                    DefaultState = defaultState,
                    ReadState    = () => SystemStateReader.ReadDesktopIconShown(capturedGuid),
                    Apply        = enable =>
                    {
                        const string basePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
                        using var key = Registry.CurrentUser.CreateSubKey(basePath);
                        key?.SetValue(capturedGuid, enable ? 0 : 1, RegistryValueKind.DWord);
                        Service?.Log($"[DESKTOP] {capturedTitle} {(enable ? "shown" : "hidden")}.");
                    },
                });
            }

            // ── Shortcuts ─────────────────────────────────────────────────────
            var shortcutSection = TweakHelpers.BuildSection(panel, "Shortcuts");

            TweakHelpers.AddTweakRow(shortcutSection, new TweakDefinition
            {
                Id          = "customize-desktop-remove-shortcut-arrow",
                Name        = "Remove Shortcut Arrow Overlay",
                Description = "Removes the small arrow overlay from shortcut icons (rebuilds icon cache)",
                Group       = "Shortcuts",
                // Not modelled by Winhance. Windows ships the arrow overlay → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadShortcutArrowRemoved,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
                    if (enable)
                    {
                        var blankIco = EnsureBlankIcon();
                        Registry.SetValue(path, "29", blankIco, RegistryValueKind.String);
                    }
                    else
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", writable: true);
                        key?.DeleteValue("29", throwOnMissingValue: false);
                    }
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[DESKTOP] Shortcut arrow {(enable ? "removed" : "restored")}.");
                },
            });

            TweakHelpers.AddTweakRow(shortcutSection, new TweakDefinition
            {
                Id          = "customize-desktop-remove-shortcut-suffix",
                Name        = "Remove '- Shortcut' Suffix",
                Description = "Stops Windows appending '- Shortcut' to newly created shortcuts",
                Group       = "Shortcuts",
                // Not modelled by Winhance. Windows ships the suffix enabled (0x1E) → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadShortcutSuffixRemoved,
                Apply       = enable =>
                {
                    using var key = Registry.CurrentUser.CreateSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Explorer");
                    key?.SetValue("link",
                        enable ? new byte[] { 0x00, 0x00, 0x00, 0x00 }
                               : new byte[] { 0x1E, 0x00, 0x00, 0x00 },
                        RegistryValueKind.Binary);
                    Service?.Log($"[DESKTOP] '- Shortcut' suffix {(enable ? "removed" : "restored")}.");
                },
            });
        }


        /// <summary>Writes a 256x256 fully transparent .ico to %WINDIR% and returns its path.</summary>
        private static string EnsureBlankIcon()
        {
            var icoPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "blank.ico");
            if (!System.IO.File.Exists(icoPath))
            {
                const string b64 = "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";
                try
                {
                    System.IO.File.WriteAllBytes(icoPath, Convert.FromBase64String(b64));
                }
                catch
                {
                    // Non-elevated or write-protected %WINDIR% — registry value
                    // will point at a missing file, Explorer falls back to default arrow.
                }
            }
            return icoPath;
        }
    }
}
