# run-tests.ps1 - the phase's one committed command: build the solution, run the tests,
# print a MEASURED test count.
#
#   powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1
#
# Nine later phases' Build Gate lines cite this exact path and name, so the file name and
# location are a roadmap-wide contract - see Task 1 of plan 01-01. Do NOT rename it and do
# NOT split it: the Build Gate must always name exactly one command.
#
# ---------------------------------------------------------------------------
# -- TWO INVARIANTS THIS FILE OWES THE ROADMAP ------------------------------
# ---------------------------------------------------------------------------
# 1. THIS FILE'S PATH IS A CITATION TARGET. Every remaining roadmap phase quotes
#    `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` verbatim in
#    its Build Gate. Renaming or moving it invalidates gates across nine phases.
# 2. THIS FILE MUST NEVER BE WHERE A VISUAL STUDIO PATH IS BAKED IN. AGENTS.md
#    names a Visual Studio **Community** MSBuild path, under Program Files rather
#    than Program Files (x86) - and that path DOES NOT EXIST on this machine. The
#    only VS-family install here is Visual Studio Build Tools 2026, discovered via
#    vswhere. Both executables are therefore discovered on every single run, and
#    this file deliberately contains NO Visual Studio install literal at all. Do
#    not "simplify" the discovery back to a constant copied out of a doc: a
#    hardcoded path is a roadmap-wide fragility across nine later phases.
#
# ---------------------------------------------------------------------------
# -- EXIT CODES (nine phases will branch on these) ---------------------------
# ---------------------------------------------------------------------------
#   0  green   - no compile error outside the allowlist, and no failing or
#                not-runnable test.
#   1  RED     - the build or the test run produced a real failure: a compile
#                error that is NOT allowlisted, Counters/@failed > 0, or
#                Counters/@notRunnable > 0. The console names the metric that
#                moved ("tests <n> failed" / "errors <n> not allowlisted").
#   2  PRECONDITION - a precondition failed before any verdict was possible:
#                toolchain not found, session not elevated, or an expected test
#                assembly missing. A non-elevated run reports this rather than
#                a green it did not earn.
#
# NOTE ON THE BUILD'S EXIT CODE: MSBuild is EXPECTED to exit non-zero here. The
# WINAPPSDKGENERATEPROJECTPRIFILE PRI175/PRI252 errors are pre-existing, tolerated
# (D-16) and Out of Scope, and every assembly still emits. Therefore
# $LASTEXITCODE is NEVER used as the build verdict anywhere in this script - the
# verdict comes from the error LIST. Gating on the exit code would either fail on
# the tolerated errors or invite someone to widen the allowlist until the exit
# code went green, which is the exact failure D-16 forbids.
#
# ---------------------------------------------------------------------------
# -- WHAT THIS TASK DELIBERATELY DOES NOT DO YET ---------------------------
# ---------------------------------------------------------------------------
# This is the phase's reporting loop. The machine-checked gate (forced rebuild,
# baseline comparison, "no increase, solution-wide" tolerance) is plan 01-02's
# job and widens the -Mode ValidateSet below. Nothing here compares to a recorded
# baseline, and nothing here should pretend to.
#
# Sampling note: the build is incremental (/t:Build), so MSBuild only re-emits
# warnings for files it actually recompiles - the warning number can under-report
# on a warm tree. That is accepted HERE because this mode only REPORTS the
# number; the gate that must trust it forces /t:Rebuild instead.

param(
    [string]$Configuration = 'Debug',
    [string]$Platform      = 'x64',

    # Where vstest writes the TRX. Defaults to a fresh per-run directory under
    # $env:TEMP - NEVER inside the repository. TRX carries machine- and run-specific
    # test names, outcomes and stack traces; committing it would mix run state
    # into the source tree and make every commit noisy.
    [string]$ResultsDirectory,

    # Plan 01-01 ships Build only. Plan 01-02 widens this ValidateSet to
    # Build | Record | Baseline | Allowlist.
    [ValidateSet('Build')]
    [string]$Mode = 'Build'
)

$ErrorActionPreference = 'Stop'

