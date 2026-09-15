# CONTROL PANEL granular tweaks -- auto-generated from Fr33thy registryoptimize/defaults.
# Pure PowerShell, no JSON. Edit by hand freely; each entry is a hashtable:
#   Id         unique key (also the registry temp filename)
#   Title      text shown on the row
#   Tab        Debloat | Appearance | Tweaks | System   (which tab the row appears in)
#   Section    card heading within that tab
#   Revertible $true = shows Optimize/Default buttons; $false = single Apply (one-way)
#   Optimize   .reg body applied by Optimize/Apply
#   Default    .reg body applied by Default (empty when not revertible)
# Add your own by copying an entry, changing the fields, and recompiling.

$sync.CpTweaks = @(
    @{
        Id = 'disable_narrator'
        Title = 'Disable Narrator'
        Tab = 'Appearance'; Section = 'Ease of Access'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam]
"DuckAudio"=dword:00000000
"WinEnterLaunchEnabled"=dword:00000000
"ScriptingEnabled"=dword:00000000
"OnlineServicesEnabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Narrator]
"NarratorCursorHighlight"=dword:00000000
"CoupleNarratorCursorKeyboard"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam]
"DuckAudio"=-
"WinEnterLaunchEnabled"=-
"ScriptingEnabled"=-
"OnlineServicesEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Narrator]
"NarratorCursorHighlight"=-
"CoupleNarratorCursorKeyboard"=-
'@
    }
    @{
        Id = 'disable_ease_of_access_settings'
        Title = 'Disable Ease Of Access Settings'
        Tab = 'Appearance'; Section = 'Ease of Access'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Ease of Access]
"selfvoice"=dword:00000000
"selfscan"=dword:00000000
[HKEY_CURRENT_USER\Control Panel\Accessibility]
"Sound on Activation"=dword:00000000
"Warning Sounds"=dword:00000000
[HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast]
"Flags"="4194"
[HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response]
"Flags"="2"
"AutoRepeatRate"="0"
"AutoRepeatDelay"="0"
[HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys]
"Flags"="130"
"MaximumSpeed"="39"
"TimeToMaximumSpeed"="3000"
[HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys]
"Flags"="2"
[HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys]
"Flags"="34"
[HKEY_CURRENT_USER\Control Panel\Accessibility\SoundSentry]
"Flags"="0"
"FSTextEffect"="0"
"TextEffect"="0"
"WindowsEffect"="0"
[HKEY_CURRENT_USER\Control Panel\Accessibility\SlateLaunch]
"ATapp"=""
"LaunchAT"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Ease of Access]
"selfvoice"=-
"selfscan"=-

[HKEY_CURRENT_USER\Control Panel\Accessibility]
"Sound on Activation"=-
"Warning Sounds"=-

[HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast]
"Flags"="126"

[HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response]
"Flags"="126"
"AutoRepeatRate"="500"
"AutoRepeatDelay"="1000"

[HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys]
"Flags"="62"
"MaximumSpeed"="80"
"TimeToMaximumSpeed"="3000"

[HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys]
"Flags"="510"

[HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys]
"Flags"="62"

[HKEY_CURRENT_USER\Control Panel\Accessibility\SoundSentry]
"Flags"="2"
"FSTextEffect"="0"
"TextEffect"="0"
"WindowsEffect"="1"

[HKEY_CURRENT_USER\Control Panel\Accessibility\SlateLaunch]
"ATapp"="narrator"
"LaunchAT"=dword:00000001
'@
    }
    @{
        Id = 'disable_notify_me_when_the_clock_changes'
        Title = 'Disable Notify Me When The Clock Changes'
        Tab = 'Appearance'; Section = 'Clock & Region'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\TimeDate]
"DstNotification"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\TimeDate]
"DstNotification"=-
'@
    }
    @{
        Id = 'open_file_explorer_to_this_pc'
        Title = 'Open File Explorer To This Pc'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"LaunchTo"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"LaunchTo"=-
'@
    }
    @{
        Id = 'hide_frequent_folders_in_quick_access'
        Title = 'Hide Frequent Folders In Quick Access'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"ShowFrequent"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"ShowFrequent"=-
'@
    }
    @{
        Id = 'show_file_name_extensions'
        Title = 'Show File Name Extensions'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"HideFileExt"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"HideFileExt"=dword:00000001
'@
    }
    @{
        Id = 'disable_search_history'
        Title = 'Disable Search History'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsDeviceSearchHistoryEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsDeviceSearchHistoryEnabled"=-
'@
    }
    @{
        Id = 'disable_show_files_from_office_com'
        Title = 'Disable Show Files From Office.Com'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"ShowCloudFilesInQuickAccess"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"ShowCloudFilesInQuickAccess"=-
'@
    }
    @{
        Id = 'disable_display_file_size_information_in_fol'
        Title = 'Disable Display File Size Information In Folder Tips'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"FolderContentsInfoTip"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"FolderContentsInfoTip"=-
'@
    }
    @{
        Id = 'enable_display_full_path_in_the_title_bar'
        Title = 'Enable Display Full Path In The Title Bar'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState]
"FullPath"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState]
"FullPath"=dword:00000000
'@
    }
    @{
        Id = 'disable_show_pop_up_description_for_folder_a'
        Title = 'Disable Show Pop-Up Description For Folder And Desktop Items'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowInfoTip"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowInfoTip"=dword:00000001
'@
    }
    @{
        Id = 'disable_show_preview_handlers_in_preview_pan'
        Title = 'Disable Show Preview Handlers In Preview Pane'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowPreviewHandlers"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowPreviewHandlers"=-
'@
    }
    @{
        Id = 'disable_show_status_bar'
        Title = 'Disable Show Status Bar'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowStatusBar"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowStatusBar"=dword:00000001
'@
    }
    @{
        Id = 'disable_show_sync_provider_notifications'
        Title = 'Disable Show Sync Provider Notifications'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowSyncProviderNotifications"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowSyncProviderNotifications"=-
'@
    }
    @{
        Id = 'disable_use_sharing_wizard'
        Title = 'Disable Use Sharing Wizard'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"SharingWizardOn"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"SharingWizardOn"=-
'@
    }
    @{
        Id = 'disable_show_network'
        Title = 'Disable Show Network'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Classes\CLSID\{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}]
"System.IsPinnedToNameSpaceTree"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Classes\CLSID\{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}]
"System.IsPinnedToNameSpaceTree"=-
'@
    }
    @{
        Id = 'disable_lock'
        Title = 'Disable Lock'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings]
"ShowLockOption"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings]
"ShowLockOption"=-
'@
    }
    @{
        Id = 'disable_sleep'
        Title = 'Disable Sleep'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings]
"ShowSleepOption"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings]
"ShowSleepOption"=-
'@
    }
    @{
        Id = 'sound_communications_do_nothing'
        Title = 'Sound Communications Do Nothing'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio]
"UserDuckingPreference"=dword:00000003
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio]
"UserDuckingPreference"=-
'@
    }
    @{
        Id = 'disable_startup_sound'
        Title = 'Disable Startup Sound'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation]
"DisableStartupSound"=dword:00000001
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides]
"UserSetting_DisableStartupSound"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation]
"DisableStartupSound"=dword:00000000

[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides]
"UserSetting_DisableStartupSound"=dword:00000000
'@
    }
    @{
        Id = 'sound_scheme_none'
        Title = 'Sound Scheme None'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\AppEvents\Schemes]
@=".None"
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\.Default\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\CriticalBatteryAlarm\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceFail\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\FaxBeep\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\LowBatteryAlarm\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\MailBeep\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\MessageNudge\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Default\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.IM\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Mail\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Proximity\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Reminder\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.SMS\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\ProximityConnection\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemAsterisk\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemExclamation\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemHand\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemNotification\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\WindowsUAC\.Current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\DisNumbersSound\.current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubOffSound\.current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubOnSound\.current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubSleepSound\.current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\MisrecoSound\.current]
@=""
[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\PanelSound\.current]
@=""
'@
        Default = @'
[HKEY_CURRENT_USER\AppEvents\Schemes]
@=".Default"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\.Default\.Current]
@="C:\\Windows\\media\\Windows Background.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\CriticalBatteryAlarm\.Current]
@="C:\\Windows\\media\\Windows Foreground.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current]
@="C:\\Windows\\media\\Windows Hardware Insert.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current]
@="C:\\Windows\\media\\Windows Hardware Remove.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceFail\.Current]
@="C:\\Windows\\media\\Windows Hardware Fail.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\FaxBeep\.Current]
@="C:\\Windows\\media\\Windows Notify Email.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\LowBatteryAlarm\.Current]
@="C:\\Windows\\media\\Windows Background.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\MailBeep\.Current]
@="C:\\Windows\\media\\Windows Notify Email.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\MessageNudge\.Current]
@="C:\\Windows\\media\\Windows Message Nudge.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Default\.Current]
@="C:\\Windows\\media\\Windows Notify System Generic.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.IM\.Current]
@="C:\\Windows\\media\\Windows Notify Messaging.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Mail\.Current]
@="C:\\Windows\\media\\Windows Notify Email.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Proximity\.Current]
@="C:\\Windows\\media\\Windows Proximity Notification.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.Reminder\.Current]
@="C:\\Windows\\media\\Windows Notify Calendar.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\Notification.SMS\.Current]
@="C:\\Windows\\media\\Windows Notify Messaging.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\ProximityConnection\.Current]
@="C:\\Windows\\media\\Windows Proximity Connection.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemAsterisk\.Current]
@="C:\\Windows\\media\\Windows Background.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemExclamation\.Current]
@="C:\\Windows\\media\\Windows Background.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemHand\.Current]
@="C:\\Windows\\media\\Windows Foreground.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\SystemNotification\.Current]
@="C:\\Windows\\media\\Windows Background.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\WindowsUAC\.Current]
@="C:\\Windows\\media\\Windows User Account Control.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\DisNumbersSound\.current]
@="C:\\Windows\\media\\Speech Disambiguation.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubOffSound\.current]
@="C:\\Windows\\media\\Speech Off.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubOnSound\.current]
@="C:\\Windows\\media\\Speech On.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\HubSleepSound\.current]
@="C:\\Windows\\media\\Speech Sleep.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\MisrecoSound\.current]
@="C:\\Windows\\media\\Speech Misrecognition.wav"

