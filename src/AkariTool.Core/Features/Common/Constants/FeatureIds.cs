namespace AkariTool.Core.Features.Common.Constants;

/// <summary>
/// Winhance FeatureIds 1:1: stable string ids identifying each feature area.
/// Used as keys by CompatibleSettingsRegistry / GlobalSettingsRegistry and for
/// feature→setting ownership lookups. Values match Akari's existing
/// SettingGroup.FeatureId strings where a group id exists.
/// </summary>
public static class FeatureIds
{
    // Customize features
    public const string Taskbar = "customize-taskbar";
    public const string StartMenu = "customize-startmenu";
    public const string ExplorerCustomization = "customize-explorer";
    public const string Desktop = "customize-desktop";
    public const string Appearance = "customize-appearance";

    // Optimize features
    public const string GamingPerformance = "gaming";
    public const string Power = "power";
    public const string Notifications = "notifications";
    public const string Privacy = "privacy";
    public const string Sound = "sound";
    public const string Update = "update";

    // Software (catalogs are app-model based; registered for completeness)
    public const string WindowsApps = "WindowsApps";
    public const string ExternalApps = "ExternalApps";
}
