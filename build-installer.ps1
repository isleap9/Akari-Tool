# build-installer.ps1 - one command to produce a release-ready installer.
#
#   .\build-installer.ps1
#
# Reads the version from <Version> in AkariTool.csproj, publishes a
# self-contained win-x64 build, then compiles the Inno Setup installer to
# installer-output\AkariTool-Setup-vX.Y.Z.exe - that file is what you upload
# as the GitHub release asset.
#
# Requires Inno Setup 6: winget install JRSoftware.InnoSetup

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# --- 1. Read version from csproj (single source of truth) -------------------
$csproj = Join-Path $root "AkariTool.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    Write-Error ("No Version found in AkariTool.csproj - add e.g. " +
        "<Version>2.0.0</Version> to the first PropertyGroup.")
}
Write-Host "Building Akari Tool v$version" -ForegroundColor Cyan

# --- 2. Publish (self-contained: users don't need the .NET runtime) ---------
dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed." }

# --- 3. Compile installer ----------------------------------------------------
$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles       "Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Error "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup"
}

& $iscc "/DMyAppVersion=$version" (Join-Path $root "installer\AkariTool.iss")
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed." }

$out = Join-Path $root "installer-output\AkariTool-Setup-v$version.exe"
Write-Host ""
Write-Host "Done -> $out" -ForegroundColor Green
Write-Host "Upload this file as the asset on the GitHub release tagged v$version."