[HKEY_CURRENT_USER\AppEvents\Schemes\Apps\sapisvr\PanelSound\.current]
@="C:\\Windows\\media\\Speech Disambiguation.wav"
'@
    }
    @{
        Id = 'disable_autoplay'
        Title = 'Disable Autoplay'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers]
"DisableAutoplay"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers]
"DisableAutoplay"=dword:00000000
'@
    }
    @{
        Id = 'mouse_pointers_scheme_none'
        Title = 'Mouse Pointers Scheme None'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Cursors]
"AppStarting"=hex(2):00,00
"Arrow"=hex(2):00,00
"ContactVisualization"=dword:00000000
"Crosshair"=hex(2):00,00
"GestureVisualization"=dword:00000000
"Hand"=hex(2):00,00
"Help"=hex(2):00,00
"IBeam"=hex(2):00,00
"No"=hex(2):00,00
"NWPen"=hex(2):00,00
"Scheme Source"=dword:00000000
"SizeAll"=hex(2):00,00
"SizeNESW"=hex(2):00,00
"SizeNS"=hex(2):00,00
"SizeNWSE"=hex(2):00,00
"SizeWE"=hex(2):00,00
"UpArrow"=hex(2):00,00
"Wait"=hex(2):00,00
@=""
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Cursors]
"AppStarting"="C:\\Windows\\cursors\\aero_working.ani"
"Arrow"="C:\\Windows\\cursors\\aero_arrow.cur"
"ContactVisualization"=dword:00000001
"Crosshair"=""
"GestureVisualization"=dword:0000001f
"Hand"="C:\\Windows\\cursors\\aero_link.cur"
"Help"="C:\\Windows\\cursors\\aero_helpsel.cur"
"IBeam"=""
"No"="C:\\Windows\\cursors\\aero_unavail.cur"
"NWPen"="C:\\Windows\\cursors\\aero_pen.cur"
"Scheme Source"=dword:00000002
"SizeAll"="C:\\Windows\\cursors\\aero_move.cur"
"SizeNESW"="C:\\Windows\\cursors\\aero_nesw.cur"
"SizeNS"="C:\\Windows\\cursors\\aero_ns.cur"
"SizeNWSE"="C:\\Windows\\cursors\\aero_nwse.cur"
"SizeWE"="C:\\Windows\\cursors\\aero_ew.cur"
"UpArrow"="C:\\Windows\\cursors\\aero_up.cur"
"Wait"="C:\\Windows\\cursors\\aero_busy.ani"
@="Windows Default"
'@
    }
    @{
        Id = 'disable_device_installation_settings'
        Title = 'Disable Device Installation Settings'
        Tab = 'Tweaks'; Section = 'Hardware & Sound'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata]
"PreventDeviceMetadataFromNetwork"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata]
"PreventDeviceMetadataFromNetwork"=dword:00000000
'@
    }
    @{
        Id = 'disable_allow_other_network_users_to_control'
        Title = 'Disable Allow Other Network Users To Control Or Disable The Shared Internet Connection'
        Tab = 'Tweaks'; Section = 'Network'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Network\SharedAccessConnection]
"EnableControl"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Network\SharedAccessConnection]
"EnableControl"=dword:00000001
'@
    }
    @{
        Id = 'disable_defragment_and_optimize_your_drives'
        Title = 'Disable Defragment And Optimize Your Drives'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Dfrg\TaskSettings]
"fAllVolumes"=dword:00000001
"fDeadlineEnabled"=dword:00000000
"fExclude"=dword:00000000
"fTaskEnabled"=dword:00000000
"fUpgradeRestored"=dword:00000001
"TaskFrequency"=dword:00000004
"Volumes"=" "
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Dfrg\TaskSettings]
"fAllVolumes"=-
"fDeadlineEnabled"=-
"fExclude"=-
"fTaskEnabled"=-
"fUpgradeRestored"=-
"TaskFrequency"=-
"Volumes"=-
'@
    }
    @{
        Id = 'set_appearance_options_to_custom'
        Title = 'Set Appearance Options To Custom'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects]
"VisualFXSetting"=dword:3
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects]
"VisualFXSetting"=-
'@
    }
    @{
        Id = 'enable_animate_controls_and_elements_inside_'
        Title = 'Enable Animate Controls And Elements Inside Windows (Disabled Breaks Instagram Scrolling) (+7 more)'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"UserPreferencesMask"=hex(2):90,12,03,80,12,00,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"UserPreferencesMask"=hex(2):9e,1e,07,80,12,00,00,00
'@
    }
    @{
        Id = 'disable_animate_windows_when_minimizing_and_'
        Title = 'Disable Animate Windows When Minimizing And Maximizing'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics]
"MinAnimate"="0"
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics]
"MinAnimate"="1"
'@
    }
    @{
        Id = 'disable_animations_in_the_taskbar'
        Title = 'Disable Animations In The Taskbar'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAnimations"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAnimations"=dword:1
'@
    }
    @{
        Id = 'disable_enable_peek'
        Title = 'Disable Enable Peek'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"EnableAeroPeek"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"EnableAeroPeek"=dword:1
'@
    }
    @{
        Id = 'disable_save_taskbar_thumbnail_previews'
        Title = 'Disable Save Taskbar Thumbnail Previews'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"AlwaysHibernateThumbnails"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"AlwaysHibernateThumbnails"=dword:0
'@
    }
    @{
        Id = 'enable_show_thumbnails_instead_of_icons'
        Title = 'Enable Show Thumbnails Instead Of Icons'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IconsOnly"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IconsOnly"=dword:0
'@
    }
    @{
        Id = 'disable_show_translucent_selection_rectangle'
        Title = 'Disable Show Translucent Selection Rectangle'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ListviewAlphaSelect"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ListviewAlphaSelect"=dword:1
'@
    }
    @{
        Id = 'disable_show_window_contents_while_dragging'
        Title = 'Disable Show Window Contents While Dragging'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"DragFullWindows"="0"
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"DragFullWindows"="1"
'@
    }
    @{
        Id = 'enable_smooth_edges_of_screen_fonts'
        Title = 'Enable Smooth Edges Of Screen Fonts'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"FontSmoothing"="2"
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"FontSmoothing"="2"
'@
    }
    @{
        Id = 'disable_use_drop_shadows_for_icon_labels_on_'
        Title = 'Disable Use Drop Shadows For Icon Labels On The Desktop'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ListviewShadow"=dword:0
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ListviewShadow"=dword:1
'@
    }
    @{
        Id = 'adjust_for_best_performance_of_programs'
        Title = 'Adjust For Best Performance Of Programs'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl]
"Win32PrioritySeparation"=dword:00000026
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl]
"Win32PrioritySeparation"=dword:00000002
'@
    }
    @{
        Id = 'disable_remote_assistance'
        Title = 'Disable Remote Assistance'
        Tab = 'Tweaks'; Section = 'Visual Effects'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance]
"fAllowToGetHelp"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance]
"fAllowToGetHelp"=dword:00000001
'@
    }
    @{
        Id = 'disable_automatic_maintenance'
        Title = 'Disable Automatic Maintenance'
        Tab = 'Tweaks'; Section = 'Maintenance'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance]
"MaintenanceDisabled"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance]
"MaintenanceDisabled"=-
'@
    }
    @{
        Id = 'disable_report_problems'
        Title = 'Disable Report Problems'
        Tab = 'Tweaks'; Section = 'Maintenance'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting]
"Disabled"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting]
"Disabled"=-
'@
    }
    @{
        Id = 'disable_delivery_optimization'
        Title = 'Disable Delivery Optimization'
        Tab = 'Debloat'; Section = 'Windows Update'; Revertible = $true
        Optimize = @'
[HKEY_USERS\S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings]
"DownloadMode"=dword:00000000
'@
        Default = @'
[HKEY_USERS\S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings]
"DownloadMode"=-
'@
    }
    @{
        Id = 'disable_find_my_device'
        Title = 'Disable Find My Device'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\MdmCommon\SettingValues]
"LocationSyncEnabled"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\MdmCommon\SettingValues]
"LocationSyncEnabled"=dword:00000001
'@
    }
    @{
        Id = 'disable_show_me_notification_in_the_settings'
        Title = 'Disable Show Me Notification In The Settings App'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications]
