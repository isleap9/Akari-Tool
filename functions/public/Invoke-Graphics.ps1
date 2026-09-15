# DDU (Driver Clean) — Auto / Manual (1:1 with "5 Graphics/1 Driver Clean.ps1")
function Invoke-BtnDduAuto {
    $r = [System.Windows.MessageBox]::Show("Clean ALL graphics drivers with DDU and restart? Runs in Safe Mode, auto-cleans with DDU's CleanAllGpus, and restarts into Windows. Continue?", "DDU: Auto", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "DDU Auto: preparing safe-mode clean..." -StatusDone "DDU Auto scheduled — restarting into Safe Mode." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Wagnardsoft.DisplayDriverUninstaller`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $dduDir = "$env:SystemDrive\Program Files (x86)\Display Driver Uninstaller"
        New-Item -Path $dduDir -Name "Settings" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddusettings" "$dduDir\Settings\Settings.xml"
        Set-ItemProperty -Path "$dduDir\Settings\Settings.xml" -Name IsReadOnly -Value $true
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\DriverSearching`" /v `"SearchOrderConfig`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller\Display Driver Uninstaller.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddu" "$env:SystemRoot\Temp\ddu.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*ddu`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\ddu.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnDduManual {
    $r = [System.Windows.MessageBox]::Show("Open DDU in Safe Mode for a manual clean? Runs in Safe Mode and opens the DDU GUI so you pick the options. Continue?", "DDU: Manual", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "DDU Manual: preparing safe-mode clean..." -StatusDone "DDU Manual scheduled — restarting into Safe Mode." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Wagnardsoft.DisplayDriverUninstaller`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $dduDir = "$env:SystemDrive\Program Files (x86)\Display Driver Uninstaller"
        New-Item -Path $dduDir -Name "Settings" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddusettings" "$dduDir\Settings\Settings.xml"
        Set-ItemProperty -Path "$dduDir\Settings\Settings.xml" -Name IsReadOnly -Value $true
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\DriverSearching`" /v `"SearchOrderConfig`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller\Display Driver Uninstaller.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddumanual" "$env:SystemRoot\Temp\ddumanual.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*ddumanual`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\ddumanual.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}

# Driver updated install — NVIDIA / AMD / Intel (1:1 with "5 Graphics/2 Driver Updated Install.ps1")
function Invoke-BtnDriverNvidia {
    Invoke-RunInBackground -StatusStart "Downloading latest NVIDIA driver..." -StatusDone "NVIDIA driver downloaded — installer opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $uri = 'https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=120&pfid=929&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1'
        try {
            $response = Invoke-WebRequest -Uri $uri -Method GET -UseBasicParsing
            $payload = $response.Content | ConvertFrom-Json
            $version = $payload.IDS[0].downloadInfo.Version
            $windowsVersion = if ([Environment]::OSVersion.Version -ge (New-Object 'Version' 9, 1)) { 'win10-win11' } else { 'win8-win7' }
            $windowsArchitecture = if ([Environment]::Is64BitOperatingSystem) { '64bit' } else { '32bit' }
            $url = "https://international.download.nvidia.com/Windows/$version/$version-desktop-$windowsVersion-$windowsArchitecture-international-dch-whql.exe"
            Invoke-WebRequest $url -OutFile "$env:SystemRoot\Temp\nvidiadriver.exe"
        } catch {
            Start-Process "https://www.nvidia.com/en-us/drivers"
            return
        }
        Start-Process "$env:SystemRoot\Temp\nvidiadriver.exe"
    }
}
function Invoke-BtnDriverAmd {
    Invoke-RunInBackground -StatusStart "Downloading latest AMD driver..." -StatusDone "AMD driver downloaded — installer opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try {
            $DownloadAmd = Invoke-WebRequest "https://www.amd.com/en/support/download/drivers.html" -UseBasicParsing |
                Select-Object -ExpandProperty Links |
                Where-Object { $_.href -match "drivers\.amd\.com/drivers/installer/.*/whql/amd-software-adrenalin-edition-.*-minimalsetup-.*_web\.exe" } | Select-Object href
            $spoofwebbrowser = @{
                "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                "Accept"     = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
                "Referer"    = "https://www.amd.com/"
            }
            Invoke-WebRequest $DownloadAmd.href -UseBasicParsing -Headers $spoofwebbrowser -OutFile "$env:SystemRoot\Temp\amddriver.exe" -ErrorAction SilentlyContinue | Out-Null
            Start-Process "$env:SystemRoot\Temp\amddriver.exe"
        } catch {
            Start-Process "https://www.amd.com/en/support/download/drivers.html"
        }
    }
}
function Invoke-BtnDriverIntel {
    Start-Process "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads&cf-downloadsppth=Graphics"
}

