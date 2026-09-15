# ── Windows: Quick Settings ───────────────────────────────────────────────────
function Invoke-BtnGamemode        { Start-Process "ms-settings:gaming-gamemode" }
function Invoke-BtnPointerPrecision{ Start-Process "control.exe" -ArgumentList "main.cpl ,2" }
function Invoke-BtnScalingSettings { Start-Process "ms-settings:display-advanced" }

# ── Windows: Loudness EQ (1:1 with "6 Windows/24 Loudness EQ.ps1") ───────────
function Invoke-BtnLoudnessEQ {
    Invoke-RunInBackground -StatusStart "Unhiding Enhancements tab..." -StatusDone "Enhancements tab unhidden for all sound devices." -ScriptBlock {
        Stop-Service audiosrv -Force
        Stop-Service AudioEndpointBuilder -Force
        $basePath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"
        $guids = Get-ChildItem -Path $basePath -Force -ErrorAction SilentlyContinue
        $regContent = "Windows Registry Editor Version 5.00`n"
        foreach ($guid in $guids) {
            $regPath = $guid.Name
            $regContent += "`n[$regPath\FxProperties]`n"
            $regContent += "`"{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},3`"=`"{5860E1C5-F95C-4a7a-8EC8-8AEF24F379A1}`"`n"
        }
        [IO.File]::WriteAllText("$env:SystemRoot\Temp\loudnesseq.reg", $regContent, (New-Object Text.UTF8Encoding($false)))
        regedit /s "$env:SystemRoot\Temp\loudnesseq.reg"
        Start-Service audiosrv
        Start-Service AudioEndpointBuilder
        Start-Process mmsys.cpl
    }
}

# ── Windows: Control Panel Settings (1:1 with "6 Windows/22 Control Panel
#    Settings.ps1" Optimize / Default branches) ───────────────────────────────
# Local Run-Trusted copy used inside runspaces below (TrustedInstaller swap trick).
function Invoke-BtnCPOptimize {
    $r = [System.Windows.MessageBox]::Show("Apply all Control Panel optimizations? Includes the registry tweaks (write/import two reg files), TrustedInstaller app-permission reset, scheduled defrag disable, console lock, priority notifications, and disabling app actions. Continue?", "Control Panel Settings: Optimize", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Applying Control Panel optimizations..." -StatusDone "Control Panel optimizations applied. Log out to apply fully." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        # fix 1 for turn off privacy & security app permissions
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        $capabilityconsentstoragedb = "Remove-item `"$env:ProgramData\Microsoft\Windows\CapabilityAccessManager\CapabilityConsentStorage.db*`" -Force"
        Run-Trusted -command $capabilityconsentstoragedb
        # fix for disable windows backup
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\CDPUserSvc`" /v `"Start`" /t REG_DWORD /d `"4`" /f >nul 2>&1"
        # registry optimizations
        Write-Asset "registryoptimize" "$env:SystemRoot\Temp\registryoptimize.reg"
        Regedit.exe /S "$env:SystemRoot\Temp\registryoptimize.reg"
        # fix 2 for turn off privacy & security app permissions
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        Run-Trusted -command $capabilityconsentstoragedb
        # disable defragment and optimize your drives scheduled task
        Get-ScheduledTask | Where-Object { $_.TaskName -match 'ScheduledDefrag' } | Disable-ScheduledTask | Out-Null
        # disable if you've been away sign-in prompt
        powercfg /setdcvalueindex scheme_current sub_none consolelock 0 2>$null
        powercfg /setacvalueindex scheme_current sub_none consolelock 0 2>$null
        # disable set priority notifications (per-GUID blob)
        $disableprioritynotificationsregcontent = @"
Windows Registry Editor Version 5.00

; disable set priority notifications
"@
        $disableprioritynotificationsguid = Get-ChildItem "HKCU:\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current" -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -match '^\{[a-f0-9-]+\}\$' } |
            ForEach-Object { ($_.PSChildName -split '\$')[0] } |
            Select-Object -Unique
        foreach ($guid in $disableprioritynotificationsguid) {
            $disableprioritynotificationsregcontent += "`n`n[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\$guid`$windows.data.donotdisturb.quiethoursprofile`$quiethoursprofilelist\windows.data.donotdisturb.quiethoursprofile`$microsoft.quiethoursprofile.priorityonly]`n"
            $disableprioritynotificationsregcontent += '"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,DF,B8,B4,CC,06,2A,2B,0E,D0,03,\' + "`n"
            $disableprioritynotificationsregcontent += '  43,42,01,00,C2,0A,01,CD,14,06,02,05,00,00,01,01,02,00,03,01,04,00,CC,32,12,\' + "`n"
            $disableprioritynotificationsregcontent += '  05,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,53,00,63,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,72,00,65,00,65,00,6E,00,53,00,6B,00,65,00,74,00,63,00,68,00,5F,00,38,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,41,00,70,00,70,00,29,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,2E,00,57,00,69,00,6E,00,64,00,6F,00,77,00,73,00,41,00,6C,00,61,00,72,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  6D,00,73,00,5F,00,38,00,77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,62,00,77,00,65,00,21,00,41,00,70,00,70,00,31,4D,00,69,00,63,00,72,00,6F,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,73,00,6F,00,66,00,74,00,2E,00,58,00,62,00,6F,00,78,00,41,00,70,00,70,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  5F,00,38,00,77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,65,00,21,00,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  58,00,62,00,6F,00,78,00,41,00,70,00,70,00,2D,4D,00,69,00,63,00,72,00,6F,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  73,00,6F,00,66,00,74,00,2E,00,58,00,62,00,6F,00,78,00,47,00,61,00,6D,00,69,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,6E,00,67,00,4F,00,76,00,65,00,72,00,6C,00,61,00,79,00,5F,00,38,00,77,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,41,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,70,00,70,00,29,57,00,69,00,6E,00,64,00,6F,00,77,00,73,00,2E,00,53,00,79,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,73,00,74,00,65,00,6D,00,2E,00,4E,00,65,00,61,00,72,00,53,00,68,00,61,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  72,00,65,00,45,00,78,00,70,00,65,00,72,00,69,00,65,00,6E,00,63,00,65,00,52,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,65,00,63,00,65,00,69,00,76,00,65,00,00,00,00,00'
        }
        $disableprioritynotificationsregfile = "$env:SystemRoot\Temp\disablesetprioritynotifications.reg"
        $disableprioritynotificationsregcontent | Out-File -FilePath $disableprioritynotificationsregfile -Encoding ASCII
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$disableprioritynotificationsregfile`"" -WindowStyle Hidden
        # disable app actions
        $stop = "AppActions", "CrossDeviceResume", "DesktopStickerEditorWin32Exe", "DiscoveryHubApp", "FESearchHost", "SearchHost", "SoftLandingTask", "TextInputHost", "VisualAssistExe", "WebExperienceHostApp", "WindowsBackupClient", "WindowsMigration"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Write-Asset "appactions" "$env:SystemRoot\Temp\appactions.reg"
        $settingsdat = "$env:LOCALAPPDATA\Packages\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\Settings\settings.dat"
        $regfileappactions = "$env:SystemRoot\Temp\appactions.reg"
        reg load "HKLM\Settings" $settingsdat >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg import $regfileappactions >$null 2>&1
            [gc]::Collect()
            Start-Sleep -Seconds 2
            reg unload "HKLM\Settings" >$null 2>&1
        }
    }
}
function Invoke-BtnCPDefault {
    Invoke-RunInBackground -StatusStart "Restoring Control Panel defaults..." -StatusDone "Control Panel defaults restored. Log out to apply fully." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        $capabilityconsentstoragedb = "Remove-item `"$env:ProgramData\Microsoft\Windows\CapabilityAccessManager\CapabilityConsentStorage.db*`" -Force"
        Run-Trusted -command $capabilityconsentstoragedb
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\CDPUserSvc`" /v `"Start`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        Write-Asset "registrydefaults" "$env:SystemRoot\Temp\registrydefaults.reg"
        Regedit.exe /S "$env:SystemRoot\Temp\registrydefaults.reg"
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        Run-Trusted -command $capabilityconsentstoragedb
        Get-ScheduledTask | Where-Object { $_.TaskName -match 'ScheduledDefrag' } | Enable-ScheduledTask | Out-Null
        powercfg /setdcvalueindex scheme_current sub_none consolelock 1 2>$null
        powercfg /setacvalueindex scheme_current sub_none consolelock 1 2>$null
        cmd /c "reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current /f >nul 2>&1"
        $stop = "AppActions", "CrossDeviceResume", "DesktopStickerEditorWin32Exe", "DiscoveryHubApp", "FESearchHost", "SearchHost", "SoftLandingTask", "TextInputHost", "VisualAssistExe", "WebExperienceHostApp", "WindowsBackupClient", "WindowsMigration"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Remove-Item "$env:LOCALAPPDATA\Packages\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\Settings\settings.dat" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Windows: Bloatware Checks ─────────────────────────────────────────────────
function Invoke-BtnBloatwareLegacyCheck     { Start-Process "$env:SystemDrive\Windows\system32\appwiz.cpl" }
function Invoke-BtnBloatwareLegacyFeatCheck { Start-Process "$env:SystemDrive\Windows\system32\optionalfeatures.exe" }
function Invoke-BtnBloatwareUWPFeatCheck    { Start-Process "ms-settings:optionalfeatures" }
function Invoke-BtnBloatwareTaskmgr         { Start-Process "taskmgr" }

# ── Installers: GPU Tools (1:1 with Ultimate) ────────────────────────────────
# MSI Afterburner profile setup (1:1 with "4 Installers/2 MSI Afterburner.ps1"):
# winget install, then write the baked profile configs into Program Files locations.
function Invoke-BtnInstAfterburner {
    Invoke-RunInBackground -StatusStart "Installing MSI Afterburner & applying profile..." -StatusDone "MSI Afterburner installed & configured." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        Start-Process "winget" -ArgumentList "install `"Guru3D.Afterburner.Beta`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden
        $dir = "$env:SystemDrive\Program Files (x86)"
        New-Item -Path "$dir\MSI Afterburner" -Name "Profiles" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "msiafterburnercfg" "$dir\MSI Afterburner\Profiles\MSIAfterburner.cfg"
        New-Item -Path "$dir\RivaTuner Statistics Server" -Name "Profiles" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "msiab_config" "$dir\RivaTuner Statistics Server\Profiles\Config"
        Write-Asset "msiab_global" "$dir\RivaTuner Statistics Server\Profiles\Global"
        Write-Asset "msiab_overlayeditorcfg" "$dir\RivaTuner Statistics Server\Plugins\Client\OverlayEditor.cfg"
        Write-Asset "msiab_hotkeyhandlercfg" "$dir\RivaTuner Statistics Server\Plugins\Client\HotkeyHandler.cfg"
        Write-Asset "msiab_fr33thyovl" "$dir\RivaTuner Statistics Server\Plugins\Client\Overlays\fr33thy.ovl"
        Write-Asset "msiab_desktopoverlayhostcfg" "$dir\RivaTuner Statistics Server\DesktopOverlayHost.cfg"
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\MSI Afterburner\MSI Afterburner.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\MSI Afterburner" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\RivaTuner Statistics Server\RivaTuner Statistics Server.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\RivaTuner Statistics Server" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

function Invoke-BtnInstNPI {
    Invoke-RunInBackground -StatusStart "Installing Nvidia Profile Inspector..." -StatusDone "NPI installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
    }
}

function Invoke-BtnInstMCT {
    Invoke-RunInBackground -StatusStart "Installing More Clock Tool..." -StatusDone "More Clock Tool installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        curl.exe -s -L -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" "https://www.igorslab.de/installer/MoreClockTool_v1111_2.zip" -o "$env:SystemRoot\Temp\mct.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\mct.zip" -DestinationPath "$env:SystemDrive\Program Files (x86)\More Clock Tool" -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\More Clock Tool.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\More Clock Tool\MoreClockTool.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\More Clock Tool"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\More Clock Tool.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\More Clock Tool\MoreClockTool.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\More Clock Tool"; $sc2.Save()
    }
}

function Invoke-BtnInstCRU {
    Invoke-RunInBackground -StatusStart "Installing CRU / SRE..." -StatusDone "CRU and SRE installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $dir = "$env:SystemDrive\Program Files (x86)\CRUSRE"
        Invoke-WebRequest "https://www.monitortests.com/download/cru/cru-1.5.3.zip" -OutFile "$env:SystemRoot\Temp\cru.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\cru.zip" -DestinationPath $dir -Force
        Invoke-WebRequest "https://www.monitortests.com/download/sre/sre-1.0.zip" -OutFile "$env:SystemRoot\Temp\sre.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\sre.zip" -DestinationPath $dir -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        foreach ($pair in @(
            @{ name="Custom Resolution Utility.lnk"; exe="CRU.exe" },
            @{ name="Scaled Resolution Editor.lnk";  exe="SRE.exe" }
        )) {
            $sc = $sh.CreateShortcut("$Desktop\$($pair.name)"); $sc.TargetPath = "$dir\$($pair.exe)"; $sc.WorkingDirectory = $dir; $sc.Save()
            $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$($pair.name)"); $sc2.TargetPath = "$dir\$($pair.exe)"; $sc2.WorkingDirectory = $dir; $sc2.Save()
        }
    }
}

# ── Advanced: ReBar additional options (1:1 with "8 Advanced/7 ReBar Force.ps1") ─
function Invoke-BtnReBarOff {
    Invoke-RunInBackground -StatusStart "ReBar: applying FORCE OFF profile..." -StatusDone "ReBar force-off applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        Write-Asset "inspector_forceoff" "$env:SystemRoot\Temp\forceoff.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -mergeImport -silent $env:SystemRoot\Temp\forceoff.nip"
        Start-Process "$base\nvidiaProfileInspector.exe"
    }
}
function Invoke-BtnReBarToBios {
    Invoke-RunInBackground -StatusStart "Restarting to BIOS..." -StatusDone "Restarting to BIOS." -ScriptBlock {
        cmd /c "C:\Windows\System32\shutdown.exe /r /fw /t 0"
    }
}

# ── Advanced: Keyboard Shortcuts ─────────────────────────────────────────────
function Invoke-BtnKbDefault {
    Invoke-RunInBackground -StatusStart "Restoring keyboard shortcuts..." -StatusDone "Keyboard shortcuts restored." -ScriptBlock {
        reg add "HKLM\SYSTEM\ControlSet001\Services\hidserv" /v "Start" /t REG_DWORD /d "3" /f | Out-Null
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoWinKeys`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`" /v `"DisabledHotkeys`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout`" /v `"Scancode Map`" /f >nul 2>&1"
    }
}
function Invoke-BtnKbOff {
    $r = [System.Windows.MessageBox]::Show("This disables Win key, media keys, and hotkeys. ESC rebinds to =. Cut/copy/paste still work. Continue?","Keyboard Shortcuts Off","YesNo","Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Disabling keyboard shortcuts..." -StatusDone "Keyboard shortcuts disabled." -ScriptBlock {
        reg add "HKLM\SYSTEM\ControlSet001\Services\hidserv" /v "Start" /t REG_DWORD /d "4" /f | Out-Null
        reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" /v "NoWinKeys" /t REG_DWORD /d "1" /f | Out-Null
        reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v "DisabledHotkeys" /t REG_DWORD /d "1" /f | Out-Null
        reg add "HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout" /v "Scancode Map" /t REG_BINARY /d "00000000000000000700000000005be000005ce000003800000038e00000010001000d0000000000" /f | Out-Null
    }
}

# ── Advanced: WHQL Default ────────────────────────────────────────────────────
function Invoke-BtnWhqlDefault {
    Invoke-RunInBackground -StatusStart "Restoring WHQL signing..." -StatusDone "WHQL signing restored." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`" /v `"WHQLSettings`" /f >nul 2>&1"
    }
}

# ── Advanced: Interactive — SMT/HT, Core 1 Thread 1, Priority ────────────────
# These need interactive terminal I/O — launch PS windows
function Invoke-BtnSmtOff {
    Invoke-ConsoleScript -Asset "smtht" -Status "SMT/HT tool opened (menu-driven console)."
}

function Invoke-BtnCore1Thread1 {
    Invoke-ConsoleScript -Asset "core1thread1" -Status "Core 1 Thread 1 tool opened (menu-driven console)."
}

function Invoke-BtnPriority {
    Invoke-ConsoleScript -Asset "priority" -Status "Priority tool opened (menu-driven console)."
}