"EnableAccountNotifications"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications]
"EnableAccountNotifications"=-
'@
    }
    @{
        Id = 'disable_tailored_experiences'
        Title = 'Disable Tailored Experiences'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\TailoredExperiencesWithDiagnosticDataEnabled]
"Value"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy]
"TailoredExperiencesWithDiagnosticDataEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\TailoredExperiencesWithDiagnosticDataEnabled]
"Value"=dword:00000001

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy]
"TailoredExperiencesWithDiagnosticDataEnabled"=dword:00000001
'@
    }
    @{
        Id = 'disable_location'
        Title = 'Disable Location'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_allow_location_override'
        Title = 'Disable Allow Location Override'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\UserLocationOverridePrivacySetting]
"Value"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\UserLocationOverridePrivacySetting]
"Value"=dword:00000001
'@
    }
    @{
        Id = 'disable_notify_when_apps_request_location'
        Title = 'Disable Notify When Apps Request Location'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location]
"ShowGlobalPrompts"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location]
"ShowGlobalPrompts"=-
'@
    }
    @{
        Id = 'enable_camera'
        Title = 'Enable Camera'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam]
"Value"="Allow"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam]
"Value"="Allow"
'@
    }
    @{
        Id = 'enable_microphone'
        Title = 'Enable Microphone'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone]
"Value"="Allow"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_voice_activation'
        Title = 'Disable Voice Activation'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps]
"AgentActivationEnabled"=dword:00000000
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps]
"AgentActivationLastUsed"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps]
"AgentActivationEnabled"=-
"AgentActivationLastUsed"=-
'@
    }
    @{
        Id = 'disable_notifications'
        Title = 'Disable Notifications'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings]
"NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"=dword:00000000
"NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"=dword:00000000
"NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Microsoft.SkyDrive.Desktop]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.AutoPlay]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp]
"Enabled"=dword:00000000
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement]
"ScoobeSystemSettingEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings]
"NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"=-
"NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"=-
"NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Microsoft.SkyDrive.Desktop]
"Enabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.AutoPlay]
"Enabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance]
"Enabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel]
"Enabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess]
"Enabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp]
"Enabled"=dword:00000000

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement]
"ScoobeSystemSettingEnabled"=-
'@
    }
    @{
        Id = 'disable_account_info'
        Title = 'Disable Account Info'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_contacts'
        Title = 'Disable Contacts'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\contacts]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\contacts]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_calendar'
        Title = 'Disable Calendar'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appointments]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appointments]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_phone_calls'
        Title = 'Disable Phone Calls'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\phoneCall]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\phoneCall]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_call_history'
        Title = 'Disable Call History'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\phoneCallHistory]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\phoneCallHistory]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_email'
        Title = 'Disable Email'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\email]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\email]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_tasks'
        Title = 'Disable Tasks'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userDataTasks]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userDataTasks]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_messaging'
        Title = 'Disable Messaging'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\chat]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\chat]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_radios'
        Title = 'Disable Radios'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\radios]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\radios]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_other_devices'
        Title = 'Disable Other Devices'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\bluetoothSync]
"Value"="Deny"
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\bluetoothSync]
"Value"=-
'@
    }
    @{
        Id = 'disable_app_diagnostics'
        Title = 'Disable App Diagnostics'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_documents'
        Title = 'Disable Documents'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\documentsLibrary]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\documentsLibrary]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_downloads_folder'
        Title = 'Disable Downloads Folder'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\downloadsFolder]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\downloadsFolder]
"Value"=-
'@
    }
    @{
        Id = 'disable_music_library'
        Title = 'Disable Music Library'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\musicLibrary]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\musicLibrary]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_pictures'
        Title = 'Disable Pictures'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\picturesLibrary]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\picturesLibrary]
"Value"="Deny"
'@
    }
    @{
        Id = 'disable_videos'
        Title = 'Disable Videos'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\videosLibrary]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\videosLibrary]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_file_system'
        Title = 'Disable File System'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\broadFileSystemAccess]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\broadFileSystemAccess]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_text_and_image_generation'
        Title = 'Disable Text And Image Generation'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_passkey_access'
        Title = 'Disable Passkey Access'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\passkeys]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\passkeys]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_passkey_autofill_access'
        Title = 'Disable Passkey Autofill Access'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\passkeysEnumeration]
"Value"="Deny"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\passkeysEnumeration]
"Value"="Allow"
'@
    }
    @{
        Id = 'disable_let_websites_show_me_locally_relevan'
        Title = 'Disable Let Websites Show Me Locally Relevant Content By Accessing My Language List'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\International\User Profile]
"HttpAcceptLanguageOptOut"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\International\User Profile]
"HttpAcceptLanguageOptOut"=-
'@
    }
    @{
        Id = 'disable_let_windows_improve_start_and_search'
        Title = 'Disable Let Windows Improve Start And Search Results By Tracking App Launches'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\EdgeUI]
"DisableMFUTracking"=dword:00000001
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\EdgeUI]
"DisableMFUTracking"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\EdgeUI]
"DisableMFUTracking"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\EdgeUI]
"DisableMFUTracking"=-
'@
    }
    @{
        Id = 'disable_personal_inking_and_typing_dictionar'
        Title = 'Disable Personal Inking And Typing Dictionary'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization]
"RestrictImplicitInkCollection"=dword:00000001
"RestrictImplicitTextCollection"=dword:00000001
[HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore]
"HarvestContacts"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings]
"AcceptedPrivacyPolicy"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization]
"RestrictImplicitInkCollection"=dword:00000000
"RestrictImplicitTextCollection"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore]
"HarvestContacts"=dword:00000001

[HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings]
"AcceptedPrivacyPolicy"=dword:00000001
'@
    }
    @{
        Id = 'disable_sending_required_data'
        Title = 'Disable Sending Required Data'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\DataCollection]
"AllowTelemetry"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\DataCollection]
"AllowTelemetry"=-
'@
    }
    @{
        Id = 'feedback_frequency_never'
        Title = 'Feedback Frequency Never'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules]
"NumberOfSIUFInPeriod"=dword:00000000
"PeriodInNanoSeconds"=-
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules]
"NumberOfSIUFInPeriod"=-
"PeriodInNanoSeconds"=-
'@
    }
    @{
        Id = 'disable_store_my_activity_history_on_this_de'
        Title = 'Disable Store My Activity History On This Device'
        Tab = 'Debloat'; Section = 'Privacy'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
"PublishUserActivities"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
"PublishUserActivities"=-
'@
    }
    @{
        Id = 'disable_search_highlights'
        Title = 'Disable Search Highlights'
        Tab = 'Debloat'; Section = 'Search'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsDynamicSearchBoxEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsDynamicSearchBoxEnabled"=-
'@
    }
    @{
        Id = 'disable_safe_search'
        Title = 'Disable Safe Search'
        Tab = 'Debloat'; Section = 'Search'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings]
"SafeSearchMode"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings]
"SafeSearchMode"=-
'@
    }
    @{
        Id = 'disable_cloud_content_search_for_work_or_sch'
        Title = 'Disable Cloud Content Search For Work Or School Account'
        Tab = 'Debloat'; Section = 'Search'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsAADCloudSearchEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsAADCloudSearchEnabled"=-
'@
    }
    @{
        Id = 'disable_cloud_content_search_for_microsoft_a'
        Title = 'Disable Cloud Content Search For Microsoft Account'
        Tab = 'Debloat'; Section = 'Search'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsMSACloudSearchEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings]
"IsMSACloudSearchEnabled"=-
'@
    }
    @{
        Id = 'disable_magnifier_settings'
        Title = 'Disable Magnifier Settings'
        Tab = 'Appearance'; Section = 'Ease of Access'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\ScreenMagnifier]
"FollowCaret"=dword:00000000
"FollowNarrator"=dword:00000000
"FollowMouse"=dword:00000000
"FollowFocus"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\ScreenMagnifier]
"FollowCaret"=-
"FollowNarrator"=-
"FollowMouse"=-
"FollowFocus"=-
'@
    }
    @{
        Id = 'disable_narrator_settings'
        Title = 'Disable Narrator Settings'
        Tab = 'Appearance'; Section = 'Ease of Access'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator]
"IntonationPause"=dword:00000000
"ReadHints"=dword:00000000
"ErrorNotificationType"=dword:00000000
"EchoChars"=dword:00000000
"EchoWords"=dword:00000000
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator\NarratorHome]
"MinimizeType"=dword:00000000
"AutoStart"=dword:00000000
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator\NoRoam]
"EchoToggleKeys"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator]
"IntonationPause"=-
"ReadHints"=-
"ErrorNotificationType"=-
"EchoChars"=-
"EchoWords"=-

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator\NarratorHome]
"MinimizeType"=-
"AutoStart"=-

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator\NoRoam]
"EchoToggleKeys"=-
'@
    }
    @{
        Id = 'disable_use_the_print_screen_key_to_open_scr'
        Title = 'Disable Use The Print Screen Key To Open Screen Capture'
        Tab = 'Appearance'; Section = 'Ease of Access'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Keyboard]
"PrintScreenKeyForSnippingEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Keyboard]
"PrintScreenKeyForSnippingEnabled"=-
'@
    }
    @{
        Id = 'disable_game_bar'
        Title = 'Disable Game Bar'
        Tab = 'Tweaks'; Section = 'Gaming'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=-
'@
    }
    @{
        Id = 'disable_enable_open_xbox_game_bar_using_game'
        Title = 'Disable Enable Open Xbox Game Bar Using Game Controller'
        Tab = 'Tweaks'; Section = 'Gaming'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=-
'@
    }
    @{
        Id = 'disable_use_view_menu_as_guide_button_in_app'
        Title = 'Disable Use View + Menu As Guide Button In Apps'
        Tab = 'Tweaks'; Section = 'Gaming'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=-
'@
    }
    @{
        Id = 'other_settings'
        Title = 'Other Settings'
        Tab = 'Tweaks'; Section = 'Gaming'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AudioEncodingBitrate"=dword:0001f400
"AudioCaptureEnabled"=dword:00000000
"CustomVideoEncodingBitrate"=dword:003d0900
"CustomVideoEncodingHeight"=dword:000002d0
"CustomVideoEncodingWidth"=dword:00000500
"HistoricalBufferLength"=dword:0000001e
"HistoricalBufferLengthUnit"=dword:00000001
"HistoricalCaptureEnabled"=dword:00000000
"HistoricalCaptureOnBatteryAllowed"=dword:00000001
"HistoricalCaptureOnWirelessDisplayAllowed"=dword:00000001
"MaximumRecordLength"=hex(b):00,D0,88,C3,10,00,00,00
"VideoEncodingBitrateMode"=dword:00000002
"VideoEncodingResolutionMode"=dword:00000002
"VideoEncodingFrameRateMode"=dword:00000000
"EchoCancellationEnabled"=dword:00000001
"CursorCaptureEnabled"=dword:00000000
"VKToggleGameBar"=dword:00000000
"VKMToggleGameBar"=dword:00000000
"VKSaveHistoricalVideo"=dword:00000000
"VKMSaveHistoricalVideo"=dword:00000000
"VKToggleRecording"=dword:00000000
"VKMToggleRecording"=dword:00000000
"VKTakeScreenshot"=dword:00000000
"VKMTakeScreenshot"=dword:00000000
"VKToggleRecordingIndicator"=dword:00000000
"VKMToggleRecordingIndicator"=dword:00000000
"VKToggleMicrophoneCapture"=dword:00000000
"VKMToggleMicrophoneCapture"=dword:00000000
"VKToggleCameraCapture"=dword:00000000
"VKMToggleCameraCapture"=dword:00000000
"VKToggleBroadcast"=dword:00000000
"VKMToggleBroadcast"=dword:00000000
"MicrophoneCaptureEnabled"=dword:00000000
"SystemAudioGain"=hex(b):10,27,00,00,00,00,00,00
"MicrophoneGain"=hex(b):10,27,00,00,00,00,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AudioEncodingBitrate"=-
"AudioCaptureEnabled"=-
"CustomVideoEncodingBitrate"=-
"CustomVideoEncodingHeight"=-
"CustomVideoEncodingWidth"=-
"HistoricalBufferLength"=-
"HistoricalBufferLengthUnit"=-
"HistoricalCaptureEnabled"=-
"HistoricalCaptureOnBatteryAllowed"=-
"HistoricalCaptureOnWirelessDisplayAllowed"=-
"MaximumRecordLength"=-
"VideoEncodingBitrateMode"=-
"VideoEncodingResolutionMode"=-
"VideoEncodingFrameRateMode"=-
"EchoCancellationEnabled"=-
"CursorCaptureEnabled"=-
"VKToggleGameBar"=-
"VKMToggleGameBar"=-
"VKSaveHistoricalVideo"=-
"VKMSaveHistoricalVideo"=-
"VKToggleRecording"=-
"VKMToggleRecording"=-
"VKTakeScreenshot"=-
"VKMTakeScreenshot"=-
"VKToggleRecordingIndicator"=-
"VKMToggleRecordingIndicator"=-
"VKToggleMicrophoneCapture"=-
"VKMToggleMicrophoneCapture"=-
"VKToggleCameraCapture"=-
"VKMToggleCameraCapture"=-
"VKToggleBroadcast"=-
"VKMToggleBroadcast"=-
"MicrophoneCaptureEnabled"=-
"SystemAudioGain"=-
"MicrophoneGain"=-
'@
    }
    @{
        Id = 'disable_show_the_voice_typing_mic_button'
        Title = 'Disable Show The Voice Typing Mic Button'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\input\Settings]
"IsVoiceTypingKeyEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\input\Settings]
"IsVoiceTypingKeyEnabled"=-
'@
    }
    @{
        Id = 'disable_capitalize_the_first_letter_of_each_'
        Title = 'Disable Capitalize The First Letter Of Each Sentence (+2 more)'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"EnableAutoShiftEngage"=dword:00000000
"EnableKeyAudioFeedback"=dword:00000000
"EnableDoubleTapSpace"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"EnableAutoShiftEngage"=-
"EnableKeyAudioFeedback"=-
"EnableDoubleTapSpace"=-
'@
    }
    @{
        Id = 'disable_typing_insights'
        Title = 'Disable Typing Insights'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\input\Settings]
"InsightsEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\input\Settings]
"InsightsEnabled"=-
'@
    }
    @{
        Id = 'show_the_touch_keyboard_never'
        Title = 'Show The Touch Keyboard Never'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"TouchKeyboardTapInvoke"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"TouchKeyboardTapInvoke"=-
'@
    }
    @{
        Id = 'disable_language_bar'
        Title = 'Disable Language Bar'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\CTF\LangBar]
"ExtraIconsOnMinimized"=dword:00000000
"Label"=dword:00000000
"ShowStatus"=dword:00000003
"Transparency"=dword:000000ff
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\CTF\LangBar]
"ExtraIconsOnMinimized"=-
"Label"=-
"ShowStatus"=-
"Transparency"=-
'@
    }
    @{
        Id = 'disable_language_hotkey'
        Title = 'Disable Language Hotkey'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Keyboard Layout\Toggle]
"Language Hotkey"="3"
"Hotkey"="3"
"Layout Hotkey"="3"
'@
        Default = @'
[HKEY_CURRENT_USER\Keyboard Layout\Toggle]
"Language Hotkey"=-
"Hotkey"=-
"Layout Hotkey"=-
'@
    }
    @{
        Id = 'disable_calendar_events'
        Title = 'Disable Calendar Events'
        Tab = 'Appearance'; Section = 'Typing & Input'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Search]
"GleamEnabled"=dword:00000000
"WeatherEnabled"=dword:00000000
"HolidayEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Search]
"GleamEnabled"=-
"WeatherEnabled"=-
"HolidayEnabled"=-
'@
    }
    @{
        Id = 'disable_dynamic_lock'
        Title = 'Disable Dynamic Lock'
        Tab = 'System'; Section = 'Accounts & Sign-in'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Winlogon]
"EnableGoodbye"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Winlogon]
"EnableGoodbye"=-
'@
    }
    @{
        Id = 'disable_use_my_sign_in_info_after_restart'
        Title = 'Disable Use My Sign In Info After Restart'
        Tab = 'System'; Section = 'Accounts & Sign-in'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System]
"DisableAutomaticRestartSignOn"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System]
"DisableAutomaticRestartSignOn"=-
'@
    }
    @{
        Id = 'disable_for_improved_security_only_allow_win'
        Title = 'Disable For Improved Security, Only Allow Windows Hello Sign-In'
        Tab = 'System'; Section = 'Accounts & Sign-in'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device]
"DevicePasswordLessBuildVersion"=dword:00000000
"DevicePasswordLessUpdateType"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device]
"DevicePasswordLessBuildVersion"=dword:00000002
"DevicePasswordLessUpdateType"=-
'@
    }
    @{
        Id = 'disable_windows_backup'
        Title = 'Disable Windows Backup'
        Tab = 'System'; Section = 'Accounts & Sign-in'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\SettingSync]
"DisableAccessibilitySettingSync"=dword:00000002
"DisableAccessibilitySettingSyncUserOverride"=dword:00000001
"DisableAppSyncSettingSync"=dword:00000002
"DisableAppSyncSettingSyncUserOverride"=dword:00000001
"DisableApplicationSettingSync"=dword:00000002
"DisableApplicationSettingSyncUserOverride"=dword:00000001
"DisableCredentialsSettingSync"=dword:00000002
"DisableCredentialsSettingSyncUserOverride"=dword:00000001
"DisableDesktopThemeSettingSync"=dword:00000002
"DisableDesktopThemeSettingSyncUserOverride"=dword:00000001
"DisableLanguageSettingSync"=dword:00000002
"DisableLanguageSettingSyncUserOverride"=dword:00000001
"DisablePersonalizationSettingSync"=dword:00000002
"DisablePersonalizationSettingSyncUserOverride"=dword:00000001
"DisableSettingSync"=dword:00000002
"DisableSettingSyncUserOverride"=dword:00000001
"DisableStartLayoutSettingSync"=dword:00000002
"DisableStartLayoutSettingSyncUserOverride"=dword:00000001
"DisableSyncOnPaidNetwork"=dword:00000001
"DisableWebBrowserSettingSync"=dword:00000002
"DisableWebBrowserSettingSyncUserOverride"=dword:00000001
"DisableWindowsSettingSync"=dword:00000002
"DisableWindowsSettingSyncUserOverride"=dword:00000001
"EnableWindowsBackup"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\SettingSync]
"DisableAccessibilitySettingSync"=-
"DisableAccessibilitySettingSyncUserOverride"=-
"DisableAppSyncSettingSync"=-
"DisableAppSyncSettingSyncUserOverride"=-
"DisableApplicationSettingSync"=-
"DisableApplicationSettingSyncUserOverride"=-
"DisableCredentialsSettingSync"=-
"DisableCredentialsSettingSyncUserOverride"=-
"DisableDesktopThemeSettingSync"=-
"DisableDesktopThemeSettingSyncUserOverride"=-
"DisableLanguageSettingSync"=-
"DisableLanguageSettingSyncUserOverride"=-
"DisablePersonalizationSettingSync"=-
"DisablePersonalizationSettingSyncUserOverride"=-
"DisableSettingSync"=-
"DisableSettingSyncUserOverride"=-
"DisableStartLayoutSettingSync"=-
"DisableStartLayoutSettingSyncUserOverride"=-
"DisableSyncOnPaidNetwork"=-
"DisableWebBrowserSettingSync"=-
"DisableWebBrowserSettingSyncUserOverride"=-
"DisableWindowsSettingSync"=-
"DisableWindowsSettingSyncUserOverride"=-
"EnableWindowsBackup"=-
'@
    }
    @{
        Id = 'disable_automatically_update_maps'
        Title = 'Disable Automatically Update Maps'
        Tab = 'Debloat'; Section = 'Apps'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SYSTEM\Maps]
