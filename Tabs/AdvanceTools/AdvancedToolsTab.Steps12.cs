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
        //  Step 1 — Select ISO
        // ═════════════════════════════════════════════════════════════════

        private void BuildStep1()
        {
            var body = MakeStepCard(1, "Select ISO", "\uE958", "No ISO selected");

            body.Children.Add(MakeHint(
                "Pick a Windows ISO and a working directory, then extract it. " +
                "If you already extracted an ISO before, tick the checkbox and select that folder instead."));

            // ISO row
            var selectIsoBtn = MakeButton("Select ISO");
            _isoPathText = MakePathText("—");
            selectIsoBtn.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "ISO files (*.iso)|*.iso", Title = "Select a Windows ISO" };
                if (dlg.ShowDialog() != true) return;
                if (!_wim.ValidateIsoFile(dlg.FileName)) { SetStatus(1, "Invalid ISO file"); return; }
                _isoPath = dlg.FileName;
                _isoPathText.Text = _isoPath;
                SetStatus(1, Path.GetFileName(_isoPath));
                UpdateStepStates();
            };
            body.Children.Add(HRow(selectIsoBtn, _isoPathText));

            // Working directory row
            var selectDirBtn = MakeButton("Select Folder");
            _workDirText = MakePathText(_workDir);
            selectDirBtn.Click += (_, _) =>
            {
                var dlg = new OpenFolderDialog { Title = "Select working directory" };
                if (dlg.ShowDialog() != true) return;

                if (_alreadyExtracted.IsChecked == true)
                {
                    if (!_wim.LooksLikeExtractedMedia(dlg.FolderName))
                    {
                        SetStatus(1, "Folder has no 'sources'/'boot' — not extracted media");
                        return;
                    }
                    _workDir = dlg.FolderName;
                    _extractionDone = true;
                    SetStatus(1, "Using already-extracted media", done: true);
                    _ = OnExtractionCompleteAsync();
                }
                else
                {
                    _workDir = Path.Combine(dlg.FolderName, "AkariWIM");
                }
                _workDirText.Text = _workDir;
                UpdateStepStates();
            };
            body.Children.Add(HRow(selectDirBtn, _workDirText));

            _alreadyExtracted = new CheckBox
            {
                Content = "I have already extracted an ISO — select that folder instead",
                Foreground = TweakHelpers.TextSecondary,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 12),
            };
            body.Children.Add(_alreadyExtracted);

            // Extract + download links
            _extractBtn = MakePrimaryButton("Start Extraction");
            _extractBtn.Click += async (_, _) => await RunExtractionAsync();

            var win10Btn = MakeButton("Windows 10 ISO");
            win10Btn.Click += (_, _) => Service!.OpenUrl("https://www.microsoft.com/software-download/windows10");
            var win11Btn = MakeButton("Windows 11 ISO");
            win11Btn.Click += (_, _) => Service!.OpenUrl("https://www.microsoft.com/software-download/windows11");

            var dlLabel = new TextBlock
            {
                Text = "Need an ISO?",
                FontSize = 12.5,
                Foreground = TweakHelpers.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 8, 0),
            };
            body.Children.Add(HRow(_extractBtn, dlLabel, win10Btn, win11Btn));

            // Optional conversion (populated after extraction/detection)
            _conversionPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            body.Children.Add(_conversionPanel);
        }

        private async Task RunExtractionAsync()
        {
            if (string.IsNullOrEmpty(_isoPath)) { SetStatus(1, "Select an ISO first"); return; }
            if (string.IsNullOrEmpty(_workDir)) { SetStatus(1, "Select a working directory first"); return; }

            await RunBusyAsync("ISO extraction", async ct =>
            {
                _extractionDone = false;
                var ok = await _wim.ExtractIsoAsync(_isoPath, _workDir,
                    s => SetStatus(1, s), null, ct);
                if (ok)
                {
                    _extractionDone = true;
                    SetStatus(1, "ISO extracted", done: true);
                    await OnExtractionCompleteAsync();
                }
            });
        }

        private async Task OnExtractionCompleteAsync()
        {
            UpdateStepStates();
            // Auto-expand the remaining steps (Winhance behaviour)
            for (int i = 1; i < 4; i++)
            {
                _steps[i].Body.Visibility = Visibility.Visible;
                _steps[i].Chevron.Text = "\uE70E";
            }
            await RefreshConversionCardAsync();
        }

        private async Task RefreshConversionCardAsync()
        {
            _conversionPanel.Children.Clear();
            if (!_extractionDone) return;

            var detection = await _wim.DetectImagesAsync(_workDir);
            if (detection.NeitherExists)
            {
                _conversionPanel.Children.Add(MakeHint("No install.wim or install.esd found in sources — the media may be incomplete."));
                return;
            }

            _conversionPanel.Children.Add(new Separator
            {
                Background = TweakHelpers.Hairline,
                Margin = new Thickness(0, 4, 0, 10),
                Height = 1,
            });

            if (detection.BothExist)
            {
                _conversionPanel.Children.Add(MakeHint(
                    $"Both image formats exist — install.wim ({detection.WimInfo!.SizeText}) and install.esd ({detection.EsdInfo!.SizeText}). " +
                    "Windows Setup only needs one; delete the one you don't want."));

                var delWim = MakeButton("Delete install.wim", "#FF7A88");
                delWim.Click += async (_, _) => await RunBusyAsync("Delete install.wim", async ct =>
                { await _wim.DeleteImageFileAsync(_workDir, WimImageFormat.Wim, s => SetStatus(1, s), ct); await RefreshConversionCardAsync(); });

                var delEsd = MakeButton("Delete install.esd", "#FF7A88");
                delEsd.Click += async (_, _) => await RunBusyAsync("Delete install.esd", async ct =>
                { await _wim.DeleteImageFileAsync(_workDir, WimImageFormat.Esd, s => SetStatus(1, s), ct); await RefreshConversionCardAsync(); });

                _conversionPanel.Children.Add(HRow(delWim, delEsd));
                return;
            }

            var info = detection.Single!;
            _conversionPanel.Children.Add(MakeHint(
                $"Image format: install.{info.Format.ToString().ToLowerInvariant()} · {info.SizeText} · " +
                $"{info.ImageCount} edition(s). Optional: convert the format " +
                "(WIM = editable/larger, ESD = compressed/smaller)."));

            var target = info.Format == WimImageFormat.Wim ? WimImageFormat.Esd : WimImageFormat.Wim;
            var convertBtn = MakeButton($"Convert to {target.ToString().ToUpperInvariant()}");
            convertBtn.Click += async (_, _) => await RunBusyAsync("Image conversion", async ct =>
            {
                var ok = await _wim.ConvertImageAsync(_workDir, target,
                    s => SetStatus(1, s),
                    p => SetStatus(1, $"Converting… {p:F0}%"), ct);
                if (ok) await RefreshConversionCardAsync();
            });
            _conversionPanel.Children.Add(HRow(convertBtn));
        }

        // ═════════════════════════════════════════════════════════════════
        //  Step 2 — Add XML File
        // ═════════════════════════════════════════════════════════════════

        private void BuildStep2()
        {
            var body = MakeStepCard(2, "Add XML File", "\uE8A5", "Complete Step 1 first");

            body.Children.Add(MakeHint(
                "Add an autounattend.xml to the media root so Windows installs unattended. " +
                "Generate one from your current Akari Tool selections, pick your own, or build one online."));

            var akariXmlBtn = MakePrimaryButton("Generate Akari XML");
            akariXmlBtn.ToolTip = "Builds an autounattend.xml from your current Akari Tool selections and places it in the extracted media.";
            akariXmlBtn.Click += async (_, _) => await RunBusyAsync("Akari XML generation", async _ =>
            {
                var outPath = Path.Combine(_workDir, "autounattend.xml");
                var apps = GetSelectedApps();
                var tweaks = GetSelectedTweaks();
                await Task.Run(() => _xmlGen.GenerateToFile(outPath, apps, tweaks));
                _xmlAdded = true;
                SetStatus(2, "Akari autounattend.xml added", done: true);
            });

            var hostedBtn = MakeButton("Add Hosted Akari XML");
            hostedBtn.ToolTip = "Downloads the ready-made Akari autounattend.xml from GitHub (no app selections needed).";
            hostedBtn.Click += async (_, _) => await RunBusyAsync("XML download", async ct =>
            {
                if (await _wim.DownloadAkariAutounattendXmlAsync(_workDir, s => SetStatus(2, s), ct))
                { _xmlAdded = true; SetStatus(2, "Akari autounattend.xml added", done: true); }
            });

            var selectBtn = MakeButton("Select Custom XML");
            selectBtn.Click += async (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "XML files (*.xml)|*.xml", Title = "Select an autounattend.xml" };
                if (dlg.ShowDialog() != true) return;
                if (await _wim.AddXmlToImageAsync(dlg.FileName, _workDir))
                { _xmlAdded = true; SetStatus(2, $"Added {Path.GetFileName(dlg.FileName)}", done: true); }
                else SetStatus(2, "Failed to add XML");
            };

            var schneegansBtn = MakeButton("Open Schneegans Generator");
            schneegansBtn.Click += (_, _) => Service!.OpenUrl("https://schneegans.de/windows/unattend-generator/");

            body.Children.Add(HRow(akariXmlBtn, hostedBtn, selectBtn, schneegansBtn));
        }

    }
}
