# admin
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator"))
{Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
Exit}

# silent
$progresspreference = 'silentlycontinue'

# download
iwr "https://github.com/isleap9/Akari-Tool/archive/refs/heads/main.zip" -OutFile "$env:SystemRoot\Temp\Akari-Tool.zip"

# extract
Expand-Archive -Path "$env:SystemRoot\Temp\Akari-Tool.zip" -DestinationPath "$env:SystemRoot\Temp\Akari-Tool" -Force

# rename
Rename-Item -Path "$env:SystemRoot\Temp\Akari-Tool\Akari-Tool-main" -NewName "Akari-Tool" -Force

# desktop path (OneDrive-safe)
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path

# remove existing folder on desktop
Remove-Item -Path "$Desktop\Akari-Tool" -Recurse -Force -ErrorAction SilentlyContinue

# move
Move-Item -Path "$env:SystemRoot\Temp\Akari-Tool\Akari-Tool" -Destination "$Desktop" -Force

# allow
cmd /c "reg add `"HKCR\Applications\powershell.exe\shell\open\command`" /ve /t REG_SZ /d `"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -ExecutionPolicy unrestricted -File \`"`"%1\`"`"`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell`" /v `"ExecutionPolicy`" /t REG_SZ /d `"Unrestricted`" /f >nul 2>&1"
cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell`" /v `"ExecutionPolicy`" /t REG_SZ /d `"Unrestricted`" /f >nul 2>&1"

# unblock
Get-ChildItem -Path "$Desktop\Akari-Tool" -Recurse | Unblock-File

# open
Start-Process "$Desktop\Akari-Tool"

# exit
exit
