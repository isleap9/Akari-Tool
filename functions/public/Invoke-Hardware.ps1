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

function Invoke-BtnMonitorOpt { Invoke-UltimateScript -Path "7 Hardware/6 Monitor Optimization.ps1" -Status "Monitor Optimization opened." }

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

function Invoke-BtnMouseTest       { Invoke-UltimateScript -Path "7 Hardware/3 Mouse Polling Rate Test.ps1" -Status "Mouse Polling Rate Test opened." }
function Invoke-BtnControllerOC     { Invoke-UltimateScript -Path "7 Hardware/4 Controller Overclock.ps1" -Status "Controller Overclock (hidusbf) opened." }
function Invoke-BtnControllerTest   { Invoke-UltimateScript -Path "7 Hardware/5 Controller Polling Rate Test.ps1" -Status "Controller Polling Rate Test opened." }

function Invoke-BtnBufferbloat  { Start-Process "https://www.waveform.com/tools/bufferbloat" }
function Invoke-BtnPcBuildGuide { Start-Process "https://pcpartpicker.com/user/fr33thy/saved" }
