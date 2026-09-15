# All non-trivial Windows tweaks delegate to their exact upstream Ultimate script
# (1:1 by construction, always current). Pure openers stay inline.

# Taskbar / Start Menu
function Invoke-BtnTaskbarClean   { Invoke-UltimateScript -Path "6 Windows/1 Start Menu Taskbar.ps1" -Status "Start Menu Taskbar opened — pick Clean." }
function Invoke-BtnTaskbarDefault { Invoke-UltimateScript -Path "6 Windows/1 Start Menu Taskbar.ps1" -Status "Start Menu Taskbar opened — pick Default." }

# Start Menu Layout
function Invoke-BtnStartMenu25H2 { Invoke-UltimateScript -Path "6 Windows/2 Start Menu Layout.ps1" -Status "Start Menu Layout opened." }
function Invoke-BtnStartMenu24H2 { Invoke-UltimateScript -Path "6 Windows/2 Start Menu Layout.ps1" -Status "Start Menu Layout opened." }

# Start Menu Shortcuts
function Invoke-BtnStartShortcuts { Invoke-UltimateScript -Path "6 Windows/3 Start Menu Shortcuts.ps1" -Status "Start Menu Shortcuts opened." }

# Context Menu
function Invoke-BtnContextClean   { Invoke-UltimateScript -Path "6 Windows/4 Context Menu.ps1" -Status "Context Menu opened — pick Clean." }
function Invoke-BtnContextDefault { Invoke-UltimateScript -Path "6 Windows/4 Context Menu.ps1" -Status "Context Menu opened — pick Default." }

# Theme / Black cosmetics
function Invoke-BtnThemeBlack     { Invoke-UltimateScript -Path "6 Windows/5 Theme Black.ps1" -Status "Theme Black opened." }
function Invoke-BtnWallpaperBlack { Invoke-UltimateScript -Path "6 Windows/6 Signout Lockscreen Wallpaper Black.ps1" -Status "Signout/Lockscreen/Wallpaper Black opened." }
function Invoke-BtnAccountBlack   { Invoke-UltimateScript -Path "6 Windows/7 User Account Pictures Black.ps1" -Status "User Account Pictures Black opened." }

# Widgets
function Invoke-BtnWidgetsOff     { Invoke-UltimateScript -Path "6 Windows/8 Widgets.ps1" -Status "Widgets opened — pick Off." }
function Invoke-BtnWidgetsDefault { Invoke-UltimateScript -Path "6 Windows/8 Widgets.ps1" -Status "Widgets opened — pick Default." }

# Copilot
function Invoke-BtnCopilotOff     { Invoke-UltimateScript -Path "6 Windows/9 Copilot.ps1" -Status "Copilot opened — pick Off." }
function Invoke-BtnCopilotDefault { Invoke-UltimateScript -Path "6 Windows/9 Copilot.ps1" -Status "Copilot opened — pick Default." }

# Bloatware
function Invoke-BtnBloatwareRemove { Invoke-UltimateScript -Path "6 Windows/13 Bloatware.ps1" -Status "Bloatware opened." }
function Invoke-BtnBloatwareCheck  { Start-Process "ms-settings:appsfeatures" }

# Game Bar
function Invoke-BtnGamebarOff     { Invoke-UltimateScript -Path "6 Windows/19 Gamebar.ps1" -Status "Game Bar opened — pick Off." }
function Invoke-BtnGamebarDefault { Invoke-UltimateScript -Path "6 Windows/19 Gamebar.ps1" -Status "Game Bar opened — pick Default." }

# Edge & WebView
function Invoke-BtnEdgeUninstall { Invoke-UltimateScript -Path "6 Windows/20 Edge & WebView.ps1" -Confirm "This will uninstall Microsoft Edge. Continue?" -Status "Uninstalling Edge... this may take a minute." }
function Invoke-BtnEdgeRestore   { Start-Process "https://www.microsoft.com/en-us/edge/download" }

# Notepad Settings
function Invoke-BtnNotepad { Invoke-UltimateScript -Path "6 Windows/21 Notepad Settings.ps1" -Status "Notepad Settings opened." }

