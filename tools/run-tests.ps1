# run-tests.ps1 - the phase's one committed command: build the solution, run the tests,
# and (in the gate modes) compare the result against a recorded baseline.
#
#   powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1
#   powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline
#
# Nine later phases' Build Gate lines cite this exact path and name, so the file name and
# location are a roadmap-wide contract - see Task 1 of plan 01-01. Do NOT rename it and do
# NOT split it: the Build Gate must always name exactly one command.
#
# ---------------------------------------------------------------------------
# -- THE FOUR MODES ---------------------------------------------------------
# ---------------------------------------------------------------------------
#   Build      Report only. Incremental /t:Build, run the tests, print the measured
#              counts. Compares against nothing and gates on nothing.
#   Record     The same forced rebuild as Baseline, then WRITE tools\baseline.json from
#              what this run measured. Record-only: it never compares and never gates.
#              The written file is the ONLY way that file is ever produced.
#   Baseline   THE GATE. Forced /t:Rebuild, then compare warnings / errors / tests /
#              emitted assemblies against tools\baseline.json and exit non-zero on any
#              regression in either direction of the gated metrics.
#              *** THIS IS THE COMMAND EVERY LATER PHASE'S BUILD GATE QUOTES VERBATIM. ***
#   Allowlist  Apply the gate's OWN allowlist predicate to a file instead of a build log
#              and print a per-line table. This is how the allowlist's teeth are proven
#              rather than assumed.
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
#   0  green   - no compile error outside the allowlist, no failing or
#                not-runnable test, and (in Baseline) no metric regression.
#   1  RED     - the build or the test run produced a real failure: a compile
#                error that is NOT allowlisted, Counters/@failed > 0,
#                Counters/@notRunnable > 0, or - in Baseline mode - a warning
#                count above the recorded one, a test total below it, or a
#                missing emitted assembly. The console names the metric that moved
#                ("warnings 16 -> 17" / "errors 1 not allowlisted").
#   2  PRECONDITION - a precondition failed before any verdict was possible:
#                toolchain not found, session not elevated, a reference file that
#                is missing or unparseable, or a fixture containing no error lines.
#                A non-elevated run reports this rather than a green it did not earn.
#
# NOTE ON THE BUILD'S EXIT CODE: MSBuild is EXPECTED to exit non-zero here. The
# WINAPPSDKGENERATEPROJECTPRIFILE PRI175/PRI252 errors are pre-existing, tolerated
# (D-16) and Out of Scope, and every assembly still emits. Therefore
# $LASTEXITCODE is NEVER used as the build verdict anywhere in this script - the
# verdict comes from the error LIST. The single exception, both times inside the
# restore-failure check, is legitimate: a failed restore is genuinely fatal and
# carries no allowlist meaning. Gating on the exit code would either fail on the
# tolerated errors or invite someone to widen the allowlist until the exit code
# went green, which is the exact failure D-16 forbids.
#
# ---------------------------------------------------------------------------
# -- WHY THE PRI175/PRI252 PAIR IS TOLERATED BUT NEVER REQUIRED -------------
# ---------------------------------------------------------------------------
# Measured 2026-10-05: the pair appears on a COLD build and is ABSENT once
# vendor\WinUI.Framework\bin\...\WinUI.Framework.pri (960 bytes) exists on disk.
# The set is therefore NOT reproducible run-to-run. The gate must TOLERATE these
# two codes when present and must NEVER assert that they are present - a gate that
# required "errors == 2" would pass or fail depending on whether a PRI file
# happened to be sitting in the tree. tools/baseline.json records
# errors.allowlistedCount for provenance; the comparison never reads it.
#
# ---------------------------------------------------------------------------
# -- WHY THE GATE FORCES /t:Rebuild (D-13) ----------------------------------
# ---------------------------------------------------------------------------
# MSBuild re-emits a warning only for a file it actually RECOMPILES. An
# incremental /t:Build on a warm tree therefore reports near-zero warnings while the
# tree is full of them - a false green, which is worse than no gate at all. There is
# deliberately NO switch to skip the rebuild. Do not add one.

param(
    [string]$Configuration = 'Debug',
    [string]$Platform      = 'x64',

    # Where vstest writes the TRX. Defaults to a fresh per-run directory under
    # $env:TEMP - NEVER inside the repository. TRX carries machine- and run-specific
    # test names, outcomes and stack traces; committing it would mix run state
    # into the source tree and make every commit noisy.
    [string]$ResultsDirectory,

    [ValidateSet('Build', 'Record', 'Baseline', 'Allowlist')]
    [string]$Mode = 'Build',

    # The machine-readable reference for -Mode Baseline. Empty means
    # "tools\baseline.json", resolved against the repo root below (a param default
    # cannot reference $PSScriptRoot, which is not bound yet at parse time).
    # -Mode Record has no such input - it WRITES this file.
    [string]$BaselinePath,

    # Log-shaped input for -Mode Allowlist. Empty means
    # "tools\fixtures\allowlist-self-test.txt", resolved the same way.
    [string]$AllowlistSelfTestPath
)

$ErrorActionPreference = 'Stop'

# $root is the REPO ROOT, not this script's directory. build-deelevated.ps1 can use
# $PSScriptRoot directly only because it sits at the repo root; this script lives in
# tools\ (D-15), so it has to walk one level up. Everything below addresses the repo
# relative to this anchor.
$root = Split-Path -Parent $PSScriptRoot

# The tolerated pre-existing error codes (D-16). THE WHOLE ALLOWLIST IS THE TWO
# HASH TABLES BELOW. There is no parameter, environment variable or sidecar file
# through which a third code could be admitted, and there must never be one.
#
# Both halves of the predicate are required: an error is tolerated only when it
# names this target AND carries one of these codes. An unknown code raised by that
# same target is NOT tolerated.
#
# WHAT TO DO IF A REAL RUN SURFACES ANOTHER ERROR CODE: record it verbatim in
# .planning/phases/01-baseline-spikes-test-harness/01-BASELINE.md and escalate.
# Do NOT add it here. D-16 forbids widening the allowlist to excuse any other
# compile error hiding behind these two.
$script:ErrorAllowlist = @(
    @{ Target = 'WINAPPSDKGENERATEPROJECTPRIFILE'; Code = 'PRI175' }
    @{ Target = 'WINAPPSDKGENERATEPROJECTPRIFILE'; Code = 'PRI252' }
)