# $root is the REPO ROOT, not this script's directory. build-deelevated.ps1 can use
# $PSScriptRoot directly only because it sits at the repo root; this script lives in
# tools\ (D-15), so it has to walk one level up. Everything below addresses the repo
# relative to this anchor.
$root = Split-Path -Parent $PSScriptRoot

# The tolerated pre-existing error codes (D-16). These two literals are the whole
# allowlist. There is no parameter, environment variable or sidecar file through
# which a third code could be admitted, and there must never be one.
$script:AllowlistedPriTarget = 'WINAPPSDKGENERATEPROJECTPRIFILE'
$script:AllowlistedErrorCodes = @('PRI175', 'PRI252')

# ── Resolve-VSToolchain ───────────────────────────────────────────────────
# Never a hardcoded VS path (see invariant 2 in the header). vswhere is the
# single discovery mechanism, exactly as build-deelevated.ps1:25-34 does it.
function Resolve-VSToolchain {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw ("vswhere.exe not found at '$vswhere'. Install Visual Studio 2022+ " +
               "(Build Tools are sufficient) - the full IDE is not required.")
    }

    $vsroot = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -property installationPath
    if (-not $vsroot) {
        throw ("vswhere.exe found no Visual Studio install with the MSBuild component. " +
               "Install the '.NET desktop development' workload via Build Tools.")
    }

    $msbuild = Join-Path $vsroot 'MSBuild\Current\Bin\MSBuild.exe'
    if (-not (Test-Path $msbuild)) {
        throw "MSBuild.exe not found at '$msbuild' (under the discovered install '$vsroot')."
    }

    $vst = Join-Path $vsroot 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
    if (-not (Test-Path $vst)) {
        throw ("vstest.console.exe not found at '$vst'. It ships with the Visual Studio " +
               "test-platform component; install it via the Build Tools installer.")
    }

    return [pscustomobject]@{
        VsRoot    = $vsroot
        Msbuild   = $msbuild
        Vstest    = $vst
    }
}

# ── Assert-Elevated ───────────────────────────────────────────────────────
# The test suite reads registry state and vstest.console.exe executes repo-built
# assemblies with the invoking token, so tools/ is a privileged surface by
# design. A non-elevated session is reported as an explicit precondition
# failure (exit 2), never as a green - and it is NOT a throw, because the whole
# message must be the output or an operator will miss it.
function Assert-Elevated {
    $isElevated = [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isElevated) {
        Write-Host ''
        Write-Host 'NOT ELEVATED - the test run was SKIPPED.' -ForegroundColor Red
        Write-Host 'This session is not elevated, so the suite (which reads registry state)' -ForegroundColor Red
        Write-Host 'and vstest.console.exe (which executes repo-built assemblies) were not run.' -ForegroundColor Red
        Write-Host 'Re-run from an elevated PowerShell: Start-Process powershell -Verb RunAs' -ForegroundColor Red
        exit 2
    }
}

# ── Get-MsbuildErrors ─────────────────────────────────────────────────────
# Parses the console log for compile errors. MSBuild error text arrives in two
# shapes and both must be recognised:
#   <target> : error : <CODE>: <message>            (task-raised, e.g. the PRI errors)
#   <file>(<line>,<col>): error <CODE>: <message>   (compiler-raised)
function Get-MsbuildErrors {
    param([string]$ConsoleLogPath)

    if (-not (Test-Path $ConsoleLogPath)) { return @() }

    $pattern = '^\s*(?<target>\S+)\s*:\s*error\s*:\s*(?<code>[A-Z]+\d+)|^.*\):\s*error\s+(?<code2>[A-Z]+[0-9]+):'

    $errors = foreach ($line in Get-Content -LiteralPath $ConsoleLogPath -ErrorAction SilentlyContinue) {
        if ($line -match $pattern) {
            $code = if ($matches['code']) { $matches['code'] } else { $matches['code2'] }
            [pscustomobject]@{
                Code    = $code
                Target  = $matches['target']
                Message = $line.Trim()
            }
        }
    }

    return @($errors)
}

