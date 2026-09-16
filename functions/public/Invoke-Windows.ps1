# Windows tweaks run inline (no menu console). Logic mirrors the upstream
# Ultimate scripts; each button applies exactly one option. Pure openers inline.

# Taskbar / Start Menu
function Invoke-BtnTaskbarClean {
    Invoke-RunInBackground -StatusStart "Cleaning Start Menu & Taskbar..." -StatusDone "Start Menu & Taskbar cleaned." -ScriptBlock {
        $reg = @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh]
"AllowNewsAndInterests"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAl"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search]
"SearchboxTaskbarMode"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowTaskViewButton"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarMn"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowCopilotButton"=dword:00000000

[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds]
"EnableFeeds"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideSCAMeetNow"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex:07,00,00,00,05,db,8a,69,8a,49,d9,01

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=dword:00000000

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]
"HideRecommendedSection"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]
"IsEducationEnvironment"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=dword:00000001

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
'@
        Set-Content -Path "$env:SystemRoot\Temp\taskbarclean.reg" -Value $reg -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\taskbarclean.reg`"" -WindowStyle Hidden

        cmd /c "reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband /f >nul 2>&1"
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch" -ErrorAction SilentlyContinue | Out-Null

        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }

        $folders = @(
            "$env:USERPROFILE\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessories"
        )
        foreach ($folder in $folders) {
            if (Test-Path $folder) {
                cmd /c "attrib +h `"$folder`" >nul 2>&1"
                cmd /c "attrib +h `"$folder\*.*`" /s /d >nul 2>&1"
            }
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        $xml = @'
<LayoutModificationTemplate xmlns:defaultlayout="http://schemas.microsoft.com/Start/2014/FullDefaultLayout" xmlns:start="http://schemas.microsoft.com/Start/2014/StartLayout" Version="1" xmlns:taskbar="http://schemas.microsoft.com/Start/2014/TaskbarLayout" xmlns="http://schemas.microsoft.com/Start/2014/LayoutModification">
    <LayoutOptions StartTileGroupCellWidth="6" />
    <DefaultLayoutOverride>
        <StartLayoutCollection>
            <defaultlayout:StartLayout GroupCellWidth="6" />
        </StartLayoutCollection>
    </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@
        Set-Content -Path "C:\Windows\StartMenuLayout.xml" -Value $xml -Force -Encoding ASCII

        $layoutFile = "C:\Windows\StartMenuLayout.xml"
        $regAliases = @("HKLM", "HKCU")
        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            IF(!(Test-Path -Path $keyPath)) { New-Item -Path $basePath -Name "Explorer" | Out-Null }
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 1 | Out-Null
            Set-ItemProperty -Path $keyPath -Name "StartLayoutFile" -Value $layoutFile | Out-Null
        }

        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
        Start-Sleep -Seconds 5

        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 0
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin" -ErrorAction SilentlyContinue | Out-Null
        $start2 = '-----BEGIN CERTIFICATE-----
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
TSz1QZdkum5Y3uC7wc7paXh1rm11nwluCC7jiA==
-----END CERTIFICATE-----
'
        New-Item "$env:SystemRoot\Temp\start2.txt" -Value $start2 -Force -ErrorAction SilentlyContinue | Out-Null
        certutil.exe -decode "$env:SystemRoot\Temp\start2.txt" "$env:SystemRoot\Temp\start2.bin" >$null
        Copy-Item "$env:SystemRoot\Temp\start2.bin" -Destination "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState" -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Start`" /v `"AllAppsViewMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnTaskbarDefault {
    Invoke-RunInBackground -StatusStart "Restoring taskbar defaults..." -StatusDone "Taskbar defaults restored." -ScriptBlock {
        $reg = @'
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Taskband\AuxilliaryPins]
"MailPin"=dword:00000001

[-HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh]

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAl"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search]
"SearchboxTaskbarMode"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowTaskViewButton"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarMn"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowCopilotButton"=-

[-HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds]

[-HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex:04,00,00,00,00,00,00,00,00,00,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=-

[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
'@
        Set-Content -Path "$env:SystemRoot\Temp\taskbardefault.reg" -Value $reg -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\taskbardefault.reg`"" -WindowStyle Hidden

        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 0 -Force }
        }

        $folders = @(
            "$env:USERPROFILE\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessories"
        )
        foreach ($folder in $folders) {
            if (Test-Path $folder) {
                cmd /c "attrib -h `"$folder`" >nul 2>&1"
                cmd /c "attrib -h `"$folder\*.*`" /s /d >nul 2>&1"
            }
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        $xml = @'
<LayoutModificationTemplate xmlns:defaultlayout="http://schemas.microsoft.com/Start/2014/FullDefaultLayout" xmlns:start="http://schemas.microsoft.com/Start/2014/StartLayout" Version="1" xmlns="http://schemas.microsoft.com/Start/2014/LayoutModification">
  <LayoutOptions StartTileGroupCellWidth="6" />
  <DefaultLayoutOverride>
    <StartLayoutCollection>
      <defaultlayout:StartLayout GroupCellWidth="6">
        <start:Group Name="Productivity">
          <start:Folder Name="" Size="2x2" Column="2" Row="0">
            <start:Tile Size="2x2" Column="4" Row="2" AppUserModelID="Microsoft.Office.OneNote_8wekyb3d8bbwe!microsoft.onenoteim" />
            <start:DesktopApplicationTile Size="2x2" Column="0" Row="2" DesktopApplicationLinkPath="%APPDATA%\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk" />
            <start:Tile Size="2x2" Column="0" Row="4" AppUserModelID="Microsoft.SkypeApp_kzf8qxf38zg5c!App" />
          </start:Folder>
          <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe!Microsoft.MicrosoftOfficeHub" />
          <start:DesktopApplicationTile Size="2x2" Column="0" Row="2" DesktopApplicationLinkPath="%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk" />
          <start:Tile Size="2x2" Column="4" Row="2" AppUserModelID="7EE7776C.LinkedInforWindows_w1wdnht996qgy!App" />
          <start:Tile Size="2x2" Column="4" Row="0" AppUserModelID="microsoft.windowscommunicationsapps_8wekyb3d8bbwe!Microsoft.WindowsLive.Mail" />
          <start:Tile Size="2x2" Column="2" Row="2" AppUserModelID="Microsoft.Windows.Photos_8wekyb3d8bbwe!App" />
        </start:Group>
        <start:Group Name="Explore">
          <start:Folder Name="Play" Size="2x2" Column="4" Row="2">
            <start:Tile Size="2x2" Column="2" Row="0" AppUserModelID="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" />
            <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Clipchamp.Clipchamp_yxz26nhyzhsrt!App" />
          </start:Folder>
          <start:Tile Size="2x2" Column="4" Row="0" AppUserModelID="Microsoft.Todos_8wekyb3d8bbwe!App" />
          <start:Tile Size="2x2" Column="2" Row="2" AppUserModelID="Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe!App" />
          <start:Tile Size="2x2" Column="2" Row="0" AppUserModelID="SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" />
          <start:Tile Size="2x2" Column="0" Row="2" AppUserModelID="Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo" />
          <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Microsoft.WindowsStore_8wekyb3d8bbwe!App" />
        </start:Group>
      </defaultlayout:StartLayout>
    </StartLayoutCollection>
  </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@
        Set-Content -Path "C:\Windows\StartMenuLayout.xml" -Value $xml -Force -Encoding ASCII

        $layoutFile = "C:\Windows\StartMenuLayout.xml"
        $regAliases = @("HKLM", "HKCU")
        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            IF(!(Test-Path -Path $keyPath)) { New-Item -Path $basePath -Name "Explorer" | Out-Null }
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 1 | Out-Null
            Set-ItemProperty -Path $keyPath -Name "StartLayoutFile" -Value $layoutFile | Out-Null
        }

        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
        Start-Sleep -Seconds 5

        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 0
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin" -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Start`" /v `"AllAppsViewMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
    }
}

# Start Menu Layout
function Invoke-BtnStartMenu25H2 {
    Invoke-RunInBackground -StatusStart "Applying 25H2 Start Menu layout..." -StatusDone "25H2 Start Menu layout applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\newstartmenu.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
'@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\newstartmenu.reg`"" -WindowStyle Hidden
    }
}
function Invoke-BtnStartMenu24H2 {
    Invoke-RunInBackground -StatusStart "Applying 24H2 Start Menu layout..." -StatusDone "24H2 Start Menu layout applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\oldstartmenu.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
'@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\oldstartmenu.reg`"" -WindowStyle Hidden
    }
}