"AutoUpdateEnabled"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SYSTEM\Maps]
"AutoUpdateEnabled"=-
'@
    }
    @{
        Id = 'disable_archive_apps'
        Title = 'Disable Archive Apps'
        Tab = 'Debloat'; Section = 'Apps'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Appx]
"AllowAutomaticAppArchiving"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Appx]
"AllowAutomaticAppArchiving"=-
'@
    }
    @{
        Id = 'hide_recycle_bin_from_desktop'
        Title = 'Hide Recycle Bin From Desktop'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu]
"{645FF040-5081-101B-9F08-00AA002F954E}"=dword:00000001
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel]
"{645FF040-5081-101B-9F08-00AA002F954E}"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu]
"{645FF040-5081-101B-9F08-00AA002F954E}"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel]
"{645FF040-5081-101B-9F08-00AA002F954E}"=-
'@
    }
    @{
        Id = 'always_hide_most_used_list_in_start_menu'
        Title = 'Always Hide Most Used List In Start Menu'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"ShowOrHideMostUsedApps"=dword:00000002
[HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"ShowOrHideMostUsedApps"=-
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoStartMenuMFUprogramsList"=-
"NoInstrumentation"=-
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoStartMenuMFUprogramsList"=-
"NoInstrumentation"=-
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"ShowOrHideMostUsedApps"=-

[HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"ShowOrHideMostUsedApps"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoStartMenuMFUprogramsList"=-
"NoInstrumentation"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoStartMenuMFUprogramsList"=-
"NoInstrumentation"=-
'@
    }
    @{
        Id = 'start_menu_hide_recommended'
        Title = 'Start Menu Hide Recommended'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]
"HideRecommendedSection"=dword:00000001
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]
"IsEducationEnvironment"=dword:00000001
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]
"HideRecommendedSection"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]
"IsEducationEnvironment"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=-
'@
    }
    @{
        Id = 'more_pins_personalization_start'
        Title = 'More Pins Personalization Start'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_Layout"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_Layout"=-
'@
    }
    @{
        Id = 'disable_show_recently_added_apps'
        Title = 'Disable Show Recently Added Apps'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecentlyAddedApps"=dword:00000001
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideRecentlyAddedApps"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecentlyAddedApps"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideRecentlyAddedApps"=-
'@
    }
    @{
        Id = 'disable_show_account_related_notifications'
        Title = 'Disable Show Account-Related Notifications'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_AccountNotifications"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_AccountNotifications"=-
'@
    }
    @{
        Id = 'disable_show_websites_from_your_browsing_his'
        Title = 'Disable Show Websites From Your Browsing History'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_RecoPersonalizedSites"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_RecoPersonalizedSites"=-
'@
    }
    @{
        Id = 'disable_show_recently_opened_items_in_start_'
        Title = 'Disable Show Recently Opened Items In Start, Jump Lists And File Explorer'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_TrackDocs"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_TrackDocs"=-
'@
    }
    @{
        Id = 'touch_keyboard_never'
        Title = 'Touch Keyboard Never'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"TipbandDesiredVisibility"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"TipbandDesiredVisibility"=-
'@
    }
    @{
        Id = 'show_smaller_taskbar_icons_never'
        Title = 'Show Smaller Taskbar Icons Never'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IconSizePreference"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IconSizePreference"=-
'@
    }
    @{
        Id = 'disable_desktop_preview'
        Title = 'Disable Desktop Preview'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarSd"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarSd"=-
'@
    }
    @{
        Id = 'remove_resume_from_taskbar'
        Title = 'Remove Resume From Taskbar'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IsEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"IsEnabled"=-
'@
    }
    @{
        Id = 'remove_meet_now'
        Title = 'Remove Meet Now'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideSCAMeetNow"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideSCAMeetNow"=-
'@
    }
    @{
        Id = 'remove_news_and_interests'
        Title = 'Remove News And Interests'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds]
"EnableFeeds"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds]
"EnableFeeds"=-
'@
    }
    @{
        Id = 'show_all_taskbar_icons'
        Title = 'Show All Taskbar Icons'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=-
'@
    }
    @{
        Id = 'remove_security_taskbar_icon'
        Title = 'Remove Security Taskbar Icon'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex(3):07,00,00,00,05,DB,8A,69,8A,49,D9,01
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex:04,00,00,00,00,00,00,00,00,00,00,00
'@
    }
    @{
        Id = 'disable_use_dynamic_lighting_on_my_devices'
        Title = 'Disable Use Dynamic Lighting On My Devices'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"AmbientLightingEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"AmbientLightingEnabled"=dword:00000001
'@
    }
    @{
        Id = 'disable_compatible_apps_in_the_foreground_al'
        Title = 'Disable Compatible Apps In The Foreground Always Control Lighting'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"ControlledByForegroundApp"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"ControlledByForegroundApp"=-
'@
    }
    @{
        Id = 'disable_match_my_windows_accent_color'
        Title = 'Disable Match My Windows Accent Color'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"UseSystemAccentColor"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Lighting]
"UseSystemAccentColor"=dword:00000001
'@
    }
    @{
        Id = 'disable_show_key_background'
        Title = 'Disable Show Key Background'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"IsKeyBackgroundEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7]
"IsKeyBackgroundEnabled"=-
'@
    }
    @{
        Id = 'disable_show_recommendations_for_tips_shortc'
        Title = 'Disable Show Recommendations For Tips Shortcuts New Apps And More'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_IrisRecommendations"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"ShowRecentList"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Start_IrisRecommendations"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"ShowRecentList"=-
'@
    }
    @{
        Id = 'disable_share_any_window_from_my_taskbar'
        Title = 'Disable Share Any Window From My Taskbar'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarSn"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarSn"=dword:00000000
'@
    }
    @{
        Id = 'disable_device_usage'
        Title = 'Disable Device Usage'
        Tab = 'Appearance'; Section = 'Personalization'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\developer]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\gaming]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\family]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\creative]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\schoolwork]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\entertainment]
"Intent"=dword:00000000
"Priority"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\business]
"Intent"=dword:00000000
"Priority"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\developer]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\gaming]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\family]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\creative]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\schoolwork]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\entertainment]
"Intent"=dword:00000000
"Priority"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudExperienceHost\Intent\business]
"Intent"=dword:00000000
"Priority"=dword:00000000
'@
    }
    @{
        Id = 'disable_usb_issues_notify'
        Title = 'Disable Usb Issues Notify'
        Tab = 'Tweaks'; Section = 'Devices'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Shell\USB]
"NotifyOnUsbErrors"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Shell\USB]
"NotifyOnUsbErrors"=-
'@
    }
    @{
        Id = 'disable_let_windows_manage_my_default_printe'
        Title = 'Disable Let Windows Manage My Default Printer'
        Tab = 'Tweaks'; Section = 'Devices'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows]
"LegacyDefaultPrinterMode"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows]
"LegacyDefaultPrinterMode"=dword:ffffffff
'@
    }
    @{
        Id = 'disable_write_with_your_fingertip'
        Title = 'Disable Write With Your Fingertip'
        Tab = 'Tweaks'; Section = 'Devices'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\EmbeddedInkControl]
"EnableInkingWithTouch"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\TabletTip\EmbeddedInkControl]
"EnableInkingWithTouch"=-
'@
    }
    @{
        Id = 'disable_notifications_suggested'
        Title = 'Disable Notifications Suggested'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.Suggested]
"Enabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.Suggested]
"Enabled"=-
'@
    }
    @{
        Id = 'disable_suggested_actions'
        Title = 'Disable Suggested Actions'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard]
"Disabled"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard]
"Disabled"=-
'@
    }
    @{
        Id = 'disable_focus_assist'
        Title = 'Disable Focus Assist'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$$windows.data.notifications.quiethourssettings\Current]
