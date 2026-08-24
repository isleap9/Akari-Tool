# O&O ShutUp10++ - Run
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/oosubutton/

try {
    $ProgressPreference = 'SilentlyContinue'
    Write-Host "Downloading O&O ShutUp10++..."
    Invoke-WebRequest -Uri "https://dl5.oo-software.com/files/ooshutup10/OOSU10.exe" -OutFile "$Env:Temp\ooshutup10.exe"
    Write-Host "Launching O&O ShutUp10++..."
    Start-Process -FilePath "$Env:Temp\ooshutup10.exe"
    $ProgressPreference = 'Continue'
} catch {
    Write-Error "Couldn't download O&O ShutUp10. Make sure you have an active internet connection."
}