# Start Menu Shortcuts
function Invoke-BtnStartShortcuts {
    Invoke-RunInBackground -StatusStart "Creating Start Menu & Startup shortcuts..." -StatusDone "Shortcuts created." -ScriptBlock {
        $Wsh = New-Object -comObject WScript.Shell
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 1.lnk")
        $s.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 2.lnk")
        $s.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 1.lnk")
        $s.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 2.lnk")
        $s.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Recycle Bin.lnk")
        $s.TargetPath = '::{645ff040-5081-101b-9f08-00aa002f954e}'
        $s.Save()
        Start-Process "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
        Start-Process "$env:AppData\Microsoft\Windows\Start Menu\Programs"
    }
}

# Context Menu
function Invoke-BtnContextClean {
    Invoke-RunInBackground -StatusStart "Cleaning context menu..." -StatusDone "Context menu cleaned." -ScriptBlock {
        cmd /c "reg add `"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32`" /ve /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoCustomizeThisFolder`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\Folder\shell\pintohome`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\*\shell\pintohomefile`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\exefile\shellex\ContextMenuHandlers\Compatibility`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{9F156763-7844-4DC4-B2B1-901F640F5155}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{09A47860-11B0-4DA5-AFA5-26D86198A780}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\Folder\ShellEx\ContextMenuHandlers\Library Location`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"NoPreviousVersionsPage`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\UserLibraryFolder\shellex\ContextMenuHandlers\SendTo`" /f >nul 2>&1"
    }
}
function Invoke-BtnContextDefault {
    Invoke-RunInBackground -StatusStart "Restoring context menu defaults..." -StatusDone "Context menu restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoCustomizeThisFolder`" /f >nul 2>&1"
        $reg = @"
Windows Registry Editor Version 5.00

[HKEY_CLASSES_ROOT\Folder\shell\pintohome]
"AppliesTo"="System.ParsingName:<>\"::{f874310e-b6b7-47dc-bc84-b9e6b38f5903}\" AND System.ParsingName:<>\"::{679f85cb-0220-4080-b29b-5540cc05aab6}\" AND System.IsFolder:=System.StructuredQueryType.Boolean#True"
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51601"
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\Folder\shell\pintohome\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"

[HKEY_CLASSES_ROOT\*\shell\pintohomefile]
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51608"
"NeverDefault"=""
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\*\shell\pintohomefile\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"@
        Set-Content -Path "$env:SystemRoot\Temp\contextmenudefault.reg" -Value $reg -Force
        Regedit.exe /S "$env:SystemRoot\Temp\contextmenudefault.reg"
        cmd /c "reg add `"HKCR\exefile\shellex\ContextMenuHandlers\Compatibility`" /ve /t REG_SZ /d `"{1d27f844-3a1f-4410-85ac-14651078412d}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\Folder\ShellEx\ContextMenuHandlers\Library Location`" /ve /t REG_SZ /d `"{3dad6c5d-2167-4cae-9914-f99e41c12cfa}`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing`" /ve /t REG_SZ /d `"{e2bf9676-5f8f-435c-97eb-11607a5bedf7}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"NoPreviousVersionsPage`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\UserLibraryFolder\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
    }
}

# Theme / Black cosmetics
function Invoke-BtnThemeBlack {
    Invoke-RunInBackground -StatusStart "Applying black theme..." -StatusDone "Black theme applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\blacktheme.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize]
"AppsUseLightTheme"=dword:00000000
"ColorPrevalence"=dword:00000001
"EnableTransparency"=dword:00000000
"SystemUsesLightTheme"=dword:00000000

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize]
"AppsUseLightTheme"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent]
"AccentPalette"=hex:64,64,64,00,6b,6b,6b,00,00,00,00,00,00,00,00,00,00,00,00,\
  00,00,00,00,00,00,00,00,00,00,00,00,00
"StartColorMenu"=dword:00000000
"AccentColorMenu"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"EnableWindowColorization"=dword:00000001
"AccentColor"=dword:ff191919
"ColorizationColor"=dword:c4191919
"ColorizationAfterglow"=dword:c4191919

[HKEY_CURRENT_USER\Control Panel\Colors]
"Background"="0 0 0"
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\blacktheme.reg`"" -WindowStyle Hidden
    }
}
function Invoke-BtnWallpaperBlack {
    Invoke-RunInBackground -StatusStart "Making wallpaper / lockscreen black..." -StatusDone "Black wallpaper applied." -ScriptBlock {
        Add-Type -AssemblyName System.Windows.Forms
        $screenWidth = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Width
        $screenHeight = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Height
        Add-Type -AssemblyName System.Drawing
        $file = "C:\Windows\Black.jpg"
        $edit = New-Object System.Drawing.Bitmap $screenWidth, $screenHeight
        $graphics = [System.Drawing.Graphics]::FromImage($edit)
        $graphics.FillRectangle([System.Drawing.Brushes]::Black, 0, 0, $edit.Width, $edit.Height)
        $graphics.Dispose()
        $edit.Save($file)
        $edit.Dispose()
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP`" /v `"LockScreenImagePath`" /t REG_SZ /d `"C:\Windows\Black.jpg`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP`" /v `"LockScreenImageStatus`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Wallpaper`" /t REG_SZ /d `"C:\Windows\Black.jpg`" /f >nul 2>&1"
        rundll32.exe user32.dll, UpdatePerUserSystemParameters
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\System`" /v `"DisableAcrylicBackgroundOnLogon`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnAccountBlack {
    Invoke-RunInBackground -StatusStart "Making account pictures black..." -StatusDone "Account pictures black." -ScriptBlock {
        if (!(Test-Path "$env:SystemDrive\ProgramData\User Account Pictures")) {
            Copy-Item "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures" -Destination "$env:SystemDrive\ProgramData" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        }
        $accountPicturesPath = "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures"
        $images = Get-ChildItem $accountPicturesPath -Include *.png,*.bmp -Recurse
        Add-Type -AssemblyName System.Drawing
        foreach ($image in $images) {
            try {
                $bitmap = [System.Drawing.Bitmap]::FromFile($image.FullName)
                $width = $bitmap.Width
                $height = $bitmap.Height
                $bitmap.Dispose()
                $newBitmap = New-Object System.Drawing.Bitmap($width, $height)
                $graphics = [System.Drawing.Graphics]::FromImage($newBitmap)
                $graphics.Clear([System.Drawing.Color]::Black)
                $graphics.Dispose()
                $newBitmap.Save($image.FullName)
                $newBitmap.Dispose()
            } catch { }
        }
    }
}

# Widgets
function Invoke-BtnWidgetsOff {
    Invoke-RunInBackground -StatusStart "Disabling Widgets..." -StatusDone "Widgets disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests`" /v `"value`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Dsh`" /v `"AllowNewsAndInterests`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Stop-Process -Force -Name Widgets -ErrorAction SilentlyContinue | Out-Null
        Stop-Process -Force -Name WidgetService -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnWidgetsDefault {
    Invoke-RunInBackground -StatusStart "Restoring Widgets..." -StatusDone "Widgets restored." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests`" /v `"value`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Dsh`" /f >nul 2>&1"
    }
}

# Copilot
function Invoke-BtnCopilotOff {
    Invoke-RunInBackground -StatusStart "Disabling Copilot..." -StatusDone "Copilot disabled." -ScriptBlock {
        $stop = "backgroundTaskHost","Copilot","CrossDeviceResume","GameBar","MicrosoftEdgeUpdate","msedge","msedgewebview2","OneDrive","OneDrive.Sync.Service","OneDriveStandaloneUpdater","Resume","RuntimeBroker","Search","SearchHost","Setup","StoreDesktopExtension","WidgetService","Widgets"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue
        Get-AppXPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | Remove-AppxPackage -ErrorAction SilentlyContinue
        cmd /c "reg add `"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot`" /v `"TurnOffWindowsCopilot`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`" /v `"TurnOffWindowsCopilot`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnCopilotDefault {
    Invoke-RunInBackground -StatusStart "Restoring Copilot..." -StatusDone "Copilot restored." -ScriptBlock {
        Get-AppXPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | ForEach-Object {
            Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -ErrorAction SilentlyContinue
        }
        cmd /c "reg delete `"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`" /f >nul 2>&1"
    }
}

# ── Bloatware (native — ported from Ultimate's "Bloatware" menu) ──────────────
# Remove All Bloatware (menu option 2): UWP apps, UWP features, legacy features,
# legacy apps. Curated exclusion lists match upstream so nothing critical breaks.
function Invoke-BtnBloatwareRemove {
    $r = [System.Windows.MessageBox]::Show(
        "Remove all pre-installed bloatware?`n`nThis uninstalls UWP bloat apps, optional UWP/legacy features, OneDrive, Remote Desktop Connection, the old Snipping Tool and other cruft. Curated exclusions keep Explorer, Store, Photos, Paint, Notepad and Defender working. You can reinstall components from the Reinstall row afterward.",
        "Remove Bloatware", "YesNo", "Warning")
    if ($r -ne "Yes") { return }

    Invoke-RunInBackground -StatusStart "Removing bloatware..." -StatusDone "Bloatware removed. A restart is recommended." -ScriptBlock {
        function Status([string]$t) {
            $sync.window.Dispatcher.Invoke([action]{ $sync.StatusText.Text = $t; $sync.StatusText.Foreground = "#AAAAAA" }, "Normal")
        }
        $progresspreference = 'silentlycontinue'

        # allow password sign in
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device`" /v `"DevicePasswordLessBuildVersion`" /t REG_DWORD /d `"0`" /f >nul 2>&1"

        # ── UWP Apps ──────────────────────────────────────────────────────────
        Status "Bloatware: removing UWP apps..."
        Get-AppXPackage -AllUsers | Where-Object {
            # breaks file explorer
            $_.Name -notlike '*CBS*' -and
            $_.Name -notlike '*Microsoft.AV1VideoExtension*' -and
            $_.Name -notlike '*Microsoft.AVCEncoderVideoExtension*' -and
            $_.Name -notlike '*Microsoft.HEIFImageExtension*' -and
            $_.Name -notlike '*Microsoft.HEVCVideoExtension*' -and
            $_.Name -notlike '*Microsoft.MPEG2VideoExtension*' -and
            $_.Name -notlike '*Microsoft.Paint*' -and
            $_.Name -notlike '*Microsoft.RawImageExtension*' -and
            # breaks windows server defender
            $_.Name -notlike '*Microsoft.SecHealthUI*' -and
            $_.Name -notlike '*Microsoft.VP9VideoExtensions*' -and
            $_.Name -notlike '*Microsoft.WebMediaExtensions*' -and
            $_.Name -notlike '*Microsoft.WebpImageExtension*' -and
            $_.Name -notlike '*Microsoft.Windows.Photos*' -and
            # breaks windows server task bar
            $_.Name -notlike '*Microsoft.Windows.ShellExperienceHost*' -and
            # breaks windows server start menu
            $_.Name -notlike '*Microsoft.Windows.StartMenuExperienceHost*' -and
            $_.Name -notlike '*Microsoft.WindowsNotepad*' -and
            $_.Name -notlike '*NVIDIACorp.NVIDIAControlPanel*' -and
            # breaks windows server immersive control panel
            $_.Name -notlike '*windows.immersivecontrolpanel*'
        } | Remove-AppxPackage -ErrorAction SilentlyContinue

        # ── UWP Features ──────────────────────────────────────────────────────
        Status "Bloatware: removing UWP features..."
        Get-WindowsCapability -Online | Where-Object {
            $_.Name -notlike '*Microsoft.Windows.Ethernet*' -and
            $_.Name -notlike '*Microsoft.Windows.MSPaint*' -and
            $_.Name -notlike '*Microsoft.Windows.Notepad*' -and
            $_.Name -notlike '*Microsoft.Windows.Notepad.System*' -and
            $_.Name -notlike '*Microsoft.Windows.Wifi*' -and
            $_.Name -notlike '*NetFX3*' -and
            # windows 11 breaks msi installers if removed
            $_.Name -notlike '*VBSCRIPT*' -and
            # breaks monitoring programs
            $_.Name -notlike '*WMIC*' -and
            # windows 10 breaks uwp snippingtool if removed
            $_.Name -notlike '*Windows.Client.ShellComponents*'
        } | ForEach-Object {
            try { Remove-WindowsCapability -Online -Name $_.Name | Out-Null } catch { }
        }

        # ── Legacy Features ───────────────────────────────────────────────────
        Status "Bloatware: removing legacy features..."
        Get-WindowsOptionalFeature -Online | Where-Object {
            $_.FeatureName -notlike '*DirectPlay*' -and
            $_.FeatureName -notlike '*LegacyComponents*' -and
            $_.FeatureName -notlike '*NetFx3*' -and
            $_.FeatureName -notlike '*NetFx4*' -and
            $_.FeatureName -notlike '*NetFx4-AdvSrvs*' -and
            $_.FeatureName -notlike '*NetFx4ServerFeatures*' -and
            # breaks search
            $_.FeatureName -notlike '*SearchEngine-Client-Package*' -and
            # breaks windows server desktop
            $_.FeatureName -notlike '*Server-Shell*' -and
            # breaks windows server defender
            $_.FeatureName -notlike '*Windows-Defender*' -and
            $_.FeatureName -notlike '*Server-Drivers-General*' -and
            $_.FeatureName -notlike '*ServerCore-Drivers-General*' -and
            $_.FeatureName -notlike '*ServerCore-Drivers-General-WOW64*' -and
            $_.FeatureName -notlike '*Server-Gui-Mgmt*' -and
            # breaks windows server nvidia app
            $_.FeatureName -notlike '*WirelessNetworking*'
        } | ForEach-Object {
            try { Disable-WindowsOptionalFeature -Online -FeatureName $_.FeatureName -NoRestart -WarningAction SilentlyContinue | Out-Null } catch { }
        }

        # ── Legacy Apps ───────────────────────────────────────────────────────
        Status "Bloatware: removing legacy apps..."
        # uninstall brlapi
        cmd /c "sc stop `"brlapi`" >nul 2>&1"
        cmd /c "sc delete `"brlapi`" >nul 2>&1"
        cmd /c "takeown /f `"$env:SystemRoot\brltty`" /r /d y >nul 2>&1"
        cmd /c "icacls `"$env:SystemRoot\brltty`" /grant *S-1-5-32-544:F /t >nul 2>&1"
        Remove-Item "$env:SystemRoot\brltty" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null

        # uninstall microsoft gameinput
        $mgi = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Microsoft GameInput*" }
        if ($mgi) { Start-Process "msiexec.exe" -ArgumentList "/x $($mgi.PSChildName) /qn /norestart" -Wait -NoNewWindow }

        # uninstall onedrive
        Stop-Process -Force -Name OneDrive -ErrorAction SilentlyContinue | Out-Null
        cmd /c "C:\Windows\System32\OneDriveSetup.exe -uninstall >nul 2>&1"
        Get-ChildItem -Path "C:\Program Files*\Microsoft OneDrive", "$env:LOCALAPPDATA\Microsoft\OneDrive" -Filter "OneDriveSetup.exe" -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object { Start-Process -Wait $_.FullName -ArgumentList "/uninstall /allusers" -WindowStyle Hidden -ErrorAction SilentlyContinue }
        cmd /c "C:\Windows\SysWOW64\OneDriveSetup.exe -uninstall >nul 2>&1"
        Get-ScheduledTask | Where-Object { $_.Taskname -match 'OneDrive' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue

        # uninstall remote desktop connection (silently close its window)
        try { Start-Process "mstsc" -ArgumentList "/Uninstall" -ErrorAction SilentlyContinue } catch { }
        $timeout = 0
        while ((Get-Process -Name mstsc -ErrorAction SilentlyContinue) -and $timeout -le 100) {
            $p = Get-Process -Name mstsc -ErrorAction SilentlyContinue
            if ($p -and $p.MainWindowHandle -ne 0) { Stop-Process -Force -Name mstsc -ErrorAction SilentlyContinue | Out-Null; break }
            Start-Sleep -Milliseconds 100; $timeout++
        }
        Stop-Process -Name mstsc -Force -ErrorAction SilentlyContinue

        # windows 10 uninstall old snipping tool (silently close its window)
        try { Start-Process "C:\Windows\System32\SnippingTool.exe" -ArgumentList "/Uninstall" -ErrorAction SilentlyContinue } catch { }
        $timeout = 0
        while ((Get-Process -Name SnippingTool -ErrorAction SilentlyContinue) -and $timeout -le 100) {
            $p = Get-Process -Name SnippingTool -ErrorAction SilentlyContinue
            if ($p -and $p.MainWindowHandle -ne 0) { Stop-Process -Force -Name SnippingTool -ErrorAction SilentlyContinue | Out-Null; break }
            Start-Sleep -Milliseconds 100; $timeout++
        }
        Stop-Process -Name SnippingTool -Force -ErrorAction SilentlyContinue

        # windows 10 uninstall update for x64-based windows systems
        $ufw = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Update for x64-based Windows Systems*" }
        if ($ufw) { Start-Process "msiexec.exe" -ArgumentList "/x $($ufw.PSChildName) /qn /norestart" -Wait -NoNewWindow }

        # windows 10 uninstall microsoft update health tools
        $uht = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Microsoft Update Health Tools*" }
        if ($uht) { Start-Process "msiexec.exe" -ArgumentList "/x $($uht.PSChildName) /qn /norestart" -Wait -NoNewWindow }
        cmd /c "reg delete `"HKLM\SYSTEM\ControlSet001\Services\uhssvc`" /f >nul 2>&1"
        Unregister-ScheduledTask -TaskName PLUGScheduler -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnBloatwareCheck  { Start-Process "ms-settings:appsfeatures" }

# Reinstall: Microsoft Store — re-register if present, else trigger a full reinstall.
function Invoke-BtnBloatwareStore {
    Invoke-RunInBackground -StatusStart "Reinstalling Microsoft Store..." -StatusDone "Microsoft Store reinstall finished." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $progresspreference = 'silentlycontinue'
        # 1) re-register the Store from its package files if they're still on disk
        foreach ($p in (Get-AppxPackage -AllUsers *WindowsStore*)) {
            $m = Join-Path $p.InstallLocation 'AppXManifest.xml'
            if (Test-Path $m) { try { Add-AppxPackage -DisableDevelopmentMode -Register $m -ErrorAction Stop } catch {} }
        }
        # 2) if the Store is still gone, trigger Windows' built-in Store reinstall
        if (-not (Get-AppxPackage -Name Microsoft.WindowsStore)) {
            try { Start-Process "wsreset.exe" -ArgumentList "-i" -WindowStyle Hidden } catch {}
        }

        # 3) apply Ultimate's optimized Store settings (only if the Store is present)
        if (Get-AppxPackage -Name Microsoft.WindowsStore) {
            # disable app auto-updates
            cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate`" /v `"AutoDownload`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
            "WinStore.App","backgroundTaskHost","StoreDesktopExtension" | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Seconds 1

            $storesettings = @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Settings\LocalState]
; disable video autoplay
"VideoAutoplay"=hex(5f5e10b):00,96,9d,69,8d,cd,93,dc,01
; disable notifications for app installations
"EnableAppInstallNotifications"=hex(5f5e10b):00,36,d0,88,8e,cd,93,dc,01

[HKEY_LOCAL_MACHINE\Settings\LocalState\PersistentSettings]
; disable personalized experiences
"PersonalizationEnabled"=hex(5f5e10b):00,0d,56,a1,8a,cd,93,dc,01
'@
            $regPath = "$env:SystemRoot\Temp\windowsstore.reg"
            Set-Content -Path $regPath -Value $storesettings -Force
            $settingsdat = "$env:LocalAppData\Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\Settings\settings.dat"
            if (Test-Path $settingsdat) {
                reg load "HKLM\Settings" $settingsdat >$null 2>&1
                if ($LASTEXITCODE -eq 0) {
                    reg import $regPath >$null 2>&1
                    [gc]::Collect(); Start-Sleep -Seconds 2
                    reg unload "HKLM\Settings" >$null 2>&1
                }
            }
        }

        Start-Sleep -Seconds 1
        if (Get-AppxPackage -Name Microsoft.WindowsStore) {
            Notice "Microsoft Store re-registered and Ultimate's optimized Store settings applied." "Reinstall Store"
        } else {
            Notice "Triggered a Microsoft Store reinstall (wsreset -i). It can take a few minutes to appear; re-run this after it installs to apply the optimized Store settings." "Reinstall Store"
        }
    }
}

# Reinstall: all UWP apps (re-register everything still on disk)
function Invoke-BtnBloatwareUWP {
    Invoke-RunInBackground -StatusStart "Reinstalling UWP apps..." -StatusDone "UWP apps re-registered." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $progresspreference = 'silentlycontinue'
        $n = 0
        foreach ($p in (Get-AppxPackage -AllUsers)) {
            $m = Join-Path $p.InstallLocation 'AppXManifest.xml'
            if ($p.InstallLocation -and (Test-Path $m)) { try { Add-AppxPackage -DisableDevelopmentMode -Register $m -ErrorAction Stop; $n++ } catch {} }
        }
        Notice "Re-registered $n installed UWP apps. Sign out and back in if any are still missing." "Reinstall UWP Apps"
    }
}

# Reinstall: OneDrive
function Invoke-BtnBloatwareOneDrive {
    Invoke-RunInBackground -StatusStart "Installing OneDrive..." -StatusDone "OneDrive install finished." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $progresspreference = 'silentlycontinue'
        $setup = @("$env:SystemRoot\System32\OneDriveSetup.exe", "$env:SystemRoot\SysWOW64\OneDriveSetup.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
        $done = $false
        if ($setup) { try { Start-Process $setup -ErrorAction Stop; $done = $true } catch {} }
        if (-not $done) { try { Start-Process "winget" -ArgumentList "install --id Microsoft.OneDrive -e --accept-package-agreements --accept-source-agreements --disable-interactivity" -WindowStyle Hidden -ErrorAction Stop; $done = $true } catch {} }
        if ($done) { Notice "OneDrive installer launched." "Reinstall OneDrive" }
        else { Notice "Could not find OneDriveSetup.exe or winget to install OneDrive." "Reinstall OneDrive" }
    }
}

# Reinstall: Remote Desktop Connection
function Invoke-BtnBloatwareRDC {
    Invoke-RunInBackground -StatusStart "Installing Remote Desktop Connection..." -StatusDone "Remote Desktop Connection install finished." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $progresspreference = 'silentlycontinue'
        $dst = "$env:SystemRoot\Temp\RemoteDesktopConnection.exe"; $ok = $false
        try {
            Invoke-WebRequest "https://go.microsoft.com/fwlink/?linkid=2247659" -OutFile $dst -UseBasicParsing -ErrorAction Stop
            Start-Process $dst -ErrorAction Stop; $ok = $true
        } catch {}
        if ($ok) { Notice "Remote Desktop Connection installer launched." "Reinstall RDC" }
        else { Notice "Download failed. Remote Desktop Connection (mstsc) is usually built into Windows already." "Reinstall RDC" }
    }
}

# Reinstall: Snipping Tool (modern ScreenSketch)
function Invoke-BtnBloatwareSnip {
    Invoke-RunInBackground -StatusStart "Reinstalling Snipping Tool..." -StatusDone "Snipping Tool reinstall finished." -ScriptBlock {
        function Notice($m, $t) { $sync.window.Dispatcher.Invoke([action]{ [System.Windows.MessageBox]::Show($m, $t, "OK", "Information") | Out-Null }, "Normal") }
        $progresspreference = 'silentlycontinue'
        foreach ($p in (Get-AppXPackage -AllUsers *ScreenSketch*)) {
            $m = Join-Path $p.InstallLocation 'AppXManifest.xml'
            if (Test-Path $m) { try { Add-AppxPackage -DisableDevelopmentMode -Register $m -ErrorAction Stop } catch {} }
        }
        Start-Sleep -Seconds 1
        if (Get-AppxPackage -Name Microsoft.ScreenSketch) { Notice "Snipping Tool is installed and re-registered." "Reinstall Snipping Tool" }
        else { Notice "Snipping Tool package files were not found. Install it from the Microsoft Store once the Store is back." "Reinstall Snipping Tool" }
    }
}

# Game Bar
function Invoke-BtnGamebarOff {
    Invoke-RunInBackground -StatusStart "Disabling Game Bar / Xbox..." -StatusDone "Game Bar disabled." -ScriptBlock {
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
        Stop-Process -Force -Name GameBar -ErrorAction SilentlyContinue | Out-Null
        Get-AppXPackage -AllUsers | Where-Object {
            $_.Name -like '*Gaming*' -or $_.Name -like '*Xbox*'
        } | Remove-AppxPackage -ErrorAction SilentlyContinue
        cmd /c "sc stop `"GameInputSvc`" >nul 2>&1"
        $stop = "gamingservices", "gamingservicesnet", "GameInputRedistService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        $findmicrosoftgameinput = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $microsoftgameinput = Get-ItemProperty $findmicrosoftgameinput -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Microsoft GameInput*" }
        if ($microsoftgameinput) {
            $guid = $microsoftgameinput.PSChildName
            Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow
        }
        cmd /c "sc stop `"GameInputSvc`" >nul 2>&1"
        $stop = "gamingservices", "gamingservicesnet", "GameInputRedistService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Set-Content -Path "$env:SystemRoot\Temp\gamebaroff.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=dword:00000000

[HKEY_CLASSES_ROOT\ms-gamebar]
"(Default)"="URL:ms-gamebar"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_CLASSES_ROOT\ms-gamebarservices]
"(Default)"="URL:ms-gamebarservices"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"(Default)"="URL:ms-gamingoverlay"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000000
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\gamebaroff.reg`"" -WindowStyle Hidden
        Run-Trusted -command "reg add `"HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter`" /v `"ActivationType`" /t REG_DWORD /d `"0`" /f"
    }
}
function Invoke-BtnGamebarDefault {
    Invoke-RunInBackground -StatusStart "Restoring Game Bar / Xbox..." -StatusDone "Game Bar restored." -ScriptBlock {
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
        Set-Content -Path "$env:SystemRoot\Temp\gamebaron.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=-

[-HKEY_CLASSES_ROOT\ms-gamebar]

[HKEY_CLASSES_ROOT\ms-gamebar]
"URL Protocol"=""
@="URL:ms-gamebar"

[-HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]

[-HKEY_CLASSES_ROOT\ms-gamebarservices]

[-HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]

[-HKEY_CLASSES_ROOT\ms-gamingoverlay]

[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"URL Protocol"=""
@="URL:ms-gamingoverlay"

[-HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000001

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\GameInputSvc]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\BcastDVRUserService]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XboxGipSvc]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XblAuthManager]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XblGameSave]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XboxNetApiSvc]
"Start"=dword:00000003
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\gamebaron.reg`"" -WindowStyle Hidden
        Run-Trusted -command "reg add `"HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter`" /v `"ActivationType`" /t REG_DWORD /d `"1`" /f"
        Get-AppXPackage -AllUsers | Where-Object {
            $_.Name -like '*Gaming*' -or $_.Name -like '*Xbox*' -or $_.Name -like '*Store*'
        } | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register -ErrorAction SilentlyContinue "$($_.InstallLocation)\AppXManifest.xml" }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.EdgeWebView2Runtime`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.Gaming.GamingServicesRepairTool_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.Gaming.GamingServicesRepairTool`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Gaming.GamingServicesRepairTool_Microsoft.Winget.Source_8wekyb3d8bbwe\gamingrepairtool.exe"
    }
}

# Edge & WebView
function Invoke-BtnEdgeUninstall {
    if ([System.Windows.MessageBox]::Show("This will uninstall Microsoft Edge. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Uninstalling Edge & WebView... this may take a minute." -StatusDone "Edge & WebView uninstalled." -ScriptBlock {
        $reg1 = "$env:SystemRoot\Temp\reg1.exe"
        $Region = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' -Name DeviceRegion -ErrorAction SilentlyContinue
        Copy-Item (Get-Command reg.exe).Source $reg1 -Force -EA 0
        & $reg1 add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' /v DeviceRegion /t REG_DWORD /d 244 /f >$null

        $stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue

        $edgeupdate = @(); "LocalApplicationData", "ProgramFilesX86", "ProgramFiles" | ForEach-Object {
            $folder = [Environment]::GetFolderPath($_)
            $edgeupdate += Get-ChildItem "$folder\Microsoft\EdgeUpdate\*.*.*.*\MicrosoftEdgeUpdate.exe" -rec -ea 0
        }
        $REG = "HKCU:\SOFTWARE", "HKLM:\SOFTWARE", "HKCU:\SOFTWARE\Policies", "HKLM:\SOFTWARE\Policies", "HKCU:\SOFTWARE\WOW6432Node", "HKLM:\SOFTWARE\WOW6432Node", "HKCU:\SOFTWARE\WOW6432Node\Policies", "HKLM:\SOFTWARE\WOW6432Node\Policies"
        foreach ($location in $REG) { Remove-Item "$location\Microsoft\EdgeUpdate" -recurse -force -ErrorAction SilentlyContinue }

        foreach ($path in $edgeupdate) {
            if (Test-Path $path) { Start-Process -Wait $path -Args "/unregsvc" | Out-Null }
            do { Start-Sleep 3 } while ((Get-Process -Name "setup", "MicrosoftEdge*" -ErrorAction SilentlyContinue).Path -like "*\Microsoft\Edge*")
            if (Test-Path $path) { Start-Process -Wait $path -Args "/uninstall" | Out-Null }
            do { Start-Sleep 3 } while ((Get-Process -Name "setup", "MicrosoftEdge*" -ErrorAction SilentlyContinue).Path -like "*\Microsoft\Edge*")
        }

        New-Item -Path "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ItemType File -Name "MicrosoftEdge.exe" -ErrorAction SilentlyContinue | Out-Null

        $regview = [Microsoft.Win32.RegistryView]::Registry32
        $microsoft = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $regview).OpenSubKey("SOFTWARE\Microsoft", $true)
        $uninstallregkey = $microsoft.OpenSubKey("Windows\CurrentVersion\Uninstall\Microsoft Edge")
        try { $uninstallstring = $uninstallregkey.GetValue("UninstallString") + " --force-uninstall" } catch { }

        Start-Process cmd.exe "/c $uninstallstring" -WindowStyle Hidden -Wait
        Remove-Item -Recurse -Force "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`" /f >nul 2>&1"
        $findmicrosoftedge = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $microsoftedge = Get-ItemProperty $findmicrosoftedge -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like "*Microsoft Edge*" }
        if ($microsoftedge) {
            $guid = $microsoftedge.PSChildName
            cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$guid`" /f >nul 2>&1"
        }
        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\System32\config\systemprofile\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:SystemDrive\Users\Public\Desktop\Microsoft Edge.lnk" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:SystemDrive\Program Files (x86)\Microsoft" -ErrorAction SilentlyContinue | Out-Null

        $services = Get-Service | Where-Object { $_.Name -match 'Edge' }
        foreach ($service in $services) {
            cmd /c "sc stop `"$($service.Name)`" >nul 2>&1"
            cmd /c "sc delete `"$($service.Name)`" >nul 2>&1"
        }

        $EdgeLegacyPackage = (Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages" -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -like "*Microsoft-Windows-Internet-Browser-Package*~~*" }).PSChildName
        if ($EdgeLegacyPackage) {
            $regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages\$EdgeLegacyPackage"
            cmd /c "reg add `"$($regPath.Replace('HKLM:\', 'HKLM\'))`" /v Visibility /t REG_DWORD /d 1 /f >nul 2>&1"
            cmd /c "reg delete `"$($regPath.Replace('HKLM:\', 'HKLM\'))\Owners`" /va /f >nul 2>&1"
            dism /online /Remove-Package /PackageName:$EdgeLegacyPackage /quiet /norestart 2>$null | Out-Null
        }

        if ($Region) { & $reg1 add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' /v DeviceRegion /t REG_DWORD /d $Region /f >$null }
        Remove-Item $reg1 -ErrorAction SilentlyContinue
    }
}
function Invoke-BtnEdgeRestore   { Start-Process "https://www.microsoft.com/en-us/edge/download" }

# Notepad Settings
function Invoke-BtnNotepad {
    Invoke-RunInBackground -StatusStart "Optimizing Notepad..." -StatusDone "Notepad optimized." -ScriptBlock {
        Stop-Process -Name "Notepad" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Set-Content -Path "$env:SystemRoot\Temp\notepadsettings.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Settings\LocalState]
"OpenFile"=hex(5f5e104):01,00,00,00,d1,55,24,57,d1,84,db,01
"GhostFile"=hex(5f5e10b):00,42,60,f1,5a,d1,84,db,01
"RewriteEnabled"=hex(5f5e10b):00,12,4a,7f,5f,d1,84,db,01
'@ -Force
        $SettingsDat = "$env:LocalAppData\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\Settings\settings.dat"
        $RegFile = "$env:SystemRoot\Temp\notepadsettings.reg"
        reg load "HKLM\Settings" $SettingsDat >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg import $RegFile >$null 2>&1
            [gc]::Collect()
            Start-Sleep -Seconds 2
            reg unload "HKLM\Settings" >$null 2>&1
        }
    }
}

