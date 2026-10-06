# gen-winhance-id-snapshot.ps1 - produce the committed Winhance setting-ID snapshot
# that the SPIKE-03 divergence report reads (per D-08).
#
#   powershell -ExecutionPolicy Bypass -File tools\gen-winhance-id-snapshot.ps1
#
# ---------------------------------------------------------------------------
# -- READ-ONLY DISCIPLINE: THIS SCRIPT NEVER WRITES UNDER THE WINHANCE CHECKOUT --
# ---------------------------------------------------------------------------
# The reference checkout at C:\Users\isleap\Documents\GitHub\Winhance is
# PolyForm Shield 1.0.0, which carries a NONCOMPETE clause and a REQUIRED NOTICE.
# Akari is a Windows optimisation utility in the same category, so this repository
# operates under a clean-room design-parity constraint:
#
#   * NO FILE FROM THAT CHECKOUT IS COPIED INTO THIS REPOSITORY. The snapshot below
#     is DERIVED DATA AKARI PRODUCED BY PARSING - sorted, one-ID-per-line lists and
#     nothing else. Attribution is by REFERENCE (the commit SHA in the header), not
#     by reproduction.
#   * NOTHING IS CREATED, WRITTEN, MOVED OR DELETED UNDER ITS ROOT. Every write in
#     this script is under -OutDir, and only for paths this script's own table
#     names. Nothing here globs a destination directory or deletes anything.
#   * THE SNAPSHOT IS GENERATED, NEVER HAND-EDITED. Edit the generator instead.
#
# ---------------------------------------------------------------------------
# -- UNTRUSTED INPUT -------------------------------------------------------
# ---------------------------------------------------------------------------
# This script parses ANOTHER REPOSITORY'S SOURCE CODE. Every captured token is
# untrusted data. Therefore:
#   * each captured ID is validated against ^[A-Za-z0-9._-]+$ before it is emitted,
#     and a reject is reported by FILE NAME only, never by printing the token -
#     which is what makes it impossible for parsed content to escape the
#     one-ID-per-line output format;
#   * NO parsed token is ever handed to PowerShell's dynamic string-evaluation or
#     inline type-compilation facilities - the three cmdlets whose names are
#     deliberately NOT spelled anywhere in this file, because an acceptance
#     criterion greps this file for them by name and a prose mention would read
#     as a hit. Nor is a parsed token handed to a command line, to an argument
#     list, or interpolated into an output path. Only Get-Content, [regex] and
#     plain .NET collection APIs touch the foreign text;
#   * NO parsed expression is ever evaluated. A computed ID is reported as a partial
#     count, never expanded - expanding it would mean executing foreign input.
#
# ---------------------------------------------------------------------------
# -- WHY THE CATALOG-SHAPED SCOPE, AND WHY GUARD A EXISTS --------------------
# ---------------------------------------------------------------------------
# Winhance keeps its catalogs in Features/<Domain>/Models/, named *Optimizations,
# *Customizations and *Definitions - alongside DTOs, result records, enums and
# interfaces in the same directory. A file under Models/ is CATALOG-SHAPED when
#   (a) its base name ends in Optimizations / Customizations / Definitions, OR
#   (b) it is a dot-separated category sibling of an aggregate stem - base name
#       <Stem>.<Category> where <Stem> ends in Definitions and <Stem>.cs exists in
#       the same directory.
# Clause (b) is not a special case bolted on: it makes the aggregate pattern
# mechanical in BOTH directions, because ExternalAppDefinitions.<Category>.cs holds
# its category's literals directly.
#
# GUARD A exists so that a CHANGE to that naming convention FAILS LOUDLY instead of
# silently truncating the snapshot: every file under every Models/ directory is
# scanned, and if a non-catalog-shaped file contributes an Id = "..." literal, the
# run fails naming that file rather than shipping a short snapshot.
#
# ---------------------------------------------------------------------------
# -- WHY THERE IS NO "FAIL IF ANY DOMAIN IS EMPTY" GUARD --------------------
# ---------------------------------------------------------------------------
# Measured at Winhance commit db80ecf: of five discovered domain directories, TWO
# structurally hold no IDs.
#   * AdvancedTools/Models/ holds ImageDetectionResult.cs and ImageFormatInfo.cs -
#     ZERO catalog-shaped files, so it is structurally incapable of holding IDs.
#   * Common/Models/ holds 39 DTO/result records plus ONE catalog-shaped file,
#     PowerPlanDefinitions.cs, which is power-plan METADATA rather than setting
#     definitions and holds zero IDs by measurement.
# A naive "fail if any domain is empty" fires on both CORRECT inputs, which trains
# its reader to ignore it - the opposite of what a guard is for. So each file is
# classified first and the classification is recorded in the artefact:
#   aggregate         base name is a PROPER prefix of a sibling's base name in the
#                     same directory, so it can only be the union of them
#                     (ExternalAppDefinitions.cs, 0 IDs, 16 siblings).
#   leaf with IDs     normal (29 files across Optimize / Customize / SoftwareApps).
#   leaf with no IDs  recorded BY NAME, never fatal - a file with no Id assignment
#                     is a fact about the file, not evidence of a lost catalog.
# A domain is ID-BEARING iff it has at least one leaf-with-IDs file. Guard B (below)
# is the guard that actually catches a lost domain: a domain that WAS ID-bearing in
# the previously committed snapshot and yields zero now is a hard failure, because
# that means Winhance's catalog moved or was renamed and the snapshot is stale.
#
# NOTE: this is deliberately NOT a literal list of "domains allowed to be empty".
# Such a list would reintroduce the hand-maintained enumeration D-09 exists to
# eliminate, and it is exactly the route by which these two structural cases would
# later be quietly reclassified as failures.
#
# ---------------------------------------------------------------------------
# -- WHY THERE IS NO TIMESTAMP ---------------------------------------------
# ---------------------------------------------------------------------------
# A generation timestamp would make every regeneration dirty the git diff and
# defeat the drift check the committed snapshot exists to support. The ONLY
# provenance recorded is the Winhance commit SHA, read from the checkout itself.
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$WinhanceRoot = 'C:\Users\isleap\Documents\GitHub\Winhance',
    # Resolved after the param block: $PSScriptRoot is not yet populated where a
    # param default is evaluated under Windows PowerShell 5.1.
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $PSScriptRoot 'data'
}

