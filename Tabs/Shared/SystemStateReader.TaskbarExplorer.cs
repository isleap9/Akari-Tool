using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // TASKBAR TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Taskbar alignment: 0 = left, 1 = center (default)</summary>
        public static bool? ReadTaskbarAlignLeft()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Search box hidden when SearchboxTaskbarMode == 0</summary>
        public static bool? ReadSearchHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Task View button hidden when ShowTaskViewButton == 0</summary>
        public static bool? ReadTaskViewHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Widgets hidden when TaskbarDa == 0</summary>
        public static bool? ReadWidgetsHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Chat/Meet Now hidden when TaskbarMn == 0</summary>
        public static bool? ReadChatHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Taskbar auto-hide when StuckRects3 settings byte is set</summary>
        public static bool? ReadTaskbarAutoHide()
        {
            try
            {
                using var key = OpenRealHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3");
                if (key?.GetValue("Settings") is byte[] data && data.Length > 8)
                    return (data[8] & 0x01) == 0x01;
                return null;
            }
            catch { return null; }
        }

        /// <summary>Seconds in clock shown when ShowSecondsInSystemClock == 1</summary>
        public static bool? ReadClockSeconds()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "ShowSecondsInSystemClock");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>All tray icons shown when EnableAutoTray == 0 (Windows default is 1 = collapse to overflow)</summary>
        public static bool? ReadAllTrayIconsShown()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "EnableAutoTray");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Secondary-display taskbars hidden when MMTaskbarEnabled == 0 (Windows default is 1 = shown)</summary>
        public static bool? ReadMultiMonitorTaskbarHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "MMTaskbarEnabled");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Combine taskbar buttons: 0=always, 1=when full, 2=never</summary>
        public static int? ReadCombineTaskbarButtons()
        {
            return ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarGlomLevel");
        }

        /// <summary>Verbose status messages enabled when verbosestatus == 1</summary>
        public static bool? ReadVerboseStatus()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "verbosestatus");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>News and Interests hidden when ShellFeedsTaskbarViewMode == 2</summary>
        public static bool? ReadNewsHidden()
        {
            var val = ReadDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds",
                "EnableFeeds");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Notification badges hidden when TaskbarBadges == 0</summary>
        public static bool? ReadTaskbarBadgesHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarBadges");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Taskbar thumbnails disabled when ExtendedUIHoverTime is very high</summary>
        public static bool? ReadTaskbarThumbnailsDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ExtendedUIHoverTime");
            return val.HasValue ? val >= 30000 : null;
        }

        /// <summary>Snap assist disabled when SnapAssist == 0</summary>
        public static bool? ReadSnapAssistDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SnapAssist");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Aero Peek disabled when DisablePreviewDesktop == 1</summary>
        public static bool? ReadAeroPeekDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisablePreviewDesktop");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Copilot hidden when ShowCopilotButton == 0</summary>
        public static bool? ReadCopilotHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Copilot Companion hidden when TaskbarCompanion == 0</summary>
        public static bool? ReadCopilotCompanionHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarCompanion");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Copilot PWA pin hidden when CopilotPWAPin == 0</summary>
        public static bool? ReadCopilotPwaPinHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "CopilotPWAPin");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Recall pin hidden when RecallPin == 0</summary>
        public static bool? ReadRecallPinHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "RecallPin");
            return val.HasValue ? val == 0 : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // EXPLORER TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>File extensions shown when HideFileExt == 0</summary>
        public static bool? ReadShowFileExtensions()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Hidden files shown when Hidden == 1</summary>
        public static bool? ReadShowHiddenFiles()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Protected OS files shown when ShowSuperHidden == 1</summary>
        public static bool? ReadShowSuperHidden()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSuperHidden");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Full path in title bar when FullPath == 1</summary>
        public static bool? ReadFullPathTitleBar()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", "FullPath");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Full path in address bar when FullPathAddress == 1</summary>
        public static bool? ReadFullPathAddressBar()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", "FullPathAddress");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Compact view enabled when UseCompactMode == 1</summary>
        public static bool? ReadCompactView()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "UseCompactMode");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Opens to This PC when LaunchTo == 1</summary>
        public static bool? ReadLaunchToThisPC()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>End Task on taskbar enabled when TaskbarEndTask == 1</summary>
        public static bool? ReadEndTask()
        {
            var val = ReadHkcuDword(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                "TaskbarEndTask");
            return val.HasValue ? val == 1 : null;
        }

        /// <summary>Quick Access disabled when ShowRecent == 0</summary>
        public static bool? ReadQuickAccessDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Sharing wizard disabled when SharingWizardOn == 0</summary>
        public static bool? ReadSharingWizardDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SharingWizardOn");
            return val.HasValue ? val == 0 : null;
        }

        /// <summary>Drive letters shown first when ShowDriveLettersFirst == 4</summary>
        /// <summary>.lnk extension shown when HKCR\lnkfile\NeverShowExt is absent</summary>
        public static bool? ReadLnkExtensionShown()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\lnkfile");
                if (key == null) return null;
                return key.GetValue("NeverShowExt") == null;
            }
            catch { return null; }
        }

        /// <summary>AutoPlay suppressed when DisableAutoplay == 1</summary>
        public static bool? ReadAutoplayDisabled()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay");
            return val.HasValue ? val == 1 : false; // value absent = Windows default = AutoPlay on
        }

        /// <summary>Photo Viewer enabled when HKCU .jpg class default == PhotoViewer ProgID</summary>
        public static bool? ReadPhotoViewerEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\.jpg");
                return key?.GetValue("") as string == "PhotoViewer.FileAssoc.Tiff";
            }
            catch { return null; }
        }

        /// <summary>Legacy Notepad active when IFEO notepad.exe UseFilter == 0</summary>
        public static bool? ReadLegacyNotepadEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe");
                if (key?.GetValue("UseFilter") is int v) return v == 0;
                return false; // value absent = Store redirect active (Windows default)
            }
            catch { return null; }
        }

        public static bool? ReadDriveLettersFirst()
        {
            var val = ReadHkcuDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "ShowDriveLettersFirst");
            return val.HasValue ? val == 4 : null;
        }

        /// <summary>Folder discovery disabled when FolderType == NotSpecified</summary>
        public static bool? ReadFolderDiscoveryDisabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell");
                return key?.GetValue("FolderType") is string val && val == "NotSpecified" ? true : false;
            }
            catch { return null; }
        }

        /// <summary>
        /// Per-user folder auto-discovery suppression — distinct from the
        /// FolderType=NotSpecified policy read by ReadFolderDiscoveryDisabled().
        /// Disabled when FolderContentsInfoTip == 0 and ClassicViewState == 1.
        /// </summary>
        public static bool? ReadFolderAutoDiscoveryDisabled()
        {
            const string advanced = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            var infoTip = ReadHkcuDword(advanced, "FolderContentsInfoTip");
            var classic = ReadHkcuDword(advanced, "ClassicViewState");
            if (infoTip == null && classic == null) return false; // both absent = Windows default
            return infoTip == 0 && classic == 1;
        }

        /// <summary>Home hidden from the nav pane when its DelegateFolders key is absent</summary>
        public static bool? ReadHomeDelegateFolderHidden()
        {
            try
            {
                var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}");
                return key == null; // key present = Windows default = Home shown
            }
            catch { return null; }
        }

        /// <summary>Gallery hidden from the nav pane when its Desktop NameSpace key is absent</summary>
        public static bool? ReadGallerySidebarHidden()
        {
            try
            {
                var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}");
                return key == null; // key present = Windows default = Gallery shown
            }
            catch { return null; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // THIS PC FOLDERS
        // ═════════════════════════════════════════════════════════════════════

        private const string MyComputerNameSpace =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace";

        /// <summary>This PC folder visible when HiddenByDefault == 0 or absent</summary>
        public static bool? ReadThisPcFolderVisible(string guid)
        {
            var val = ReadDword(RegistryHive.LocalMachine, $@"{MyComputerNameSpace}\{guid}", "HiddenByDefault");
            return val.HasValue ? val == 0 : true; // value absent = Windows default = visible
        }

        /// <summary>3D Objects shown when its MyComputer NameSpace key exists</summary>
        public static bool? ReadThreeDObjectsShown()
        {
            try
            {
                var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(
                    $@"{MyComputerNameSpace}\{{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}}");
                return key != null;
            }
            catch { return null; }
        }

        /// <summary>Duplicate removable drives hidden when the DelegateFolders key is absent</summary>
        public static bool? ReadDuplicateRemovableDrivesHidden()
        {
            try
            {
                var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}");
                return key == null; // key present = Windows default = duplicates shown
            }
            catch { return null; }
        }

        /// <summary>Home hidden when System.IsPinnedToNameSpaceTree == 0 on Home CLSID</summary>
        public static bool? ReadHomeHidden()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}");
                if (key?.GetValue("System.IsPinnedToNameSpaceTree") is int val)
                    return val == 0;
                return null;
            }
            catch { return null; }
        }

    }
}
