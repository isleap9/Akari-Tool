// Ported 1:1 from Winhance (ItemDefinition / ItemGroup / ExternalAppMetadata),
// adapted to AkariTool's code-behind architecture. AppDefinition doubles as the
// row view state (IsInstalled / IsSelected are mutable runtime fields).

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AkariTool.Tabs;

/// <summary>Which detection method discovered that an app is installed.</summary>
public enum AppDetectionSource
{
    None,
    AppX,
    Registry,
    Capability,
    OptionalFeature,
    FileSystem
}

/// <summary>
/// Definition of a removable Windows app / capability / optional feature or an
/// installable external app. Immutable identity + mutable runtime state.
/// </summary>
public class AppDefinition : INotifyPropertyChanged
{
    // ── Immutable definition (matches Winhance ItemDefinition property names) ──
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public string[]? AppxPackageName { get; init; }
    public string[]? WinGetPackageId { get; init; }
    public string? MsStoreId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ChocoPackageId { get; init; }
    /// <summary>Replaces the winget manifest's InstallerSwitches via `winget install --override`.</summary>
    public string? WinGetInstallerOverride { get; init; }
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    /// <summary>Dedicated PowerShell removal script (Edge, OneDrive).</summary>
    public Func<string>? RemovalScript { get; init; }
    /// <summary>Registry DisplayName pattern. Supports {version}, {arch}, {locale} placeholders.</summary>
    public string? RegistryDisplayName { get; init; }
    /// <summary>Registry SubKeyName pattern. Supports {version}, {arch}, {locale} placeholders.</summary>
    public string? RegistrySubKeyName { get; init; }
    /// <summary>Paths checked for existence as a detection fallback. Supports env vars.</summary>
    public string[]? DetectionPaths { get; init; }
    public string[]? ProcessesToStop { get; init; }
    public string? WebsiteUrl { get; init; }
    /// <summary>Removal may destabilise Windows (e.g. Edge) — renders a Warning pill.</summary>
    public bool HasInstabilityWarning { get; init; }
    public ExternalAppMetadata? ExternalApp { get; init; }

    // ── Runtime state ──────────────────────────────────────────────────────
    private bool _isInstalled;
    private bool _isSelected;

    public bool IsInstalled
    {
        get => _isInstalled;
        set { if (_isInstalled != value) { _isInstalled = value; OnPropertyChanged(); } }
    }

    public AppDetectionSource DetectedVia { get; set; } = AppDetectionSource.None;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    /// <summary>Item type label shown in the UI (Winhance "Type" column).</summary>
    public string ItemTypeDescription =>
        CapabilityName != null ? "Capability"
        : OptionalFeatureName != null ? "Optional Feature"
        : "App";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>A named group of app definitions (Winhance ItemGroup).</summary>
public record AppGroup
{
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<AppDefinition> Items { get; init; }
}

/// <summary>Direct-download metadata for external apps not fully covered by winget.</summary>
public sealed record ExternalAppMetadata
{
    public string? DownloadUrl { get; init; }
    public string? FallbackDownloadUrl { get; init; }
    public string? DownloadUrlArm64 { get; init; }
    public string? DownloadUrlX64 { get; init; }
    public string? DownloadUrlX86 { get; init; }
    public bool IsGitHubRelease { get; init; }
    public string? AssetPattern { get; init; }
    public bool RequiresDirectDownload { get; init; }
}
