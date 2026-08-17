using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Desktop.Icons.cs.
    // Sections "Desktop Icons" (6 rows via a generator loop, Id = tuple id) and
    // "Shortcuts" (2 literal rows). The EnsureBlankIcon helper moved along unchanged.
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] DesktopIcons(Action<string> Log)
        {
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

            var result = new List<TweakDefinition>();

            foreach (var (id, title, desc, guid, defaultState) in desktopIcons)
            {
                var capturedGuid  = guid;
                var capturedTitle = title;
                result.Add(new TweakDefinition
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
                        Log($"[DESKTOP] {capturedTitle} {(enable ? "shown" : "hidden")}.");
                    },
                });
            }

            return result.ToArray();
        }

        public static TweakDefinition[] DesktopShortcuts(Action<string> Log) => new[]
        {
            new TweakDefinition
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
                    Log($"[DESKTOP] Shortcut arrow {(enable ? "removed" : "restored")}.");
                },
            },
            new TweakDefinition
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
                    Log($"[DESKTOP] '- Shortcut' suffix {(enable ? "removed" : "restored")}.");
                },
            },
        };

        /// <summary>Writes a 256x256 fully transparent .ico to %WINDIR% and returns its path.</summary>
        private static string EnsureBlankIcon()
        {
            var icoPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "blank.ico");
            if (!System.IO.File.Exists(icoPath))
            {
                const string b64 = "AAABAAEAAAAAAAEAIAC5BwAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAEAAAABAAgGAAAAXHKoZgAAB4BJREFUeNrt3eGSmzYAhVFnp+//xJlM67ZJ3Y0XkJBA0j1nJn+yDggEnzG2N98eQKxvdw8AuI8AQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDB/gRG/ewS3uwoeAAAAABJRU5ErkJggg==";
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