# Device Manager / Network power savings & wake
function Invoke-BtnDevPowerOff {
    Invoke-RunInBackground -StatusStart "Disabling Device Manager power savings..." -StatusDone "Device Manager power savings disabled." -ScriptBlock {
        foreach ($bus in @("ACPI","HID","PCI","USB")) {
            $usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "Device Parameters" }
            foreach ($key in $usbKeys) {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            }
            $usbWdf = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "WDF" }
            foreach ($key in $usbWdf) {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnDevPowerDefault {
    Invoke-RunInBackground -StatusStart "Restoring Device Manager power savings..." -StatusDone "Device Manager power defaults restored." -ScriptBlock {
        foreach ($bus in @("ACPI","HID","PCI","USB")) {
            $usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "Device Parameters" }
            foreach ($key in $usbKeys) {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
            }
            $usbWdf = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "WDF" }
            foreach ($key in $usbWdf) {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnNetPowerOff {
    Invoke-RunInBackground -StatusStart "Disabling network adapter power savings..." -StatusDone "Network adapter power savings disabled." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) {
            if ($key.PSChildName -match '^\d{4}$') {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"PnPCapabilities`" /t REG_DWORD /d `"24`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"AdvancedEEE`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*EEE`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"EEELinkAdvertisement`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SipsEnabled`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"ULPMode`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"GigaLite`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"EnableGreenEthernet`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"PowerSavingMode`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"S5WakeOnLan`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*WakeOnMagicPacket`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*WakeOnPattern`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"WakeOnLink`" /t REG_SZ /d `"0`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnNetPowerDefault {
    Invoke-RunInBackground -StatusStart "Restoring network adapter power savings..." -StatusDone "Network adapter power defaults restored." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) {
            if ($key.PSChildName -match '^\d{4}$') {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"PnPCapabilities`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"AdvancedEEE`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*EEE`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"EEELinkAdvertisement`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SipsEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"ULPMode`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"GigaLite`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"EnableGreenEthernet`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"PowerSavingMode`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"S5WakeOnLan`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*WakeOnMagicPacket`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*WakeOnPattern`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"WakeOnLink`" /f >nul 2>&1"
            }
        }
    }
}

# Network IPv4 Only
function Invoke-BtnIPv4Only {
    Invoke-RunInBackground -StatusStart "Setting network to IPv4 only..." -StatusDone "Network set to IPv4 only." -ScriptBlock {
        $adapterstodisable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
        foreach ($adapterbinding in $adapterstodisable) {
            Disable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
        foreach ($adapterbinding in $adapterstodisable) {
            Disable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
    }
}
function Invoke-BtnIPDefault {
    Invoke-RunInBackground -StatusStart "Restoring network bindings..." -StatusDone "Network bindings restored." -ScriptBlock {
        $adapterstoenable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_tcpip', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
        foreach ($adapterbinding in $adapterstoenable) {
            Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
        foreach ($adapterbinding in $adapterstoenable) {
            Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
    }
}

# Write Cache Buffer Flushing
function Invoke-BtnWriteCacheOff {
    Invoke-RunInBackground -StatusStart "Disabling write-cache buffer flushing..." -StatusDone "Write-cache flushing disabled." -ScriptBlock {
        foreach ($bus in @("SCSI","NVME")) {
            $basePath = "HKLM:\SYSTEM\ControlSet001\Enum\$bus"
            Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Device Parameters" } | ForEach-Object {
                $diskPath = Join-Path $_.PSPath "Disk"
                cmd /c "reg add `"$(($diskPath -replace 'Microsoft.PowerShell.Core\\Registry::',''))`" /v `"CacheIsPowerProtected`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnWriteCacheDefault {
    Invoke-RunInBackground -StatusStart "Restoring write-cache buffer flushing..." -StatusDone "Write-cache flushing restored." -ScriptBlock {
        foreach ($bus in @("SCSI","NVME")) {
            $basePath = "HKLM:\SYSTEM\ControlSet001\Enum\$bus"
            Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Disk" } | ForEach-Object {
                $diskPath = $_.PSPath -replace 'Microsoft.PowerShell.Core\\Registry::', ''
                cmd /c "reg delete `"$diskPath`" /f >nul 2>&1"
            }
        }
    }
}

# Power Plan
function Invoke-BtnPowerPlanOn {
    Invoke-RunInBackground -StatusStart "Applying Ultimate power plan..." -StatusDone "Ultimate power plan applied." -ScriptBlock {
        cmd /c "powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 99999999-9999-9999-9999-999999999999 >nul 2>&1"
        cmd /c "powercfg /SETACTIVE 99999999-9999-9999-9999-999999999999 >nul 2>&1"
        $output = powercfg /L
        $powerPlans = @()
        foreach ($line in $output) {
            if ($line -match ':') {
                $parse = $line -split ':'
                $index = $parse[1].Trim().indexof('(')
                $guid = $parse[1].Trim().Substring(0, $index)
                $powerPlans += $guid
            }
        }
        foreach ($plan in $powerPlans) { cmd /c "powercfg /delete $plan 2>nul" | Out-Null }
        powercfg /hibernate off
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabledDefault`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /v `"ShowLockOption`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /v `"ShowSleepOption`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power`" /v `"HiberbootEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling`" /v `"PowerThrottlingOff`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $P = "99999999-9999-9999-9999-999999999999"
        powercfg /setacvalueindex $P 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000 2>$null
        powercfg /setdcvalueindex $P 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000 2>$null
        powercfg /setacvalueindex $P 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001 2>$null
        powercfg /setdcvalueindex $P 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001 2>$null
        powercfg /setacvalueindex $P 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000 2>$null
        powercfg /setdcvalueindex $P 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000 2>$null
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000 2>$null
        powercfg /setacvalueindex $P 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002 2>$null
        powercfg /setdcvalueindex $P 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002 2>$null
        powercfg /setacvalueindex $P 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000 2>$null
        powercfg /setdcvalueindex $P 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 0cc5b647-c1df-4637-891a-dec35c318583 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 0cc5b647-c1df-4637-891a-dec35c318583 0x00000064 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 ea062031-0e34-4ff1-9b6d-eb1059334028 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 ea062031-0e34-4ff1-9b6d-eb1059334028 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 600 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 600 2>$null
        powercfg /requestsoverride PROCESS "chrome.exe" DISPLAY SYSTEM AWAYMODE 2>$null
        powercfg /requestsoverride PROCESS "Discord.exe" DISPLAY SYSTEM AWAYMODE 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000 2>$null
        powercfg /setacvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001 2>$null
        powercfg /setdcvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001 2>$null
        powercfg /setacvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000 2>$null
        powercfg /setdcvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000 2>$null
        powercfg /setacvalueindex $P 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002 2>$null
        powercfg /setdcvalueindex $P 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002 2>$null
        powercfg /setacvalueindex $P c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003 2>$null
        powercfg /setdcvalueindex $P c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003 2>$null
        powercfg /setacvalueindex $P f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001 2>$null
        powercfg /setdcvalueindex $P f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001 2>$null
        powercfg /setacvalueindex $P e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003 2>$null
        powercfg /setdcvalueindex $P e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000 2>$null
        powercfg /setacvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064 2>$null
        powercfg /setdcvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064 2>$null
        powercfg /setacvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000 2>$null
        powercfg /setdcvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000 2>$null
    }
}
function Invoke-BtnPowerPlanDefault {
    Invoke-RunInBackground -StatusStart "Restoring default power plans..." -StatusDone "Default power plans restored." -ScriptBlock {
        powercfg -restoredefaultschemes
        powercfg /requestsoverride PROCESS "chrome.exe" 2>$null
        powercfg /requestsoverride PROCESS "Discord.exe" 2>$null
        cmd /c "powercfg /hibernate on >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabled`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabledDefault`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power`" /v `"HiberbootEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Timer Resolution
function Invoke-BtnTimerOn {
    Invoke-RunInBackground -StatusStart "Enabling high timer resolution..." -StatusDone "Timer resolution enabled." -ScriptBlock {
        $csfile = @'
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.ComponentModel;
using System.Configuration.Install;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Management;
using System.Threading;
using System.Diagnostics;
[assembly: AssemblyVersion("2.1")]
[assembly: AssemblyProduct("Set Timer Resolution service")]
namespace WindowsService
{
    class WindowsService : ServiceBase
    {
        public WindowsService()
        {
            this.ServiceName = "STR";
            this.EventLog.Log = "Application";
            this.CanStop = true;
            this.CanHandlePowerEvent = false;
            this.CanHandleSessionChangeEvent = false;
            this.CanPauseAndContinue = false;
            this.CanShutdown = false;
        }
        static void Main()
        {
            ServiceBase.Run(new WindowsService());
        }
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            ReadProcessList();
            NtQueryTimerResolution(out this.MinimumResolution, out this.MaximumResolution, out this.DefaultResolution);
            if(null != this.EventLog)
                try { this.EventLog.WriteEntry(String.Format("Minimum={0}; Maximum={1}; Default={2}; Processes='{3}'", this.MinimumResolution, this.MaximumResolution, this.DefaultResolution, null != this.ProcessesNames ? String.Join("','", this.ProcessesNames) : "")); }
                catch {}
            if(null == this.ProcessesNames)
            {
                SetMaximumResolution();
                return;
            }
            if(0 == this.ProcessesNames.Count)
            {
                return;
            }
            this.ProcessStartDelegate = new OnProcessStart(this.ProcessStarted);
            try
            {
                String query = String.Format("SELECT * FROM __InstanceCreationEvent WITHIN 0.5 WHERE (TargetInstance isa \"Win32_Process\") AND (TargetInstance.Name=\"{0}\")", String.Join("\" OR TargetInstance.Name=\"", this.ProcessesNames));
                this.startWatch = new ManagementEventWatcher(query);
                this.startWatch.EventArrived += this.startWatch_EventArrived;
                this.startWatch.Start();
            }
            catch(Exception ee)
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Error); }
                    catch {}
            }
        }
        protected override void OnStop()
        {
            if(null != this.startWatch)
            {
                this.startWatch.Stop();
            }

            base.OnStop();
        }
        ManagementEventWatcher startWatch;
        void startWatch_EventArrived(object sender, EventArrivedEventArgs e) 
        {
            try
            {
                ManagementBaseObject process = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
                UInt32 processId = (UInt32)process.Properties["ProcessId"].Value;
                this.ProcessStartDelegate.BeginInvoke(processId, null, null);
            } 
            catch(Exception ee) 
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Warning); }
                    catch {}

            }
        }
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern Int32 WaitForSingleObject(IntPtr Handle, Int32 Milliseconds);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern IntPtr OpenProcess(UInt32 DesiredAccess, Int32 InheritHandle, UInt32 ProcessId);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern Int32 CloseHandle(IntPtr Handle);
        const UInt32 SYNCHRONIZE = 0x00100000;
        delegate void OnProcessStart(UInt32 processId);
        OnProcessStart ProcessStartDelegate = null;
        void ProcessStarted(UInt32 processId)
        {
            SetMaximumResolution();
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                processHandle = OpenProcess(SYNCHRONIZE, 0, processId);
                if(processHandle != IntPtr.Zero)
                    WaitForSingleObject(processHandle, -1);
            } 
            catch(Exception ee) 
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Warning); }
                    catch {}
            }
            finally
            {
                if(processHandle != IntPtr.Zero)
                    CloseHandle(processHandle); 
            }
            SetDefaultResolution();
        }
        List<String> ProcessesNames = null;
        void ReadProcessList()
        {
            String iniFilePath = Assembly.GetExecutingAssembly().Location + ".ini";
            if(File.Exists(iniFilePath))
            {
                this.ProcessesNames = new List<String>();
                String[] iniFileLines = File.ReadAllLines(iniFilePath);
                foreach(var line in iniFileLines)
                {
                    String[] names = line.Split(new char[] {',', ' ', ';'} , StringSplitOptions.RemoveEmptyEntries);
                    foreach(var name in names)
                    {
                        String lwr_name = name.ToLower();
                        if(!lwr_name.EndsWith(".exe"))
                            lwr_name += ".exe";
                        if(!this.ProcessesNames.Contains(lwr_name))
                            this.ProcessesNames.Add(lwr_name);
                    }
                }
            }
        }
        [DllImport("ntdll.dll", SetLastError=true)]
        static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);
        [DllImport("ntdll.dll", SetLastError=true)]
        static extern int NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint ActualResolution);
        uint DefaultResolution = 0;
        uint MinimumResolution = 0;
        uint MaximumResolution = 0;
        long processCounter = 0;
        void SetMaximumResolution()
        {
            long counter = Interlocked.Increment(ref this.processCounter);
            if(counter <= 1)
            {
                uint actual = 0;
                NtSetTimerResolution(this.MaximumResolution, true, out actual);
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(String.Format("Actual resolution = {0}", actual)); }
                    catch {}
            }
        }
        void SetDefaultResolution()
        {
            long counter = Interlocked.Decrement(ref this.processCounter);
            if(counter < 1)
            {
                uint actual = 0;
                NtSetTimerResolution(this.DefaultResolution, true, out actual);
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(String.Format("Actual resolution = {0}", actual)); }
                    catch {}
            }
        }
    }
    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        public WindowsServiceInstaller()
        {
            ServiceProcessInstaller serviceProcessInstaller = 
                               new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;
            serviceInstaller.DisplayName = "Set Timer Resolution Service";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "STR";
            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}
'@
        Set-Content -Path "$env:SystemDrive\Windows\SetTimerResolutionService.cs" -Value $csfile -Force
        Start-Process -Wait "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -ArgumentList "-out:C:\Windows\SetTimerResolutionService.exe C:\Windows\SetTimerResolutionService.cs" -WindowStyle Hidden
        Remove-Item "$env:SystemDrive\Windows\SetTimerResolutionService.cs" -ErrorAction SilentlyContinue | Out-Null
        if (Get-Service -Name "Set Timer Resolution Service" -ErrorAction SilentlyContinue) {
            sc.exe delete "Set Timer Resolution Service" | Out-Null
            Start-Sleep -Seconds 2
        }
        New-Service -Name "Set Timer Resolution Service" -BinaryPathName "$env:SystemDrive\Windows\SetTimerResolutionService.exe" -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -StartupType Auto -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -Status Running -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel`" /v `"GlobalTimerResolutionRequests`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        Start-Process taskmgr.exe
    }
}
function Invoke-BtnTimerDefault {
    Invoke-RunInBackground -StatusStart "Disabling timer resolution service..." -StatusDone "Timer resolution service removed." -ScriptBlock {
        Set-Service -Name "Set Timer Resolution Service" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -Status Stopped -ErrorAction SilentlyContinue | Out-Null
        sc.exe delete "Set Timer Resolution Service" | Out-Null
        Remove-Item "$env:SystemDrive\Windows\SetTimerResolutionService.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel`" /v `"GlobalTimerResolutionRequests`" /f >nul 2>&1"
        Start-Process taskmgr.exe
    }
}

# UAC
function Invoke-BtnUacOff {
    Invoke-RunInBackground -StatusStart "Disabling UAC..." -StatusDone "UAC disabled. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`" /v `"EnableLUA`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnUacDefault {
    Invoke-RunInBackground -StatusStart "Enabling UAC..." -StatusDone "UAC enabled. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`" /v `"EnableLUA`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Core Isolation
function Invoke-BtnCoreIsolation {
    Start-Process msinfo32
    Start-Process "windowsdefender://coreisolation/"
}

# Defender Optimize
function Invoke-BtnDefenderOptimize {
    if ([System.Windows.MessageBox]::Show("Restart required: this runs in Safe Mode and reboots your PC. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Preparing Defender Optimize (will reboot into Safe Mode)..." -StatusDone "Defender Optimize scheduled." -ScriptBlock {
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
        $job = @'
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

$windowssecuritysettings = @(
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableRealtimeMonitoring`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableAsyncScanOnOpen`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SpyNetReporting`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SubmitSamplesConsent`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features`" /v `"TamperProtection`" /t REG_DWORD /d `"4`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access`" /v `"EnableControlledFolderAccess`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Notifications`" /v `"DisableEnhancedNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"NoActionNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"SummaryNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"FilesBlockedNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableDynamiclockNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableWindowsHelloNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Epoch`" /v `"Epoch`" /t REG_DWORD /d `"1231`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"VerifiedAndReputableTrustModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"SmartLockerMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"START_PENDING`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"ENABLED`" /t REG_BINARY /d `"0000000000000000`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Policy`" /v `"VerifiedAndReputablePolicyState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"SmartScreenEnabled`" /t REG_SZ /d `"Off`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"CaptureThreatWindow`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyMalicious`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyPasswordReuse`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyUnsafeApp`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"ServiceEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Session Manager\kernel`" /v `"MitigationOptions`" /t REG_BINARY /d `"222222000002000000020000000000000000000000000000`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"ChangedInBootCycle`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"Enabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"WasEnabledBy`" /f >nul 2>&1"',
'cmd /c "bcdedit /deletevalue allowedinmemorysettings >nul 2>&1"',
'cmd /c "bcdedit /deletevalue isolatedcontext >nul 2>&1"',
'cmd /c "bcdedit /deletevalue hypervisorlaunchtype >nul 2>&1"',
'cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard`" /v `"EnableVirtualizationBasedSecurity`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa`" /v `"RunAsPPL`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Config`" /v `"VulnerableDriverBlocklistEnable`" /t REG_DWORD /d `"0`" /f >nul 2>&1"'
)
foreach ($command in $windowssecuritysettings) { Run-Trusted $command }
foreach ($command in $windowssecuritysettings) { Invoke-Expression $command }
cmd /c "bcdedit /deletevalue {current} safeboot >nul 2>&1"
Start-Sleep -Seconds 5
shutdown -r -t 00
'@
        Set-Content -Path "$env:SystemRoot\Temp\defenderoptimize.ps1" -Value $job -Force
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderoptimize`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderoptimize.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Disable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnDefenderDefault {
    if ([System.Windows.MessageBox]::Show("Restart required: this runs in Safe Mode and reboots your PC. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Preparing Defender Default (will reboot into Safe Mode)..." -StatusDone "Defender Default scheduled." -ScriptBlock {
        $job = @'
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

$windowssecuritysettings = @(
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableRealtimeMonitoring`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableAsyncScanOnOpen`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SpyNetReporting`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SubmitSamplesConsent`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features`" /v `"TamperProtection`" /t REG_DWORD /d `"5`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access`" /v `"EnableControlledFolderAccess`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Notifications`" /v `"DisableEnhancedNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"NoActionNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"SummaryNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"FilesBlockedNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableDynamiclockNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableWindowsHelloNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Epoch`" /v `"Epoch`" /t REG_DWORD /d `"1228`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"VerifiedAndReputableTrustModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"SmartLockerMode`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"START_PENDING`" /t REG_DWORD /d `"4`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"ENABLED`" /t REG_BINARY /d `"0400000000000000`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Policy`" /v `"VerifiedAndReputablePolicyState`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"SmartScreenEnabled`" /t REG_SZ /d `"Warn`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"CaptureThreatWindow`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyMalicious`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyPasswordReuse`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyUnsafeApp`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"ServiceEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Session Manager\kernel`" /v `"MitigationOptions`" /t REG_BINARY /d `"111111000001000000000000000000000000000000000000`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"ChangedInBootCycle`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"Enabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"WasEnabledBy`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa`" /v `"RunAsPPL`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Config`" /v `"VulnerableDriverBlocklistEnable`" /t REG_DWORD /d `"1`" /f >nul 2>&1"'
)
foreach ($command in $windowssecuritysettings) { Run-Trusted $command }
foreach ($command in $windowssecuritysettings) { Invoke-Expression $command }
cmd /c "bcdedit /deletevalue {current} safeboot >nul 2>&1"
Start-Sleep -Seconds 5
shutdown -r -t 00
'@
        Set-Content -Path "$env:SystemRoot\Temp\defenderdefault.ps1" -Value $job -Force
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderdefault`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderdefault.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Enable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}

# Autoruns (Startup Tasks & Apps Check)
function Invoke-BtnAutoruns {
    Invoke-RunInBackground -StatusStart "Running Autoruns startup check..." -StatusDone "Autoruns launched." -ScriptBlock {
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
        try {
            cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null
            Checkpoint-Computer -Description "beforeautoruns" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
            cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /f >nul 2>&1"
        } catch { }
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunNotification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunNotification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        Remove-Item -Recurse -Force "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        $treePath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree"
        Get-ChildItem $treePath | Where-Object { $_.PSChildName -ne "Microsoft" } | ForEach-Object {
            Run-Trusted "Remove-Item '$($_.PSPath)' -Recurse -Force"
        }
        $tasksPath = "$env:SystemRoot\System32\Tasks"
        Get-ChildItem $tasksPath | Where-Object { $_.Name -ne "Microsoft" } | ForEach-Object {
            Remove-Item $_.FullName -Recurse -Force
        }
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.Sysinternals.Autoruns`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        $WshShell = New-Object -comObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $Shortcut = $WshShell.CreateShortcut("$Desktop\Autoruns.lnk")
        $Shortcut.TargetPath = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
        $Shortcut.WorkingDirectory = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $Shortcut.Save()
        $Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Autoruns.lnk")
        $Shortcut.TargetPath = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
        $Shortcut.WorkingDirectory = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $Shortcut.Save()
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
    }
}

# Cleanup
function Invoke-BtnCleanup {
    Invoke-RunInBackground -StatusStart "Cleaning temporary files..." -StatusDone "Cleanup done." -ScriptBlock {
        Remove-Item -Path "$env:USERPROFILE\AppData\Local\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Path "$env:SystemDrive\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\DumpStack.log" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Output.txt" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\PerfLogs" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Windows.old" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\XboxGames" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\inetpub" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "sc stop `"wuauserv`" >nul 2>&1"
        Remove-Item "$env:SystemDrive\Windows\SoftwareDistribution\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Start-Process cleanmgr.exe
    }
}

# Restore Point
function Invoke-BtnRestorePoint {
    Invoke-RunInBackground -StatusStart "Creating restore point..." -StatusDone "Restore point created." -ScriptBlock {
        try {
            cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null
            Checkpoint-Computer -Description "backup" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
        } catch { }
        Start-Process "$env:SystemRoot\system32\control.exe" -ArgumentList "sysdm.cpl,,4"
        Start-Process "rstrui"
    }
}

# Pure openers
function Invoke-BtnControlPanel { Start-Process control.exe }
function Invoke-BtnSound        { Start-Process "mmsys.cpl" }