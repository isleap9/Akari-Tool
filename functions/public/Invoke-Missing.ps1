# ── Windows: Quick Settings ───────────────────────────────────────────────────
function Invoke-BtnGamemode        { Start-Process "ms-settings:gaming-gamemode" }
function Invoke-BtnPointerPrecision{ Start-Process "control.exe" -ArgumentList "main.cpl ,2" }
function Invoke-BtnScalingSettings { Start-Process "ms-settings:display-advanced" }

# ── Windows: Loudness EQ ─────────────────────────────────────────────────────
function Invoke-BtnLoudnessEQ { Invoke-UltimateScript -Path "6 Windows/24 Loudness EQ.ps1" -Status "Loudness EQ opened." }

# ── Windows: Control Panel Settings ──────────────────────────────────────────
# This script is too large and uses TrustedInstaller — launch a PS window
function Invoke-BtnCPOptimize {
    Invoke-UltimateScript -Path "6 Windows/22 Control Panel Settings.ps1" -Status "Opening Control Panel Settings (menu-driven console)..."
}
function Invoke-BtnCPDefault {
    Invoke-UltimateScript -Path "6 Windows/22 Control Panel Settings.ps1" -Status "Opening Control Panel Settings (menu-driven console)..."
}

# ── Windows: Bloatware Checks ─────────────────────────────────────────────────
function Invoke-BtnBloatwareLegacyCheck     { Start-Process "$env:SystemDrive\Windows\system32\appwiz.cpl" }
function Invoke-BtnBloatwareLegacyFeatCheck { Start-Process "$env:SystemDrive\Windows\system32\optionalfeatures.exe" }
function Invoke-BtnBloatwareUWPFeatCheck    { Start-Process "ms-settings:optionalfeatures" }
function Invoke-BtnBloatwareTaskmgr         { Start-Process "taskmgr" }

# ── Installers: GPU Tools (1:1 with Ultimate) ────────────────────────────────
# MSI Afterburner has a large upstream profile-setup script — delegate to it.
function Invoke-BtnInstAfterburner { Invoke-UltimateScript -Path "4 Installers/2 MSI Afterburner.ps1" -Status "MSI Afterburner installer opened." }

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

# ── Advanced: ReBar additional options (same upstream menu) ──────────────────
function Invoke-BtnReBarOff    { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick Force Off." }
function Invoke-BtnReBarToBios { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick To Bios." }

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
    Invoke-UltimateScript -Path "8 Advanced/8 Smt Ht.ps1" -Status "SMT/HT tool opened (menu-driven console)."
}

function Invoke-BtnCore1Thread1 {
    Invoke-UltimateScript -Path "8 Advanced/9 Core 1 Thread 1.ps1" -Status "Core 1 Thread 1 tool opened (menu-driven console)."
}

function Invoke-BtnPriority {
    Invoke-UltimateScript -Path "8 Advanced/10 Priority.ps1" -Status "Priority tool opened (menu-driven console)."
}
