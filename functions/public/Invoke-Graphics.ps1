# DDU (Driver Clean) — Auto / Manual menu, delegates to live upstream
function Invoke-BtnDduAuto   { Invoke-UltimateScript -Path "5 Graphics/1 Driver Clean.ps1" -Status "DDU (Driver Clean) opened — pick Auto." }
function Invoke-BtnDduManual { Invoke-UltimateScript -Path "5 Graphics/1 Driver Clean.ps1" -Status "DDU (Driver Clean) opened — pick Manual." }

# Driver updated install — NVIDIA / AMD / Intel menu, delegates to live upstream
function Invoke-BtnDriverNvidia { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick NVIDIA." }
function Invoke-BtnDriverAmd    { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick AMD." }
function Invoke-BtnDriverIntel  { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick Intel." }

# Debloat driver install & settings — delegates to live upstream
function Invoke-BtnDriverDebloatNvidia { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick NVIDIA." }
function Invoke-BtnDriverDebloatAmd    { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick AMD." }
function Invoke-BtnDriverDebloatIntel  { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick Intel." }

# GPU Settings — full upstream scripts (On/Default menu), delegates to live upstream
function Invoke-BtnNvidiaOn      { Invoke-UltimateScript -Path "5 Graphics/5 Nvidia Settings.ps1" -Status "NVIDIA settings opened — pick On." }
function Invoke-BtnNvidiaDefault { Invoke-UltimateScript -Path "5 Graphics/5 Nvidia Settings.ps1" -Status "NVIDIA settings opened — pick Default." }
function Invoke-BtnAmdOn         { Invoke-UltimateScript -Path "5 Graphics/6 Amd Settings.ps1" -Status "AMD settings opened — pick On." }
function Invoke-BtnAmdDefault    { Invoke-UltimateScript -Path "5 Graphics/6 Amd Settings.ps1" -Status "AMD settings opened — pick Default." }
function Invoke-BtnIntelOn       { Invoke-UltimateScript -Path "5 Graphics/7 Intel Settings.ps1" -Status "Intel settings opened — pick On." }
function Invoke-BtnIntelDefault  { Invoke-UltimateScript -Path "5 Graphics/7 Intel Settings.ps1" -Status "Intel settings opened — pick Default." }

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