"Data"=hex(3):02,00,00,00,B4,67,2B,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,14,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,55,00,6E,00,72,00,65,00,73,00,74,00,72,00,69,00,63,00,74,00,65,00,64,00,CA,28,D0,14,02,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentfullscreen$windows.data.notifications.quietmoment\Current]
"Data"=hex(3):02,00,00,00,97,1D,2D,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,1E,26,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,41,00,6C,00,61,00,72,00,6D,00,73,00,4F,00,6E,00,6C,00,79,00,C2,28,01,CA,50,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentgame$windows.data.notifications.quietmoment\Current]
"Data"=hex(3):02,00,00,00,6C,39,2D,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,1E,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,50,00,72,00,69,00,6F,00,72,00,69,00,74,00,79,00,4F,00,6E,00,6C,00,79,00,C2,28,01,CA,50,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentpostoobe$windows.data.notifications.quietmoment\Current]
"Data"=hex(3):02,00,00,00,06,54,2D,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,1E,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,50,00,72,00,69,00,6F,00,72,00,69,00,74,00,79,00,4F,00,6E,00,6C,00,79,00,C2,28,01,CA,50,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentpresentation$windows.data.notifications.quietmoment\Current]
"Data"=hex(3):02,00,00,00,83,6E,2D,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,1E,26,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,41,00,6C,00,61,00,72,00,6D,00,73,00,4F,00,6E,00,6C,00,79,00,C2,28,01,CA,50,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentscheduled$windows.data.notifications.quietmoment\Current]
"Data"=hex(3):02,00,00,00,2E,8A,2D,68,F0,0B,D8,01,00,00,00,00,43,42,01,00,C2,0A,01,D2,1E,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,50,00,72,00,69,00,6F,00,72,00,69,00,74,00,79,00,4F,00,6E,00,6C,00,79,00,C2,28,01,D1,32,80,E0,AA,8A,99,30,D1,3C,80,E0,F6,C5,D5,0E,CA,50,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$$windows.data.notifications.quiethourssettings\Current]
"Data"=hex:02,00,00,00,74,a9,70,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,d2,14,28,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,55,00,6e,00,72,00,65,00,73,00,74,00,72,00,69,00,63,00,74,00,65,00,64,00,ca,28,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentfullscreen$windows.data.notifications.quietmoment\Current]
"Data"=hex:02,00,00,00,82,a3,71,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,c2,14,01,d2,1e,26,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,41,00,6c,00,61,00,72,00,6d,00,73,00,4f,00,6e,00,6c,00,79,00,ca,50,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentgame$windows.data.notifications.quietmoment\Current]
"Data"=hex:02,00,00,00,a5,c1,71,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,c2,14,01,d2,1e,28,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,50,00,72,00,69,00,6f,00,72,00,69,00,74,00,79,00,4f,00,6e,00,6c,00,79,00,ca,50,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentpostoobe$windows.data.notifications.quietmoment\Current]
"Data"=hex:02,00,00,00,85,de,71,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,c2,14,01,d2,1e,28,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,50,00,72,00,69,00,6f,00,72,00,69,00,74,00,79,00,4f,00,6e,00,6c,00,79,00,ca,50,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentpresentation$windows.data.notifications.quietmoment\Current]
"Data"=hex:02,00,00,00,a4,fa,71,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,c2,14,01,d2,1e,26,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,41,00,6c,00,61,00,72,00,6d,00,73,00,4f,00,6e,00,6c,00,79,00,ca,50,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\Cache\DefaultAccount\$quietmomentscheduled$windows.data.notifications.quietmoment\Current]
"Data"=hex:02,00,00,00,fe,17,72,73,03,82,da,01,00,00,00,00,43,42,01,00,c2,0a,01,d2,1e,28,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,51,00,75,00,69,00,65,00,74,00,48,00,6f,00,75,00,72,00,73,00,50,00,72,00,6f,00,66,00,69,00,6c,00,65,00,2e,00,50,00,72,00,69,00,6f,00,72,00,69,00,74,00,79,00,4f,00,6e,00,6c,00,79,00,d1,32,80,e0,aa,8a,99,30,d1,3c,80,e0,f6,c5,d5,0e,ca,50,00,00
'@
    }
    @{
        Id = 'disable_turn_on_do_not_disturb_automatically'
        Title = 'Disable Turn On Do Not Disturb Automatically'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentpresentation]
"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,E2,F3,AA,CC,06,2A,2B,0E,5A,43,42,01,00,C2,0A,01,D2,1E,26,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,41,00,6C,00,61,00,72,00,6D,00,73,00,4F,00,6E,00,6C,00,79,00,CA,50,00,00,00,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentgame]
"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,E1,F3,AA,CC,06,2A,2B,0E,5E,43,42,01,00,C2,0A,01,D2,1E,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,50,00,72,00,69,00,6F,00,72,00,69,00,74,00,79,00,4F,00,6E,00,6C,00,79,00,CA,50,00,00,00,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentfullscreen]
"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,E0,F3,AA,CC,06,2A,2B,0E,5A,43,42,01,00,C2,0A,01,D2,1E,26,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,41,00,6C,00,61,00,72,00,6D,00,73,00,4F,00,6E,00,6C,00,79,00,CA,50,00,00,00,00,00
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentpostoobe]
"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,DF,F3,AA,CC,06,2A,2B,0E,5E,43,42,01,00,C2,0A,01,D2,1E,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,51,00,75,00,69,00,65,00,74,00,48,00,6F,00,75,00,72,00,73,00,50,00,72,00,6F,00,66,00,69,00,6C,00,65,00,2E,00,50,00,72,00,69,00,6F,00,72,00,69,00,74,00,79,00,4F,00,6E,00,6C,00,79,00,CA,50,00,00,00,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentpresentation]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,2a,00,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentgame]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,2a,00,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentfullscreen]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,2a,00,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quietmoment$quietmomentlist\windows.data.donotdisturb.quietmoment$quietmomentpostoobe]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,2a,00,00,00
'@
    }
    @{
        Id = 'disable_set_priority_notifications'
        Title = 'Disable Set Priority Notifications'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quiethoursprofile$quiethoursprofilelist\windows.data.donotdisturb.quiethoursprofile$microsoft.quiethoursprofile.priorityonly]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,06,be,89,ab,cc,06,2a,2b,0e,d0,03,43,42,01,00,c2,0a,01,cd,14,06,02,05,00,00,01,01,02,00,03,01,04,00,cc,32,12,05,28,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,53,00,63,00,72,00,65,00,65,00,6e,00,53,00,6b,00,65,00,74,00,63,00,68,00,5f,00,38,00,77,00,65,00,6b,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,41,00,70,00,70,00,29,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,57,00,69,00,6e,00,64,00,6f,00,77,00,73,00,41,00,6c,00,61,00,72,00,6d,00,73,00,5f,00,38,00,77,00,65,00,6b,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,41,00,70,00,70,00,31,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,58,00,62,00,6f,00,78,00,41,00,70,00,70,00,5f,00,38,00,77,00,65,00,6b,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,58,00,62,00,6f,00,78,00,41,00,70,00,70,00,2d,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,2e,00,58,00,62,00,6f,00,78,00,47,00,61,00,6d,00,69,00,6e,00,67,00,4f,00,76,00,65,00,72,00,6c,00,61,00,79,00,5f,00,38,00,77,00,65,00,6b,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,41,00,70,00,70,00,29,57,00,69,00,6e,00,64,00,6f,00,77,00,73,00,2e,00,53,00,79,00,73,00,74,00,65,00,6d,00,2e,00,4e,00,65,00,61,00,72,00,53,00,68,00,61,00,72,00,65,00,45,00,78,00,70,00,65,00,72,00,69,00,65,00,6e,00,63,00,65,00,52,00,65,00,63,00,65,00,69,00,76,00,65,00,00,00,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.donotdisturb.quiethoursprofile$quiethoursprofilelist\windows.data.donotdisturb.quiethoursprofile$microsoft.quiethoursprofile.priorityonly]
"Data"=hex:43,42,01,00,0a,02,01,00,2a,2a,00,00,00
'@
    }
    @{
        Id = 'disable_focus_settings'
        Title = 'Disable Focus Settings'
        Tab = 'Debloat'; Section = 'Notifications'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.shell.focussessionactivetheme\windows.data.shell.focussessionactivetheme${1b019365-25a5-4ff1-b50a-c155229afc8f}]
"Data"=hex(3):43,42,01,00,0A,00,2A,06,F4,E2,AA,CC,06,2A,2B,0E,08,43,42,01,00,C2,0A,01,00,00,00,00
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.shell.focussessionactivetheme\windows.data.shell.focussessionactivetheme${1b019365-25a5-4ff1-b50a-c155229afc8f}]
"Data"=-
'@
    }
    @{
        Id = 'battery_options_optimize_for_video_quality'
        Title = 'Battery Options Optimize For Video Quality'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\VideoSettings]
"VideoQualityOnBattery"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\VideoSettings]
"VideoQualityOnBattery"=-
'@
    }
    @{
        Id = 'disable_storage_sense'
        Title = 'Disable Storage Sense'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"04"=dword:00000000
'@
        Default = @'
[]
"04"=-
'@
    }
    @{
        Id = 'disable_keep_windows_running_smoothly'
        Title = 'Disable Keep Windows Running Smoothly'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $false
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense]
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters]
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\CachedSizes]
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy]
'@
        Default = ''
    }
    @{
        Id = 'don_t_auto_delete_temp_files'
        Title = 'Don''t Auto Delete Temp Files'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"2048"=dword:00000000
'@
        Default = @'
[]
"2048"=-
'@
    }
    @{
        Id = 'don_t_auto_empty_recycle_bin'
        Title = 'Don''t Auto Empty Recycle Bin'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"08"=dword:00000000
'@
        Default = @'
[]
"08"=-
'@
    }
    @{
        Id = 'don_t_auto_delete_downloads'
        Title = 'Don''t Auto Delete Downloads'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"256"=dword:00000000
'@
        Default = @'
[]
"256"=-
'@
    }
    @{
        Id = 'never_auto_run_storage_sense'
        Title = 'Never Auto Run Storage Sense'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"32"=dword:00000000
'@
        Default = @'
[]
"32"=-
'@
    }
    @{
        Id = 'settings_set'
        Title = 'Settings Set'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
"StoragePoliciesChanged"=dword:00000001
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy\SpaceHistory]
'@
        Default = @'
