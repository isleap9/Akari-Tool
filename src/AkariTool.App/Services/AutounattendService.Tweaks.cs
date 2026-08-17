using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    /// <summary>
    /// Curated set of Akari Tool tweaks that are safe and meaningful during an
    /// unattended install. Only scripts that make sense on a fresh system are
    /// listed; scope (SYSTEM vs per-user) was verified against each script's
    /// registry hive usage.
    /// </summary>
    public static class UnattendTweakCatalog
    {
        public static readonly UnattendTweakOption[] All =
        {
            // ── System-wide (run as SYSTEM during specialize) ───────────────
            new("telemetry",             "Telemetry.ps1",            "Disable Telemetry",             "Sets AllowTelemetry=0 policy and disables the DiagTrack service.",                       false, true),
            new("consumer-features",     "ConsumerFeatures.ps1",     "Disable Consumer Features",     "Stops Windows from auto-installing suggested store apps and games.",                    false, true),
            new("activity-history",      "ActivityHistory.ps1",      "Disable Activity History",      "Turns off the activity feed and timeline data collection policies.",                    false, true),
            new("location-tracking",     "LocationTracking.ps1",     "Disable Location Tracking",     "Denies the system-wide location consent and sensor permissions.",                       false, true),
            new("delivery-optimization", "DeliveryOptimization.ps1", "Disable Delivery Optimization", "Stops Windows from uploading updates to other PCs using your bandwidth.",               false, true),
            new("windows-ai",            "WindowsAI.ps1",            "Disable Windows AI / Recall",   "Disables Copilot, Recall and related Windows AI policy surfaces.",                      false, true),
            new("widgets",               "Widgets.ps1",              "Remove Widgets",                "Removes the Widgets platform and Web Experience pack.",                                 false, true),
            new("ps7-telemetry",         "PS7Telemetry.ps1",         "Disable PowerShell Telemetry",  "Sets the machine-wide POWERSHELL_TELEMETRY_OPTOUT environment variable.",               false, true),
            new("clean-start-menu",      null,                       "Clean Start Menu",              "Applies an empty pinned-apps Start Menu layout for new users (Win10 & Win11).",         false, true),
            new("services",              "Services.ps1",             "Disable Akari Service Set",     "Disables telemetry/Xbox/Edge-update services. Note: includes UsoSvc and StorSvc.",      false, false),
            new("edge-debloat",          "EdgeDebloat.ps1",          "Debloat Microsoft Edge",        "Applies Edge policies that disable sidebar, shopping, Bing features, and more. Only relevant if Edge is kept.", false, false),
            new("hibernation-off",       "Hibernation.ps1",          "Disable Hibernation",           "Turns off hibernation and removes hiberfil.sys.",                                       false, false),
            new("utc-time",              "UTC.ps1",                  "Hardware Clock = UTC",          "For dual-boot with Linux: makes Windows use UTC for the hardware clock.",               false, false),
            new("wpbt",                  "WPBT.ps1",                 "Disable WPBT",                  "Blocks the Windows Platform Binary Table (vendor firmware executables).",               false, false),

            // ── Per-user (run as the logged-in user after OOBE) ─────────────
            new("right-click-menu",      "RightClickMenu.ps1",       "Classic Context Menu",          "Restores the full Windows 10 style right-click menu in Explorer.",                      true, false),
            new("remove-home-gallery",   "RemoveHomeAndGallery.ps1", "Remove Home & Gallery",         "Removes Home and Gallery from the Explorer sidebar; This PC becomes the default view.", true, false),
            new("bg-apps",               "DisableBGApps.ps1",        "Disable Background Apps",       "Stops Microsoft Store apps from running in the background globally.",                   true, false),
            new("folder-discovery",      "FolderDiscovery.ps1",      "Disable Folder Type Discovery", "Speeds up Explorer by disabling automatic folder type detection.",                      true, false),
            new("storage-sense",         "StorageSense.ps1",         "Configure Storage Sense",       "Applies Akari's Storage Sense cleanup configuration.",                                  true, false),
            new("end-task",              "EndTask.ps1",              "Taskbar End Task",              "Adds the End Task option to taskbar right-click menus.",                                true, false),
            new("visual-effects",        "VisualEffects.ps1",        "Performance Visual Effects",    "Sets visual effects for best performance (keeps font smoothing & thumbnails).",         true, false),
        };
    }
}