# ── Debloat driver install & settings (1:1 with "5 Graphics/4 Driver Debloat Install & Settings.ps1") ──
# Common tail used by all three vendors: AutoColorManagementEnabled, MSI mode,
# show all hidden taskbar icons, then restart.
function Invoke-BtnDriverDebloatNvidia {
    $r = [System.Windows.MessageBox]::Show("Download the NVIDIA driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded .exe. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: NVIDIA", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.nvidia.com/en-us/drivers"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded NVIDIA driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing NVIDIA driver..." -StatusDone "NVIDIA driver installed with settings. Restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        # download and install 7-zip
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        # extract driver
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemRoot\Temp\nvidiadriver" -y | Out-Null
        # debloat
        $RemoveList = "Display.Nview","FrameViewSDK","HDAudio","MSVCRT","NvApp.MessageBus","NvBackend","NvContainer","NvCpl","NvDLISR","NVPCF","NvTelemetry","NvVAD","PhysX","PPC","ShadowPlay"
        foreach ($d in $RemoveList) { Remove-Item "$env:SystemRoot\Temp\nvidiadriver\$d" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null }
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\CEF" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\osc" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\Plugins" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\UpgradeConsent" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\www" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\7z.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\7z.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\DarkModeCheck.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\InstallerExtension.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvApp.nvi" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvAppApi.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvAppExt.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvConfigGenerator.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        # install driver
        Start-Process "$env:SystemRoot\Temp\nvidiadriver\setup.exe" -ArgumentList "-s -noreboot -noeula -clean" -Wait -NoNewWindow
        # nvidia control panel
        try { Start-Process "winget" -ArgumentList "install `"9NF8H0H7WMLT`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sc2 = $sh.CreateShortcut("$Desktop\NVIDIA Control Panel.lnk"); $sc2.TargetPath = "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"; $sc2.WorkingDirectory = "shell:appsFolder"; $sc2.Save()
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\NVIDIA" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # settings
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "1" /f | Out-Null } }
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "1" /f | Out-Null } }
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RmProfilingAdminOnly" /t REG_DWORD /d "0" /f | Out-Null } }
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\NVIDIA Corporation\NvTray`" /v `"StartOnLogin`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        # nvidia profile inspector + profile
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sc3 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        $sc4 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc4.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc4.WorkingDirectory = $base; $sc4.Save()
        Write-Asset "inspector_default" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}
function Invoke-BtnDriverDebloatAmd {
    $r = [System.Windows.MessageBox]::Show("Download the AMD driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded .exe. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: AMD", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.amd.com/en/support/download/drivers.html"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded AMD driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing AMD driver..." -StatusDone "AMD driver installed with settings. Restarting." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemRoot\Temp\amddriver" -y | Out-Null
        # edit xml files, set enabled & hidden to false
        $xmlFiles = @(
            "$env:SystemRoot\Temp\amddriver\Config\AMDAUEPInstaller.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDCOMPUTE.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDLinkDriverUpdate.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDRELAUNCHER.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDScoSupportTypeUpdate.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDUpdater.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDUWPLauncher.xml",
            "$env:SystemRoot\Temp\amddriver\Config\EnableWindowsDriverSearch.xml",
            "$env:SystemRoot\Temp\amddriver\Config\InstallUEP.xml",
            "$env:SystemRoot\Temp\amddriver\Config\ModifyLinkUpdate.xml"
        )
        foreach ($file2 in $xmlFiles) {
            if (Test-Path $file2) {
                $content = Get-Content $file2 -Raw
                $content = $content -replace '<Enabled>true</Enabled>', '<Enabled>false</Enabled>'
                $content = $content -replace '<Hidden>true</Hidden>', '<Hidden>false</Hidden>'
                Set-Content $file2 -Value $content -NoNewline
            }
        }
        # edit json files, set installbydefault to no
        $jsonFiles = @(
            "$env:SystemRoot\Temp\amddriver\Config\InstallManifest.json",
            "$env:SystemRoot\Temp\amddriver\Bin64\cccmanifest_64.json"
        )
        foreach ($file2 in $jsonFiles) {
            if (Test-Path $file2) {
                $content = Get-Content $file2 -Raw
                $content = $content -replace '"InstallByDefault"\s*:\s*"Yes"', '"InstallByDefault" : "No"'
                Set-Content $file2 -Value $content -NoNewline
            }
        }
        # install driver
        Start-Process -Wait "$env:SystemRoot\Temp\amddriver\Bin64\ATISetup.exe" -ArgumentList "-INSTALL -VIEW:2" -WindowStyle Hidden
        Start-Sleep -Seconds 90
        # cleanup
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"AMDNoiseSuppression`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"StartRSX`" /f >nul 2>&1"
        Unregister-ScheduledTask -TaskName "StartCN" -Confirm:$false -ErrorAction SilentlyContinue
        cmd /c "sc stop `"AMD Crash Defender Service`" >nul 2>&1"; cmd /c "sc delete `"AMD Crash Defender Service`" >nul 2>&1"
        cmd /c "sc stop `"amdfendr`" >nul 2>&1"; cmd /c "sc delete `"amdfendr`" >nul 2>&1"
        cmd /c "sc stop `"amdfendrmgr`" >nul 2>&1"; cmd /c "sc delete `"amdfendrmgr`" >nul 2>&1"
        cmd /c "sc stop `"amdacpbus`" >nul 2>&1"; cmd /c "sc delete `"amdacpbus`" >nul 2>&1"
        cmd /c "sc stop `"AMDSAFD`" >nul 2>&1"; cmd /c "sc delete `"AMDSAFD`" >nul 2>&1"
        cmd /c "sc stop `"AtiHDAudioService`" >nul 2>&1"; cmd /c "sc delete `"AtiHDAudioService`" >nul 2>&1"
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\AMD Bug Report Tool" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Windows\SysWOW64\AMDBugReportTool.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        $findamdinstallmanager = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $amdinstallmanager = Get-ItemProperty $findamdinstallmanager -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like "*AMD Install Manager*" }
        if ($amdinstallmanager) { $guid = $amdinstallmanager.PSChildName; Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow }
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        $sc2 = $sh.CreateShortcut("$Desktop\AMD Radeon Software.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files\AMD\CNext\CNext"; $sc2.Save()
        $folderName = "AMD Software$([char]0xA789) Adrenalin Edition"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$folderName\$folderName.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$folderName" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:SystemDrive\AMD" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # import settings
        Start-Process "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"
        Start-Sleep -Seconds 15
        Stop-Process -Name "RadeonSoftware" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /t REG_SZ /d `"PROFILE_CUSTOM`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"3000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"TFQ`" /t REG_BINARY /d `"3200`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"3100`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"3200`" /f >nul 2>&1"
        }
        cmd /c "reg add `"HKCU\Software\AMD\CN\CustomResolutions`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\DisplayOverride`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"abmlevel`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" }
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\FreeSync`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\OverlayNotification`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}
function Invoke-BtnDriverDebloatIntel {
    $r = [System.Windows.MessageBox]::Show("Download the Intel driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded file. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: Intel", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads&cf-downloadsppth=Graphics"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded Intel driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing Intel driver..." -StatusDone "Intel driver installed with settings. Restarting." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemDrive\inteldriver" -y | Out-Null
        Start-Process "cmd.exe" -ArgumentList "/c `"$env:SystemDrive\inteldriver\Installer.exe`" -f --noExtras --terminateProcesses -s" -WindowStyle Hidden -Wait
        $IntelGraphicsSoftware = Get-ChildItem "$env:SystemDrive\inteldriver\Resources\Extras\IntelGraphicsSoftware_*.exe" | Select-Object -First 1 -ExpandProperty Name
        if ($IntelGraphicsSoftware) { Start-Process "$env:SystemDrive\inteldriver\Resources\Extras\$IntelGraphicsSoftware" -ArgumentList "/s" -Wait -NoNewWindow }
        $sc2 = $sh.CreateShortcut("$Desktop\Intel Graphics Software.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\IntelGraphicsSoftware.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software"; $sc2.Save()
        $FileName = "Intel$([char]0xAE) Graphics Software"
        cmd /c "reg delete `"HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"$FileName`" /f >nul 2>&1"
        cmd /c "sc stop `"IntelGFXFWupdateTool`" >nul 2>&1"; cmd /c "sc delete `"IntelGFXFWupdateTool`" >nul 2>&1"
        cmd /c "sc stop `"cplspcon`" >nul 2>&1"; cmd /c "sc delete `"cplspcon`" >nul 2>&1"
        cmd /c "sc stop `"CtaChildDriver`" >nul 2>&1"; cmd /c "sc delete `"CtaChildDriver`" >nul 2>&1"
        cmd /c "sc stop `"GSCAuxDriver`" >nul 2>&1"; cmd /c "sc delete `"GSCAuxDriver`" >nul 2>&1"
        cmd /c "sc stop `"GSCx64`" >nul 2>&1"; cmd /c "sc delete `"GSCx64`" >nul 2>&1"
        $stop = "IntelGraphicsSoftware", "PresentMonService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Remove-Item "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\PresentMonService.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel\Intel Graphics Software\$FileName.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:SystemDrive\Intel" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\inteldriver" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # create 3dkeys + settings
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath\3DKeys`" /f >nul 2>&1" } }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_AsyncFlipMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_LowLatency`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`" /v `"DirectXUserGlobalSettings`" /t REG_SZ /d `"SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;`" /f >nul 2>&1"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}

# GPU Settings — On / Default (1:1 with "5 Graphics/5 Nvidia Settings.ps1")
function Invoke-BtnNvidiaOn {
    Invoke-RunInBackground -StatusStart "Applying NVIDIA settings (On)..." -StatusDone "NVIDIA settings applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"9NF8H0H7WMLT`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\NVIDIA Control Panel.lnk"); $sc.TargetPath = "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"; $sc.WorkingDirectory = "shell:appsFolder"; $sc.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RmProfilingAdminOnly" /t REG_DWORD /d "0" /f | Out-Null } }
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\NVIDIA Corporation\NvTray`" /v `"StartOnLogin`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sc2 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        $sc3 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        Write-Asset "inspector_default" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        Start-Process "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
    }
}
function Invoke-BtnNvidiaDefault {
    Invoke-RunInBackground -StatusStart "Applying NVIDIA settings (Default)..." -StatusDone "NVIDIA settings restored." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { cmd /c "reg delete `"$key`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\NVIDIA Corporation\NvTray`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc2 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        $sc3 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        Write-Asset "inspector_empty" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        Start-Process "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
    }
}

# GPU Settings — AMD On / Default (1:1 with "5 Graphics/6 Amd Settings.ps1")
function Invoke-BtnAmdOn {
    Invoke-RunInBackground -StatusStart "Applying AMD settings (On)..." -StatusDone "AMD settings applied." -ScriptBlock {
        Start-Process "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"
        Start-Sleep -Seconds 30
        Stop-Process -Name "RadeonSoftware" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\AIM`" /v `"LaunchBugTool`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\DVR`" /v `"HotkeysDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\DVR`" /v `"ShowRSOverlay`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"RSXBrowserUnavailable`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AllowWebContent`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /t REG_SZ /d `"PROFILE_CUSTOM`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"3000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"TFQ`" /t REG_BINARY /d `"3200`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"3100`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"3200`" /f >nul 2>&1"
        }
        cmd /c "reg add `"HKCU\Software\AMD\CN\CustomResolutions`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\DisplayOverride`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"abmlevel`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsAutoDefault`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" } }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsComponentControl`" /t REG_BINARY /d `"0f000000`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\FreeSync`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\OverlayNotification`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnAmdDefault {
    Invoke-RunInBackground -StatusStart "Restoring AMD settings (Default)..." -StatusDone "AMD settings restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\AIM`" /v `"LaunchBugTool`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\DVR`" /v `"HotkeysDisabled`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\DVR`" /v `"ShowRSOverlay`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"RSXBrowserUnavailable`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AllowWebContent`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"31000000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg delete `"$regPath`" /v `"TFQ`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"360034000000`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"30000000`" /f >nul 2>&1"
        }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\CustomResolutions`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\DisplayOverride`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg delete `"$regPath`" /v `"abmlevel`" /f >nul 2>&1" }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsAutoDefault`" /t REG_DWORD /d `"1`" /f >nul 2>&1" } }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsComponentControl`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\FreeSync`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\OverlayNotification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /f >nul 2>&1"
    }
}

# GPU Settings — Intel On / Default (1:1 with "5 Graphics/7 Intel Settings.ps1")
function Invoke-BtnIntelOn {
    Invoke-RunInBackground -StatusStart "Applying Intel settings (On)..." -StatusDone "Intel settings applied." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath\3DKeys`" /f >nul 2>&1" } }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_AsyncFlipMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_LowLatency`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`" /v `"DirectXUserGlobalSettings`" /t REG_SZ /d `"SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;`" /f >nul 2>&1"
    }
}
function Invoke-BtnIntelDefault {
    Invoke-RunInBackground -StatusStart "Restoring Intel settings (Default)..." -StatusDone "Intel settings restored." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg delete `"$regPath\3DKeys`" /f >nul 2>&1" } }
    }
}

