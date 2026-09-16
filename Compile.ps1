param ([switch]$Run)

Write-Host "Compiling Akari Tool..." -ForegroundColor Cyan

$nl = "`r`n"

# Helper — read a file and append with a blank line separator
function Append-File($path) {
    $content = Get-Content -Path $path -Raw -Encoding UTF8
    # Ensure file ends with newline before next block
    return $content.TrimEnd() + $nl + $nl
}

# --- Start script (init, types) ---
$script = Append-File "$PSScriptRoot\scripts\start.ps1"

# --- Functions: private then public (one file at a time, always newline-separated) ---
Get-ChildItem -Path "$PSScriptRoot\functions\private" -File -Filter "*.ps1" -ErrorAction SilentlyContinue |
    ForEach-Object { $script += Append-File $_.FullName }

Get-ChildItem -Path "$PSScriptRoot\functions\public" -File -Filter "*.ps1" -ErrorAction SilentlyContinue |
    ForEach-Object { $script += Append-File $_.FullName }

# --- Config JSON files ---
Get-ChildItem "$PSScriptRoot\config" -Filter "*.json" -ErrorAction SilentlyContinue | ForEach-Object {
    $json = Get-Content -Path $_.FullName -Raw -Encoding UTF8
    $name = $_.BaseName
    $script += "`$sync.configs.$name = @'" + $nl + $json.TrimEnd() + $nl + "'@ | ConvertFrom-Json" + $nl + $nl
}

# --- Embed assets (base64) so akari.ps1 stays self-contained ---
$script += "`$sync.assets = @{}" + $nl
$logoPng = "$PSScriptRoot\assets\AkariLogo.png"
if (Test-Path $logoPng) {
    $script += "`$sync.assets.logo = '" + [Convert]::ToBase64String([IO.File]::ReadAllBytes($logoPng)) + "'" + $nl
}
# Multi-resolution .ico for the window / taskbar icon (crisper than the PNG)
$logoIco = "$PSScriptRoot\assets\AkariLogo.ico"
if (Test-Path $logoIco) {
    $script += "`$sync.assets.icon = '" + [Convert]::ToBase64String([IO.File]::ReadAllBytes($logoIco)) + "'" + $nl
}
# Text assets (assets/text/*) — ~/.reg/.ps1 blobs decoded at runtime into $sync.assets.<filename>
Get-ChildItem "$PSScriptRoot\assets\text" -File -ErrorAction SilentlyContinue | ForEach-Object {
    $script += "`$sync.assets." + $_.BaseName + " = '" + [Convert]::ToBase64String([IO.File]::ReadAllBytes($_.FullName)) + "'" + $nl
}
$script += $nl

# --- Embed XAML (shell + per-tab panels injected at the @PANELS@ marker) ---
$xaml = Get-Content -Path "$PSScriptRoot\xaml\MainWindow.xaml" -Raw -Encoding UTF8

# Stitch each xaml/panels/*.xaml (sorted by NN- prefix) into the shell where the marker sits
$panelsDir = "$PSScriptRoot\xaml\panels"
if (Test-Path $panelsDir) {
    $panels = Get-ChildItem -Path $panelsDir -File -Filter "*.xaml" | Sort-Object Name |
        ForEach-Object { (Get-Content -Path $_.FullName -Raw -Encoding UTF8).TrimEnd() }
    $panelsXaml = ($panels -join ($nl + $nl))
    $xaml = [regex]::Replace($xaml, '[^\r\n]*<!-- @PANELS@[^\r\n]*-->', [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $panelsXaml })
}

$script += "`$inputXML = @'" + $nl + $xaml.TrimEnd() + $nl + "'@" + $nl + $nl

# --- Main script ---
$script += Append-File "$PSScriptRoot\scripts\main.ps1"

Set-Content -Path "$PSScriptRoot\akari.ps1" -Value $script -Encoding UTF8

Write-Host "Done -> akari.ps1" -ForegroundColor Green

if ($Run) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot\akari.ps1`"" -Verb RunAs
}
