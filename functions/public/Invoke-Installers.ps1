# Per-app installers — 1:1 with Ultimate "4 Installers/1 Installers.ps1"
# (winget install + debloat config + shortcut cleanup), wrapped in Akari's GUI runner.

# ── Steam ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInstSteam {
    Invoke-RunInBackground -StatusStart "Installing Steam..." -StatusDone "Steam installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Valve.Steam`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"Steam`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Steam\Steam.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Steam" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Epic Games ────────────────────────────────────────────────────────────────
function Invoke-BtnInstEpic {
    Invoke-RunInBackground -StatusStart "Installing Epic Games..." -StatusDone "Epic Games installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"EpicGames.EpicGamesLauncher`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"EpicGamesLauncher`" /f >nul 2>&1"
    }
}

# ── Battle.net ────────────────────────────────────────────────────────────────
function Invoke-BtnInstBattlenet {
    Invoke-RunInBackground -StatusStart "Installing Battle.net..." -StatusDone "Battle.net installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Blizzard.BattleNet`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade --location `"$env:SystemDrive\Program Files (x86)\Battle.net`"" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "getinstaller" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"Battle.net`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"Battle.net`" /f >nul 2>&1"
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battle.net" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Battle.net.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\Battle.net\Battle.net Launcher.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Battle.net"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battle.net.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\Battle.net\Battle.net Launcher.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Battle.net"; $sc2.Save()
    }
}

