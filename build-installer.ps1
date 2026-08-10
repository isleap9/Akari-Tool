# build-installer.ps1 - one command to produce a release-ready installer.
#
#   .\build-installer.ps1
#
# Reads the version from <Version> in AkariTool.csproj, publishes a FULLY
# SELF-CONTAINED unpackaged WinUI 3 (win-x64) build, then compiles the Inno
# Setup installer to installer-output\AkariTool-Setup-vX.Y.Z.exe - that file is
# what you upload as the GitHub release asset.
#
# -- Runtime strategy: FULLY SELF-CONTAINED ---------------------------------
# This is an UNPACKAGED WinUI 3 app (WindowsPackageType=None) with no Store /
# MSIX dependency, so the target machine may have neither the .NET Desktop
# Runtime nor the Windows App Runtime installed. The publish therefore bundles
# BOTH:
#   * SelfContained=true              -> the .NET 8 runtime ships in-app
#   * WindowsAppSDKSelfContained=true -> the Windows App SDK runtime ships in-app
# The installer then just xcopy-deploys the whole publish folder. No prerequisite
# install, no Windows App Runtime bootstrapper on the target. (The csproj keeps
# these OFF so the dev inner loop stays framework-dependent and fast; they are
# turned ON here, at publish time only.)
#
# -- Build engine: VS MSBuild, not `dotnet` ---------------------------------
# Unpackaged WinUI 3 needs the MSBuild that ships with Visual Studio for the
# PRI / resource targets; the standalone .NET SDK lacks them. We locate it via
# vswhere.
#
# Requires: Visual Studio 2022+ (with the WinUI/.NET desktop workload) and
#           Inno Setup 6 (winget install JRSoftware.InnoSetup).

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$tfm  = "net8.0-windows10.0.19041.0"   # keep in sync with <TargetFramework>
$publishDir = Join-Path $root "bin\x64\Release\$tfm\win-x64\publish"
$buildDir   = Join-Path $root "bin\x64\Release\$tfm\win-x64"

# --- 1. Read version from csproj (single source of truth) -------------------
$csproj = Join-Path $root "AkariTool.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    Write-Error ("No Version found in AkariTool.csproj - add e.g. " +
        "<Version>2.0.0</Version> to the first PropertyGroup.")
}
Write-Host "Building Akari Tool v$version (unpackaged WinUI 3, fully self-contained)" -ForegroundColor Cyan

# --- 2. Locate VS MSBuild via vswhere ---------------------------------------
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found. Install Visual Studio 2022+ (Build Tools are sufficient)."
}
$msbuild = & $vswhere -latest -prerelease -products * `
    -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { Write-Error "MSBuild.exe not found via vswhere. Is the .NET desktop / WinUI workload installed?" }
Write-Host "Using MSBuild: $msbuild" -ForegroundColor DarkGray

# --- 3. Publish (VS MSBuild, self-contained .NET + Windows App SDK) ----------
& $msbuild $csproj `
    /t:Publish `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:RuntimeIdentifier=win-x64 `
    /p:SelfContained=true `
    /p:WindowsAppSDKSelfContained=true `
    /p:PublishReadyToRun=true `
    /p:PublishSingleFile=false `
    /p:AppxPackage=false `
    /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { Write-Error "MSBuild publish failed." }

# --- 3a. Carry the app PRI into publish (WinUI-unpackaged publish gap) -------
# The Publish target copies the FRAMEWORK PRIs (Microsoft.UI.pri, ...) but drops
# the app's own AkariTool.pri. Without it, XAML resource resolution fails and the
# app launches to NO WINDOW (process alive, blank). Copy it from the build output
# and hard-fail if it is not there, so a broken payload can never ship.
$priSrc = Join-Path $buildDir "AkariTool.pri"
if (-not (Test-Path $priSrc)) { Write-Error "AkariTool.pri not found in build output ($priSrc) - cannot assemble a working payload." }
Copy-Item $priSrc (Join-Path $publishDir "AkariTool.pri") -Force
if (-not (Test-Path (Join-Path $publishDir "AkariTool.pri"))) {
    Write-Error "AkariTool.pri missing from publish output - WinUI resource resolution would fail on the target."
}
Write-Host "Payload assembled: $publishDir" -ForegroundColor DarkGray

# --- 4. Compile installer ----------------------------------------------------
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
# Code signing (SignPath) is a separate, later step - nothing here blocks it.
