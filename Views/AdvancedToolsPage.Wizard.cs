using System.IO;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Views;

/// <summary>
/// Advanced Tools ▸ WIM wizard — faithful port of net8 AdvancedToolsTab.Wizard/.Steps12/
/// .Steps34. Four collapsible step cards driven by the byte-identical WimUtilService.
/// </summary>
public sealed partial class AdvancedToolsPage
{
    private void BuildWizard()
    {
        // Back row: back button + title + cancel
        var backRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var backBtn = MakeButton("");
        var backContent = new StackPanel { Orientation = Orientation.Horizontal };
        backContent.Children.Add(new TextBlock
        {
            Text = G("E72B"),
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
        });
        backContent.Children.Add(new TextBlock { Text = "Back", VerticalAlignment = VerticalAlignment.Center });
        backBtn.Content = backContent;
        backBtn.Click += (_, _) => ShowLanding();

        var wizardTitle = new TextBlock
        {
            Text = "Windows Installation Media Utility",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        Grid.SetColumn(wizardTitle, 1);

        _cancelBtn = MakeButton("Cancel", "#FF7A88");
        _cancelBtn.Visibility = Visibility.Collapsed;
        _cancelBtn.Click += (_, _) => _cts?.Cancel();
        Grid.SetColumn(_cancelBtn, 2);

        backRow.Children.Add(backBtn);
        backRow.Children.Add(wizardTitle);
        backRow.Children.Add(_cancelBtn);
        WizardPanel.Children.Add(backRow);

        BuildStep1();
        BuildStep2();
        BuildStep3();
        BuildStep4();
        UpdateStepStates();
    }

    // ── Step card factory ──────────────────────────────────────────────
    private StackPanel MakeStepCard(int index, string title, string glyph, string initialStatus)
    {
        var ui = new StepUi();
        _steps[index - 1] = ui;

        ui.Card = new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 12),
        };

        var outer = new StackPanel();