# HDCP
function Invoke-BtnHdcpOff {
    Invoke-RunInBackground -StatusStart "Disabling HDCP..." -StatusDone "HDCP disabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "1" /f | Out-Null } }
    }
}
function Invoke-BtnHdcpDefault {
    Invoke-RunInBackground -StatusStart "Enabling HDCP..." -StatusDone "HDCP enabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "0" /f | Out-Null } }
    }
}

# P0 State
function Invoke-BtnP0On {
    Invoke-RunInBackground -StatusStart "Enabling P0 state..." -StatusDone "P0 state enabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "1" /f | Out-Null } }
    }
}
function Invoke-BtnP0Default {
    Invoke-RunInBackground -StatusStart "Restoring P0 state..." -StatusDone "P0 state restored." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "0" /f | Out-Null } }
    }
}

# MSI Mode
function Invoke-BtnMsiOn {
    Invoke-RunInBackground -StatusStart "Enabling MSI mode for GPU..." -StatusDone "MSI mode enabled." -ScriptBlock {
        $gpus = Get-PnpDevice -Class Display
        foreach ($gpu in $gpus) {
            cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$($gpu.InstanceId)\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        }
    }
}
function Invoke-BtnMsiOff {
    Invoke-RunInBackground -StatusStart "Disabling MSI mode for GPU..." -StatusDone "MSI mode disabled." -ScriptBlock {
        $gpus = Get-PnpDevice -Class Display
        foreach ($gpu in $gpus) {
            cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$($gpu.InstanceId)\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        }
    }
}

