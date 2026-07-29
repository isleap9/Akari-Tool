using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        private static bool? ReadStartPinsCleaned()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer");
                return k?.GetValue("ConfigureStartPins") is string;
            }
            catch { return null; }
        }

        private static bool? ReadRecommendedPolicyHidden()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer");
                return k?.GetValue("HideRecommendedSection") is int i && i == 1;
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // START MENU
        // ─────────────────────────────────────────────────────────────────────

        private void BuildStartMenu(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Start Menu", "Customize the Windows 11 Start Menu layout and behavior.",
                withActions: true, panel));

            var layoutSection = TweakHelpers.BuildSection(panel, "Layout");

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-start-clean-pins",
                Name        = "Clean Start Menu (Remove All Pins)",
                Description = "Removes all default pinned apps from the Start Menu — clears Edge, Settings and File Explorer pins on 26200.8521+ builds",
                Group       = "Layout",
                // Winhance ConfigureStartPins: Recommended={"pinnedList":[]} → EnabledValue → ON;
                // Default=null → DisabledValue → OFF. Same polarity as this row.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = ReadStartPinsCleaned,
                Apply       = enable =>
                {
                    const string EmptyPins = @"{""pinnedList"":[]}";
                    // Both paths, matching Winhance: MDM/CSP for older builds,
                    // GPO path (added by KB5062660, Jul 2025) for newer ones.
                    if (enable)
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            "ConfigureStartPins", EmptyPins, RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            "ConfigureStartPins", EmptyPins, RegistryValueKind.String);

                        // Winhance parity: the policy alone doesn't always flush the
                        // cached layout. Delete each real profile's start*.bin and
                        // restart the Start Menu host so it rebuilds immediately.
                        // ProfileImagePath handles non-default profile locations.
                        const string clearCache =
                            "$sys = @('Public','Default','All Users','Default User'); " +
                            "Get-ChildItem 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList' | " +
                            "Where-Object { $_.PSChildName -like 'S-1-5-21-*' } | ForEach-Object { " +
                            "$pp = (Get-ItemProperty $_.PSPath -Name 'ProfileImagePath' -ErrorAction SilentlyContinue).ProfileImagePath; " +
                            "if ($pp -and ((Split-Path $pp -Leaf) -notin $sys)) { " +
                            "Remove-Item ($pp + '\\AppData\\Local\\Packages\\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\\LocalState\\start*.bin') -Force -ErrorAction SilentlyContinue } }; " +
                            "Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue";
                        TweakHelpers.RunCommand("powershell.exe",
                            $"-NoProfile -ExecutionPolicy Bypass -Command \"{clearCache}\"");
                    }
                    else
                    {
                        using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PolicyManager\current\device\Start", true))
                            k?.DeleteValue("ConfigureStartPins", false);
                        using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer", true))
                            k?.DeleteValue("ConfigureStartPins", false);
                    }
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[START] Clean Start Menu {(enable ? "applied" : "reverted")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-start-hide-recommended-policy",
                Name        = "Hide Recommended Section (Policy)",
                Description = "Completely removes the Recommended section from Start using the Education-SKU policy trick",
                Group       = "Layout",
                // Winhance start-recommended-section carries no Recommended/Default value.
                // Windows ships the Recommended section visible → OFF is the factory state.
                DefaultState = false,
                ReadState   = ReadRecommendedPolicyHidden,
                Apply       = enable =>
                {
                    var v = enable ? 1 : 0;
                    Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                        "HideRecommendedSection", v, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                        "HideRecommendedSection", v, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                        "HideRecommendedSection", v, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education",
                        "IsEducationEnvironment", v, RegistryValueKind.DWord);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[START] Recommended section {(enable ? "hidden" : "restored")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-start-more-pins",
                Name        = "More Pins (Less Recommendations)",
                Description = "Sets Start Menu layout to show more pinned apps and fewer recommendations",
                Group       = "Layout",
                // Winhance start-menu-layout (Start_Layout) is a preference dropdown with no
                // recommended value; Windows ships the balanced default layout → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadStartMorePins,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_Layout", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[START] More pins layout {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-start-disable-recommended",
                Name        = "Disable Recommended Section",
                Description = "Hides the Recommended apps/files section from the Start Menu",
                Group       = "Layout",
                // Winhance Start_IrisRecommendations: Recommended=0 → OFF (hidden),
                // Default=1 → ON (shown). This row inverts the polarity (Disable).
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadStartRecommendationsHidden,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_IrisRecommendations", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[START] Recommended section {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-start-disable-recently-added-apps",
                Name        = "Disable Recently Added Apps",
                Description = "Hides recently installed apps from the Start Menu",
                Group       = "Layout",
                // Winhance start-show-recently-added-apps (ShowRecentList): Recommended=0 → hidden,
                // Default=1 → shown. Inverted here. NOTE: Winhance drives the per-user
                // ShowRecentList value; this row drives the HideRecentlyAddedApps policy.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadRecentlyAddedAppsHidden,
                Apply       = enable =>
                {
                    const string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer";
                    Registry.SetValue(path, "HideRecentlyAddedApps", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[START] Recently added apps {(enable ? "hidden" : "shown")}.");
                },
            });

            var behaviorSection = TweakHelpers.BuildSection(panel, "Behavior");

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-start-disable-bing-search",
                Name        = "Disable Bing Search in Start",
                Description = "Removes web (Bing) search results from the Start Menu search box",
                Group       = "Behavior",
                // Winhance BingSearchEnabled: Recommended=0 → OFF, Default=1 → ON; and
                // DisableSearchBoxSuggestions: Recommended=1 → suggestions off. Both agree
                // that "no web results" is recommended. This row inverts the polarity.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadBingSearchDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows",
                        "DisableSearchBoxSuggestions", enable ? 1 : 0, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                        "BingSearchEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[START] Bing search {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-start-disable-account-notifications",
                Name        = "Disable Account-Related Notifications",
                Description = "Removes 'Add an account' and Microsoft account prompts from Start",
                Group       = "Behavior",
                // Winhance Start_AccountNotifications: Recommended=0 → OFF (hidden),
                // Default=1 → ON (shown). This row inverts the polarity (Disable).
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadAccountNotificationsDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_AccountNotifications", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[START] Account notifications {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-start-disable-web-suggestions",
                Name        = "Disable Web Suggestions in Search",
                Description = "Prevents Windows Search from showing online/web suggestions",
                Group       = "Behavior",
                // Winhance BingSearchEnabled: Recommended=0 → OFF, Default=1 → ON. Inverted here.
                // NOTE: this row and customize-start-disable-bing-search both write
                // BingSearchEnabled, so they can fight each other — see the review notes.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadWebSuggestionsDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                        "BingSearchEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                        "CortanaConsent", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[START] Web search suggestions {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-start-show-recently-opened-items",
                Name        = "Show Recently Opened Items",
                Description = "Shows recently opened files in Start, Jump Lists and File Explorer Quick Access",
                Group       = "Behavior",
                // Winhance start-show-recommended-files (Start_TrackDocs): Recommended=0
                // → OFF; EnabledValue=[1,null] → absent = ON; Default=1 → ON.
                // Same polarity as this row (no inversion).
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                                                "Start_TrackDocs") is int v ? v != 0 : true,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_TrackDocs", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[START] Recently opened items {(enable ? "shown" : "hidden")}.");
                },
            });

            // Winhance's "Show most used apps" (Start_TrackProgs) is marked
            // IsWindows10Only → intentionally skipped (Akari is Windows 11 only).
        }

    }
}
