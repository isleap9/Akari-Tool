using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Services;
using WinUI.Framework.Mvvm;
using WinUI.Framework.Services;

namespace AkariTool.ViewModels;

/// <summary>The kind of a parsed changelog line, driving its render template.</summary>
public enum ChangelogLineKind { Paragraph, Header, Bullet }

/// <summary>One parsed line of a changelog body (a header, a bullet, or a paragraph).</summary>
public sealed class ChangelogLine
{
    public string Text { get; init; } = "";
    public ChangelogLineKind Kind { get; init; }
}

/// <summary>One changelog entry (live from GitHub, or the static fallback).</summary>
public sealed class ReleaseNote
{
    public string Version { get; init; } = "";
    public IReadOnlyList<ChangelogLine> Lines { get; init; } = Array.Empty<ChangelogLine>();
    public bool IsCurrent { get; init; }
    public Visibility CurrentPillVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// Settings page view model. Hosts three sections:
///   • Appearance — theme, via the framework's <see cref="IThemeService"/> (unchanged).
///   • Updates    — the self-updater (Check / Update Now / live changelog) over the
///                  already-ported static <see cref="UpdateService"/>. Ported from net8's
///                  AppUpdateTab as MVVM state + commands (the imperative chip/spinner
///                  code-behind is replaced by an InfoBar bound to StatusSeverity).
///   • About      — static; built in the page code-behind (relocated verbatim from AboutPage).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themes;
    private UpdateCheckResult? _lastResult;

    public SettingsViewModel(IThemeService themes)
    {
        _themes = themes;
        Title = "Settings";
        SelectedTheme = themes.CurrentTheme;

        Detail("click Check for Updates to query GitHub");
        SeedFallbackChangelog();
        _ = LoadChangelogAsync();   // fire-and-forget; keeps the fallback on failure
    }

    // ── Appearance ────────────────────────────────────────────────────────────
    public IReadOnlyList<AppTheme> Themes { get; } = new[] { AppTheme.Default, AppTheme.Light, AppTheme.Dark };