# ── Get-MsbuildWarnings ───────────────────────────────────────────────────
# Counts warning lines from the WarningsOnly file logger, which is the
# deterministic source for this number (the console log can interleave them).
# NOTE: on an incremental build this can under-report - see the sampling note in
# the header. Mode Baseline (plan 01-02) forces /t:Rebuild so the number is real.
function Get-MsbuildWarnings {
    param([string]$WarningLogPath)

    if (-not (Test-Path $WarningLogPath)) { return 0 }

    $count = 0
    foreach ($line in Get-Content -LiteralPath $WarningLogPath -ErrorAction SilentlyContinue) {
        if ($line -match ': warning [A-Z]+[0-9]+:') { $count++ }
    }
    return $count
}

# ── Invoke-Vstest ─────────────────────────────────────────────────────────
# Runs the given assemblies through vstest.console.exe with a TRX logger into the
# per-run results directory, and returns the process exit code. vstest's own exit
# code is reported but never used as the verdict: the authoritative signals are
# the TRX Counters (failed / notRunnable), read by Read-TrxCounters below.
function Invoke-Vstest {
    param(
        [string]$VstPath,
        [string[]]$AssemblyPaths,
        [string]$ResultsDirectoryPath,
        [string]$TrxFileName
    )

    & $VstPath @AssemblyPaths /Platform:x64 `
        "/logger:trx;LogFileName=$TrxFileName" "/ResultsDirectory:$ResultsDirectoryPath" | Out-Null

    return $LASTEXITCODE
}

# ── Read-TrxCounters ──────────────────────────────────────────────────────
# Reads TestRun/ResultSummary/Counters with [xml]. Never scrapes the
# `Total tests:` console line, which is verbosity- and locale-dependent.
# A missing TRX or a missing Counters element is a hard failure: a zero-count
# parse must never be mistaken for success.
function Read-TrxCounters {
    param([string]$TrxPath)

    if (-not (Test-Path $TrxPath)) {
        Write-Host "FAIL: TRX results file was not written to '$TrxPath'." -ForegroundColor Red
        Write-Host 'tests <unknown> - the test count could not be measured.' -ForegroundColor Red
        exit 1
    }

    try {
        [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
    } catch {
        Write-Host "FAIL: could not parse TRX at '$TrxPath': $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    $counters = $trx.TestRun.ResultSummary.Counters
    if (-not $counters) {
        Write-Host "FAIL: TRX at '$TrxPath' has no TestRun/ResultSummary/Counters element." -ForegroundColor Red
        Write-Host 'tests <unknown> - the test count could not be measured.' -ForegroundColor Red
        exit 1
    }

    # Read defensively: a missing attribute is 0, never null-crash.
    $read = {
        param($name)
        $attr = $counters.GetAttribute($name)
        if ([string]::IsNullOrWhiteSpace($attr)) { return 0 }
        $n = 0
        if ([int]::TryParse($attr, [ref]$n)) { return $n }
        return 0
    }

    $total       = & $read 'total'
    $executed    = & $read 'executed'
    $passed      = & $read 'passed'
    $failed      = & $read 'failed'
    $notRunnable = & $read 'notRunnable'
    $notExecuted = & $read 'notExecuted'
    # vstest prints "Skipped: n" and records those results with outcome
    # NotExecuted, but the Counters element does not expose a 'skipped'
    # attribute - so the measured "ran nothing" figure is total - executed.
    # Derived from the run, never a literal.
    $notRun = [Math]::Max($total - $executed, 0)

    return [pscustomobject]@{
        Total       = $total
        Executed    = $executed
        Passed      = $passed
        Failed      = $failed
        NotRunnable = $notRunnable
        NotExecuted = $notExecuted
        NotRun      = $notRun
    }
}

# ═════════════════════════════════════════════════════════════════════════
# MAIN
# ═════════════════════════════════════════════════════════════════════════

Write-Host ''
Write-Host "Akari Tool - build + test ($Mode, $Configuration|$Platform)" -ForegroundColor Cyan

# 1. Preconditions. Exit 2 on any of these; never a throw (operator must see it).
Assert-Elevated

$toolchain = Resolve-VSToolchain

Write-Host "Using MSBuild: $($toolchain.Msbuild)" -ForegroundColor DarkGray
Write-Host "Using vstest:  $($toolchain.Vstest)" -ForegroundColor DarkGray

# Per-run scratch space under $env:TEMP. The TRX and both build logs live here so
# nothing run-specific ever lands inside the repository.
if (-not $ResultsDirectory) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $ResultsDirectory = Join-Path $env:TEMP "akari-tests\$stamp"
}
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$consoleLog  = Join-Path $ResultsDirectory 'msbuild-console.log'
$warningLog  = Join-Path $ResultsDirectory 'msbuild-warnings.log'
$trxName     = "run-$stamp.trx"
$trxPath     = Join-Path $ResultsDirectory $trxName

# 2. Expected test assemblies - EXPLICIT PATHS, resolved from -Configuration.
#    NEVER a recursive glob: bin\DeElevated\ holds an asInvoker duplicate of every
#    assembly (build-deelevated.ps1), so a glob would run a manifest the runner did
#    not intend. Test-Path each and name the missing one (build-installer.ps1:86-91).
function Get-ExpectedAssemblyPaths {
    param([string]$RepoRoot, [string]$Config)

    $testProjects = @(
        @{ Project = 'AkariTool.Core.Tests'           ; Csp = 'tests\AkariTool.Core.Tests\AkariTool.Core.Tests.csproj' },
        @{ Project = 'AkariTool.Infrastructure.Tests'; Csp = 'tests\AkariTool.Infrastructure.Tests\AkariTool.Infrastructure.Tests.csproj' }
    )

    $paths = foreach ($entry in $testProjects) {
        $cspPath = Join-Path $RepoRoot $entry.Csp
        # The TFM folder is read from the project rather than hardcoded, so a TFM
        # bump does not silently break the runner.
        $tfm = ([xml](Get-Content -LiteralPath $cspPath -Raw)).Project.PropertyGroup.TargetFramework
        if (-not $tfm) { throw "No <TargetFramework> in '$cspPath'." }
        Join-Path $RepoRoot "tests\$($entry.Project)\bin\$Config\$tfm\$($entry.Project).dll"
    }

    return @($paths)
}

$expectedAssemblies = Get-ExpectedAssemblyPaths -RepoRoot $root -Config $Configuration
foreach ($dll in $expectedAssemblies) {
    if (-not (Test-Path $dll)) {
        Write-Host ''
        Write-Host "FAIL: expected test assembly not found:" -ForegroundColor Red
        Write-Host "      $dll" -ForegroundColor Red
        Write-Host 'errors <unknown> - the build did not emit every expected test assembly.' -ForegroundColor Red
        exit 2
    }
}

# 3. Restore FIRST, as a separate pass, then Build. A combined /t:Restore,Rebuild
#    breaks WinUI XAML codegen - see build-deelevated.ps1:52-53. Restore is the
#    only step whose exit code is honoured: a failed restore is genuinely fatal and
#    carries no allowlist meaning.
Write-Host 'Restoring...' -ForegroundColor DarkGray
& $toolchain.Msbuild 'AkariTool.sln' /t:Restore `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /v:minimal /nologo | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host "FAIL: MSBuild restore failed (exit $LASTEXITCODE)." -ForegroundColor Red
    Write-Host 'errors <restore> - the solution could not be restored.' -ForegroundColor Red
    exit 1
}