# DirectX & C++
function Invoke-BtnDirectX {
    Invoke-RunInBackground -StatusStart "Installing DirectX..." -StatusDone "DirectX installed." -ScriptBlock {
        $progresspreference='silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.DirectX`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
    }
}
function Invoke-BtnCppRuntime {
    Invoke-RunInBackground -StatusStart "Installing C++ runtimes..." -StatusDone "C++ runtimes installed." -ScriptBlock {
        $progresspreference='silentlycontinue'
        $packages = @(
            "Microsoft.VCRedist.2005.x86","Microsoft.VCRedist.2005.x64",
            "Microsoft.VCRedist.2008.x86","Microsoft.VCRedist.2008.x64",
            "Microsoft.VCRedist.2010.x86","Microsoft.VCRedist.2010.x64",
            "Microsoft.VCRedist.2012.x86","Microsoft.VCRedist.2012.x64",
            "Microsoft.VCRedist.2013.x86","Microsoft.VCRedist.2013.x64",
            "Microsoft.VCRedist.2015+.x86","Microsoft.VCRedist.2015+.x64"
        )
        foreach ($pkg in $packages) { try { Start-Process "winget" -ArgumentList "install `"$pkg`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {} }
    }
}

function Invoke-BtnResolution { Start-Process "ms-settings:display" }
function Invoke-BtnHags       { Start-Process "ms-settings:display-advancedgraphics" }