[]
"StoragePoliciesChanged"=-
'@
    }
    @{
        Id = 'disable_drag_tray'
        Title = 'Disable Drag Tray'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CDP]
"DragTrayEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CDP]
"DragTrayEnabled"=-
'@
    }
    @{
        Id = 'disable_snap_window_settings'
        Title = 'Disable Snap Window Settings'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"SnapAssist"=dword:00000000
"DITest"=dword:00000000
"EnableSnapBar"=dword:00000000
"EnableTaskGroups"=dword:00000000
"EnableSnapAssistFlyout"=dword:00000000
"SnapFill"=dword:00000000
"JointResize"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"SnapAssist"=-
"DITest"=-
"EnableSnapBar"=-
"EnableTaskGroups"=-
"EnableSnapAssistFlyout"=-
"SnapFill"=-
"JointResize"=-
'@
    }
    @{
        Id = 'enable_endtask_menu_taskbar'
        Title = 'Enable Endtask Menu Taskbar'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings]
"TaskbarEndTask"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings]
"TaskbarEndTask"=dword:00000000
'@
    }
    @{
        Id = 'enable_long_paths'
        Title = 'Enable Long Paths'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem]
"LongPathsEnabled"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem]
"LongPathsEnabled"=-
'@
    }
    @{
        Id = 'alt_tab_open_windows_only'
        Title = 'Alt Tab Open Windows Only'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"MultiTaskingAltTabFilter"=dword:00000003
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"MultiTaskingAltTabFilter"=-
'@
    }
    @{
        Id = 'disable_share_across_devices'
        Title = 'Disable Share Across Devices'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CDP]
"RomeSdkChannelUserAuthzPolicy"=dword:00000000
"CdpSessionUserAuthzPolicy"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CDP]
"RomeSdkChannelUserAuthzPolicy"=dword:00000001
"CdpSessionUserAuthzPolicy"=-
'@
    }
    @{
        Id = 'disable_recommended_troubleshooter_preferenc'
        Title = 'Disable Recommended Troubleshooter Preferences'
        Tab = 'Tweaks'; Section = 'Performance'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsMitigation]
"UserPreference"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsMitigation]
"UserPreference"=-
'@
    }
    @{
        Id = 'disable_update_apps_automatically'
        Title = 'Disable Update Apps Automatically'
        Tab = 'Debloat'; Section = 'Store'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate]
"AutoDownload"=dword:00000002
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate]
"AutoDownload"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-
'@
    }
    @{
        Id = 'set_start_menu_apps_view_to_list'
        Title = 'Set Start Menu Apps View To List'
        Tab = 'Appearance'; Section = 'Start Menu'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
'@
    }
    @{
        Id = 'disable_windows_input_experience_preload'
        Title = 'Disable Windows Input Experience Preload'
        Tab = 'Debloat'; Section = 'Apps & Background'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\input]
"IsInputAppPreloadEnabled"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Dsh]
"IsPrelaunchEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\input]
"IsInputAppPreloadEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Dsh]
"IsPrelaunchEnabled"=-
'@
    }
    @{
        Id = 'disable_ms_gamebar_notifications_with_xbox_c'
        Title = 'Disable Ms-Gamebar Notifications With Xbox Controller Plugged In'
        Tab = 'Debloat'; Section = 'Apps & Background'; Revertible = $true
        Optimize = @'
[HKEY_CLASSES_ROOT\ms-gamebar]
"(Default)"="URL:ms-gamebar"
"URL Protocol"=""
"NoOpenWith"=""
[HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"
[HKEY_CLASSES_ROOT\ms-gamebarservices]
"(Default)"="URL:ms-gamebarservices"
"URL Protocol"=""
"NoOpenWith"=""
[HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"
[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"(Default)"="URL:ms-gamingoverlay"
"URL Protocol"=""
"NoOpenWith"=""
[HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager]
"ContentDeliveryAllowed"=dword:00000000
"FeatureManagementEnabled"=dword:00000000
"OemPreInstalledAppsEnabled"=dword:00000000
"PreInstalledAppsEnabled"=dword:00000000
"PreInstalledAppsEverEnabled"=dword:00000000
"RotatingLockScreenEnabled"=dword:00000000
"RotatingLockScreenOverlayEnabled"=dword:00000000
"SilentInstalledAppsEnabled"=dword:00000000
"SlideshowEnabled"=dword:00000000
"SoftLandingEnabled"=dword:00000000
"SubscribedContent-310093Enabled"=dword:00000000
"SubscribedContent-314563Enabled"=dword:00000000
"SubscribedContent-338388Enabled"=dword:00000000
"SubscribedContent-338389Enabled"=dword:00000000
"SubscribedContent-338393Enabled"=dword:00000000
"SubscribedContent-353694Enabled"=dword:00000000
"SubscribedContent-353696Enabled"=dword:00000000
"SubscribedContent-353698Enabled"=dword:00000000
"SubscribedContentEnabled"=dword:00000000
"SystemPaneSuggestionsEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CLASSES_ROOT\ms-gamebar]
"(Default)"=-
"URL Protocol"=""
"NoOpenWith"=-

[HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]
"(Default)"=-

[HKEY_CLASSES_ROOT\ms-gamebarservices]
"(Default)"=-
"URL Protocol"=-
"NoOpenWith"=-

[HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]
"(Default)"=-

[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"(Default)"=-
"URL Protocol"=""
"NoOpenWith"=-

[HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]
"(Default)"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000001

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager]
"ContentDeliveryAllowed"=dword:00000001
"FeatureManagementEnabled"=dword:00000001
"OemPreInstalledAppsEnabled"=dword:00000001
"PreInstalledAppsEnabled"=dword:00000001
"PreInstalledAppsEverEnabled"=dword:00000001
"RotatingLockScreenEnabled"=dword:00000001
"RotatingLockScreenOverlayEnabled"=dword:00000001
"SilentInstalledAppsEnabled"=dword:00000001
"SlideshowEnabled"=dword:00000001
"SoftLandingEnabled"=dword:00000001
"SubscribedContent-310093Enabled"=-
"SubscribedContent-314563Enabled"=-
"SubscribedContent-338388Enabled"=-
"SubscribedContent-338389Enabled"=-
"SubscribedContent-338393Enabled"=-
"SubscribedContent-353694Enabled"=-
"SubscribedContent-353696Enabled"=-
"SubscribedContent-353698Enabled"=-
"SubscribedContentEnabled"=dword:00000001
"SystemPaneSuggestionsEnabled"=dword:00000001
'@
    }
    @{
        Id = 'remove_3d_objects'
        Title = 'Remove 3D Objects'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $false
        Optimize = @'
[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}]
[-HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}]
'@
        Default = ''
    }
    @{
        Id = 'remove_quick_access'
        Title = 'Remove Quick Access'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer]
"HubMode"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer]
"HubMode"=-
'@
    }
    @{
        Id = 'remove_home_broken_on_new_update'
        Title = 'Remove Home (Broken On New Update) (+1 more)'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}]
"System.IsPinnedToNameSpaceTree"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}]
"System.IsPinnedToNameSpaceTree"=-
'@
    }
    @{
        Id = 'disable_menu_show_delay'
        Title = 'Disable Menu Show Delay'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"MenuShowDelay"="0"
'@
        Default = @'
[HKEY_CURRENT_USER\Control Panel\Desktop]
"MenuShowDelay"="400"
'@
    }
    @{
        Id = 'disable_driver_searching_updates'
        Title = 'Disable Driver Searching & Updates'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching]
"SearchOrderConfig"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching]
"SearchOrderConfig"=dword:00000001
'@
    }
    @{
        Id = 'disable_phone_companion_in_start_menu'
        Title = 'Disable Phone Companion In Start Menu'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"RightCompanionToggledOpen"=dword:00000000
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe]
"IsEnabled"=dword:00000000
"IsAvailable"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"RightCompanionToggledOpen"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe]
"IsEnabled"=-
"IsAvailable"=-
'@
    }
    @{
        Id = 'more_info_on_bsod'
        Title = 'More Info On Bsod'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\CrashControl]
"DisplayParameters"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\CrashControl]
"DisplayParameters"=dword:00000000
'@
    }
    @{
        Id = 'disable_windows_platform_binary_table'
        Title = 'Disable Windows Platform Binary Table'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager]
"DisableWpbtExecution"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager]
"DisableWpbtExecution"=-
'@
    }
    @{
        Id = 'no_web_services_in_explorer'
        Title = 'No Web Services In Explorer'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoWebServices"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"NoWebServices"=-
'@
    }
    @{
        Id = 'disable_cross_device_resume'
        Title = 'Disable Cross Device Resume'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CrossDeviceResume\Configuration]