# ── Electronic Arts ───────────────────────────────────────────────────────────
function Invoke-BtnInstEA {
    Invoke-RunInBackground -StatusStart "Installing EA App..." -StatusDone "EA App installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"ElectronicArts.EADesktop`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "EAappInstaller" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"EADM`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"EADM`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\EA\EA.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\EA" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Ubisoft Connect ───────────────────────────────────────────────────────────
function Invoke-BtnInstUbisoft {
    Invoke-RunInBackground -StatusStart "Installing Ubisoft Connect..." -StatusDone "Ubisoft Connect installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Ubisoft.Connect`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Ubisoft\Ubisoft Connect\Ubisoft Connect.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Ubisoft" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Rockstar Games ────────────────────────────────────────────────────────────
function Invoke-BtnInstRockstar {
    Invoke-RunInBackground -StatusStart "Installing Rockstar Games..." -StatusDone "Rockstar Games installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RockstarGames.Launcher`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Rockstar Games\Rockstar Games Launcher.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Rockstar Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── League of Legends ─────────────────────────────────────────────────────────
function Invoke-BtnInstLOL {
    Invoke-RunInBackground -StatusStart "Installing League of Legends..." -StatusDone "League of Legends installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RiotGames.LeagueOfLegends.NA`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"RiotClient`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games\*" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Valorant ──────────────────────────────────────────────────────────────────
function Invoke-BtnInstValorant {
    Invoke-RunInBackground -StatusStart "Installing Valorant..." -StatusDone "Valorant installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RiotGames.Valorant.NA`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"RiotClient`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games\*" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Escape from Tarkov ────────────────────────────────────────────────────────
function Invoke-BtnInstEFT {
    Invoke-RunInBackground -StatusStart "Installing Escape From Tarkov..." -StatusDone "Escape From Tarkov installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        Invoke-WebRequest "https://launcher.escapefromtarkov.com/launcher/download" -OutFile "$env:SystemRoot\Temp\Escape From Tarkov.exe"
        Start-Process -Wait "$env:SystemRoot\Temp\Escape From Tarkov.exe" -ArgumentList "/VERYSILENT /NORESTART"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Battlestate Games Launcher.lnk"); $sc.TargetPath = "$env:SystemDrive\Battlestate Games\BsgLauncher\BsgLauncher.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Battlestate Games\BsgLauncher"; $sc.Save()
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battlestate Games\Battlestate Games Launcher.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battlestate Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Roblox ────────────────────────────────────────────────────────────────────
function Invoke-BtnInstRoblox {
    Invoke-RunInBackground -StatusStart "Installing Roblox..." -StatusDone "Roblox installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Roblox.Roblox`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sh = New-Object -ComObject WScript.Shell
        $sc = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Roblox.url"); $sc.TargetPath = "roblox://placeId=0"; $sc.Save()
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc2 = $sh.CreateShortcut("$Desktop\Roblox.url"); $sc2.TargetPath = "roblox://placeId=0"; $sc2.Save()
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Roblox" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Google Chrome ─────────────────────────────────────────────────────────────
function Invoke-BtnInstChrome {
    Invoke-RunInBackground -StatusStart "Installing Google Chrome..." -StatusDone "Chrome installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Google.Chrome`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist`" /v `"1`" /t REG_SZ /d `"ddkjiahejlhfcafbddmgiahcphecmpfh;https://clients2.google.com/service/update2/crx`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKLM:\Software\Microsoft\Active Setup\Installed Components"
        Get-ChildItem $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            $val = (Get-ItemProperty $_.PsPath)."(default)"
            if ($val -like "*Chrome*") { Remove-Item $_.PsPath -Force -ErrorAction SilentlyContinue }
        }
        Get-Service | Where-Object { $_.Name -match 'Google' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Google*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
    }
}

# ── Brave ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInstBrave {
    Invoke-RunInBackground -StatusStart "Installing Brave..." -StatusDone "Brave installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Brave.Brave`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallForcelist`" /v `"1`" /t REG_SZ /d `"ddkjiahejlhfcafbddmgiahcphecmpfh;https://clients2.google.com/service/update2/crx`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Get-Item $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($valueName in $_.GetValueNames()) { if ($valueName -like "*Brave*") { Remove-ItemProperty -Path $_.PsPath -Name $valueName -Force -ErrorAction SilentlyContinue } }
        }
        Get-Service | Where-Object { $_.Name -match 'Brave' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Brave*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Brave.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Firefox ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstFirefox {
    Invoke-RunInBackground -StatusStart "Installing Firefox..." -StatusDone "Firefox installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Mozilla.Firefox`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Process -FilePath "C:\Program Files (x86)\Mozilla Maintenance Service\uninstall.exe" -ArgumentList "/S" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
        Get-ScheduledTask | Where-Object { $_.Taskname -match 'Firefox' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        $uBlockDir = "C:\Program Files\Mozilla Firefox\distribution\extensions"
        If (!(Test-Path $uBlockDir)) { New-Item -ItemType Directory -Path $uBlockDir -Force | Out-Null }
        Invoke-WebRequest "https://addons.mozilla.org/firefox/downloads/latest/ublock-origin/latest.xpi" -OutFile "$uBlockDir\uBlock0@raymondhill.net.xpi"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Mozilla\Firefox`" /v `"AppAutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Start-Process -FilePath "$env:SystemDrive\Program Files\Mozilla Firefox\firefox.exe" -ArgumentList "--headless"
        Start-Sleep -Seconds 5
        Stop-Process -Name "firefox" -Force -ErrorAction SilentlyContinue
        $JsFile = @'
user_pref("layers.acceleration.disabled", true);
user_pref("gfx.direct2d.disabled", true);
'@
        $FireFoxProfile = Get-ChildItem "$env:APPDATA\Mozilla\Firefox\Profiles" -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '\.default-release$' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($FireFoxProfile) { [System.IO.File]::WriteAllText("$($FireFoxProfile.FullName)\user.js", $JsFile, [System.Text.UTF8Encoding]::new($false)) }
        $basePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Get-Item $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($valueName in $_.GetValueNames()) { if ($valueName -like "*Firefox*") { Remove-ItemProperty -Path $_.PsPath -Name $valueName -Force -ErrorAction SilentlyContinue } }
        }
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Firefox Private Browsing.lnk" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Firefox.lnk" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Discord ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstDiscord {
    Invoke-RunInBackground -StatusStart "Installing Discord..." -StatusDone "Discord installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        New-Item -Path "$env:APPDATA\discord\settings.json" -ItemType File -Force | Out-Null
        $DiscordSettings = @'
{
    "SKIP_HOST_UPDATE": true,
    "DEVELOPER_MODE": true,
    "enableHardwareAcceleration": false,
    "MINIMIZE_TO_TRAY": true,
    "OPEN_ON_STARTUP": false,
    "START_MINIMIZED": false,
    "IS_MAXIMIZED": true,
    "IS_MINIMIZED": false,
    "debugLogging": false
}
'@
        Set-Content -Path "$env:APPDATA\discord\settings.json" -Value $DiscordSettings -Force | Out-Null
        try { Start-Process "winget" -ArgumentList "install `"Discord.Discord`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "Update" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"Discord`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"Discord`" /f >nul 2>&1"
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Discord.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Discord Inc" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Spotify ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstSpotify {
    Invoke-RunInBackground -StatusStart "Installing Spotify..." -StatusDone "Spotify installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        New-Item -Path "$env:APPDATA\Spotify\prefs" -ItemType File -Force | Out-Null
        $SpotifySettingsPrefs = @'
app.autostart-configured=true
app.autostart-mode="off"
ui.hardware_acceleration=false
'@
        Set-Content -Path "$env:APPDATA\Spotify\prefs" -Value $SpotifySettingsPrefs -Force | Out-Null
        $tempDir = (([System.IO.Path]::GetTempPath())).TrimEnd('\')
        Invoke-WebRequest "https://download.scdn.co/SpotifySetup.exe" -OutFile "$tempDir\Spotify.exe"
        Start-Process "explorer.exe" -ArgumentList "$tempDir\Spotify.exe"
        Start-Sleep -Seconds 5
        Get-Process -Name "Spotify" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Spotify.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── OBS Studio ────────────────────────────────────────────────────────────────
function Invoke-BtnInstOBS {
    Invoke-RunInBackground -StatusStart "Installing OBS Studio..." -StatusDone "OBS Studio installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"OBSProject.OBSStudio`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
    }
}

# ── Notepad++ ────────────────────────────────────────────────────────────────
function Invoke-BtnInstNotepad {
    Invoke-RunInBackground -StatusStart "Installing Notepad++..." -StatusDone "Notepad++ installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Notepad++.Notepad++`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        New-Item -Path "$env:AppData" -Name "Notepad++" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        $NotePadConfig = @'
<?xml version="1.0" encoding="UTF-8" ?>
<NotepadPlus>
    <GUIConfigs>
        <GUIConfig name="ToolBar" visible="yes">small</GUIConfig>
        <GUIConfig name="StatusBar">show</GUIConfig>
        <GUIConfig name="TabSetting" replaceBySpace="no" size="4" />
        <GUIConfig name="noUpdate" intervalDays="15" nextUpdateDate="20260401" autoUpdateMode="0">yes</GUIConfig>
        <GUIConfig name="Auto-detection">yes</GUIConfig>
        <GUIConfig name="TrayIcon">0</GUIConfig>
        <GUIConfig name="RememberLastSession">no</GUIConfig>
        <GUIConfig name="DetectEncoding">yes</GUIConfig>
        <GUIConfig name="NewDocDefaultSettings" format="0" encoding="4" lang="0" codepage="-1" openAnsiAsUTF8="yes" addNewDocumentOnStartup="no" />
        <GUIConfig name="DarkMode" enable="yes" colorTone="0" enableWindowsMode="no" darkThemeName="DarkModeDefault.xml" />
        <GUIConfig name="MenuBar">show</GUIConfig>
    </GUIConfigs>
    <History nbMaxFile="0" inSubMenu="no" customLength="-1" />
</NotepadPlus>
'@
        Set-Content -Path "$env:AppData\Notepad++\config.xml" -Value $NotePadConfig -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Notepad++.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\Notepad++\notepad++.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\Notepad++"; $sc.Save()
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad++\Notepad++.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad++" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── 7-Zip ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInst7Zip {
    Invoke-RunInBackground -StatusStart "Installing 7-Zip..." -StatusDone "7-Zip installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
    }
}

# ── GOG launcher ──────────────────────────────────────────────────────────────
function Invoke-BtnInstGOG {
    Invoke-RunInBackground -StatusStart "Installing GOG Galaxy..." -StatusDone "GOG Galaxy installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"GOG.Galaxy`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"GalaxyClient`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"GogGalaxy`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\GOG.com\GOG GALAXY\GOG GALAXY.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\GOG.com" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── PotPlayer ────────────────────────────────────────────────────────────────
function Invoke-BtnInstPotPlayer {
    Invoke-RunInBackground -StatusStart "Installing PotPlayer..." -StatusDone "PotPlayer installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Daum.PotPlayer`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\PotPlayer\PotPlayer 64 bit.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\PotPlayer" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Onboard Memory Manager ────────────────────────────────────────────────────
function Invoke-BtnInstOMM {
    Invoke-RunInBackground -StatusStart "Installing Onboard Memory Manager..." -StatusDone "Onboard Memory Manager installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Logitech.OnboardMemoryManager_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Logitech.OnboardMemoryManager`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Logitech.OnboardMemoryManager_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Onboard Memory Manager.lnk"); $sc.TargetPath = "$base\OnboardMemoryManager.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Onboard Memory Manager.lnk"); $sc2.TargetPath = "$base\OnboardMemoryManager.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
    }
}

# ── FrameView ────────────────────────────────────────────────────────────────
function Invoke-BtnInstFrameView {
    Invoke-RunInBackground -StatusStart "Installing FrameView..." -StatusDone "FrameView installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Nvidia.FrameView`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA FrameView\FrameView.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA FrameView" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── NVIDIA App ────────────────────────────────────────────────────────────────
function Invoke-BtnInstNvApp {
    Invoke-RunInBackground -StatusStart "Installing NVIDIA App..." -StatusDone "NVIDIA App installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"XP8CLZL93F5Z4P`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation\NVIDIA App.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Helium ────────────────────────────────────────────────────────────────────
function Invoke-BtnInstHelium {
    Invoke-RunInBackground -StatusStart "Installing Helium..." -StatusDone "Helium installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"ImputNet.Helium`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKLM:\Software\Microsoft\Active Setup\Installed Components"
        Get-ChildItem $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            $val = (Get-ItemProperty $_.PsPath)."(default)"
            if ($val -like "*Helium*") { Remove-Item $_.PsPath -Force -ErrorAction SilentlyContinue }
        }
        Get-Service | Where-Object { $_.Name -match 'Helium' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Helium*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Helium.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}