# NOTE: /p:SelfContained and /p:WindowsAppSDKSelfContained must NOT be passed here.
# They are GLOBAL properties and would flow into every ProjectReference
# (AkariTool.Core / AkariTool.Infrastructure / the test projects are class
# libraries), where Microsoft.WindowsAppSDK.Base.targets hard-errors with
# "WindowsAppSDKSelfContained should not be applied to a class library."
# build-installer.ps1:63-68 documents the same trap and the same workaround:
# project-file properties do NOT flow through ProjectReferences, a command-line
# global property does.
Write-Host 'Building...' -ForegroundColor DarkGray
$consoleLines = & $toolchain.Msbuild 'AkariTool.sln' /t:Build `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /v:minimal /nologo "/flp:LogFile=$warningLog;WarningsOnly" 2>&1 |
    ForEach-Object { $_.ToString() }

# Deliberately NOT checking $LASTEXITCODE here: the build is EXPECTED to exit
# non-zero because PRI175/PRI252 are tolerated (see the header). The verdict is
# computed from the error LIST below.
$consoleLines | Set-Content -LiteralPath $consoleLog -Encoding UTF8
$consoleLines | ForEach-Object { Write-Host $_ }

$buildErrors = @(Get-MsbuildErrors -ConsoleLogPath $consoleLog)
$warningCount = Get-MsbuildWarnings -WarningLogPath $warningLog

# 4. Classify the error list. The rule, named so plan 01-02's tightening reads as
#    a tightening: an error is tolerated ONLY when its target is
#    WINAPPSDKGENERATEPROJECTPRIFILE AND its code is PRI175 or PRI252. Anything
#    else - including an unknown code raised by that same target - fails.
$notAllowlisted = @()
$byCode = @{}
foreach ($err in $buildErrors) {
    if (-not $byCode.ContainsKey($err.Code)) { $byCode[$err.Code] = 0 }
    $byCode[$err.Code]++

    $isAllowlisted =
        ($err.Target -eq $script:AllowlistedPriTarget) -and
        ($script:AllowlistedErrorCodes -contains $err.Code)
    if (-not $isAllowlisted) { $notAllowlisted += $err }
}

$breakdown = ($byCode.GetEnumerator() | Sort-Object Name |
    ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '

if ($buildErrors.Count -gt 0) {
    Write-Host ''
    Write-Host "Build errors by code: $breakdown" -ForegroundColor DarkGray
    foreach ($grp in ($buildErrors | Group-Object Code | Sort-Object Name)) {
        Write-Host "  [$($grp.Name)] x$($grp.Count)" -ForegroundColor DarkGray
        $grp.Group | Select-Object -First 1 | ForEach-Object {
            Write-Host "    e.g. $($_.Message)" -ForegroundColor DarkGray
        }
    }
}

# 5. Run the tests. Exact paths, x64 platform, TRX logger into the temp dir.
$vstestExit = Invoke-Vstest -VstPath $toolchain.Vstest `
    -AssemblyPaths $expectedAssemblies `
    -ResultsDirectoryPath $ResultsDirectory `
    -TrxFileName $trxName

