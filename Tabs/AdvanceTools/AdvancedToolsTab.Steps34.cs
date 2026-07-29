using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AdvancedTools
{
    public partial class AdvancedToolsTab
    {
        // ═════════════════════════════════════════════════════════════════
        //  Step 3 — Add Drivers
        // ═════════════════════════════════════════════════════════════════

        private void BuildStep3()
        {
            var body = MakeStepCard(3, "Add Drivers", "\uE964", "Complete Step 1 first");

            body.Children.Add(MakeHint(
                "Bundle drivers into the media. Storage drivers are loaded during setup ($WinpeDriver$); " +
                "everything else is installed automatically after setup via SetupComplete.cmd."));

            var systemBtn = MakePrimaryButton("Extract & Add System Drivers");
            systemBtn.Click += async (_, _) => await RunBusyAsync("Driver export", async ct =>
            {
                if (await _wim.AddDriversAsync(_workDir, null,
                        s => SetStatus(3, s),
                        p => SetStatus(3, $"Exporting drivers… {p:F0}%"), ct))
                { _driversAdded = true; SetStatus(3, "System drivers added", done: true); }
            });

            var customBtn = MakeButton("Select Custom Driver Folder");
            customBtn.Click += async (_, _) =>
            {
                var dlg = new OpenFolderDialog { Title = "Select a folder containing drivers (.inf)" };
                if (dlg.ShowDialog() != true) return;
                await RunBusyAsync("Driver copy", async ct =>
                {
                    if (await _wim.AddDriversAsync(_workDir, dlg.FolderName, s => SetStatus(3, s), null, ct))
                    { _driversAdded = true; SetStatus(3, "Custom drivers added", done: true); }
                });
            };

            body.Children.Add(HRow(systemBtn, customBtn));
        }

        // ═════════════════════════════════════════════════════════════════
        //  Step 4 — Create ISO
        // ═════════════════════════════════════════════════════════════════

        private void BuildStep4()
        {
            var body = MakeStepCard(4, "Create ISO", "\uE7B8", "Complete Step 1 first");

            _oscdimgStatus = MakeHint("Checking for oscdimg.exe…");
            body.Children.Add(_oscdimgStatus);

            _oscdimgBtn = MakeButton("Install oscdimg");
            _oscdimgBtn.Click += async (_, _) => await RunBusyAsync("oscdimg install", async ct =>
            {
                await _wim.EnsureOscdimgAvailableAsync(s => SetStatus(4, s), null, ct);
                await RefreshOscdimgStateAsync();
            });

            var selectOutBtn = MakeButton("Select Output Location");
            _outputPathText = MakePathText("—");
            selectOutBtn.Click += (_, _) =>
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "ISO files (*.iso)|*.iso",
                    FileName = "AkariWindows.iso",
                    Title = "Save bootable ISO as",
                };
                if (dlg.ShowDialog() != true) return;
                _outputIsoPath = dlg.FileName;
                _outputPathText.Text = _outputIsoPath;
                SetStatus(4, $"Output: {Path.GetFileName(_outputIsoPath)}");
                UpdateStepStates();
            };

            _createIsoBtn = MakePrimaryButton("Create ISO");
            _createIsoBtn.Click += async (_, _) =>
            {
                if (string.IsNullOrEmpty(_outputIsoPath)) { SetStatus(4, "Select an output location first"); return; }
                await RunBusyAsync("ISO creation", async ct =>
                {
                    var ok = await _wim.CreateIsoAsync(_workDir, _outputIsoPath,
                        s => SetStatus(4, s),
                        p => SetStatus(4, $"Building ISO… {p:F0}%"), ct);
                    if (ok) SetStatus(4, $"ISO created: {Path.GetFileName(_outputIsoPath)}", done: true);
                });
            };

            body.Children.Add(HRow(_oscdimgBtn, selectOutBtn, _outputPathText));
            body.Children.Add(HRow(_createIsoBtn));
        }

        private async Task RefreshOscdimgStateAsync()
        {
            var path = await Task.Run(() => _wim.GetOscdimgPath());
            if (string.IsNullOrEmpty(path))
            {
                _oscdimgStatus.Text = "oscdimg.exe (Microsoft's ISO build tool) is not installed. " +
                                      "Install it below — winget's Microsoft.OSCDIMG package is tried first, then the Windows ADK.";
                _oscdimgBtn.Visibility = Visibility.Visible;
            }
            else
            {
                _oscdimgStatus.Text = $"oscdimg.exe found: {path}";
                _oscdimgBtn.Visibility = Visibility.Collapsed;
            }
        }

    }
}
