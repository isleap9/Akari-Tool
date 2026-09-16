# ── Home tab handlers ─────────────────────────────────────────────────────────

# Create a system restore point (bypasses the once-per-24h throttle for this run)
function Invoke-BtnHomeRestorePoint {
    Invoke-RunInBackground -StatusStart "Creating restore point..." -StatusDone "Restore point created." -ScriptBlock {
        try {
            $drive = $env:SystemDrive
            Enable-ComputerRestore -Drive "$drive\" -ErrorAction SilentlyContinue
            # Allow more than one restore point per 24h
            New-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" `
                -Name "SystemRestorePointCreationFrequency" -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null
            Checkpoint-Computer -Description "Akari Tool" -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
        } catch {
            $sync.window.Dispatcher.Invoke([action]{
                [System.Windows.MessageBox]::Show(
                    "Could not create a restore point automatically. System Protection may be turned off.`n`n$($_.Exception.Message)",
                    "Restore Point", "OK", "Warning")
            }, "Normal")
        }
    }
}

# Recommended-path shortcut buttons — jump to the matching tab
function Invoke-BtnGoCheck      { if ($sync.NavCheck)      { $sync.NavCheck.IsChecked      = $true } }
function Invoke-BtnGoRefresh    { if ($sync.NavRefresh)    { $sync.NavRefresh.IsChecked    = $true } }
function Invoke-BtnGoSetup      { if ($sync.NavSetup)      { $sync.NavSetup.IsChecked      = $true } }
function Invoke-BtnGoInstallers { if ($sync.NavInstallers) { $sync.NavInstallers.IsChecked = $true } }
function Invoke-BtnGoGraphics   { if ($sync.NavGraphics)   { $sync.NavGraphics.IsChecked   = $true } }
function Invoke-BtnGoWindows    { if ($sync.NavWindows)    { $sync.NavWindows.IsChecked    = $true } }
function Invoke-BtnGoHardware   { if ($sync.NavHardware)   { $sync.NavHardware.IsChecked   = $true } }
function Invoke-BtnGoAdvanced   { if ($sync.NavAdvanced)   { $sync.NavAdvanced.IsChecked   = $true } }
function Invoke-BtnGoTweaks     { if ($sync.NavTweaks)     { $sync.NavTweaks.IsChecked     = $true } }

# Sidebar GitHub link
function Invoke-BtnGithub { Start-Process "https://github.com/FR33THYFR33THY/Ultimate" }

# About links
function Invoke-BtnHomeGuide  { Start-Process "https://youtu.be/zwPEDXteJYQ" }
function Invoke-BtnHomeGithub { Start-Process "https://github.com/FR33THYFR33THY/Ultimate" }

$script:AkariUrl = "https://raw.githubusercontent.com/isleap9/Akari-Tool/main/akari.ps1"

# Write the embedded icon to the local install dir and return its path (for shortcuts)
function Save-AkariIcon {
    $dir = Join-Path $env:LOCALAPPDATA "AkariTool"
    New-Item -ItemType Directory -Path $dir -Force -ErrorAction SilentlyContinue | Out-Null
    $icoPath = Join-Path $dir "AkariLogo.ico"
    if ($sync.assets -and $sync.assets.icon) {
        try { [IO.File]::WriteAllBytes($icoPath, [Convert]::FromBase64String($sync.assets.icon)) } catch {}
    }
    return $icoPath
}

# Create a Desktop .lnk (elevated) with the Akari icon
function New-AkariShortcut([string]$arguments, [string]$icoPath) {
    $lnkPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Akari Tool.lnk"
    $sh = New-Object -ComObject WScript.Shell
    $sc = $sh.CreateShortcut($lnkPath)
    $sc.TargetPath       = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $sc.Arguments        = $arguments
    $sc.WorkingDirectory = $env:SystemRoot
    $sc.Description      = "Akari Tool"
    if ($icoPath -and (Test-Path $icoPath)) { $sc.IconLocation = "$icoPath,0" }
    $sc.Save()
    # flip the "Run as administrator" bit so it goes straight to UAC
    try {
        $bytes = [IO.File]::ReadAllBytes($lnkPath)
        $bytes[0x15] = $bytes[0x15] -bor 0x20
        [IO.File]::WriteAllBytes($lnkPath, $bytes)
    } catch {}
    return $lnkPath
}