    [ObservableProperty] public partial AppTheme SelectedTheme { get; set; }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (value != _themes.CurrentTheme) _themes.ApplyTheme(value);
    }

    // ── Updates ───────────────────────────────────────────────────────────────
    [ObservableProperty] public partial string StatusTitle { get; set; } = "You're up to date";
    [ObservableProperty] public partial string StatusDetail { get; set; } = "";
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Success;
    [ObservableProperty] public partial string UpdateActionText { get; set; } = "Update Now";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateButtonVisibility))]
    public partial bool IsUpdateAvailable { get; set; }

    /// <summary>Not-busy — Check/Update buttons enable off this.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Update Now is only shown once a newer release is found.</summary>
    public Visibility UpdateButtonVisibility => IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<ReleaseNote> Changelog { get; } = new();

    private void Detail(string tail) =>
        StatusDetail = $"Akari Tool {UpdateService.CurrentVersionDisplay} · {tail}";

    // ── Check ─────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        IsUpdateAvailable = false;
        _lastResult = null;

        StatusSeverity = InfoBarSeverity.Informational;
        StatusTitle = "Checking for updates…";
        Detail("contacting github.com");

        var result = await UpdateService.CheckAsync();
        _lastResult = result;
        IsBusy = false;

        switch (result.Status)
        {
            case UpdateStatus.UpdateAvailable:
                StatusSeverity = InfoBarSeverity.Warning;
                StatusTitle = $"Update available — {result.LatestTag}";
                UpdateActionText = result.InstallerUrl != null ? "Update Now" : "View Release";
                Detail(result.InstallerUrl != null
                    ? "one click to download and install"
                    : "no installer attached — opens the release page");
                IsUpdateAvailable = true;
                break;

            case UpdateStatus.UpToDate:
                StatusSeverity = InfoBarSeverity.Success;
                StatusTitle = "You're on the latest version";
                Detail($"latest release is {result.LatestTag} · checked just now");
                break;

            case UpdateStatus.NoReleases:
                StatusSeverity = InfoBarSeverity.Success;
                StatusTitle = "You're on the latest version";
                Detail("no releases published on GitHub yet");
                break;

            default: // Error
                StatusSeverity = InfoBarSeverity.Error;
                StatusTitle = "Couldn't check for updates";
                Detail(result.ErrorMessage ?? "network error — try again later");
                break;
        }
    }

    // ── Seamless update: download → silent install → relaunch ─────────────────
    // Matches net8 AppUpdateTab.UpdateNow verbatim in behavior: no extra confirm
    // dialog beyond the button click. Downloads the installer and launches it
    // /VERYSILENT /RELAUNCH=1, then exits for the in-place upgrade.
    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (IsBusy || _lastResult is null) return;

        if (_lastResult.InstallerUrl is null)
        {
            OpenUrl(_lastResult.ReleasePageUrl ?? UpdateService.ReleasesPageUrl);
            return;
        }

        IsBusy = true;                       // Update Now stays visible but disabled (IsIdle)
        StatusSeverity = InfoBarSeverity.Informational;
        StatusTitle = $"Downloading {_lastResult.LatestTag}…";
        Detail("starting download");

        try
        {
            var progress = new Progress<double>(p => Detail($"downloading installer — {p:P0}"));
            string setupPath = await UpdateService.DownloadInstallerAsync(_lastResult.InstallerUrl, progress);

            StatusTitle = "Installing update…";
            Detail("Akari Tool will restart automatically");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setupPath)
            {
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1",
                UseShellExecute = true
            });

            await Task.Delay(800);           // let the installer spin up
            Application.Current.Exit();       // WinUI equivalent of WPF Shutdown()
        }
        catch (Exception ex)
        {
            StatusSeverity = InfoBarSeverity.Error;
            StatusTitle = "Update failed";
            Detail(ex.Message);
            IsBusy = false;
        }
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* browser launch failed — nothing sensible to do */ }
    }

    // ── Changelog (live from GitHub, static fallback) ──────────────────────────
    private void SeedFallbackChangelog()
    {
        // Offline fallback only — real changelog comes from GitHub releases.
        var current = UpdateService.CurrentVersionDisplay;
        (string V, string D)[] fallback =
        {
            ("v2.0", "New collapsible sidebar with grouped sections, About & Update pages, and the Advanced Tools custom-ISO builder."),
            ("v1.5", "Added the Advanced Tools tab: WIM utility and autounattend.xml generator."),
            ("v1.0", "Initial release — debloat, gaming, privacy, power and customization tweaks."),
        };
        foreach (var (v, d) in fallback)
            Changelog.Add(new ReleaseNote { Version = v, Lines = ParseChangelog(d), IsCurrent = v == current });
    }

    private async Task LoadChangelogAsync()
    {
        var releases = await UpdateService.GetReleasesAsync();
        if (releases is null) return;   // offline / rate-limited → keep fallback

        Changelog.Clear();
        foreach (var r in releases)
        {
            string body = string.IsNullOrWhiteSpace(r.Body)
                ? (string.IsNullOrWhiteSpace(r.Name) ? "No release notes." : r.Name)
                : r.Body;
            Changelog.Add(new ReleaseNote { Version = ShortTag(r.Tag), Lines = ParseChangelog(body), IsCurrent = r.IsCurrent });
        }
    }

    private static string ShortTag(string tag) =>
        tag.EndsWith(".0") && tag.Count(c => c == '.') == 2 ? tag[..^2] : tag;

    /// <summary>
    /// Parses a (very light) markdown changelog body into classified lines for display.
    /// Recognises GitHub's two header styles — "## X" and a standalone "**X**" line — as
    /// Header, "- "/"* " lines as Bullet, everything else as Paragraph. Inline emphasis
    /// (** and `) is stripped to plain text. Plain fallback sentences (no markdown) fall
    /// through as a single Paragraph line, unchanged in look.
    /// </summary>
    public static List<ChangelogLine> ParseChangelog(string md)
    {
        var result = new List<ChangelogLine>();
        if (string.IsNullOrWhiteSpace(md)) return result;

        foreach (var raw in md.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            ChangelogLineKind kind;
            if (line.StartsWith("#"))
            {
                line = line.TrimStart('#', ' ');
                kind = ChangelogLineKind.Header;
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                line = line[2..].Trim();
                kind = ChangelogLineKind.Bullet;
            }
            else if (line.Length > 4 && line.StartsWith("**") && line.EndsWith("**"))
            {
                line = line.Trim('*', ' ');   // standalone bold line → pseudo-header
                kind = ChangelogLineKind.Header;
            }
            else
            {
                kind = ChangelogLineKind.Paragraph;
            }

            line = line.Replace("**", "").Replace("`", "");   // strip inline emphasis
            if (line.Length == 0) continue;

            result.Add(new ChangelogLine { Text = line, Kind = kind });
        }
        return result;
    }
}
