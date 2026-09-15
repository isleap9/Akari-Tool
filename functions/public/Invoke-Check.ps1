# Check PC — install OCCT, create shortcuts, launch, show guidance
function Invoke-BtnCheckPC {
    Invoke-RunInBackground -StatusStart "Installing OCCT..." -StatusDone "OCCT installed & launched." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code OCBase.OCCT.Personal_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"OCBase.OCCT.Personal`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\OCBase.OCCT.Personal_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\OCCT.lnk"); $sc.TargetPath = "$base\OCCT.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\OCCT.lnk"); $sc2.TargetPath = "$base\OCCT.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Start-Process "$base\OCCT.exe"
    }
    $guidance = @"
DRIVES
- Keep drives at least 10% free
- Check drive errors and device health

RAM
- Check RAM profile is enabled
- Verify RAM is in the correct slots
- Confirm there is no mismatch in RAM modules
- At least two RAM sticks (dual channel) is ideal

GPU
- Check Video Bus is at maximum
- Check Resizable BAR is enabled
- Verify monitor cable is connected to the GPU
- Confirm GPU is in the top PCIe motherboard slot

TEST
Run a CPU, RAM & GPU stress test to check for errors.
Keep an eye on temps and WHEA errors during this test.
Errors should not be ignored as they can lead to stutters, hitches, corrupted Windows, poor performance, black/blue screens, input lag, and shutdowns.

TROUBLESHOOTING
- RAM overheating? Typically over 55deg (fix case flow/ram fan)
- Unlucky CPU memory controller? (lower RAM speed)
- CPU overheating? (repaste/retighten/RMA cooler)
- Overclock? (turn it off/dial it down)
- BIOS bugged out? (clear CMOS)
- Incompatible RAM? (check QVL)
- Faulty RAM stick or motherboard? (RMA)
"@
    [System.Windows.MessageBox]::Show($guidance, "PC Check Guidance", "OK", "Information")
}

# Check BIOS — enable password sign-in, search motherboard, show BIOS tips
function Invoke-BtnCheckBios {
    # Allow password sign-in (disables passwordless requirement)
    cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device`" /v `"DevicePasswordLessBuildVersion`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    # Search motherboard ID in web browser
    $instanceID = (Get-CimInstance Win32_BaseBoard).Product
    $query = [uri]::EscapeDataString($instanceID)
    Start-Process "https://www.google.com/search?q=$query"
    $tips = @"
UPDATE BIOS & OPTIMIZE SETTINGS

INTEL CPU
- ENABLE ram profile (XMP DOCP EXPO)
- DISABLE c-state (K CHIPS ONLY)
- ENABLE resizable bar (REBAR C.A.M)

AMD CPU
- ENABLE ram profile (XMP DOCP EXPO)
- ENABLE precision boost overdrive (PBO)
- ENABLE resizable bar (REBAR C.A.M)

DISABLE unused features (BT/WIFI/IGPU/ETC)
DISABLE driver installer software (Armory Crate / MSI Utility / Gigabyte Update / Asrock Utility)
MAX pump and set fans to performance
"@
    $r = [System.Windows.MessageBox]::Show("$tips`n`nRestart to BIOS now?", "BIOS Settings", "YesNo", "Information")
    if ($r -eq "Yes") { shutdown /r /fw /t 0 }
}