# Device Manager / Network power savings & wake
function Invoke-BtnDevPowerOff     { Invoke-UltimateScript -Path "6 Windows/25 Device Manager Power Savings & Wake.ps1" -Status "Device Manager Power opened — pick Off." }
function Invoke-BtnDevPowerDefault { Invoke-UltimateScript -Path "6 Windows/25 Device Manager Power Savings & Wake.ps1" -Status "Device Manager Power opened — pick Default." }
function Invoke-BtnNetPowerOff     { Invoke-UltimateScript -Path "6 Windows/26 Network Adapter Power Savings & Wake.ps1" -Status "Network Adapter Power opened — pick Off." }
function Invoke-BtnNetPowerDefault { Invoke-UltimateScript -Path "6 Windows/26 Network Adapter Power Savings & Wake.ps1" -Status "Network Adapter Power opened — pick Default." }

# Network IPv4 Only
function Invoke-BtnIPv4Only  { Invoke-UltimateScript -Path "6 Windows/27 Network IPv4 Only.ps1" -Status "Network IPv4 Only opened — pick On." }
function Invoke-BtnIPDefault { Invoke-UltimateScript -Path "6 Windows/27 Network IPv4 Only.ps1" -Status "Network IPv4 Only opened — pick Default." }

# Write Cache Buffer Flushing
function Invoke-BtnWriteCacheOff     { Invoke-UltimateScript -Path "6 Windows/28 Write Cache Buffer Flushing.ps1" -Status "Write Cache opened — pick Off." }
function Invoke-BtnWriteCacheDefault { Invoke-UltimateScript -Path "6 Windows/28 Write Cache Buffer Flushing.ps1" -Status "Write Cache opened — pick Default." }

# Power Plan
function Invoke-BtnPowerPlanOn      { Invoke-UltimateScript -Path "6 Windows/29 Power Plan.ps1" -Status "Power Plan opened — pick On." }
function Invoke-BtnPowerPlanDefault { Invoke-UltimateScript -Path "6 Windows/29 Power Plan.ps1" -Status "Power Plan opened — pick Default." }

# Timer Resolution
function Invoke-BtnTimerOn      { Invoke-UltimateScript -Path "6 Windows/30 Timer Resolution.ps1" -Status "Timer Resolution opened — pick On." }
function Invoke-BtnTimerDefault { Invoke-UltimateScript -Path "6 Windows/30 Timer Resolution.ps1" -Status "Timer Resolution opened — pick Default." }

# UAC
function Invoke-BtnUacOff     { Invoke-UltimateScript -Path "6 Windows/31 UAC.ps1" -Status "UAC opened — pick Off." }
function Invoke-BtnUacDefault { Invoke-UltimateScript -Path "6 Windows/31 UAC.ps1" -Status "UAC opened — pick Default." }

# Core Isolation
function Invoke-BtnCoreIsolation { Invoke-UltimateScript -Path "6 Windows/32 Core Isolation.ps1" -Status "Core Isolation opened." }

# Defender Optimize
function Invoke-BtnDefenderOptimize { Invoke-UltimateScript -Path "6 Windows/33 Defender Optimize.ps1" -Status "Defender Optimize opened — pick Optimize." }
function Invoke-BtnDefenderDefault  { Invoke-UltimateScript -Path "6 Windows/33 Defender Optimize.ps1" -Status "Defender Optimize opened — pick Default." }

# Autoruns (Startup Tasks & Apps Check)
function Invoke-BtnAutoruns { Invoke-UltimateScript -Path "6 Windows/34 Autoruns Startup Tasks & Apps Check.ps1" -Status "Autoruns opened." }

# Cleanup
function Invoke-BtnCleanup { Invoke-UltimateScript -Path "6 Windows/35 Cleanup.ps1" -Status "Cleanup opened." }

# Restore Point
function Invoke-BtnRestorePoint { Invoke-UltimateScript -Path "6 Windows/36 Restore Point.ps1" -Status "Restore Point opened." }

# Pure openers
function Invoke-BtnControlPanel { Start-Process control.exe }
function Invoke-BtnSound        { Start-Process "mmsys.cpl" }