# ── Resolve-RepoPath ──────────────────────────────────────────────────────
# Turns a possibly-empty, possibly-relative parameter into an absolute path under
# the repo root. Keeps every default in this file anchored to $root instead of to
# the process working directory, which is not the repo when a caller invokes the
# script from elsewhere.
function Resolve-RepoPath {
    param([string]$Root, [string]$Path, [string]$DefaultRelative)

    if ([string]::IsNullOrWhiteSpace($Path)) { $Path = $DefaultRelative }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $Root $Path)
}

# ── Get-MsbuildErrorList ──────────────────────────────────────────────────
# Parses an MSBuild-shaped log into structured error records. MSBuild error text
# arrives in two shapes and both must be recognised:
#   <file>(<line>,<col>): error <CODE>: <message>   [<proj>::<Target>]
#   <Target> : error : <CODE>: <message>            (task-raised, no file)
# The target is taken from the trailing "[<proj>::<Target>]" canonical suffix when
# present, and otherwise from the leading token of the task-raised shape. A line
# with a code but NO discoverable target is reported with Target = '' and is
# therefore never allowlisted - which is the correct, conservative answer.
function Get-MsbuildErrorList {
    param([string]$LogPath)

    if (-not (Test-Path $LogPath)) { return @() }

    $found = New-Object System.Collections.ArrayList

    foreach ($line in (Get-Content -LiteralPath $LogPath -ErrorAction SilentlyContinue)) {
        $code = $null
        $target = ''

        if ($line -match ':\s*error\s+(?<c>[A-Za-z]+[0-9]+)\s*:') {
            $code = $matches['c']
        }
        elseif ($line -match '^\s*(?<t>[A-Za-z0-9_.\-]+)\s*:\s*error\s*:\s*(?<c2>[A-Za-z]+[0-9]+)\s*:') {
            $target = $matches['t']
            $code = $matches['c2']
        }

        if (-not $code) { continue }

        # The canonical trailing suffix wins over any leading token: MSBuild puts the
        # raising target there even for lines that begin with a file path.
        if ($line -match '\[(?<proj>.*?)::(?<t2>[^\[\]]+)\]\s*$') {
            $target = $matches['t2'].Trim()
        }

        [void]$found.Add([pscustomobject]@{
            Code    = $code
            Target  = $target
            Message = $line.Trim()
        })
    }

    return @($found)
}

# ── Test-ErrorAllowlisted ─────────────────────────────────────────────────
# THE gate predicate, and the exact same function -Mode Allowlist exercises.
# There is deliberately no second copy of this rule anywhere in this file.
function Test-ErrorAllowlisted {
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    foreach ($entry in $script:ErrorAllowlist) {
        if ($ErrorRecord.Code -eq $entry.Code -and $ErrorRecord.Target -eq $entry.Target) {
            return $true
        }
    }
    return $false
}

# ── Get-MsbuildWarnings ───────────────────────────────────────────────────
# Counts DISTINCT solution-wide warning instances from the WarningsOnly file
# logger, which is the deterministic source for this number (the console log can
# interleave them and repeats lines).
#
# WHY "distinct" AND NOT A RAW LINE COUNT: MSBuild re-emits the same diagnostic
# once per consuming project, so a raw count double-counts every transitive-consumer
# warning. Measured on this solution: 28 raw warning lines collapse to 16 distinct
# instances. D-14 specifies a SOLUTION-WIDE tolerance, so the number compared must
# be the solution-wide one; a raw count would make the gate trip for a reason that
# is not a regression.
#
# The identity key is (source location, code, message) with the trailing
# "[<project>]" attribution stripped - that suffix is precisely the part that
# differs between the duplicate emissions of one warning.
#
# PER-PROJECT warning counts are deliberately NOT gated (D-14): gating them would
# turn Phase 2's expected deletions of dead code into extra work.
function Get-MsbuildWarnings {
    param([string]$WarningLogPath)

    if (-not (Test-Path $WarningLogPath)) { return 0 }

    $distinct = New-Object System.Collections.Generic.HashSet[string]

    foreach ($line in (Get-Content -LiteralPath $WarningLogPath -ErrorAction SilentlyContinue)) {
        if ($line -notmatch ': warning [A-Za-z]+[0-9]+:') { continue }

        $key = $line
        if ($key -match '^(?<rest>.*?)\s*\[(?<proj>[^\[\]]+)\]\s*$') {
            $key = $matches['rest']
        }
        [void]$distinct.Add($key.Trim())
    }

    return $distinct.Count
}

# ── Get-MsbuildWarningsByCode ─────────────────────────────────────────────
# The recorded (never gated) per-code split, so a future reader can see WHICH
# codes make up the solution-wide number instead of guessing from the total.
function Get-MsbuildWarningsByCode {
    param([string]$WarningLogPath)

    $counts = [ordered]@{}
    if (-not (Test-Path $WarningLogPath)) { return $counts }

    $seen = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in (Get-Content -LiteralPath $WarningLogPath -ErrorAction SilentlyContinue)) {
        if ($line -notmatch ': warning (?<code>[A-Za-z]+[0-9]+):') { continue }

        # Read $matches into a local IMMEDIATELY: the next -match below overwrites
        # the automatic variable, and reading it after would yield null.
        $code = $matches['code']

        $key = $line
        if ($key -match '^(?<rest>.*?)\s*\[(?<proj>[^\[\]]+)\]\s*$') { $key = $matches['rest'] }
        $key = $key.Trim()
        if (-not $seen.Add($key)) { continue }

        if ($counts.Contains($code)) { $counts[$code] = [int]$counts[$code] + 1 }
        else { $counts[$code] = 1 }
    }

    return $counts
}

