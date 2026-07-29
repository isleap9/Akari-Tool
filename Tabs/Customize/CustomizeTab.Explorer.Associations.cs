using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── EXPLORER ▸ FILE ASSOCIATIONS ──
        private void BuildExplorerAssociations(StackPanel panel)
        {
            // ── File Associations ─────────────────────────────────────────────
            var assocSection = TweakHelpers.BuildSection(panel, "File Associations");

            var assocToggles = new TweakDefinition[]
            {
                new()
                {
                    Id          = "customize-explorer-enable-photo-viewer",
                    Name        = "Enable Windows Photo Viewer",
                    Description = "Restores the legacy Windows Photo Viewer as an available handler and default for common image formats",
                    Group       = "File Associations",
                    // Not modelled by Winhance. Photo Viewer is not a handler out of the box → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadPhotoViewerEnabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                     const string progId = "PhotoViewer.FileAssoc.Tiff";
                     string[] exts = { ".bmp", ".cr2", ".dib", ".gif", ".ico", ".jfif", ".jpe",
                                       ".jpeg", ".jpg", ".jxr", ".png", ".tif", ".tiff", ".wdp" };
                     foreach (var ext in exts)
                     {
                         if (enable)
                         {
                             using var cls = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Classes\{ext}");
                             cls?.SetValue("", progId, RegistryValueKind.String);
                             using var owp = Registry.CurrentUser.CreateSubKey(
                                 $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\OpenWithProgids");
                             owp?.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                         }
                         else
                         {
                             using var cls = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Classes\{ext}", writable: true);
                             if (cls?.GetValue("") as string == progId)
                                 cls.DeleteValue("", throwOnMissingValue: false);
                             using var owp = Registry.CurrentUser.OpenSubKey(
                                 $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\OpenWithProgids", writable: true);
                             owp?.DeleteValue(progId, throwOnMissingValue: false);
                         }
                     }
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-legacy-notepad",
                    Name        = "Use Legacy Notepad for Text Files",
                    Description = "Makes classic Notepad the .txt handler and disables the Store Notepad redirect. Requires the Notepad (Legacy) capability (installable from the Software tab) and admin rights",
                    Group       = "File Associations",
                    // Not modelled by Winhance. Windows ships redirecting to Store Notepad → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadLegacyNotepadEnabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                     try
                     {
                         // Make classic notepad.exe visible in Open With again
                         using (var app = Registry.LocalMachine.OpenSubKey(
                             @"SOFTWARE\Classes\Applications\notepad.exe", writable: true))
                         {
                             if (enable) app?.DeleteValue("NoOpenWith", throwOnMissingValue: false);
                             else        app?.SetValue("NoOpenWith", "", RegistryValueKind.String);
                         }
                         // UseFilter=0 disables the IFEO redirect to Store Notepad
                         using (var ifeo = Registry.LocalMachine.CreateSubKey(
                             @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe"))
                         {
                             ifeo?.SetValue("UseFilter", enable ? 0 : 1, RegistryValueKind.DWord);
                         }
                     }
                     catch { /* HKLM writes need elevation */ }
                     if (enable)
                     {
                         // Per-user App Paths override also redirects to Store Notepad
                         Registry.CurrentUser.OpenSubKey(
                             @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", writable: true)
                             ?.DeleteSubKey("notepad.exe", throwOnMissingSubKey: false);
                     }
                    }),
                },
            };

            foreach (var def in assocToggles)
                TweakHelpers.AddTweakRow(assocSection, def);
        }
    }
}
