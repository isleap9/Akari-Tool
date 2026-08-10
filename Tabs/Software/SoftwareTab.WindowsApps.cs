// SoftwareTab — 1:1 functional port of Winhance's Software section, card-grid UI.
//
//   • Windows Apps  (panel "Bloatware"): 56 removable apps + 10 legacy
//     capabilities + 7 optional features as selectable cards with live
//     installed status, Winhance-style badges (Installed / Warning /
//     Permanent), select-all controls, and Install / Remove / Refresh.
//     Removals run the generated BloatRemoval.ps1 pipeline (+ dedicated
//     Edge/OneDrive scripts) and persist via a SYSTEM startup task.
//   • External Apps (panel "AppInstaller"): 193 winget apps in category
//     sections (Browsers, Compression, …), same card UI.
//   • Debloat       (panel "Debloat"): hosts Tabs/Debloat/DebloatTab —
//     the script-based one-click debloat groups.
//
// Cards mirror Winhance's card view: checkbox + avatar + name/description
// + badge row, click anywhere to select, responsive column count.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class SoftwareTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // WINDOWS APPS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void BuildWindowsAppsPanel(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Windows Apps",
                "Remove pre-installed Windows apps, legacy capabilities, and optional features — or reinstall them. Removed apps stay removed across Windows updates."));

            var bar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            var left = new StackPanel { Orientation = Orientation.Horizontal };

            _waRemoveBtn = MakeActionButton("\uE74D  Remove Selected", primary: true);
            _waRemoveBtn.Click += async (_, _) => await RemoveSelectedWindowsAppsAsync();
            _waInstallBtn = MakeActionButton("\uE896  Install Selected", primary: false);
            _waInstallBtn.Click += async (_, _) => await InstallSelectedAsync(_windowsApps, isWindowsApps: true);
            _waRefreshBtn = MakeActionButton("\uE72C  Refresh", primary: false);
            _waRefreshBtn.Click += async (_, _) => await RefreshStatusAsync();

            left.Children.Add(_waRemoveBtn);
            left.Children.Add(_waInstallBtn);
            left.Children.Add(_waRefreshBtn);
            bar.Children.Add(left);

            _waSelectedCount = new TextBlock { Text = "0 selected", Foreground = BrushFrom("#cc5060"), FontSize = 12 };
            bar.Children.Add(CountBadge(_waSelectedCount));
            panel.Children.Add(bar);

            _waStatus = MakeStatusText();
            panel.Children.Add(_waStatus);

            AddSearchRow(panel, "Search Windows apps…", q => ApplySearch(q, _windowsSections));

            panel.Children.Add(SelectAllRow(
                all => SetSelection(_windowsCards, _ => true, all),
                inst => SetSelection(_windowsCards, a => a.IsInstalled, inst),
                notInst => SetSelection(_windowsCards, a => !a.IsInstalled, notInst)));

            // Winhance card-view sections: flat alphabetical grids
            BuildCardSection(panel, "Windows Apps",
                _windowsApps.Where(a => a.CapabilityName == null && a.OptionalFeatureName == null)
                            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                _windowsCards, _windowsSections, isWindowsApps: true);

            BuildCardSection(panel, "Legacy Capabilities",
                _windowsApps.Where(a => a.CapabilityName != null)
                            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                _windowsCards, _windowsSections, isWindowsApps: true);

            BuildCardSection(panel, "Optional Features",
                _windowsApps.Where(a => a.OptionalFeatureName != null)
                            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                _windowsCards, _windowsSections, isWindowsApps: true);
        }

        private async Task RemoveSelectedWindowsAppsAsync()
        {
            var selected = _windowsApps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0 || _busy) return;

            var warnings = selected.Where(a => a.HasInstabilityWarning).Select(a => a.Name).ToList();
            var msg = $"Remove {selected.Count} item(s) from Windows?\n\n" +
                      string.Join(", ", selected.Take(10).Select(a => a.Name)) +
                      (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "") +
                      (warnings.Count > 0 ? $"\n\n⚠ {string.Join(", ", warnings)}: removal can affect Windows components that depend on it." : "") +
                      "\n\nA startup task keeps these removed after Windows updates.";
            // MIGRATION: AkariDialogs is async-only under WinUI (ContentDialog).
            // The guard is otherwise identical — same message, same Yes/No, and it
            // still returns BEFORE any removal work when the user declines.
            if (!await AkariDialogs.ConfirmYesNoAsync(msg, "Remove Windows Apps"))
                return;

            _busy = true;
            SetButtonsEnabled(false);
            try
            {
                await SoftwareAppService.RemoveWindowsAppsAsync(selected,
                    log: m => Service!.Log(m),
                    status: s => DispatcherQueue.TryEnqueue(() => { _waStatus.Text = s; _waStatus.Visibility = Visibility.Visible; }));
                foreach (var app in selected) SetSelected(app, false);
            }
            finally
            {
                _busy = false;
                SetButtonsEnabled(true);
            }
            await RefreshStatusAsync();
        }

    }
}
