# Scaling Higher No Accel — 1:1 with Ultimate "7 Hardware/1 Scaling Higher No Accel.ps1"
# (per-scaling DPI + SmoothMouse compensation curves), driven by the CboScaling dropdown.
function Invoke-BtnScalingApply {
    # dpi, MouseSpeed, Threshold1/2, EnablePerProcessSystemDPI, SmoothMouseXCurve, SmoothMouseYCurve per level
    $map = @{
        "100%" = @{ dpi=96;  spd=0; t1=0; t2=0;  pp=0; x="0000000000000000c0cc0c00000000008099190000000000406626000000000000333300000000000"; y="0000000000000000000038000000000000007000000000000000a800000000000000e00000000000" }
        "125%" = @{ dpi=120; spd=1; t1=6; t2=10; pp=1; x="00000000000000000000100000000000000020000000000000003000000000000000400000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "150%" = @{ dpi=144; spd=1; t1=6; t2=10; pp=1; x="0000000000000000303313000000000060662600000000009099390000000000C0CC4C0000000000"; y="0000000000000000000038000000000000007000000000000000A800000000000000E00000000000" }
        "175%" = @{ dpi=168; spd=1; t1=6; t2=10; pp=1; x="00000000000000006066160000000000C0CC2C000000000020334300000000008099590000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "200%" = @{ dpi=192; spd=1; t1=6; t2=10; pp=1; x="00000000000000009099190000000000203333000000000B0CC4C000000000040666600000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "225%" = @{ dpi=216; spd=1; t1=6; t2=10; pp=1; x="0000000000000000C0CC1C0000000000809939000000000040665600000000000033730000000000"; y="0000000000000000000038000000000000007000000000000000A800000000000000E00000000000" }
        "250%" = @{ dpi=240; spd=1; t1=6; t2=10; pp=1; x="00000000000000000000200000000000000040000000000000006000000000000000800000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "300%" = @{ dpi=288; spd=1; t1=6; t2=10; pp=1; x="00000000000000006066260000000000C0CC4C000000000020337300000000008099990000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "350%" = @{ dpi=336; spd=1; t1=6; t2=10; pp=1; x="0000000000000000C0CC2C000000000080995900000000004066860000000000003B300000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
    }
    $selected = $sync.CboScaling.Dispatcher.Invoke([func[object]]{ $sync.CboScaling.Text })
    $s = $map[$selected]
    if (-not $s) { Set-Status "Select a scaling value first."; return }
    Invoke-RunInBackground -StatusStart "Applying $selected scaling..." -StatusDone "Scaling applied. Log out to apply." -ScriptBlock {
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSensitivity`" /t REG_SZ /d `"10`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSpeed`" /t REG_SZ /d `"$($s.spd)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold1`" /t REG_SZ /d `"$($s.t1)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold2`" /t REG_SZ /d `"$($s.t2)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseXCurve`" /t REG_BINARY /d `"$($s.x)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseYCurve`" /t REG_BINARY /d `"$($s.y)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Win8DpiScaling`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"LogPixels`" /t REG_DWORD /d `"$($s.dpi)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"EnablePerProcessSystemDPI`" /t REG_DWORD /d `"$($s.pp)`" /f >nul 2>&1"
    }.GetNewClosure()
}

# Monitor Optimization — opens the UFO frame-rate test then lists the checklist
function Invoke-BtnMonitorOpt {
    Invoke-RunInBackground -StatusStart "Opening monitor test..." -StatusDone "Monitor test opened." -ScriptBlock {
        Start-Process "https://www.testufo.com/framerates#count=6&background=none&pps=1920"
    }
    [System.Windows.MessageBox]::Show("Monitor optimizations:`n- Enable overclock mode`n- Run highest refresh rate`n- Disable adaptive brightness and variable back light`n- Turn off variable refresh rate, adaptive sync and g-sync`n- Adjust color, brightness and sharpening to your preference`n- Max overdrive without causing overshoot or reducing motion clarity", "Monitor Optimizations", "OK", "Information") | Out-Null
}

# Polling Rate
function Invoke-BtnPollingOff {
    Invoke-RunInBackground -StatusStart "Disabling background polling cap..." -StatusDone "Polling cap removed." -ScriptBlock {
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"RawMouseThrottleEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnPollingDefault {
    Invoke-RunInBackground -StatusStart "Restoring polling cap..." -StatusDone "Polling cap restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Control Panel\Mouse`" /v `"RawMouseThrottleEnabled`" /f >nul 2>&1"
    }
}

# Mouse Polling Rate Test — opens the polling test then shows the checklist
function Invoke-BtnMouseTest {
    Invoke-RunInBackground -StatusStart "Opening mouse polling test..." -StatusDone "Mouse test opened." -ScriptBlock {
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        Start-Process "https://cpstest.org/polling-rate-test"
    }
    [System.Windows.MessageBox]::Show("Mouse optimizations:`n- Turn off motion sync`n- Keep dongle close to mouse`n- Disable angle snapping`n- Set lowest debounce time`n- Use maximum polling rate`n- USB port closest to the CPU`n`nExtreme polling may affect lower end CPU's & certain game engine framerates`n`nSet a comfortable DPI - increased DPI reduces pixel skipping & latency`nSuggested minimal DPI:`n- 400dpi for 1080p`n- 800dpi for 1440p`n- 1600dpi for 4k`n`nTo prevent mouse acceleration when gaming:`n- Use 100% scaling`n- Set 6/11 & pointer precision off`n- Enable raw input in games when possible`n`nFor higher scaling with no acceleration see the Scaling section.", "Mouse Optimizations", "OK", "Information") | Out-Null
}

# Controller Overclock — installs hidusbf (driver + Setup) and shortcuts
function Invoke-BtnControllerOC {
    Invoke-RunInBackground -StatusStart "Installing hidusbf..." -StatusDone "hidusbf installed. Run Setup.exe from the desktop/Start menu." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        IWR "https://github.com/LordOfMice/hidusbf/raw/refs/heads/master/hidusbf.zip" -OutFile "$env:SystemRoot\Temp\hidusbf.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\hidusbf.zip" -DestinationPath "$env:SystemDrive\Program Files (x86)\hidusbf" -Force
        Start-Process -FilePath "rundll32.exe" -ArgumentList "setupapi.dll,InstallHinfSection DefaultInstall 132 $env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\HIDUSBF_AS.INF" -Wait
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Setup.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Setup.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"; $sc2.Save()
    }
}

# Controller Polling Rate Test — installs Polling app and opens it
function Invoke-BtnControllerTest {
    Invoke-RunInBackground -StatusStart "Installing Polling..." -StatusDone "Polling installed & opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        New-Item -Path "$env:SystemDrive\Program Files (x86)\Polling" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        IWR "https://github.com/cakama3a/Polling/releases/download/1.3.1.4/Polling.exe" -OutFile "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Polling.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Polling.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"; $sc2.Save()
        Start-Process "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
    }
}

function Invoke-BtnBufferbloat  { Start-Process "https://www.waveform.com/tools/bufferbloat" }
function Invoke-BtnPcBuildGuide { Start-Process "https://pcpartpicker.com/user/fr33thy/saved" }