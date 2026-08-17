using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // REGISTRY TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        private static void ApplyRegistryTweaks(ToolService log)
        {
            log.Log("[PLAYBOOK] Applying registry tweaks...");
            int ok = 0;

            var tweaks = new (string Path, string Name, object Value, RegistryValueKind Kind)[]
            {
                // ── Telemetry ─────────────────────────────────────────────────────
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    "AllowTelemetry", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    "LimitDiagnosticLogCollection", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    "LimitDumpCollection", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    "LimitEnhancedDiagnosticDataWindowsAnalytics", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    "DoNotShowFeedbackNotifications", 1, RegistryValueKind.DWord),

                // DiagTrack service — belt-and-suspenders alongside ServicesPreset
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack",
                    "Start", 4, RegistryValueKind.DWord),

                // Online tips
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "AllowOnlineTips", 0, RegistryValueKind.DWord),

                // Typing insights
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\input\Settings",
                    "InsightsEnabled", 0, RegistryValueKind.DWord),

                // Inking and typing telemetry
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\TextInput",
                    "AllowLinguisticDataCollection", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Policies\TextInput",
                    "AllowLinguisticDataCollection", 0, RegistryValueKind.DWord),

                // Auto maintenance
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance",
                    "MaintenanceDisabled", 1, RegistryValueKind.DWord),

                // FTH (Fault Tolerant Heap)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\FTH",
                    "Enabled", 0, RegistryValueKind.DWord),

                // CEIP
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\SQMClient\Windows",
                    "CEIPEnable", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\SQMClient\Windows",
                    "CEIPEnable", 0, RegistryValueKind.DWord),

                // PowerShell telemetry
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                    "POWERSHELL_TELEMETRY_OPTOUT", "1", RegistryValueKind.String),

                // ── Mouse / Input ─────────────────────────────────────────────────
                // Mouse acceleration — disable
                (@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed",      "0", RegistryValueKind.String),
                (@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String),
                (@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String),

                // Throttle raw mouse polling for background apps (reduces wakeups)
                (@"HKEY_CURRENT_USER\Control Panel\Mouse",
                    "RawMouseThrottleDuration", 20, RegistryValueKind.DWord),

                // Disable PrintScreen hijack by Snipping Tool
                (@"HKEY_CURRENT_USER\Control Panel\Keyboard",
                    "PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord),

                // Disable Sticky Keys / Toggle Keys / Filter Keys prompts & hotkeys
                // NOTE: Flags is REG_SZ on Windows — a DWORD here is ignored by the
                // accessibility stack. Canonical AME values: 506 / 122 / 58 keep the
                // features available in Settings but kill the hotkeys and popups.
                (@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",   "Flags", "506", RegistryValueKind.String),
                (@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response", "Flags", "122", RegistryValueKind.String),
                (@"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys",   "Flags", "58", RegistryValueKind.String),

                // ── CPU scheduling ────────────────────────────────────────────────
                // Win32PrioritySeparation = 0x2A: max foreground boost, fixed quanta
                // Most impactful CPU scheduling tweak for gaming/desktop responsiveness
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl",
                    "Win32PrioritySeparation", 0x2A, RegistryValueKind.DWord),

                // WER (Windows Error Reporting)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PCHealth\ErrorReporting",
                    "DoReport", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                    "Disabled", 1, RegistryValueKind.DWord),

                // SystemResponsiveness (10% instead of 20% — more CPU time for games)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness", 10, RegistryValueKind.DWord),

                // ── Privacy / Activity ────────────────────────────────────────────
                // Web search in Start
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                    "ConnectedSearchUseWeb", 0, RegistryValueKind.DWord),

                // Dynamic search box suggestions
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings",
                    "IsDynamicSearchBoxEnabled", 0, RegistryValueKind.DWord),

                // Push notifications
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "NoCloudApplicationNotification", 1, RegistryValueKind.DWord),

                // News and Interests / Widgets feed
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests",
                    "value", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Dsh",
                    "AllowNewsAndInterests", 0, RegistryValueKind.DWord),

                // Activity Feed / Timeline
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    "EnableActivityFeed", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\System",
                    "EnableActivityFeed", 0, RegistryValueKind.DWord),

                // Publish / upload user activities to Microsoft
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    "PublishUserActivities", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    "UploadUserActivities", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\System",
                    "PublishUserActivities", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\System",
                    "UploadUserActivities", 0, RegistryValueKind.DWord),

                // RSOP logging (Resultant Set of Policy)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    "RSoPLogging", 0, RegistryValueKind.DWord),

                // Disable automatic restart sign-on after update reboot
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "DisableAutomaticRestartSignOn", 1, RegistryValueKind.DWord),

                // Program Compatibility Assistant
                (@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                    "DisablePCA", 1, RegistryValueKind.DWord),

                // GameBar PresenceWriter — disable activatable class
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter",
                    "ActivationType", 0, RegistryValueKind.DWord),

                // GameBar
                (@"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                    "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord),

                // Auto app archiving
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Appx",
                    "AllowAutomaticAppArchiving", 0, RegistryValueKind.DWord),

                // BitLocker auto-encrypt prevention
                (@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\BitLocker",
                    "PreventDeviceEncryption", 1, RegistryValueKind.DWord),

                // Enterprise feature control
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                    "AllowTemporaryEnterpriseFeatureControl", 0, RegistryValueKind.DWord),

                // Speech model download
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Preferences",
                    "ModelDownloadAllowed", 0, RegistryValueKind.DWord),

                // Device metadata from network
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata",
                    "PreventDeviceMetadataFromNetwork", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Device Metadata",
                    "PreventDeviceMetadataFromNetwork", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\Device Metadata",
                    "PreventDeviceMetadataFromNetwork", 1, RegistryValueKind.DWord),

                // ── Explorer / UI ─────────────────────────────────────────────────
                // Gallery in Explorer sidebar — HKCU variant (complements HKLM in next block)
                (@"HKEY_CURRENT_USER\SOFTWARE\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}",
                    "System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord),

                // Gallery — HKLM variant (hides from all users)
                (@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                    "HiddenByDefault", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                    "HiddenByDefault", 1, RegistryValueKind.DWord),

                // Show file extensions
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "HideFileExt", 0, RegistryValueKind.DWord),

                // Disable Aero Shake (shake window to minimize all others)
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "DisallowShaking", 1, RegistryValueKind.DWord),

                // Hide OneDrive/SharePoint sync provider notifications in Explorer
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ShowSyncProviderNotifications", 0, RegistryValueKind.DWord),

                // Stop Explorer parsing every file for folder type detection
                (@"HKEY_CURRENT_USER\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell",
                    "FolderType", "NotSpecified", RegistryValueKind.String),

                // Disable flip3D (legacy Win+Tab 3D switcher — dead feature)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DWM",
                    "DisallowFlip3d", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\DWM",
                    "DisallowFlip3d", 1, RegistryValueKind.DWord),

                // ── Shutdown time ─────────────────────────────────────────────────
                // Cut default 5s hung-app wait down to 2s
                (@"HKEY_CURRENT_USER\Control Panel\Desktop",
                    "HungAppTimeout", "2000", RegistryValueKind.String),
                (@"HKEY_CURRENT_USER\Control Panel\Desktop",
                    "WaitToKillAppTimeOut", "2000", RegistryValueKind.String),
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                    "WaitToKillServiceTimeout", "2000", RegistryValueKind.String),

                // ── Audio ─────────────────────────────────────────────────────────
                // Disable audio ducking during calls (don't auto-reduce volume)
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Multimedia\Audio",
                    "UserDuckingPreference", 3, RegistryValueKind.DWord),

                // ── Windows Update ────────────────────────────────────────────────
                // Exclude driver updates from quality update channel (manage drivers manually)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                    "ExcludeWUDriversInQualityUpdate", 1, RegistryValueKind.DWord),

                // Set active hours so Windows won't auto-reboot during use (8am–11pm)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                    "ActiveHoursStart", 8, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                    "ActiveHoursEnd", 23, RegistryValueKind.DWord),

                // No auto-download over metered connections
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                    "AllowAutoWindowsUpdateDownloadOverMeteredNetwork", 0, RegistryValueKind.DWord),

                // ── Network / Security ────────────────────────────────────────────
                // Disable LLMNR (Link-Local Multicast Name Resolution — known attack vector)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
                    "EnableMulticast", 0, RegistryValueKind.DWord),

                // Restrict anonymous access to shares and SAM
                (@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\LanmanServer\Parameters",
                    "RestrictNullSessAccess", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Lsa",
                    "RestrictAnonymous", 1, RegistryValueKind.DWord),

                // Disable Remote Assistance
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance",
                    "fAllowToGetHelp", 0, RegistryValueKind.DWord),

                // Smart Card Plug and Play (irrelevant on gaming/desktop)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\ScPnP",
                    "EnableScPnP", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\ScPnP",
                    "EnableScPnP", 0, RegistryValueKind.DWord),

                // Device Health Attestation (cloud-based device compliance check)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\DeviceHealthAttestationService",
                    "EnableDeviceHealthAttestationService", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\DeviceHealthAttestationService",
                    "EnableDeviceHealthAttestationService", 0, RegistryValueKind.DWord),

                // ── Background apps / delivery ────────────────────────────────────
                // Force all background apps off via policy
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                    "LetAppsRunInBackground", 2, RegistryValueKind.DWord),

                // Delivery Optimization — LAN only (belt-and-suspenders with service kill)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                    "DODownloadMode", 100, RegistryValueKind.DWord),

                // ReadyBoost — disable
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\EMDMgmt",
                    "GroupPolicyDisallowCaches", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\EMDMgmt",
                    "AllowNewCachesByDefault", 0, RegistryValueKind.DWord),

                // CrossDevice Resume
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\default\Connectivity\DisableCrossDeviceResume",
                    "value", 1, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CrossDeviceResume\Configuration",
                    "IsResumeAllowed", 0, RegistryValueKind.DWord),

                // ── Windows Spotlight / Content Delivery ──────────────────────────
                (@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                    "DisableWindowsSpotlightFeatures", 1, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                    "ConfigureWindowsSpotlight", 2, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                    "DisableWindowsSpotlightOnActionCenter", 1, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                    "DisableWindowsSpotlightWindowsWelcomeExperience", 1, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                    "DisableWindowsSpotlightOnSettings", 1, RegistryValueKind.DWord),

                // Cloud tips / soft landing
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                    "DisableSoftLanding", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\CloudContent",
                    "DisableSoftLanding", 1, RegistryValueKind.DWord),

                // Content Delivery Manager (suggested apps, rotating lock screen ads)
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SoftLandingEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "RotatingLockScreenEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "RotatingLockScreenOverlayEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-338387Enabled", 0, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-338389Enabled", 0, RegistryValueKind.DWord),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SubscribedContent-353698Enabled", 0, RegistryValueKind.DWord),

                // ── Diagnostics / WDI ─────────────────────────────────────────────
                // Scheduled diagnostics
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\ScheduledDiagnostics",
                    "EnabledExecution", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\ScheduledDiagnostics",
                    "EnabledExecution", 0, RegistryValueKind.DWord),

                // Scripted diagnostics / online troubleshooting
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\ScriptedDiagnostics",
                    "EnableDiagnostics", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\ScriptedDiagnosticsProvider\Policy",
                    "EnableQueryRemoteServer", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\ScriptedDiagnosticsProvider\Policy",
                    "EnableQueryRemoteServer", 0, RegistryValueKind.DWord),

                // WDI diagnostic scenarios (crash analyzer, disk analyzer, etc.)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{a7a5847a-7511-4e4e-90b1-45ad2a002f51}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{186f47ef-626c-4670-800a-4a30756babad}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{ecfb03d1-58ee-4cc7-a1b5-9bc6febcb915}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{67144949-5132-4859-8036-a737b43825d8}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{86432a0b-3c7d-4ddf-a89c-172faa90485d}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{eb73b633-3f4e-4ba0-8f60-8f3c6f53168f}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WDI\{2698178D-FDAD-40AE-9D3C-1371703ADC5B}",
                    "ScenarioExecutionEnabled", 0, RegistryValueKind.DWord),

                // Help tips sticker (Windows EdgeUI)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\EdgeUI",
                    "DisableHelpSticker", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\EdgeUI",
                    "DisableHelpSticker", 1, RegistryValueKind.DWord),

                // ── Misc ──────────────────────────────────────────────────────────
                // Font providers (stops Windows fetching fonts from the internet)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                    "EnableFontProviders", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\System",
                    "EnableFontProviders", 0, RegistryValueKind.DWord),

                // Setting sync (cross-device Microsoft account settings sync)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\SettingSync",
                    "DisableSettingSync", 2, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\Windows\SettingSync",
                    "DisableSettingSync", 2, RegistryValueKind.DWord),

                // WMP media sharing
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsMediaPlayer",
                    "PreventLibrarySharing", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Policies\Microsoft\WindowsMediaPlayer",
                    "PreventLibrarySharing", 1, RegistryValueKind.DWord),

                // Disable driver auto-search on new hardware (manage drivers manually)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching",
                    "SearchOrderConfig", 0, RegistryValueKind.DWord),

                // App-V (enterprise application virtualization — no-op on consumer Windows)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\AppV\Client",
                    "Enabled", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\AppV\Client",
                    "Enabled", 0, RegistryValueKind.DWord),

                // Superfetch event log channels — disable ETW logging for Superfetch
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\Microsoft-Windows-Superfetch/Main",
                    "Enable", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\Microsoft-Windows-Superfetch/PfApLog",
                    "Enable", 0, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\Microsoft-Windows-Superfetch/StoreLog",
                    "Enable", 0, RegistryValueKind.DWord),

                // Zone information on file attachments
                (@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Attachments",
                    "SaveZoneInformation", 1, RegistryValueKind.DWord),

                // Adobe Type Manager Font Driver (ATMFD) — disable (security hardening)
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows",
                    "DisableATMFD", 1, RegistryValueKind.DWord),

                // Hide Sleep and Hibernate from Start menu
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                    "HideHibernate", 1, RegistryValueKind.DWord),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                    "HideSleep", 1, RegistryValueKind.DWord),
            };

            foreach (var (path, name, value, kind) in tweaks)
            {
                try
                {
                    Registry.SetValue(path, name, value, kind);
                    ok++;
                }
                catch (Exception ex)
                {
                    log.Log($"[PLAYBOOK] WARNING {name}: {ex.Message}");
                }
            }

            log.Log($"[PLAYBOOK] Registry tweaks: {ok}/{tweaks.Length} applied.");
        }

        private static void UndoRegistryTweaks(ToolService log)
        {
            log.Log("[PLAYBOOK] Reverting registry tweaks...");

            // Restore telemetry
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "AllowTelemetry", 3, RegistryValueKind.DWord);

            // Restore mouse acceleration
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed",      "1", RegistryValueKind.String);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "6", RegistryValueKind.String);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "10", RegistryValueKind.String);

            // Restore SystemResponsiveness
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "SystemResponsiveness", 20, RegistryValueKind.DWord);

            // Restore FTH
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\FTH", "Enabled", 1, RegistryValueKind.DWord);

            // Restore Gallery
            foreach (var path in new[]
            {
                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
            })
                Registry.SetValue(path, "HiddenByDefault", 0, RegistryValueKind.DWord);

            log.Log("[PLAYBOOK] Registry tweaks reverted.");
        }

    }
}