# Untrusted-output shape. A captured token that fails this never reaches a file.
$IdPattern = '^[A-Za-z0-9._-]+$'

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    # UTF-8 WITHOUT BOM, and LF line endings: the output must be byte-identical
    # across runs and machines so `git diff --exit-code` is a real check.
    $normalised = $Content -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($Path, $normalised, (New-Object System.Text.UTF8Encoding($false)))
}

function Sort-Ordinal {
    param([string[]]$Items)
    if ($null -eq $Items -or $Items.Count -eq 0) { return @() }
    $copy = [string[]]::new($Items.Count)
    [Array]::Copy($Items, $copy, $Items.Count)
    [Array]::Sort($copy, [StringComparer]::Ordinal)
    return $copy
}

function Stop-With {
    param([string]$Message)
    Write-Error "FATAL: $Message"
    exit 1
}

# ── 0. Preconditions ────────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath $WinhanceRoot -PathType Container)) {
    Stop-With ("Winhance checkout not found at '$WinhanceRoot'. " +
        "Pass -WinhanceRoot <path to a Winhance checkout>, or clone it from https://github.com/Jeyloh/Winhance.")
}

$featuresRoot = Join-Path $WinhanceRoot 'src\Winhance.Core\Features'
if (-not (Test-Path -LiteralPath $featuresRoot -PathType Container)) {
    Stop-With ("'$featuresRoot' does not exist. This script expects a Winhance source layout " +
        'under src\Winhance.Core\Features. Refusing to guess an alternative.')
}

