using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.StartMenu.cs.
    // Sections "Layout" (8 rows) and "Behavior" (4 rows). The two static readers
    // (ReadStartPinsCleaned / ReadRecommendedPolicyHidden) moved along unchanged.
    public static partial class CustomizeTweaks
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

        // ── Layout ───────────────────────────────────────────────────────────────
        public static TweakDefinition[] StartMenuLayout(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-start-clean-pins",
                Name        = "Clean Start Menu (Remove All Pins)",
                Description = "Removes all default pinned apps from the Start Menu — clears Edge, Settings and File Explorer pins on 26200.8521+ builds",
                Group       = "Layout",
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
                    Log($"[START] Clean Start Menu {(enable ? "applied" : "reverted")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-hide-recommended-policy",
                Name        = "Hide Recommended Section (Policy)",
                Description = "Completely removes the Recommended section from Start using the Education-SKU policy trick",
                Group       = "Layout",
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
                    Log($"[START] Recommended section {(enable ? "hidden" : "restored")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-more-pins",
                Name        = "More Pins (Less Recommendations)",
                Description = "Sets Start Menu layout to show more pinned apps and fewer recommendations",
                Group       = "Layout",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadStartMorePins,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_Layout", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[START] More pins layout {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-disable-recommended",
                Name        = "Disable Recommended Section",
                Description = "Hides the Recommended apps/files section from the Start Menu",
                Group       = "Layout",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadStartRecommendationsHidden,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_IrisRecommendations", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[START] Recommended section {(enable ? "hidden" : "shown")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-show-recent-apps",
                Name        = "Show Recently Added Apps",
                Group       = "Layout",
                Description = "Show a recently added apps list at the top of the Start menu",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Start", "ShowRecentList") is int v ? v != 0 : true,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                        "ShowRecentList", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Recently added apps {(on ? "shown" : "hidden")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-show-most-used",
                Name        = "Show Most Used Apps",
                Group       = "Layout",
                Description = "Show a most-used apps list in the Start menu",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Start", "ShowFrequentList") is int v ? v != 0 : true,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                        "ShowFrequentList", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Most used apps {(on ? "shown" : "hidden")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-show-suggestions",
                Name        = "Show Suggestions in Start",
                Group       = "Layout",
                Description = "Show occasional app and tip suggestions in the Start menu",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled") is int v ? v != 0 : true,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                        "SubscribedContent-338388Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Start suggestions {(on ? "enabled" : "disabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-all-apps-view",
                Name        = "All Apps View",
                Group       = "Layout",
                Description = "How the All apps list is displayed in the Start menu",
                IsPreference = true,
                InputKind   = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Category", 0),
                    new TweakDropdownOption("Grid",     1),
                    new TweakDropdownOption("List",     2, IsDefault: true),
                },
                ReadCurrentIndex = () =>
                {
                    var v = ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Start", "AllAppsViewMode");
                    return v switch { 0 => 0, 1 => 1, 2 => 2, _ => 2 };
                },
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx > 2) return;
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                        "AllAppsViewMode", idx, RegistryValueKind.DWord);
                    Log($"All apps view set to option {idx}.");
                },
            },
        };

        // ── Behavior ─────────────────────────────────────────────────────────────
        public static TweakDefinition[] StartMenuBehavior(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-start-disable-bing-search",
                Name        = "Disable Bing Search in Start",
                Description = "Removes web (Bing) search results from the Start Menu search box",
                Group       = "Behavior",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadBingSearchDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows",
                        "DisableSearchBoxSuggestions", enable ? 1 : 0, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                        "BingSearchEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[START] Bing search {(enable ? "disabled" : "enabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-disable-account-notifications",
                Name        = "Disable Account-Related Notifications",
                Description = "Removes 'Add an account' and Microsoft account prompts from Start",
                Group       = "Behavior",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadAccountNotificationsDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_AccountNotifications", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[START] Account notifications {(enable ? "disabled" : "enabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-disable-web-suggestions",
                Name        = "Disable Web Suggestions in Search",
                Description = "Prevents Windows Search from showing online/web suggestions",
                Group       = "Behavior",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadWebSuggestionsDisabled,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                        "CortanaConsent", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[START] Web search suggestions {(enable ? "disabled" : "enabled")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-start-show-recently-opened-items",
                Name        = "Show Recently Opened Items",
                Description = "Shows recently opened files in Start, Jump Lists and File Explorer Quick Access",
                Group       = "Behavior",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                                                "Start_TrackDocs") is int v ? v != 0 : true,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "Start_TrackDocs", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[START] Recently opened items {(enable ? "shown" : "hidden")}.");
                },
            },
        };
    }
}
