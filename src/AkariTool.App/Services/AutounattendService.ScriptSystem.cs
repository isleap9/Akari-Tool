using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    public partial class AutounattendService
    {
        // ── System pass sections ───────────────────────────────────────────

        private static void AppendScriptsDirectorySetup(StringBuilder sb)
        {
            sb.AppendLine("    $scriptsDir = \"" + ScriptsDir + "\"");
            sb.AppendLine("    if (!(Test-Path $scriptsDir)) {");
            sb.AppendLine("        New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null");
            sb.AppendLine("        Write-Log \"Created scripts directory: $scriptsDir\" \"SUCCESS\"");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private void AppendAppRemovalSection(StringBuilder sb, IReadOnlyList<AppDefinition> apps)
        {
            if (apps.Count == 0) return;

            // Categorize — mirrors Winhance's AppRemovalScriptSection
            var packages         = new List<string>();
            var capabilities     = new List<string>();
            var optionalFeatures = new List<string>();
            var specialApps      = new List<string>();
            var edgeNeeded       = false;
            var oneDriveNeeded   = false;

            foreach (var app in apps)
            {
                if (app.Id == "windows-app-edge")     { edgeNeeded = true; continue; }
                if (app.Id == "windows-app-onedrive") { oneDriveNeeded = true; continue; }

                if (!string.IsNullOrEmpty(app.CapabilityName))
                    capabilities.Add(app.CapabilityName);
                else if (!string.IsNullOrEmpty(app.OptionalFeatureName))
                    optionalFeatures.Add(app.OptionalFeatureName);
                else if (app.AppxPackageName?.Length > 0)
                {
                    packages.AddRange(app.AppxPackageName);
                    if (app.AppxPackageName.Any(n => n.Contains("OneNote", StringComparison.OrdinalIgnoreCase)) &&
                        !specialApps.Contains("OneNote"))
                        specialApps.Add("OneNote");
                }
            }

            sb.AppendLine("    # ============================================================================");
            sb.AppendLine("    # WINDOWS APPS REMOVAL");
            sb.AppendLine("    # ============================================================================");
            sb.AppendLine();

            var haveBloat = packages.Count > 0 || capabilities.Count > 0 || optionalFeatures.Count > 0 || specialApps.Count > 0;
            if (haveBloat)
            {
                var xboxPackages = new[] { "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay" };
                var xboxFix   = packages.Any(p => xboxPackages.Contains(p, StringComparer.OrdinalIgnoreCase));
                var teamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

                var bloat = BloatRemovalScriptGenerator.GenerateScript(
                    packages, capabilities, optionalFeatures, specialApps, xboxFix, teamsKill);
                AppendBase64Script(sb, "BloatRemoval", bloat);
            }
            if (edgeNeeded)     AppendBase64Script(sb, "EdgeRemoval",     EdgeRemovalScript.GetScript());
            if (oneDriveNeeded) AppendBase64Script(sb, "OneDriveRemoval", OneDriveRemovalScript.GetScript());

            // Execute + register self-healing scheduled tasks (same triggers as Winhance)
            sb.AppendLine();
            sb.AppendLine("    # Execute removal scripts and register scheduled tasks");
            sb.AppendLine("    $scriptsToExecute = @()");
            if (haveBloat)
                sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\BloatRemoval.ps1\"; Name = \"BloatRemoval\"; TriggerType = \"Logon\"}");
            if (edgeNeeded)
                sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\EdgeRemoval.ps1\"; Name = \"EdgeRemoval\"; TriggerType = \"Startup\"}");
            if (oneDriveNeeded)
                sb.AppendLine("    $scriptsToExecute += @{Path = \"$scriptsDir\\OneDriveRemoval.ps1\"; Name = \"OneDriveRemoval\"; TriggerType = \"Logon\"}");
            sb.AppendLine(@"
    foreach ($script in $scriptsToExecute) {
        if (Test-Path $script.Path) {
            Write-Log ""Executing $($script.Name) script..."" ""INFO""
            try {
                Start-Process powershell.exe -ArgumentList ""-ExecutionPolicy Bypass -NoProfile -File `""$($script.Path)`"""" -Wait -NoNewWindow
                Write-Log ""$($script.Name) execution completed"" ""SUCCESS""
            } catch {
                Write-Log ""$($script.Name) execution failed: $($_.Exception.Message)"" ""WARNING""
            }

            Write-Log ""Registering scheduled task for $($script.Name)..."" ""INFO""
            try {
                $action = New-ScheduledTaskAction -Execute ""powershell.exe"" -Argument ""-ExecutionPolicy Bypass -NoProfile -File `""$($script.Path)`""""
                if ($script.TriggerType -eq ""Startup"") {
                    $trigger = New-ScheduledTaskTrigger -AtStartup
                } else {
                    $trigger = New-ScheduledTaskTrigger -AtLogon
                }
                $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0
                $principal = New-ScheduledTaskPrincipal -UserId ""SYSTEM"" -LogonType ServiceAccount -RunLevel Highest
                Register-ScheduledTask -TaskName $script.Name -TaskPath ""\AkariTool"" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
                Write-Log ""Registered scheduled task: $($script.Name)"" ""SUCCESS""
            } catch {
                Write-Log ""Failed to register task $($script.Name): $($_.Exception.Message)"" ""ERROR""
            }
        }
    }

    Write-Log ""Windows Apps removal configuration completed"" ""SUCCESS""");
            sb.AppendLine();
        }

        private void AppendSystemTweaksSection(StringBuilder sb, IReadOnlyList<UnattendTweakOption> tweaks)
        {
            var scripted = tweaks.Where(t => t.ScriptName != null).ToList();
            if (scripted.Count == 0) return;

            sb.AppendLine("    # ============================================================================");
            sb.AppendLine("    # AKARI SYSTEM TWEAKS");
            sb.AppendLine("    # ============================================================================");
            sb.AppendLine();
            foreach (var t in scripted)
            {
                var content = LoadScriptContent(t.ScriptName!);
                AppendBase64Script(sb, ScriptFileBase(t.ScriptName!), content);
                sb.AppendLine($"    Invoke-TweakScript -Path \"$scriptsDir\\{t.ScriptName}\" -Name \"{t.Name}\"");
                sb.AppendLine();
            }
        }

        /// <summary>
        /// User-scoped tweak scripts must exist on disk before the per-user pass
        /// runs at first logon, so they're extracted during the SYSTEM pass.
        /// </summary>
        private void AppendUserTweakExtraction(StringBuilder sb, IReadOnlyList<UnattendTweakOption> userTweaks)
        {
            var scripted = userTweaks.Where(t => t.ScriptName != null).ToList();
            if (scripted.Count == 0) return;

            sb.AppendLine("    # ============================================================================");
            sb.AppendLine("    # EXTRACT PER-USER TWEAK SCRIPTS (applied at first logon)");
            sb.AppendLine("    # ============================================================================");
            sb.AppendLine();
            foreach (var t in scripted)
                AppendBase64Script(sb, ScriptFileBase(t.ScriptName!), LoadScriptContent(t.ScriptName!));
            sb.AppendLine();
        }

        private static void AppendCleanStartMenuSection(StringBuilder sb)
        {
            sb.AppendLine(@"
    # ============================================================================
    # START MENU LAYOUT
    # ============================================================================

    Write-Log ""Configuring clean Start Menu layout..."" ""INFO""
    $buildNumber = [System.Environment]::OSVersion.Version.Build
    Write-Log ""Detected Windows build: $buildNumber"" ""INFO""

    if ($buildNumber -ge 22000) {
        try {
            Set-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Start' -Name 'ConfigureStartPins' -Type 'String' -Value '{""pinnedList"":[]}' -Description 'Clean Start Menu'
            Write-Log ""Windows 11 Start Menu layout applied successfully"" ""SUCCESS""
        } catch {
            Write-Log ""Failed to apply Windows 11 Start Menu layout: $($_.Exception.Message)"" ""ERROR""
        }
    }
    else {
        try {
            $ShellPath = ""C:\Users\Default\AppData\Local\Microsoft\Windows\Shell""
            New-Item -Path $ShellPath -ItemType Directory -Force | Out-Null
            $xmlContent = @'
<?xml version=""1.0"" encoding=""utf-8""?>
<LayoutModificationTemplate Version=""1"" xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification"">
    <LayoutOptions StartTileGroupCellWidth=""6"" />
    <DefaultLayoutOverride>
        <StartLayoutCollection>
            <StartLayout GroupCellWidth=""6"" xmlns=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout"" />
        </StartLayoutCollection>
    </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@
            $xmlContent | Out-File -FilePath ""$ShellPath\LayoutModification.xml"" -Encoding UTF8
            Write-Log ""Clean Start Menu Template created"" ""SUCCESS""
        } catch {
            Write-Log ""Failed to create Start Menu Template: $($_.Exception.Message)"" ""ERROR""
        }
    }");
            sb.AppendLine();
        }

        /// <summary>
        /// PowerShell installer for Akari Tool — queries the GitHub API for the
        /// latest release, downloads the Setup exe, and runs it. No browser needed
        /// (unattended installs typically remove Edge). Version-agnostic: matches
        /// any *Setup*.exe asset in the latest release.
        /// </summary>
        private const string InstallAkariToolScript = @"# Downloads and runs the latest Akari Tool installer from GitHub (no browser required)
$Host.UI.RawUI.WindowTitle = 'Akari Tool Installer'
$ErrorActionPreference = 'Stop'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Host 'Fetching latest Akari Tool release...' -ForegroundColor Cyan
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/isleap9/Akari-Tool/releases/latest' -UseBasicParsing
    $asset = $release.assets | Where-Object { $_.name -like '*Setup*.exe' } | Select-Object -First 1
    if (-not $asset) { $asset = $release.assets | Where-Object { $_.name -like '*.exe' } | Select-Object -First 1 }
    if (-not $asset) { throw 'No installer found in the latest release.' }
    $dest = Join-Path $env:TEMP $asset.name
    Write-Host (""Downloading {0} ({1} MB)..."" -f $asset.name, [math]::Round($asset.size / 1MB, 1)) -ForegroundColor Cyan
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $dest -UseBasicParsing
    Write-Host 'Starting installer...' -ForegroundColor Green
    Start-Process -FilePath $dest
} catch {
    Write-Host (""Failed to download Akari Tool: {0}"" -f $_.Exception.Message) -ForegroundColor Red
    Write-Host 'You can download it manually from: https://github.com/isleap9/Akari-Tool/releases/latest'
    Read-Host 'Press Enter to close'
}
";

        private void AppendAkariInstallerShortcut(StringBuilder sb)
        {
            // 1. Embed the downloader script into the scripts directory
            AppendBase64Script(sb, "InstallAkariTool", InstallAkariToolScript);

            // 2. Create a .lnk on the Default user's desktop that runs it via
            //    PowerShell — mirrors Winhance's browserless installer shortcut,
            //    including the byte[21]=34 patch that sets the Run-as-Administrator flag.
            sb.AppendLine(@"    # Create desktop shortcut that downloads & installs Akari Tool (works without a browser)
    try {
        $shortcutPath = ""C:\Users\Default\Desktop\Install Akari Tool.lnk""
        $WshShell = New-Object -ComObject WScript.Shell
        $shortcut = $WshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = ""C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe""
        $shortcut.Arguments = '-ExecutionPolicy Bypass -NoProfile -File ""' + $scriptsDir + '\InstallAkariTool.ps1""'
        $shortcut.IconLocation = ""C:\Windows\System32\appwiz.cpl,0""
        $shortcut.WorkingDirectory = ""C:\Windows\System32""
        $shortcut.Description = ""Download and install Akari Tool from GitHub""
        $shortcut.Save()
        $bytes = [System.IO.File]::ReadAllBytes($shortcutPath)
        $bytes[21] = 34
        [System.IO.File]::WriteAllBytes($shortcutPath, $bytes)
        Write-Log ""Created desktop shortcut: $shortcutPath"" ""SUCCESS""
    } catch {
        Write-Log ""Failed to create desktop shortcut: $($_.Exception.Message)"" ""ERROR""
    }");
            sb.AppendLine();
        }

        private static void AppendUserCustomizationsScheduledTask(StringBuilder sb)
        {
            sb.AppendLine(@"
    # ============================================================================
    # USER CUSTOMIZATIONS SCHEDULED TASK
    # ============================================================================

    Write-Log ""Registering UserCustomizations scheduled task..."" ""INFO""
    try {
        $action = New-ScheduledTaskAction -Execute ""powershell.exe"" -Argument ""-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File " + MainScript + @" -UserCustomizations""
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0
        $principal = New-ScheduledTaskPrincipal -UserId ""SYSTEM"" -LogonType ServiceAccount -RunLevel Highest
        Register-ScheduledTask -TaskName ""AkariUserCustomizations"" -TaskPath ""\AkariTool"" -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
        Write-Log ""Registered scheduled task: AkariUserCustomizations"" ""SUCCESS""
    } catch {
        Write-Log ""Failed to register UserCustomizations task: $($_.Exception.Message)"" ""ERROR""
    }");
            sb.AppendLine();
        }

    }
}