# Online: Desktop shortcut that re-runs the web launcher (always latest, nothing kept)
function Invoke-BtnHomeShortcutOnline {
    try {
        $ico = Save-AkariIcon
        $args = "-NoProfile -ExecutionPolicy Bypass -Command `"irm $script:AkariUrl | iex`""
        New-AkariShortcut $args $ico | Out-Null
        Set-Status "Online shortcut created on Desktop." "#66BB6A"
        [System.Windows.MessageBox]::Show("Created 'Akari Tool' on your Desktop.`n`nIt runs the latest version from the web each time (internet required) and runs as admin.", "Desktop Shortcut", "OK", "Information") | Out-Null
    } catch {
        Set-Status "Could not create shortcut: $($_.Exception.Message)" "#EF5350"
        [System.Windows.MessageBox]::Show("Could not create the shortcut:`n$($_.Exception.Message)", "Desktop Shortcut", "OK", "Warning") | Out-Null
    }
}

# Offline: save a local copy of akari.ps1 under AppData + Desktop shortcut to it (tune offline)
function Invoke-BtnHomeShortcutOffline {
    # capture the running script path here (empty inside the background runspace)
    $sync.AkariSrc = if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) { $PSCommandPath } else { "" }
    Invoke-RunInBackground -StatusStart "Installing Akari Tool locally..." -StatusDone "Akari Tool installed." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $url = "https://raw.githubusercontent.com/isleap9/Akari-Tool/main/akari.ps1"
        try {
            $dir = Join-Path $env:LOCALAPPDATA "AkariTool"
            New-Item -ItemType Directory -Path $dir -Force -ErrorAction SilentlyContinue | Out-Null
            $target = Join-Path $dir "akari.ps1"

            # obtain the script: copy the running file if we have one, else download it
            if ($sync.AkariSrc -and (Test-Path -LiteralPath $sync.AkariSrc)) {
                Copy-Item -LiteralPath $sync.AkariSrc -Destination $target -Force
            } else {
                Invoke-WebRequest $url -OutFile $target -UseBasicParsing -ErrorAction Stop
            }

            # local icon
            $icoPath = Join-Path $dir "AkariLogo.ico"
            if ($sync.assets -and $sync.assets.icon) { [IO.File]::WriteAllBytes($icoPath, [Convert]::FromBase64String($sync.assets.icon)) }

            # Desktop shortcut -> local file, elevated
            $lnkPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Akari Tool.lnk"
            $sh = New-Object -ComObject WScript.Shell
            $sc = $sh.CreateShortcut($lnkPath)
            $sc.TargetPath       = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
            $sc.Arguments        = "-NoProfile -ExecutionPolicy Bypass -File `"$target`""
            $sc.WorkingDirectory = $dir
            $sc.Description      = "Akari Tool"
            if (Test-Path $icoPath) { $sc.IconLocation = "$icoPath,0" }
            $sc.Save()
            try { $b = [IO.File]::ReadAllBytes($lnkPath); $b[0x15] = $b[0x15] -bor 0x20; [IO.File]::WriteAllBytes($lnkPath, $b) } catch {}

            Notice "Installed to:`n$target`n`nA Desktop shortcut was created. It runs the local copy (works offline) and runs as admin." "Akari Tool Installed"
        } catch {
            $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show("Install failed:`n$($_.Exception.Message)", "Akari Tool", "OK", "Warning") | Out-Null }, "Normal")
        }
    }
}