# ── Get-CsprojElementValue ────────────────────────────────────────────────
# Reads one element's value out of a csproj, correctly.
#
# WHY NOT THE OBVIOUS $xml.Project.PropertyGroup.TargetFramework: that is the
# PowerShell XML ADAPTER's projection, and it enumerates EVERY PropertyGroup,
# yielding an EMPTY STRING for each one that does not declare the element.
# AkariTool.App.csproj has three PropertyGroups and only the first declares
# TargetFramework, so the adapter handed back a 3-element Object[] whose string
# form is "net10.0-windows10.0.26100.0  " - two trailing spaces. Interpolated
# into a path that produced "…\net10.0-windows10.0.26100.0  \AkariTool.App.Tests.dll",
# which Test-Path rejected in one place and vstest rejected in another. The test
# csprojs each have a single PropertyGroup, which is why the same expression was
# harmless for them and silently wrong for the App.
#
# SelectNodes is used instead, and a CONFLICTING set of values throws rather than
# picking one: a csproj that declares TargetFramework conditionally cannot be
# resolved statically, and guessing would point the inventory at a directory this
# build never writes - the exact false green documented on
# Get-ExpectedAssemblyPaths.
function Get-CsprojElementValue {
    param(
        [string]$ProjectPath,
        [string]$ElementName,
        [string]$Fallback
    )

    if (-not (Test-Path $ProjectPath)) { throw "Project file not found: '$ProjectPath'" }

    $doc = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
    $values = @(
        $doc.SelectNodes("//*[local-name()='$ElementName']") |
            ForEach-Object { ([string]$_.InnerText).Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )

    if ($values.Count -eq 0) {
        if ($PSBoundParameters.ContainsKey('Fallback')) { return $Fallback }
        throw "No <$ElementName> in '$ProjectPath'."
    }
    if ($values.Count -gt 1) {
        throw ("'$ProjectPath' declares <$ElementName> more than once with differing " +
               "values ($($values -join ', ')). The assembly inventory cannot be " +
               "resolved statically - remove the conditional or extend this helper.")
    }
    return [string]$values[0]
}

# ── Get-TargetFramework ───────────────────────────────────────────────────
# Reads <TargetFramework> from a csproj so a TFM bump does not silently break the
# inventory below. Never hardcoded.
function Get-TargetFramework {
    param([string]$ProjectPath)

    return (Get-CsprojElementValue -ProjectPath $ProjectPath -ElementName 'TargetFramework')
}

# ── Get-RuntimeIdentifier ─────────────────────────────────────────────────
# Reads <RuntimeIdentifier> so the App output path's RID segment is derived, not
# spelled. Defaults to win-x64 (the only RID this solution declares) so a missing
# element cannot empty the path.
function Get-RuntimeIdentifier {
    param([string]$ProjectPath, [string]$Default = 'win-x64')

    return (Get-CsprojElementValue -ProjectPath $ProjectPath -ElementName 'RuntimeIdentifier' -Fallback $Default)
}

# ── Get-ExpectedAssemblyPaths ─────────────────────────────────────────────
# The emitted-assembly inventory: TWELVE repo-relative paths, derived from
# -Configuration / -Platform rather than hardcoding Debug.
#
# TWELVE, NOT THREE. The three test assemblies prove tests ran; the other nine
# prove the SHIPPING output emitted - which is what "the solution builds" has to
# mean when a phase's real deliverable is an App-layer change.
#
# EXACT PATHS ONLY, NEVER A GLOB. Two reasons, both measured:
#   * src\AkariTool.App\bin\...\win-x64\ holds ~224 dll/exe files because the App
#     project is self-contained and so the whole WinAppSDK runtime payload is
#     copied beside the app. A *.dll count there is meaningless.
#   * bin\DeElevated\ holds an asInvoker duplicate of every assembly
#     (build-deelevated.ps1 /p:DeElevatedTest=true), so a recursive glob would
#     assert on, or execute, a manifest the runner did not intend. No entry below
#     may ever contain "DeElevated".
#
# THE PLATFORM SEGMENT IS PER-PROJECT, AND GETTING IT WRONG IS A FALSE GREEN.
# Measured from the solution build's own "<Project> -> <output>" console lines:
#
#   Plat 'yes' -> bin\<Plat>\<Config>\<tfm>[\<rid>]   AkariTool.App, App.Tests
#   Plat 'no'  -> bin\<Config>\<tfm>                  Core, Infrastructure,
#                                                    Core.Tests, Infra.Tests
#
# Only the two x64-only projects get the segment. AkariTool.App declares
# Platforms=x64. AkariTool.App.Tests ProjectReferences it, and an MSIL test
# assembly referencing an AMD64 one is rejected with "error MSB3270: There was a
# mismatch between the processor architecture of the project being built MSIL and
# the reference AMD64" - so App.Tests is mapped to the real x64 platform in
# AkariTool.sln as well. Everything else is mapped to Any CPU.
#
# This is not a cosmetic detail. src\AkariTool.App\bin\Debug\... ALSO exists in
# this working tree, left behind by an earlier direct-csproj build, and it holds
# a complete, plausible-looking AkariTool.dll. Asserting the un-segmented path
# would Test-Path true against a stale tree indefinitely. Prefer reading the
# "<Project> -> <path>" line out of this run's own console log over trusting any
# hand-written table here.
#
# vendor\WinGet.Interop and vendor\WinUI.Framework are NOT in AkariTool.sln yet
# are emitted transitively. Assembly PRESENCE is what the inventory records -
# solution membership is not the gate.
function Get-ExpectedAssemblyPaths {
    param([string]$RepoRoot, [string]$Config, [string]$Plat)

    $appCsproj   = Join-Path $RepoRoot 'src\AkariTool.App\AkariTool.App.csproj'
    $coreTfm     = Get-TargetFramework -ProjectPath (Join-Path $RepoRoot 'src\AkariTool.Core\AkariTool.Core.csproj')
    $infraTfm    = Get-TargetFramework -ProjectPath (Join-Path $RepoRoot 'src\AkariTool.Infrastructure\AkariTool.Infrastructure.csproj')
    $appTfm      = Get-TargetFramework -ProjectPath $appCsproj
    $rid         = Get-RuntimeIdentifier -ProjectPath $appCsproj
    $coreTestTfm = Get-TargetFramework -ProjectPath (Join-Path $RepoRoot 'tests\AkariTool.Core.Tests\AkariTool.Core.Tests.csproj')
    $infraTestTfm = Get-TargetFramework -ProjectPath (Join-Path $RepoRoot 'tests\AkariTool.Infrastructure.Tests\AkariTool.Infrastructure.Tests.csproj')
    $appTestTfm  = Get-TargetFramework -ProjectPath (Join-Path $RepoRoot 'tests\AkariTool.App.Tests\AkariTool.App.Tests.csproj')

    # The App's own output directory, and the plain Any CPU ones.
    $appOut   = "src\AkariTool.App\bin\$Plat\$Config\$appTfm\$rid"
    $coreOut  = "src\AkariTool.Core\bin\$Config\$coreTfm"
    $infraOut = "src\AkariTool.Infrastructure\bin\$Config\$infraTfm"

    $entries = @(
        @{ Dir = $appOut;   File = 'AkariTool.dll' }
        @{ Dir = $appOut;   File = 'AkariTool.exe' }
        @{ Dir = $appOut;   File = 'AkariTool.Core.dll' }
        @{ Dir = $appOut;   File = 'AkariTool.Infrastructure.dll' }
        # The two vendored assemblies. Not in AkariTool.sln; emitted transitively
        # through the App's ProjectReferences.
        @{ Dir = $appOut;   File = 'WinGet.Interop.dll' }
        @{ Dir = $appOut;   File = 'WinUI.Framework.dll' }
        # The .pri is the on-disk EVIDENCE that the tolerated PRI175/PRI252 pair is
        # tolerated rather than fatal: the App still produced its resource index.
        @{ Dir = $appOut;   File = 'AkariTool.pri' }
        @{ Dir = $coreOut;  File = 'AkariTool.Core.dll' }
        @{ Dir = $infraOut; File = 'AkariTool.Infrastructure.dll' }
        @{ Dir = "tests\AkariTool.Core.Tests\bin\$Config\$coreTestTfm";            File = 'AkariTool.Core.Tests.dll' }
        @{ Dir = "tests\AkariTool.Infrastructure.Tests\bin\$Config\$infraTestTfm"; File = 'AkariTool.Infrastructure.Tests.dll' }
        @{ Dir = "tests\AkariTool.App.Tests\bin\$Plat\$Config\$appTestTfm";        File = 'AkariTool.App.Tests.dll' }
    )

    $paths = foreach ($e in $entries) { Join-Path $RepoRoot (Join-Path $e.Dir $e.File) }
    return @($paths)
}

# ── Get-RepoRelativePath ──────────────────────────────────────────────────
# Turns an absolute inventory path into the repo-relative form that
# tools/baseline.json stores, so the file is machine-independent.
function Get-RepoRelativePath {
    param([string]$Root, [string]$Path)

    $prefix = $Root.TrimEnd('\', '/') + '\'
    if ($Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $Path.Substring($prefix.Length)
    }
    return $Path
}

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

# ── Get-SdkBuildToolsVersion ──────────────────────────────────────────────
# Reads the resolved Microsoft.Windows.SDK.BuildTools version out of the NuGet
# cache. PROVENANCE ONLY - tools/baseline.json records it and the comparison
# never reads it. A gated toolchain version would fail the gate for a reason that
# is not a regression.
function Get-SdkBuildToolsVersion {
    $pkgRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    if (-not (Test-Path $pkgRoot)) { return 'unavailable' }

    $versions = @(Get-ChildItem $pkgRoot -Directory | ForEach-Object {
        $v = $null
        if ([version]::TryParse($_.Name, [ref]$v)) { $v }
    } | Sort-Object)
    if ($versions.Count -eq 0) { return 'unavailable' }
    return [string]$versions[-1]
}

# ── Get-ToolVersionString ─────────────────────────────────────────────────
# ProductVersion of an executable, read from disk. Never run the executable just
# to ask its version.
function Get-ToolVersionString {
    param([string]$Path)

    if (-not (Test-Path $Path)) { return 'unavailable' }
    $v = (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($v)) { return 'unavailable' }
    return $v
}

# ── Invoke-Vstest ─────────────────────────────────────────────────────────
# Runs the given assemblies through vstest.console.exe with a TRX logger into the
# per-run results directory.
#
# vstest's PROCESS EXIT CODE IS DELIBERATELY NOT CAPTURED OR REPORTED. The
# authoritative signals are the TRX Counters (@failed / @notRunnable, read by
# Read-TrxCounters) plus the presence of the TRX itself, and a raw process exit
# code is not one of them - it can be non-zero for reasons that have nothing to
# do with a test outcome. Keeping one fewer exit-code number in the script also
# keeps one fewer number available for somebody to gate on later, which is the
# exact failure mode this script's header warns about.
function Invoke-Vstest {
    param(
        [string]$VstPath,
        [string[]]$AssemblyPaths,
        [string]$ResultsDirectoryPath,
        [string]$TrxFileName
    )

    & $VstPath @AssemblyPaths /Platform:x64 `
        "/logger:trx;LogFileName=$TrxFileName" "/ResultsDirectory:$ResultsDirectoryPath" | Out-Null
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

# ── Get-TrxPerAssembly ────────────────────────────────────────────────────
# The per-assembly test split, from the join documented below. RECORDED, never
# gated (D-14 forbids per-project gating; recording it costs nothing and makes a
# later regression legible).
#
# The join, and why the obvious substitutes are wrong:
#     TestRun/Results/UnitTestResult/@testId
#   + TestRun/TestDefinitions/UnitTest[@id]/@className   -> declaring class
# The declaring assembly is the className's namespace root. Filtering
# UnitTestResult by @testName or by console output is NOT equivalent and must not be
# substituted: the same test class name can appear in more than one assembly.
#
# A result whose className matches no known test namespace lands in the explicit
# '<unclassified>' bucket rather than being silently attributed - a test that
# escapes the split is visible instead of missing.
function Get-TrxPerAssembly {
    param([string]$TrxPath, [string[]]$KnownNamespaces)

    [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw

    $idToClass = @{}
    foreach ($u in @($trx.TestRun.TestDefinitions.UnitTest)) {
        if ($u.id) { $idToClass[[string]$u.id] = [string]$u.className }
    }

    $counts = [ordered]@{}
    foreach ($ns in $KnownNamespaces) { $counts[$ns] = 0 }
    $unclassified = '<unclassified>'
    $counts[$unclassified] = 0

    foreach ($r in @($trx.TestRun.Results.UnitTestResult)) {
        $className = ''
        if ($r.testId -and $idToClass.ContainsKey([string]$r.testId)) {
            $className = $idToClass[[string]$r.testId]
        }

        $bucket = $unclassified
        foreach ($ns in $KnownNamespaces) {
            if ($className -like "$ns.*") { $bucket = $ns; break }
        }
        $counts[$bucket] = [int]$counts[$bucket] + 1
    }

    return $counts
}

# ── Compare-ToBaseline ────────────────────────────────────────────────────
# The verdict computation. Four metrics, each with ok / baseline / measured /
# detail. NO BRANCH IN HERE READS $LASTEXITCODE - every input is a structured
# artefact produced by this run:
#
#   warnings    MSBuild WarningsOnly file logger, de-duplicated (D-14). Gate on
#               INCREASE only: reductions pass silently so Phase 2's expected
#               deletions of dead code do not become extra work.
#   errors      The parsed error LIST minus the allowlist. Gate on ANY remaining
#               line, whatever the recorded count was. The recorded
#               allowlistedCount is NEVER compared - see the header's
#               "tolerated but never required" note.
#   tests       TRX Counters. A total BELOW the recorded one fails (tests went
#               missing - a regression in the opposite direction, which must not
#               pass silently); a total above it passes (new tests are welcome).
#               @failed and @notRunnable are hard failures REGARDLESS of the
#               baseline. @notExecuted is recorded, never gated: one deliberate
#               skip already exists and skipping is not a defect.
#   assemblies  Test-Path on the twelve inventory entries. Any missing path fails
#               and is named.
function Compare-ToBaseline {
    param(
        [Parameter(Mandatory = $true)]$Baseline,
        [Parameter(Mandatory = $true)]$Measurement
    )

    $warningsOk = ($Measurement.Warnings -le $Baseline.warnings)

    $errorsOk = ($Measurement.ErrorsNotAllowlisted.Count -eq 0)

    $testsOk =
        ($Measurement.Tests.Total -ge [int]$Baseline.tests.total) -and
        ($Measurement.Tests.Failed -eq 0) -and
        ($Measurement.Tests.NotRunnable -eq 0)

    $assembliesOk = ($Measurement.Assemblies.Missing.Count -eq 0)

    return [ordered]@{
        Warnings = [ordered]@{
            Ok       = $warningsOk
            Baseline = [int]$Baseline.warnings
            Measured = [int]$Measurement.Warnings
            Detail   = "warnings $($Baseline.warnings) -> $($Measurement.Warnings)"
        }
        Errors = [ordered]@{
            Ok       = $errorsOk
            Baseline = [int]$Baseline.errors.allowlistedCount
            Measured = [int]$Measurement.ErrorsNotAllowlisted.Count
            Detail   = "errors $($Measurement.ErrorsNotAllowlisted.Count) not allowlisted of $($Measurement.ErrorsTotal) parsed"
        }
        Tests = [ordered]@{
            Ok          = $testsOk
            Baseline    = [int]$Baseline.tests.total
            Measured    = [int]$Measurement.Tests.Total
            Failed      = [int]$Measurement.Tests.Failed
            NotRunnable = [int]$Measurement.Tests.NotRunnable
            Detail      = "tests $($Measurement.Tests.Total) vs baseline $($Baseline.tests.total); failed $($Measurement.Tests.Failed); notRunnable $($Measurement.Tests.NotRunnable)"
        }
        Assemblies = [ordered]@{
            Ok       = $assembliesOk
            Baseline = @($Baseline.assemblies).Count
            Measured = [int]$Measurement.Assemblies.Present
            Detail   = "assemblies $($Measurement.Assemblies.Present) of $($Measurement.Assemblies.Expected.Count) emitted"
        }
        Ok = ($warningsOk -and $errorsOk -and $testsOk -and $assembliesOk)
    }
}

# ── Write-VerdictLine ─────────────────────────────────────────────────────
# EXACTLY ONE PASS (...) or FAIL (...) line per run. A metric that failed is
# rendered "recorded->measured"; a metric that passed keeps its documented
# shape ("<=" for warnings, ">=" for tests). That way the failing metric AND both
# of its values are on the line, which is what makes a red legible without
# re-running anything.
#
# Write-Host, never Write-Error: $ErrorActionPreference is 'Stop', so Write-Error
# would raise a terminating error whose message format hides the numbers.
function Write-VerdictLine {
    param($Verdict)

    $seg = @()
    $seg += if ($Verdict.Warnings.Ok) { "warnings $($Verdict.Warnings.Baseline)<=$($Verdict.Warnings.Measured)" }
            else { "warnings $($Verdict.Warnings.Baseline)->$($Verdict.Warnings.Measured)" }
    $seg += "errors $($Verdict.Errors.Measured) allowlisted"
    $seg += if ($Verdict.Tests.Ok) { "tests $($Verdict.Tests.Measured)>=$($Verdict.Tests.Baseline)" }
            else { "tests $($Verdict.Tests.Measured)->$($Verdict.Tests.Baseline)" }

    $line = if ($Verdict.Ok) {
        "PASS ($($seg -join ', '))"
    } else {
        "FAIL ($($seg -join ', '))"
    }

    Write-Host ''
    Write-Host $line -ForegroundColor $(if ($Verdict.Ok) { 'Green' } else { 'Red' })
}

# ── Write-BaselineFile ────────────────────────────────────────────────────
# The ONLY writer of tools/baseline.json. Written from what the run measured;
# never hand-edited, because a hand-edited baseline is indistinguishable from a
# measured one once committed and makes every later gate fail for the wrong
# reason. UTF-8 WITHOUT BOM - a BOM would make the file's first byte sequence
# 0xEF 0xBB 0xBF and every diff of it noisy.
function Write-BaselineFile {
    param([string]$Path, $Document)

    $json = $Document | ConvertTo-Json -Depth 8
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, $utf8NoBom)
}

# ── Read-BaselineFile ─────────────────────────────────────────────────────
# Precondition validation for -Mode Baseline, run BEFORE MSBuild is invoked: a bad
# reference path is an operator error, and burning the most expensive command in
# the phase to discover a typo is not acceptable. Exits 2 naming the file.
function Read-BaselineFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Host ''
        Write-Host "FAIL: baseline reference file not found:" -ForegroundColor Red
        Write-Host "      $Path" -ForegroundColor Red
        Write-Host 'Record one first: powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Record' -ForegroundColor Red
        exit 2
    }

    try {
        $doc = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        Write-Host ''
        Write-Host "FAIL: baseline reference file is not parseable JSON:" -ForegroundColor Red
        Write-Host "      $Path" -ForegroundColor Red
        Write-Host "      $($_.Exception.Message)" -ForegroundColor Red
        exit 2
    }

    # A baseline without provenance cannot be shown to have been MEASURED, and a
    # hand-authored one that looked measured is the exact tampering risk this
    # check exists to refuse. Refuse to compare rather than compare optimistically.
    if (-not $doc.PSObject.Properties['recordedOn'] -or [string]::IsNullOrWhiteSpace([string]$doc.recordedOn)) {
        Write-Host ''
        Write-Host 'FAIL: the baseline reference carries no recordedOn provenance stamp.' -ForegroundColor Red
        Write-Host "      $Path" -ForegroundColor Red
        Write-Host 'A baseline is written only by -Mode Record from the run that just completed.' -ForegroundColor Red
        exit 2
    }

    foreach ($required in @('warnings', 'tests', 'assemblies', 'errors')) {
        if (-not $doc.PSObject.Properties[$required]) {
            Write-Host ''
            Write-Host "FAIL: the baseline reference has no '$required' block." -ForegroundColor Red
            Write-Host "      $Path" -ForegroundColor Red
            exit 2
        }
    }

    return $doc
}

# ── Assert-ErrorAllowlistShape ────────────────────────────────────────────
# Structural tripwire on the allowlist literal itself, run by -Mode Allowlist.
# With the two-entry literal above, both checks are satisfied by construction -
# which is the point. They exist so that a FUTURE edit which adds a third entry,
# or drops the Code half and leaves only the Target half (turning the pair into a
# wildcard for every error that target raises), fails LOUDLY here instead of
# silently turning the allowlist into a no-op.
function Assert-ErrorAllowlistShape {
    $breaches = 0

    for ($i = 0; $i -lt $script:ErrorAllowlist.Count; $i++) {
        $entry = $script:ErrorAllowlist[$i]
        if ([string]::IsNullOrWhiteSpace([string]$entry.Target) -or
            [string]::IsNullOrWhiteSpace([string]$entry.Code)) {
            $breaches++
            Write-Host ''
            Write-Host "ALLOWLIST BREACH: entry #$($i + 1) does not name BOTH a target and a code." -ForegroundColor Red
            Write-Host 'An entry missing either half is a wildcard - it would tolerate every error' -ForegroundColor Red
            Write-Host 'that half matches. Every entry must name both (D-16).' -ForegroundColor Red
        }
    }

    if ($script:ErrorAllowlist.Count -ne 2) {
        $breaches++
        Write-Host ''
        Write-Host "ALLOWLIST BREACH: the allowlist holds $($script:ErrorAllowlist.Count) entries; D-16 fixes it at exactly 2." -ForegroundColor Red
        Write-Host 'A new error code is a finding to escalate and record verbatim, not a' -ForegroundColor Red
        Write-Host 'tolerance to add. Do not widen this literal.' -ForegroundColor Red
    }

    return $breaches
}

# ── Write-AllowlistTable ──────────────────────────────────────────────────
# -Mode Allowlist: the gate's own predicate, applied to a file instead of a build
# log. This is what makes the allowlist's teeth PROVABLE rather than assumed.
#
# Read the exit code carefully: this mode exits NON-ZERO precisely BECAUSE the
# committed fixture deliberately contains a non-allowlisted line. A
# non-allowlisted line being REJECTED is the correct, desired outcome. The
# dangerous direction - a non-allowlisted line being TOLERATED - is reported as a
# distinct, explicitly labelled row so it can never be confused with the ordinary
# case.
function Write-AllowlistTable {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Host ''
        Write-Host 'FAIL: allowlist self-test fixture not found:' -ForegroundColor Red
        Write-Host "      $Path" -ForegroundColor Red
        exit 2
    }

    $errors = @(Get-MsbuildErrorList -LogPath $Path)
    if ($errors.Count -eq 0) {
        Write-Host ''
        Write-Host 'FAIL: the allowlist self-test fixture contains no parseable error lines:' -ForegroundColor Red
        Write-Host "      $Path" -ForegroundColor Red
        exit 2
    }

    Write-Host ''
    Write-Host "Allowlist self-test over '$Path'" -ForegroundColor Cyan
    Write-Host ''
    Write-Host ('  {0,-3} {1,-10} {2,-38} {3}' -f 'ln', 'code', 'target', 'verdict')
    Write-Host ('  {0,-3} {1,-10} {2,-38} {3}' -f '---', '----------', '--------------------------------------', '-------')

    $notTolerated = 0
    $ln = 0
    foreach ($e in $errors) {
        $ln++
        $allowed = Test-ErrorAllowlisted -ErrorRecord $e
        if ($allowed) {
            $verdict = 'TOLERATED (allowlisted)'
            $colour = 'Yellow'
        } else {
            $notTolerated++
            $verdict = 'REJECTED (not allowlisted)'
            $colour = 'Green'
        }
        Write-Host ('  {0,-3} {1,-10} {2,-38} {3}' -f $ln, $e.Code, $e.Target, $verdict) -ForegroundColor $colour
    }

    Write-Host ''
    if ($notTolerated -gt 0) {
        Write-Host "ALLOWLIST SELF-TEST: $notTolerated of $($errors.Count) error line(s) correctly REJECTED." -ForegroundColor Yellow
        Write-Host 'A rejected line is the DESIRED outcome here: the fixture exists to prove a real' -ForegroundColor DarkGray
        Write-Host 'compile error cannot hide behind the tolerated PRI175/PRI252 pair (D-16).' -ForegroundColor DarkGray
    } else {
        Write-Host "ALLOWLIST SELF-TEST: 0 of $($errors.Count) error line(s) rejected - the fixture proves nothing." -ForegroundColor Red
        Write-Host 'The committed fixture must contain at least one NON-allowlisted line.' -ForegroundColor Red
    }

    # Defensive: with the two-entry literal above this is unreachable. It exists so
    # that a future edit which widens the allowlist fails LOUDLY here instead of
    # silently turning the allowlist into a no-op.
    $breach = Assert-ErrorAllowlistShape
    if ($breach -gt 0) { exit 1 }

    if ($notTolerated -eq 0) { exit 1 }
    exit 1
}

# ═════════════════════════════════════════════════════════════════════════
# MAIN
# ═════════════════════════════════════════════════════════════════════════

$baselineFile = Resolve-RepoPath -Root $root -Path $BaselinePath -DefaultRelative 'tools\baseline.json'
$selfTestFile = Resolve-RepoPath -Root $root -Path $AllowlistSelfTestPath -DefaultRelative 'tools\fixtures\allowlist-self-test.txt'

# ── -Mode Allowlist runs FIRST, before elevation, toolchain discovery and
#    MSBuild. It touches no build, no registry and no test assembly, so it must
#    not print an MSBuild invocation line and must not burn a rebuild. A caller
#    proving the allowlist's teeth gets the answer in seconds.
if ($Mode -eq 'Allowlist') {
    # Write-AllowlistTable terminates the script itself with the gate's exit code
    # for that log. The explicit exit below is a belt-and-braces guard.
    Write-AllowlistTable -Path $selfTestFile
    exit 1
}

# ── -Mode Baseline validates its reference file before anything expensive.
if ($Mode -eq 'Baseline') {
    $baselineDoc = Read-BaselineFile -Path $baselineFile
}

Write-Host ''
Write-Host "Akari Tool - build + test ($Mode, $Configuration|$Platform)" -ForegroundColor Cyan

# 1. Preconditions. Exit 2 on any of these; never a throw (operator must see it).
Assert-Elevated

$toolchain = Resolve-VSToolchain

Write-Host "Using MSBuild: $($toolchain.Msbuild)" -ForegroundColor DarkGray
Write-Host "Using vstest:  $($toolchain.Vstest)" -ForegroundColor DarkGray

# Per-run scratch space under $env:TEMP. The TRX and both build logs live here so
# nothing run-specific ever lands inside the repository.
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $env:TEMP "akari-tests\$stamp"
}
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$consoleLog  = Join-Path $ResultsDirectory 'msbuild-console.log'
$warningLog  = Join-Path $ResultsDirectory 'msbuild-warnings.log'
$trxName     = "run-$stamp.trx"
$trxPath     = Join-Path $ResultsDirectory $trxName

# 2. The emitted-assembly inventory. Resolved here (so a missing csproj is a clean
#    precondition failure) but ASSERTED after the build - the assertion's whole
#    meaning is "the build emitted this", so it must run on the build's output.
$expectedAssemblies = Get-ExpectedAssemblyPaths -RepoRoot $root -Config $Configuration -Plat $Platform
$testAssemblies = @(
    $expectedAssemblies |
        Where-Object { $_ -match 'AkariTool\.(Core|Infrastructure|App)\.Tests\.dll$' }
)

# 3. The build target. Record and Baseline both force /t:Rebuild (D-13) because
#    an incremental build re-emits warnings only for the files it recompiles, so
#    it reports near-zero on a warm tree and the gate would pass on a tree full of
#    them. Build stays incremental because it only REPORTS the number. Written as
#    two explicit branches so each gate mode states its own requirement.
$buildTarget = 'Build'
if ($Mode -eq 'Record') {
    $buildTarget = 'Rebuild'
}
elseif ($Mode -eq 'Baseline') {
    $buildTarget = 'Rebuild'
}

# 4. Restore FIRST, as a separate pass, then build. A combined /t:Restore,Rebuild
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
Write-Host "Building (/t:$buildTarget)..." -ForegroundColor DarkGray
$consoleLines = & $toolchain.Msbuild 'AkariTool.sln' "/t:$buildTarget" `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /v:minimal /nologo "/flp:LogFile=$warningLog;WarningsOnly" 2>&1 |
    ForEach-Object { $_.ToString() }

# Deliberately NOT checking $LASTEXITCODE here: the build is EXPECTED to exit
# non-zero because PRI175/PRI252 are tolerated (see the header). The verdict is
# computed from the error LIST below.
$consoleLines | Set-Content -LiteralPath $consoleLog -Encoding UTF8
$consoleLines | ForEach-Object { Write-Host $_ }

$buildErrors = @(Get-MsbuildErrorList -LogPath $consoleLog)
$warningCount = Get-MsbuildWarnings -WarningLogPath $warningLog
$warningByCode = Get-MsbuildWarningsByCode -WarningLogPath $warningLog

# 5. Classify the error list against the two-entry allowlist. An error is
#    tolerated ONLY when its target is WINAPPSDKGENERATEPROJECTPRIFILE AND its
#    code is PRI175 or PRI252. Anything else - including an unknown code raised by
#    that same target - fails, and its line is printed verbatim.
$allowlisted = @()
$notAllowlisted = @()
foreach ($err in $buildErrors) {
    if (Test-ErrorAllowlisted -ErrorRecord $err) { $allowlisted += $err } else { $notAllowlisted += $err }
}

$breakdown = ((@($buildErrors) | Group-Object Code | Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' ')

if ($buildErrors.Count -gt 0) {
    Write-Host ''
    Write-Host "Build errors by code: $breakdown" -ForegroundColor DarkGray
    foreach ($grp in (@($buildErrors) | Group-Object Code | Sort-Object Name)) {
        Write-Host "  [$($grp.Name)] x$($grp.Count)" -ForegroundColor DarkGray
        $grp.Group | Select-Object -First 1 | ForEach-Object {
            Write-Host "    e.g. $($_.Message)" -ForegroundColor DarkGray
        }
    }
}

# 6. The emitted-assembly inventory, asserted now that the build has run. Each
#    path is exact; nothing under bin\DeElevated\ is ever named (see the header of
#    Get-ExpectedAssemblyPaths).
$missingAssemblies = @()
$presentCount = 0
foreach ($dll in $expectedAssemblies) {
    if (Test-Path $dll) { $presentCount++ } else { $missingAssemblies += (Get-RepoRelativePath -Root $root -Path $dll) }
}

# 7. Run the tests. Exact paths, x64 platform, TRX logger into the temp dir.
# Get-ExpectedAssemblyPaths already returns ABSOLUTE paths (rooted at $root), so
# the filtered subset is already what vstest must be handed. Do NOT join $root a
# second time - that produced a doubled path and vstest's "test source file ...
# was not found".
$testAssemblyPaths = @($testAssemblies)
Write-Host "Test assemblies handed to vstest ($($testAssemblyPaths.Count)):" -ForegroundColor DarkGray
$testAssemblyPaths | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
Invoke-Vstest -VstPath $toolchain.Vstest `
    -AssemblyPaths $testAssemblyPaths `
    -ResultsDirectoryPath $ResultsDirectory `
    -TrxFileName $trxName

$counters = Read-TrxCounters -TrxPath $trxPath
$testNamespaces = @(
    'AkariTool.Core.Tests',
    'AkariTool.Infrastructure.Tests',
    'AkariTool.App.Tests'
)
$perAssembly = Get-TrxPerAssembly -TrxPath $trxPath -KnownNamespaces $testNamespaces

$measurement = [pscustomobject]@{
    Warnings              = $warningCount
    WarningsByCode        = $warningByCode
    ErrorsTotal           = $buildErrors.Count
    ErrorsAllowlisted     = $allowlisted
    ErrorsNotAllowlisted  = $notAllowlisted
    Tests                 = $counters
    PerAssembly           = $perAssembly
    Assemblies            = [pscustomobject]@{
        Expected = @($expectedAssemblies | ForEach-Object { Get-RepoRelativePath -Root $root -Path $_ })
        Present  = $presentCount
        Missing  = $missingAssemblies
    }
}

# 8. Mode dispatch: record, gate, or report.

if ($Mode -eq 'Record') {
    # RECORD ONLY. This branch never compares and never gates: writing a baseline
    # and judging against one are different operations and mixing them would make
    # the recorded numbers depend on the judgement.
    $doc = [ordered]@{
        schemaVersion = 1
        recordedOn    = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        toolchain     = [ordered]@{
            msbuildVersion          = (Get-ToolVersionString -Path $toolchain.Msbuild)
            vstestVersion           = (Get-ToolVersionString -Path $toolchain.Vstest)
            visualStudioInstallPath = $toolchain.VsRoot
            sdkBuildToolsVersion    = (Get-SdkBuildToolsVersion)
        }
        configuration = $Configuration
        platform      = $Platform
        buildTarget   = "/t:$buildTarget"
        assemblies    = @($measurement.Assemblies.Expected)
        errors        = [ordered]@{
            total                = $measurement.ErrorsTotal
            allowlistedCodes     = @($script:ErrorAllowlist | ForEach-Object { $_.Code })
            allowlistedCount     = @($measurement.ErrorsAllowlisted).Count
            notAllowlistedCount  = @($measurement.ErrorsNotAllowlisted).Count
            notAllowlisted       = @($measurement.ErrorsNotAllowlisted | ForEach-Object { $_.Message })
        }
        warnings      = $measurement.Warnings
        warningsByCode = $measurement.WarningsByCode
        tests         = [ordered]@{
            total        = $counters.Total
            executed     = $counters.Executed
            passed       = $counters.Passed
            failed       = $counters.Failed
            notRunnable  = $counters.NotRunnable
            notExecuted  = $counters.NotExecuted
            notRun       = $counters.NotRun
            perAssembly  = $measurement.PerAssembly
        }
    }

    $baselineDir = Split-Path -Parent $baselineFile
    if (-not (Test-Path $baselineDir)) { New-Item -ItemType Directory -Force -Path $baselineDir | Out-Null }
    Write-BaselineFile -Path $baselineFile -Document $doc

    Write-Host ''
    Write-Host "RECORDED $baselineFile" -ForegroundColor Green
    $recordFormat = 'tests {0} total, {1} passed, {2} failed, {3} notExecuted (total-executed), notRunnable {4}; errors {5} [{6}]; warnings {7} distinct'
    Write-Host ($recordFormat -f
                 $counters.Total, $counters.Passed, $counters.Failed, $counters.NotRun,
                 $counters.NotRunnable, $measurement.ErrorsTotal, $breakdown, $warningCount) `
        -ForegroundColor $(if ($notAllowlisted.Count -eq 0 -and $counters.Failed -eq 0 -and
                                $counters.NotRunnable -eq 0) { 'Green' } else { 'Red' })
    Write-Host "Run artifacts (outside the repository): $ResultsDirectory" -ForegroundColor DarkGray

    # Record still refuses to write a green record of a broken run: a baseline
    # captured over a failing build would make the gate fail on its first
    # comparison for a reason that has nothing to do with a later regression.
    if ($notAllowlisted.Count -gt 0 -or $counters.Failed -gt 0 -or $counters.NotRunnable -gt 0) {
        Write-Host 'errors/test failures present - the run above is NOT a usable baseline.' -ForegroundColor Red
        exit 1
    }
    exit 0
}

if ($Mode -eq 'Baseline') {
    $verdict = Compare-ToBaseline -Baseline $baselineDoc -Measurement $measurement

    # Detail first, then exactly one verdict line - so the numbers a human needs
    # are on screen before the single machine-readable conclusion.
    if (-not $verdict.Warnings.Ok) {
        Write-Host ''
        Write-Host "warnings $($verdict.Warnings.Baseline) -> $($verdict.Warnings.Measured) - warning count INCREASED (D-14)" -ForegroundColor Red
    }
    if (-not $verdict.Errors.Ok) {
        Write-Host ''
        Write-Host "errors $($notAllowlisted.Count) not allowlisted" -ForegroundColor Red
        $notAllowlisted | ForEach-Object { Write-Host "  $($_.Message)" -ForegroundColor Red }
    }
    if (-not $verdict.Tests.Ok) {
        Write-Host ''
        Write-Host "tests $($counters.Total) vs baseline $($verdict.Tests.Baseline); failed $($counters.Failed); notRunnable $($counters.NotRunnable)" -ForegroundColor Red
    }
    if (-not $verdict.Assemblies.Ok) {
        Write-Host ''
        Write-Host "assemblies $($missingAssemblies.Count) missing:" -ForegroundColor Red
        $missingAssemblies | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }

    Write-VerdictLine -Verdict $verdict
    Write-Host "Run artifacts (outside the repository): $ResultsDirectory" -ForegroundColor DarkGray

    if (-not $verdict.Ok) { exit 1 }
    exit 0
}

# ── -Mode Build: report only. No baseline, no comparison, no gating of counts.
# The summary format string is bound to a variable BEFORE -f is applied:
# PowerShell's -f binds tighter than +, so an inline ("a" + "b") -f x would format
# only "b" and leave "a"'s placeholders as literal text.
$summaryFormat = 'tests {0} total, {1} passed, {2} failed, {3} notExecuted (total-executed), notRunnable {4}; errors {5} [{6}]; warnings {7} distinct'

Write-Host ''
Write-Host ($summaryFormat -f
             $counters.Total, $counters.Passed, $counters.Failed, $counters.NotRun,
             $counters.NotRunnable, $buildErrors.Count, $breakdown, $warningCount) `
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
if ($missingAssemblies.Count -gt 0) {
    Write-Host ''
    Write-Host "assemblies $($missingAssemblies.Count) missing" -ForegroundColor Red
    $missingAssemblies | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}

Write-Host "Run artifacts (outside the repository): $ResultsDirectory" -ForegroundColor DarkGray

if ($notAllowlisted.Count -gt 0 -or $counters.Failed -gt 0 -or $counters.NotRunnable -gt 0) { exit 1 }
exit 0
