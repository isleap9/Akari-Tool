# AkariOS AME Playbook — Unwanted Apps Removal
# Package list sourced from Chris Titus WinUtil (WPFTweaksDeBloat)
# OneDrive excluded — handled separately by RemoveOneDrive.ps1

$ErrorActionPreference = 'SilentlyContinue'

function Remove-AppxFamily($pattern) {
    Get-AppxPackage -Name $pattern -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like $pattern } |
        Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
}

Write-Host "[DEBLOAT] Starting bloatware removal..." -ForegroundColor Cyan

# ── Chris Titus WinUtil package list ─────────────────────────────────────────

$packages = @(
    "Microsoft.WindowsFeedbackHub",
    "Microsoft.BingNews",
    "Microsoft.BingSearch",
    "Microsoft.BingWeather",
    "Clipchamp.Clipchamp",
    "Microsoft.Todos",
    "Microsoft.PowerAutomateDesktop",
    "Microsoft.MicrosoftSolitaireCollection",
    "Microsoft.WindowsSoundRecorder",
    "Microsoft.MicrosoftStickyNotes",
    "Microsoft.Windows.DevHome",
    "Microsoft.Paint",
    "Microsoft.OutlookForWindows",
    "Microsoft.WindowsAlarms",
    "Microsoft.StartExperiencesApp",
    "Microsoft.GetHelp",
    "Microsoft.ZuneMusic",
    "MicrosoftCorporationII.QuickAssist",
    "MSTeams"
)

foreach ($pkg in $packages) {
    Write-Host "[DEBLOAT] Removing $pkg..." -ForegroundColor Gray
    Get-AppxPackage -Name $pkg -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $pkg } |
        Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
}

# ── Teams classic (legacy installer path) ────────────────────────────────────

$TeamsPath = "$Env:LocalAppData\Microsoft\Teams\Update.exe"
if (Test-Path $TeamsPath) {
    Write-Host "[DEBLOAT] Uninstalling Teams (legacy)..." -ForegroundColor Gray
    Start-Process $TeamsPath -ArgumentList "-uninstall" -Wait -ErrorAction SilentlyContinue
    Remove-Item (Split-Path $TeamsPath) -Recurse -Force -ErrorAction SilentlyContinue
}

# ── Additional removals (kept from AkariOS list) ─────────────────────────────

# Edge stub (full Edge removal handled by RemoveEdge.ps1)
Remove-AppxFamily '*Microsoft.MicrosoftEdge*'

# Xbox
Remove-AppxFamily '*Microsoft.Xbox*'
Remove-AppxFamily '*Microsoft.GamingApp*'

# Copilot / AI
Remove-AppxFamily '*Microsoft.Windows.Ai.Copilot*'
Remove-AppxFamily '*MicrosoftWindows.Client.AIX*'

# Widgets
Remove-AppxFamily '*MicrosoftWindows.Client.WebExperience*'

# Cortana
Remove-AppxFamily '*Microsoft.549981C3F5F10*'

# Phone Link
Remove-AppxFamily '*Microsoft.YourPhone*'
Remove-AppxFamily '*MicrosoftWindows.CrossDevice*'

Write-Host "[DEBLOAT] Done." -ForegroundColor Green