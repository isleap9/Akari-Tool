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
        // SHARED ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        private async Task InstallSelectedAsync(List<AppDefinition> catalog, bool isWindowsApps)
        {
            var selected = catalog.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0 || _busy) return;

            var notReinstallable = selected.Where(a => !a.CanBeReinstalled).ToList();
            if (notReinstallable.Count > 0)
            {
                // MIGRATION: async ContentDialog (see AkariDialogs). Same copy; the
                // await preserves the original ordering — the notice is dismissed
                // before the filtered install proceeds.
                await AkariDialogs.InfoAsync(
                    "These items are permanent — once removed they can't be reinstalled — and will be skipped:\n\n" +
                    string.Join(", ", notReinstallable.Select(a => a.Name)),
                    "Permanent Items");
                selected = selected.Where(a => a.CanBeReinstalled).ToList();
                if (selected.Count == 0) return;
            }

            var status = isWindowsApps ? _waStatus : _eaStatus;
            _busy = true;
            SetButtonsEnabled(false);
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var app = selected[i];
                    status.Text = $"Installing {app.Name}… ({i + 1}/{selected.Count})";
                    status.Visibility = Visibility.Visible;
                    var ok = await SoftwareAppService.InstallAppAsync(app, m => Service!.Log(m));
                    if (ok) SetSelected(app, false);
                }

                // Reinstalled Windows apps must leave the keep-removed script
                if (isWindowsApps)
                    await SoftwareAppService.RemoveFromSavedScriptAsync(selected, m => Service!.Log(m));
            }
            finally
            {
                _busy = false;
                SetButtonsEnabled(true);
            }
            await RefreshStatusAsync();
        }

        private async Task RefreshStatusAsync()
        {
            if (_busy) return;
            _busy = true;
            SetButtonsEnabled(false);
            _waStatus.Text = "Checking installed status…";
            _waStatus.Visibility = Visibility.Visible;
            _eaStatus.Text = "Checking installed status…";
            _eaStatus.Visibility = Visibility.Visible;

            try
            {
                var snapshot = await SoftwareAppService.GetInstallSnapshotAsync();
                SoftwareAppService.ApplyInstallStatus(_windowsApps, snapshot);
                SoftwareAppService.ApplyInstallStatus(_externalApps, snapshot);
                RefreshBadges(_windowsCards);
                RefreshBadges(_externalCards);
            }
            catch (Exception ex)
            {
                Service?.Log($"[ERROR] Status refresh failed: {ex.Message}");
            }
            finally
            {
                _busy = false;
                SetButtonsEnabled(true);
                _waStatus.Visibility = Visibility.Collapsed;
                _eaStatus.Visibility = Visibility.Collapsed;
                RefreshCounts();
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            foreach (var b in new[] { _waInstallBtn, _waRemoveBtn, _waRefreshBtn, _eaInstallBtn, _eaUninstallBtn, _eaRefreshBtn })
                if (b != null) b.IsEnabled = enabled;
        }

    }
}