        var header = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        ui.BadgeText = new TextBlock
        {
            Text = index.ToString(),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ui.Badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = Res("SubtleFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = ui.BadgeText,
        };
        Grid.SetColumn(ui.Badge, 0);

        var iconAndTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        iconAndTitle.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0),
        });
        iconAndTitle.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(iconAndTitle, 1);

        ui.Status = new TextBlock
        {
            Text = initialStatus,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 12, 0),
        };
        Grid.SetColumn(ui.Status, 3);

        ui.Chevron = new TextBlock
        {
            Text = index == 1 ? G("E70E") : G("E70D"), // step 1 starts expanded
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = Res("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(ui.Chevron, 4);

        header.Children.Add(ui.Badge);
        header.Children.Add(iconAndTitle);
        header.Children.Add(ui.Status);
        header.Children.Add(ui.Chevron);

        ui.Body = new StackPanel
        {
            Margin = new Thickness(36, 14, 0, 0),
            Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed,
        };

        header.Tapped += (_, _) =>
        {
            if (!ui.Card.IsHitTestVisible) return;
            var open = ui.Body.Visibility == Visibility.Visible;
            ui.Body.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            ui.Chevron.Text = open ? G("E70D") : G("E70E");
        };

        outer.Children.Add(header);
        outer.Children.Add(ui.Body);
        ui.Card.Child = outer;
        WizardPanel.Children.Add(ui.Card);
        return ui.Body;
    }

    // ═════════════════════════════════════════════════════════════════
    //  Step 1 — Select ISO
    // ═════════════════════════════════════════════════════════════════
    private void BuildStep1()
    {
        var body = MakeStepCard(1, "Select ISO", G("E958"), "No ISO selected");

        body.Children.Add(MakeHint(
            "Pick a Windows ISO and a working directory, then extract it. " +
            "If you already extracted an ISO before, tick the checkbox and select that folder instead."));

        var selectIsoBtn = MakeButton("Select ISO");
        _isoPathText = MakePathText("");
        selectIsoBtn.Click += async (_, _) =>
        {
            var picked = await _files.PickSingleFileAsync(new[] { ".iso" });
            if (picked is null) return;
            if (!_wim.ValidateIsoFile(picked.Path)) { SetStatus(1, "Invalid ISO file"); return; }
            _isoPath = picked.Path;
            _isoPathText.Text = _isoPath;
            SetStatus(1, Path.GetFileName(_isoPath));
            UpdateStepStates();
        };
        body.Children.Add(HRow(selectIsoBtn, _isoPathText));

        var selectDirBtn = MakeButton("Select Folder");
        _workDirText = MakePathText(_workDir);
        selectDirBtn.Click += async (_, _) =>
        {
            var pickedDir = await _files.PickFolderAsync();
            if (pickedDir is null) return;

            if (_alreadyExtracted.IsChecked == true)
            {
                if (!_wim.LooksLikeExtractedMedia(pickedDir.Path))
                {
                    SetStatus(1, "Folder has no 'sources'/'boot' — not extracted media");
                    return;
                }
                _workDir = pickedDir.Path;
                _extractionDone = true;
                SetStatus(1, "Using already-extracted media", done: true);
                _ = OnExtractionCompleteAsync();
            }
            else
            {
                _workDir = Path.Combine(pickedDir.Path, "AkariWIM");
            }
            _workDirText.Text = _workDir;
            UpdateStepStates();
        };
        body.Children.Add(HRow(selectDirBtn, _workDirText));

        _alreadyExtracted = new CheckBox
        {
            Content = "I have already extracted an ISO — select that folder instead",
            Foreground = Res("TextFillColorSecondaryBrush"),
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 0, 12),
        };
        body.Children.Add(_alreadyExtracted);

        _extractBtn = MakePrimaryButton("Start Extraction");
        _extractBtn.Click += async (_, _) => await RunExtractionAsync();

        var win10Btn = MakeButton("Windows 10 ISO");
        win10Btn.Click += (_, _) => _tool.OpenUrl("https://www.microsoft.com/software-download/windows10");
        var win11Btn = MakeButton("Windows 11 ISO");
        win11Btn.Click += (_, _) => _tool.OpenUrl("https://www.microsoft.com/software-download/windows11");

        var dlLabel = new TextBlock
        {
            Text = "Need an ISO?",
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
        };
        body.Children.Add(HRow(_extractBtn, dlLabel, win10Btn, win11Btn));

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
            var ok = await _wim.ExtractIsoAsync(_isoPath, _workDir, s => SetStatus(1, s), null, ct);
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
        for (int i = 1; i < 4; i++)
        {
            _steps[i].Body.Visibility = Visibility.Visible;
            _steps[i].Chevron.Text = G("E70E");
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

        _conversionPanel.Children.Add(new Border
        {
            Background = Res("CardStrokeColorDefaultBrush"),
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
                pct => SetStatus(1, $"Converting… {pct:F0}%"), ct);
            if (ok) await RefreshConversionCardAsync();
        });
        _conversionPanel.Children.Add(HRow(convertBtn));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Step 2 — Add XML File
    // ═════════════════════════════════════════════════════════════════
    private void BuildStep2()
    {
        var body = MakeStepCard(2, "Add XML File", G("E8A5"), "Complete Step 1 first");

        body.Children.Add(MakeHint(
            "Add an autounattend.xml to the media root so Windows installs unattended. " +
            "Generate one from your current Akari Tool selections, pick your own, or build one online."));

        var akariXmlBtn = MakePrimaryButton("Generate Akari XML");
        ToolTipService.SetToolTip(akariXmlBtn, "Builds an autounattend.xml from your current Akari Tool selections and places it in the extracted media.");
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
        ToolTipService.SetToolTip(hostedBtn, "Downloads the ready-made Akari autounattend.xml from GitHub (no app selections needed).");
        hostedBtn.Click += async (_, _) => await RunBusyAsync("XML download", async ct =>
        {
            if (await _wim.DownloadAkariAutounattendXmlAsync(_workDir, s => SetStatus(2, s), ct))
            { _xmlAdded = true; SetStatus(2, "Akari autounattend.xml added", done: true); }
        });

        var selectBtn = MakeButton("Select Custom XML");
        selectBtn.Click += async (_, _) =>
        {
            var pickedXml2 = await _files.PickSingleFileAsync(new[] { ".xml" });
            if (pickedXml2 is null) return;
            if (await _wim.AddXmlToImageAsync(pickedXml2.Path, _workDir))
            { _xmlAdded = true; SetStatus(2, $"Added {Path.GetFileName(pickedXml2.Path)}", done: true); }
            else SetStatus(2, "Failed to add XML");
        };

        var schneegansBtn = MakeButton("Open Schneegans Generator");
        schneegansBtn.Click += (_, _) => _tool.OpenUrl("https://schneegans.de/windows/unattend-generator/");

        body.Children.Add(HRow(akariXmlBtn, hostedBtn, selectBtn, schneegansBtn));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Step 3 — Add Drivers
    // ═════════════════════════════════════════════════════════════════
    private void BuildStep3()
    {
        var body = MakeStepCard(3, "Add Drivers", G("E964"), "Complete Step 1 first");

        body.Children.Add(MakeHint(
            "Bundle drivers into the media. Storage drivers are loaded during setup ($WinpeDriver$); " +
            "everything else is installed automatically after setup via SetupComplete.cmd."));

        var systemBtn = MakePrimaryButton("Extract & Add System Drivers");
        systemBtn.Click += async (_, _) => await RunBusyAsync("Driver export", async ct =>
        {
            if (await _wim.AddDriversAsync(_workDir, null,
                    s => SetStatus(3, s),
                    pct => SetStatus(3, $"Exporting drivers… {pct:F0}%"), ct))
            { _driversAdded = true; SetStatus(3, "System drivers added", done: true); }
        });

        var customBtn = MakeButton("Select Custom Driver Folder");
        customBtn.Click += async (_, _) =>
        {
            var pickedDrv = await _files.PickFolderAsync();
            if (pickedDrv is null) return;
            await RunBusyAsync("Driver copy", async ct =>
            {
                if (await _wim.AddDriversAsync(_workDir, pickedDrv.Path, s => SetStatus(3, s), null, ct))
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
        var body = MakeStepCard(4, "Create ISO", G("E7B8"), "Complete Step 1 first");

        _oscdimgStatus = MakeHint("Checking for oscdimg.exe…");
        body.Children.Add(_oscdimgStatus);

        _oscdimgBtn = MakeButton("Install oscdimg");
        _oscdimgBtn.Click += async (_, _) => await RunBusyAsync("oscdimg install", async ct =>
        {
            await _wim.EnsureOscdimgAvailableAsync(s => SetStatus(4, s), null, ct);
            await RefreshOscdimgStateAsync();
        });

        var selectOutBtn = MakeButton("Select Output Location");
        _outputPathText = MakePathText("");
        selectOutBtn.Click += async (_, _) =>
        {
            var pickedOut = await _files.SaveFileAsync("AkariWindows.iso", new[] { ".iso" });
            if (pickedOut is null) return;
            _outputIsoPath = pickedOut.Path;
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
                    pct => SetStatus(4, $"Building ISO… {pct:F0}%"), ct);
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