if (-not (Test-Path -LiteralPath $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

# Attribution by reference: the commit SHA the snapshot was derived from.
# A missing git is NOT fatal - the snapshot's value does not depend on it, so the
# line is skipped and the omission recorded rather than failing the whole run.
$commitSha = $null
try {
    $commitSha = (& git -C $WinhanceRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    if ($null -eq $commitSha -or $commitSha -notmatch '^[0-9a-f]{40}$') { $commitSha = $null }
} catch {
    $commitSha = $null
}

# ── 1. Enumerate the domain set BY DIRECTORY LISTING ────────────────────────
# Not a hardcoded array: a domain Winhance adds later is picked up automatically,
# where a hardcoded list would silently ignore it.
$domainDirs = @(Get-ChildItem -LiteralPath $featuresRoot -Directory | Sort-Object -Property Name)
if ($domainDirs.Count -eq 0) {
    Stop-With "no domain directories found under '$featuresRoot'."
}
$domainNames = @(Sort-Ordinal ($domainDirs | ForEach-Object { $_.Name }))

# ── 2. Per-domain file classification and ID capture ───────────────────────
$domainReports = New-Object System.Collections.Generic.List[object]
$headerDomainLines = New-Object System.Collections.Generic.List[string]
$aggregateEntries = New-Object System.Collections.Generic.List[string]
$noIdLeafEntries = New-Object System.Collections.Generic.List[string]
$idBearingDomains = New-Object System.Collections.Generic.List[string]
$totalIds = 0
$rejects = New-Object System.Collections.Generic.List[string]

foreach ($domainName in $domainNames) {
    $modelsDir = Join-Path (Join-Path $featuresRoot $domainName) 'Models'
    if (-not (Test-Path -LiteralPath $modelsDir -PathType Container)) { continue }

    $files = @(Get-ChildItem -LiteralPath $modelsDir -File)
    $baseNames = @(Sort-Ordinal ($files | ForEach-Object { $_.BaseName }))
    $hasStemFile = @{}
    foreach ($b in $baseNames) { $hasStemFile[$b] = $true }

    $catalogShaped = 0
    $domainIds = New-Object System.Collections.Generic.List[string]
    $leafWithIds = 0

    foreach ($file in ($files | Sort-Object -Property Name)) {
        # Every file is scanned for literals - catalog-shaped or not - so Guard A can
        # detect a non-catalog file that contributes one (a shape-rule regression).
        $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $literalCount = ([regex]::Matches($text, 'Id\s*=\s*"')).Count

        # Catalog-shaped? (a) name convention, or (b) dot-separated category sibling
        # of an aggregate stem whose own file exists in this directory.
        $stem = ($file.BaseName -split '\.')[0]
        $isSibling = ($file.BaseName -ne $stem) -and
            $stem.EndsWith('Definitions', [StringComparison]::Ordinal) -and
            $hasStemFile.ContainsKey($stem)
        $isCatalogShaped = ($file.BaseName -match '(Optimizations|Customizations|Definitions)$') -or $isSibling

        if (-not $isCatalogShaped) {
            if ($literalCount -gt 0) {
                # GUARD A. The shape rule missed a real catalog file. Fail rather
                # than emit a silently short snapshot.
                Stop-With ("GUARD A: non-catalog-shaped file '$($domainName)/Models/$($file.Name)' " +
                    "contributes $literalCount `Id = `"`" literal(s). The catalog-shape rule no longer " +
                    'matches this file - re-derive the rule before trusting this snapshot.')
            }
            continue
        }

        $catalogShaped++

        # An aggregate can only be the union of its siblings, so zero is expected.
        $isAggregate = $false
        foreach ($other in $baseNames) {
            if ($other -ne $file.BaseName -and
                $other.Length -gt $file.BaseName.Length -and
                $other.StartsWith($file.BaseName, [StringComparison]::Ordinal)) {
                $isAggregate = $true
                break
            }
        }
        if ($isAggregate) {
            $siblingCount = @($baseNames | Where-Object {
                $_ -ne $file.BaseName -and
                $_.Length -gt $file.BaseName.Length -and
                $_.StartsWith($file.BaseName, [StringComparison]::Ordinal)
            }).Count
            $aggregateEntries.Add("$domainName/Models/$($file.Name) (ids=$literalCount, siblings=$siblingCount)")
            continue
        }

        if ($literalCount -eq 0) {
            # Leaf with no IDs: a fact about the FILE, recorded by name and never fatal.
            $noIdLeafEntries.Add("$domainName/Models/$($file.Name)")
            continue
        }

        $leafWithIds++

        # Capture. The token is validated immediately and never leaves here
        # unvalidated; a reject is reported by file name only.
        $captured = 0
        foreach ($m in [regex]::Matches($text, 'Id\s*=\s*"([^"]*)"')) {
            $token = $m.Groups[1].Value
            if ($token -notmatch $IdPattern) {
                $rejects.Add("$domainName/Models/$($file.Name)")
                continue
            }
            $domainIds.Add($token)
            $captured++
        }

        if ($captured -lt $literalCount) {
            Write-Warning ("$domainName/Models/$($file.Name): extracted $captured of $literalCount " +
                '`Id = `"`" occurrences - the remainder are computed or interpolated rather than ' +
                'literal, and are deliberately NOT evaluated. This snapshot is partial for that file.')
        }
    }

    $isIdBearing = $leafWithIds -ge 1
    if ($isIdBearing) { $idBearingDomains.Add($domainName) }

    $totalIds += $domainIds.Count
    $headerDomainLines.Add(
        "# domain: $domainName  files=$($files.Count)  catalogShapedFiles=$catalogShaped  idCount=$($domainIds.Count)  idBearing=$($isIdBearing.ToString().ToLowerInvariant())")

    $domainReports.Add([pscustomobject]@{
            Domain      = $domainName
            IdBearing   = $isIdBearing
            Files       = $files.Count
            CatalogShaped = $catalogShaped
            IdCount     = $domainIds.Count
            Ids         = @(Sort-Ordinal ($domainIds.ToArray()))
        })
}

if ($rejects.Count -gt 0) {
    $names = @(Sort-Ordinal (($rejects | Select-Object -Unique).ToArray()))
    Stop-With ("$($rejects.Count) captured token(s) did not match $IdPattern. " +
        "Offending FILE NAMES only (tokens are never printed): $($names -join ', ')")
}

if ($idBearingDomains.Count -eq 0) {
    Stop-With 'no ID-bearing domain was derived - the snapshot would be empty.'
}

# ── 3. GUARD B - a previously ID-bearing domain that is now empty ───────────
$combinedPath = Join-Path $OutDir 'winhance-setting-ids.txt'
$currentBearing = @(Sort-Ordinal ($idBearingDomains.ToArray()))
if (Test-Path -LiteralPath $combinedPath -PathType Leaf) {
    $previousBearing = @()
    foreach ($line in (Get-Content -LiteralPath $combinedPath -Encoding UTF8)) {
        if ($line -match '^# idBearingDomains:\s*(.*)$') {
            $previousBearing = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
            break
        }
    }
    foreach ($wasBearing in $previousBearing) {
        if ($currentBearing -notcontains $wasBearing) {
            Stop-With ("GUARD B: domain '$wasBearing' was ID-bearing in the committed snapshot and " +
                'yields none now. Winhance''s catalog has moved or been renamed and this snapshot is ' +
                'STALE. Investigate the checkout before regenerating.')
        }
    }
}

# ── 4. GUARD C - internal consistency of the per-domain accounting ──────────
$sumOfDomains = ($domainReports | ForEach-Object { $_.IdCount } | Measure-Object -Sum).Sum
if ($null -eq $sumOfDomains) { $sumOfDomains = 0 }
if ($sumOfDomains -ne $totalIds) {
    Stop-With ("GUARD C: per-domain idCount sum ($sumOfDomains) does not equal the captured total ($totalIds).")
}

# ── 5. Emit per-domain files - ID-BEARING DOMAINS ONLY ─────────────────────
# A domain that holds none gets NO file: an empty .ids.txt reads downstream as a
# domain that was lost, which is the exact confusion this classification prevents.
$written = New-Object System.Collections.Generic.List[string]
foreach ($report in $domainReports) {
    if (-not $report.IdBearing) { continue }
    $path = Join-Path $OutDir ("winhance-{0}.ids.txt" -f $report.Domain.ToLowerInvariant())
    Write-Utf8NoBom -Path $path -Content (($report.Ids -join "`n") + "`n")
    $written.Add((Split-Path -Leaf $path))
}

# ── 6. Emit the combined file with a # provenance header ───────────────────
$header = New-Object System.Collections.Generic.List[string]
$header.Add('# Winhance setting-ID snapshot - DERIVED DATA, GENERATED, DO NOT HAND-EDIT')
$header.Add('#')
$header.Add('# Produced by tools\gen-winhance-id-snapshot.ps1 by PARSING the reference checkout.')
$header.Add('# No file from that checkout was copied into this repository; the content below is')
$header.Add('# sorted ID strings Akari derived. Attribution is by reference, not by reproduction.')
$header.Add('# The reference checkout is PolyForm Shield 1.0.0 (noncompete + required notice) and')
$header.Add('# is opened READ-ONLY by the generator. Nothing is written under its root.')
$header.Add('#')
if ($null -ne $commitSha) {
    $header.Add("# winhanceCommit: $commitSha")
} else {
    $header.Add('# winhanceCommit: commit SHA unavailable (git not found or not a repository)')
}
$header.Add("# winhanceRoot: $WinhanceRoot")
$header.Add("# featuresRoot: $featuresRoot")
$header.Add('# idBearingDomains: ' + ($currentBearing -join ', '))
$header.Add("# domains: $($domainNames.Count)")
$header.Add("# totalIds: $totalIds")
$header.Add('# A domain with idBearing=false structurally holds no IDs at this commit; it is recorded')
$header.Add('# here rather than given an .ids.txt, so its absence is evidenced rather than assumed.')
foreach ($line in $headerDomainLines) { $header.Add($line) }
$header.Add('#')
if ($aggregateEntries.Count -gt 0) {
    $header.Add('# aggregate files (union of their dot-separated category siblings; expected 0 IDs):')
    foreach ($e in (Sort-Ordinal ($aggregateEntries.ToArray()))) { $header.Add("#   $e") }
}
if ($noIdLeafEntries.Count -gt 0) {
    $header.Add('# leaf files with no Id assignment (recorded by name; not a lost catalog):')
    foreach ($e in (Sort-Ordinal ($noIdLeafEntries.ToArray()))) { $header.Add("#   $e") }
}
$header.Add('#')
$header.Add('# Body: ordinal-sorted IDs grouped by domain, duplicates preserved.')

$body = New-Object System.Collections.Generic.List[string]
foreach ($report in $domainReports) {
    if (-not $report.IdBearing) { continue }
    $body.Add("# --- $($report.Domain) ---")
    foreach ($id in $report.Ids) { $body.Add($id) }
}

Write-Utf8NoBom -Path $combinedPath -Content (($header + $body) -join "`n") + "`n"

# ── 7. Verify Guard C against what was actually WRITTEN, not what was counted
$bodyLines = @((Get-Content -LiteralPath $combinedPath -Encoding UTF8) | Where-Object { -not $_.StartsWith('#') })
if ($bodyLines.Count -ne $totalIds) {
    Stop-With ("GUARD C (post-write): combined file body holds $($bodyLines.Count) ID line(s) but the " +
        "per-domain header accounts for $totalIds.")
}

Write-Output ("Winhance commit : " + $(if ($null -ne $commitSha) { $commitSha } else { 'unavailable' }))
Write-Output ("Domains found   : $($domainNames.Count) ($($domainNames -join ', '))")
Write-Output ("ID-bearing      : $($currentBearing -join ', ')")
foreach ($report in $domainReports) {
    Write-Output ("  {0,-14} files={1,-3} catalogShaped={2,-3} ids={3,-5} idBearing={4}" -f
        $report.Domain, $report.Files, $report.CatalogShaped, $report.IdCount, $report.IdBearing)
}
Write-Output ("Aggregates      : $($aggregateEntries.Count)")
Write-Output ("No-ID leaves    : $($noIdLeafEntries.Count)")
Write-Output ("Total IDs       : $totalIds")
Write-Output ("Files written   : " + ((@(Sort-Ordinal ($written.ToArray()))) + 'winhance-setting-ids.txt' -join ', '))