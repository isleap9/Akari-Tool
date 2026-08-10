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
        // EXTERNAL APPS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void BuildExternalAppsPanel(StackPanel panel)
        {
            panel.Children.Add(PageHeader("External Apps",
                "Install and manage third-party applications via WinGet — browsers, media, gaming, development tools, runtimes, and more."));

            var bar = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            var left = new StackPanel { Orientation = Orientation.Horizontal };

            _eaInstallBtn = MakeActionButton("\uE896  Install Selected", primary: true);
            _eaInstallBtn.Click += async (_, _) => await InstallSelectedAsync(_externalApps, isWindowsApps: false);
            _eaUninstallBtn = MakeActionButton("\uE74D  Uninstall Selected", primary: false);
            _eaUninstallBtn.Click += async (_, _) => await UninstallSelectedExternalAsync();
            _eaRefreshBtn = MakeActionButton("\uE72C  Refresh", primary: false);
            _eaRefreshBtn.Click += async (_, _) => await RefreshStatusAsync();

            left.Children.Add(_eaInstallBtn);
            left.Children.Add(_eaUninstallBtn);
            left.Children.Add(_eaRefreshBtn);
            bar.Children.Add(left);

            _eaSelectedCount = new TextBlock { Text = "0 selected", Foreground = BrushFrom("#cc5060"), FontSize = 12 };
            bar.Children.Add(CountBadge(_eaSelectedCount));
            panel.Children.Add(bar);

            _eaStatus = MakeStatusText();
            panel.Children.Add(_eaStatus);

            AddSearchRow(panel, "Search external apps…", q => ApplySearch(q, _externalSections));

            panel.Children.Add(SelectAllRow(
                all => SetSelection(_externalCards, _ => true, all),
                inst => SetSelection(_externalCards, a => a.IsInstalled, inst),
                notInst => SetSelection(_externalCards, a => !a.IsInstalled, notInst)));

            // Category sections (Winhance order), alphabetical within
            foreach (var group in _externalApps.GroupBy(a => a.GroupName ?? "Other"))
                BuildCardSection(panel, group.Key,
                    group.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    _externalCards, _externalSections, isWindowsApps: false);
        }

        private async Task UninstallSelectedExternalAsync()
        {
            var selected = _externalApps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0 || _busy) return;

            var msg = $"Uninstall {selected.Count} app(s)?\n\n" +
                      string.Join(", ", selected.Take(10).Select(a => a.Name)) +
                      (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "");
            // MIGRATION: async ContentDialog (see AkariDialogs). Same message, same
            // Yes/No, still returns before any uninstall work when declined.
            if (!await AkariDialogs.ConfirmYesNoAsync(msg, "Uninstall External Apps"))
                return;

            _busy = true;
            SetButtonsEnabled(false);
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var app = selected[i];
                    _eaStatus.Text = $"Uninstalling {app.Name}… ({i + 1}/{selected.Count})";
                    _eaStatus.Visibility = Visibility.Visible;
                    await SoftwareAppService.UninstallExternalAppAsync(app, m => Service!.Log(m));
                    SetSelected(app, false);
                }
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