$counters = Read-TrxCounters -TrxPath $trxPath

# 6. Verdict.
#
# The summary format string is bound to a variable BEFORE -f is applied: PowerShell's
# -f binds tighter than +, so an inline ("a" + "b") -f x would format only "b" and leave
# "a"'s placeholders as literal text.
$summaryFormat = 'tests {0} total, {1} passed, {2} failed, {3} notExecuted (total-executed), notRunnable {4}; errors {5} [{6}]; warnings {7}; vstest exit {8}'

Write-Host ''
Write-Host ($summaryFormat -f
             $counters.Total, $counters.Passed, $counters.Failed, $counters.NotRun,
             $counters.NotRunnable, $buildErrors.Count, $breakdown, $warningCount, $vstestExit) `
    -ForegroundColor $(if ($notAllowlisted.Count -eq 0 -and $counters.Failed -eq 0 -and
                            $counters.NotRunnable -eq 0) { 'Green' } else { 'Red' })

# Failure reporting names the metric that moved, so a red is legible from the
# console output alone without re-running anything.
if ($notAllowlisted.Count -gt 0) {
    Write-Host ''
    Write-Host "errors $($notAllowlisted.Count) not allowlisted" -ForegroundColor Red
    $notAllowlisted | ForEach-Object { Write-Host "  $($_.Message)" -ForegroundColor Red }
}
if ($counters.Failed -gt 0)      { Write-Host "tests $($counters.Failed) failed" -ForegroundColor Red }
if ($counters.NotRunnable -gt 0) { Write-Host "tests $($counters.NotRunnable) not runnable" -ForegroundColor Red }

if ($notAllowlisted.Count -gt 0 -or $counters.Failed -gt 0 -or $counters.NotRunnable -gt 0) {
    exit 1
}

Write-Host "Run artifacts (outside the repository): $ResultsDirectory" -ForegroundColor DarkGray
exit 0