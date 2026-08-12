# build-deelevated.ps1
# ---------------------------------------------------------------------------
# Builds the asInvoker DE-ELEVATED TEST COPY of Akari Tool (used for UIA /
# automated verification, which can't drive an elevated window) into a FULLY
# ISOLATED intermediate + output directory.
#
# WHY (MIGRATION_LOG3 "Phase 6"): earlier de-elevated builds overrode only
# OutputPath and shared the normal obj\ — so the asInvoker Win32-manifest
# intermediate poisoned obj\, and later NORMAL builds re-embedded asInvoker
# instead of requireAdministrator. This script routes the de-elevated build's
# intermediate to obj\DeElevated\ and its output to bin\DeElevated\, so it can
# NEVER write into the obj\ the real (requireAdministrator) build uses. The
# isolation is structural: this is the only supported way to build the test copy.
#
# The asInvoker manifest is generated on the fly from app.manifest INTO the
# isolated obj — nothing asInvoker ever exists outside obj\DeElevated\.
# ---------------------------------------------------------------------------
param(
    [string]$Configuration = 'Debug',
    [string]$Platform      = 'x64'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Locate VS MSBuild (never dotnet build — WinUI PRI/packaging needs VS MSBuild).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null
if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
}
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw "VS MSBuild.exe not found via vswhere."
}

# Isolated leaf intermediate (obj\DeElevated\, via /p:DeElevatedTest=true in the csproj)
# + isolated output (bin\DeElevated\). BaseIntermediateOutputPath stays the default obj\,
# so restore assets are shared and obj\** stays glob-excluded; only the build
# intermediates (incl. the asInvoker manifest) are redirected to obj\DeElevated\.
$objDir = Join-Path $root 'obj\DeElevated\'
$binDir = Join-Path $root 'bin\DeElevated\'
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

# Generate the asInvoker manifest from app.manifest INTO the isolated leaf obj.
$manifest = (Get-Content (Join-Path $root 'app.manifest') -Raw) `
    -replace 'level="requireAdministrator"', 'level="asInvoker"'
$manifestPath = Join-Path $objDir 'app.asinvoker.manifest'
Set-Content -Path $manifestPath -Value $manifest -Encoding utf8

Write-Host "De-elevated build -> IntermediateOutputPath: $objDir  OutputPath: $binDir"

# Restore first (shared obj\ assets), as a SEPARATE pass — a combined /t:Restore,Rebuild
# breaks WinUI XAML codegen (see Phase 6).
& $msbuild 'AkariTool.csproj' /t:Restore `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $msbuild 'AkariTool.csproj' /t:Rebuild `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /p:DeElevatedTest=true `
    /p:ApplicationManifest="$manifestPath" `
    /p:OutputPath="$binDir" `
    /v:minimal /nologo
exit $LASTEXITCODE