"IsResumeAllowed"=dword:00000000
"IsOneDriveResumeAllowed"=dword:00000000
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\default\Connectivity\DisableCrossDeviceResume]
"value"=dword:00000001
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\8\1387020943]
"EnabledState"=dword:00000001
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\8\1694661260]
"EnabledState"=dword:00000001
'@
        Default = @'
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CrossDeviceResume\Configuration]
"IsResumeAllowed"=-
"IsOneDriveResumeAllowed"=-

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\default\Connectivity\DisableCrossDeviceResume]
"value"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\8\1387020943]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\8\1694661260]
"EnabledState"=-
'@
    }
    @{
        Id = 'hide_home_in_settings'
        Title = 'Hide Home In Settings'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"SettingsPageVisibility"="hide:home;"
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"SettingsPageVisibility"=-
'@
    }
    @{
        Id = 'disable_open_terminal_by_default'
        Title = 'Disable Open Terminal By Default'
        Tab = 'Appearance'; Section = 'File Explorer'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Console\%%Startup]
"DelegationConsole"="{B23D10C0-E52E-411E-9D5B-C09FDF709C7D}"
"DelegationTerminal"="{B23D10C0-E52E-411E-9D5B-C09FDF709C7D}"
'@
        Default = @'
[HKEY_CURRENT_USER\Console\%%Startup]
"DelegationConsole"=-
"DelegationTerminal"=-
'@
    }
    @{
        Id = 'black_powershell_console'
        Title = 'Black Powershell Console'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\Console\%SystemRoot%_System32_WindowsPowerShell_v1.0_powershell.exe]
"ScreenColors"=dword:0000000F
'@
        Default = @'
[HKEY_CURRENT_USER\Console\%SystemRoot%_System32_WindowsPowerShell_v1.0_powershell.exe]
"ScreenColors"=dword:00000056
'@
    }
    @{
        Id = 'fix_enter_your_pin_hello_face_sign_in_bug_al'
        Title = 'Fix Enter Your Pin Hello Face Sign In Bug Allow Password Instead'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device]
"DevicePasswordLessBuildVersion"=dword:00000000
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device]
"DevicePasswordLessBuildVersion"=dword:00000002
'@
    }
    @{
        Id = 'disable_finish_setting_up_your_device'
        Title = 'Disable Finish Setting Up Your Device'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement]
"ScoobeSystemSettingEnabled"=dword:00000000
'@
        Default = @'
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement]
"ScoobeSystemSettingEnabled"=-
'@
    }
    @{
        Id = 'disable_background_blur_during_sign_in'
        Title = 'Disable Background Blur During Sign-In'
        Tab = 'System'; Section = 'System Extras'; Revertible = $true
        Optimize = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
"DisableAcrylicBackgroundOnLogon"=dword:00000001
'@
        Default = @'
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System]
"DisableAcrylicBackgroundOnLogon"=-
'@
    }
)

# Apply one Control Panel tweak by Id. $Mode = 'optimize' (default) or 'default'.
function Invoke-CpTweak {
    param([Parameter(Mandatory)][string]$Id, [ValidateSet('optimize','default')][string]$Mode = 'optimize')
    $t = $sync.CpTweaks | Where-Object { $_.Id -eq $Id } | Select-Object -First 1
    if (-not $t) { return }
    $body = if ($Mode -eq 'default') { $t.Default } else { $t.Optimize }
    if ([string]::IsNullOrWhiteSpace($body)) { return }
    $reg = "Windows Registry Editor Version 5.00`r`n`r`n" + $body.Trim()
    $f = Join-Path $env:SystemRoot "Temp\akari_cp_$Id.reg"
    [IO.File]::WriteAllText($f, $reg, (New-Object Text.UTF8Encoding($false)))
    Start-Process reg.exe -ArgumentList "import `"$f`"" -WindowStyle Hidden -Wait
    Set-Status ("{0}: {1} applied. Some settings need a sign-out." -f $t.Title, $Mode) "#66BB6A"
}

# Build the granular tweak rows into each tab's panel at startup (called from main.ps1).
function Render-CpTweaks {
    if (-not $sync.CpTweaks) { return }
    $w = $sync.window
    $cardStyle  = $w.FindResource('Card')
    $hdrStyle   = $w.FindResource('CardGroupHeader')
    $titleStyle = $w.FindResource('CardTitle')
    $descStyle  = $w.FindResource('CardDesc')
    $sepStyle   = $w.FindResource('Sep')
    $btnStyle   = $w.FindResource('Btn')
    $btnAccent  = $w.FindResource('BtnAccent')
    $expStyle   = $w.FindResource('TweakExpander')
    $star = New-Object System.Windows.GridLength -ArgumentList @([double]1, [System.Windows.GridUnitType]::Star)
    $auto = [System.Windows.GridLength]::Auto

    # search bookkeeping: each expander + each card's searchable text
    $sync.CpExpanders = New-Object System.Collections.ArrayList
    $sync.CpCards     = New-Object System.Collections.ArrayList

    $panelMap = @{ Debloat = $sync.PanelDebloat; Appearance = $sync.PanelAppearance; Tweaks = $sync.PanelTweaks; System = $sync.PanelSystem }
    foreach ($tab in $panelMap.Keys) {
        $panel = $panelMap[$tab]
        if (-not $panel) { continue }
        $container = $panel.Content   # the StackPanel that already holds the curated cards
        $tweaks = @($sync.CpTweaks | Where-Object { $_.Tab -eq $tab })
        if ($tweaks.Count -eq 0) { continue }

        # collapsed "Individual tweaks" expander holds all this tab's sections
        $exp = New-Object System.Windows.Controls.Expander
        $exp.Style = $expStyle
        $exp.Header = "Individual tweaks  ($($tweaks.Count) settings)"
        $exp.IsExpanded = $false
        $inner = New-Object System.Windows.Controls.StackPanel
        [void]$sync.CpExpanders.Add($exp)

        # group by section, preserving first-seen order
        $sections = [ordered]@{}
        foreach ($t in $tweaks) {
            if (-not $sections.Contains($t.Section)) { $sections[$t.Section] = New-Object System.Collections.ArrayList }
            [void]$sections[$t.Section].Add($t)
        }

        foreach ($sec in $sections.Keys) {
            $card = New-Object System.Windows.Controls.Border; $card.Style = $cardStyle
            $sp = New-Object System.Windows.Controls.StackPanel
            $hdr = New-Object System.Windows.Controls.TextBlock; $hdr.Style = $hdrStyle; $hdr.Text = $sec.ToUpper()
            [void]$sp.Children.Add($hdr)
            $cardText = $sec

            $first = $true
            foreach ($t in $sections[$sec]) {
                $cardText += " " + $t.Title
                if (-not $first) { $s = New-Object System.Windows.Controls.Separator; $s.Style = $sepStyle; [void]$sp.Children.Add($s) }
                $first = $false

                $grid = New-Object System.Windows.Controls.Grid
                $c0 = New-Object System.Windows.Controls.ColumnDefinition; $c0.Width = $star
                $c1 = New-Object System.Windows.Controls.ColumnDefinition; $c1.Width = $auto
                $grid.ColumnDefinitions.Add($c0); $grid.ColumnDefinitions.Add($c1)

                $tb = New-Object System.Windows.Controls.TextBlock; $tb.Style = $titleStyle
                $tb.Text = $t.Title; $tb.VerticalAlignment = 'Center'; $tb.TextWrapping = 'Wrap'
                [System.Windows.Controls.Grid]::SetColumn($tb, 0); [void]$grid.Children.Add($tb)

                $btns = New-Object System.Windows.Controls.StackPanel; $btns.Orientation = 'Horizontal'
                $btns.VerticalAlignment = 'Center'
                [System.Windows.Controls.Grid]::SetColumn($btns, 1)
                $id = $t.Id
                if ($t.Revertible) {
                    $bOpt = New-Object System.Windows.Controls.Button; $bOpt.Style = $btnAccent; $bOpt.Content = 'Optimize'
                    $bOpt.Margin = New-Object System.Windows.Thickness -ArgumentList @([double]0, [double]0, [double]8, [double]0)
                    $bOpt.Add_Click({ Invoke-CpTweak -Id $id -Mode 'optimize' }.GetNewClosure())
                    $bDef = New-Object System.Windows.Controls.Button; $bDef.Style = $btnStyle; $bDef.Content = 'Default'
                    $bDef.Add_Click({ Invoke-CpTweak -Id $id -Mode 'default' }.GetNewClosure())
                    [void]$btns.Children.Add($bOpt); [void]$btns.Children.Add($bDef)
                } else {
                    $bApp = New-Object System.Windows.Controls.Button; $bApp.Style = $btnAccent; $bApp.Content = 'Apply'
                    $bApp.ToolTip = 'One-way tweak (no automatic revert)'
                    $bApp.Add_Click({ Invoke-CpTweak -Id $id -Mode 'optimize' }.GetNewClosure())
                    [void]$btns.Children.Add($bApp)
                }
                [void]$grid.Children.Add($btns)
                [void]$sp.Children.Add($grid)
            }
            $card.Child = $sp
            [void]$inner.Children.Add($card)
            [void]$sync.CpCards.Add([pscustomobject]@{ Card = $card; Text = $cardText.ToLowerInvariant(); Expander = $exp; Panel = "Panel$tab" })
        }
        $exp.Content = $inner
        [void]$container.Children.Add($exp)
    }
}