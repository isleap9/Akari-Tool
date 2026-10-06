# gen-setting-id-diff.ps1 - generate the SPIKE-03 setting-ID divergence report.
#
#   powershell -ExecutionPolicy Bypass -File tools\gen-setting-id-diff.ps1
#
# ---------------------------------------------------------------------------
# -- THIS GENERATOR GATES NOTHING (per D-10) ---------------------------------
# ---------------------------------------------------------------------------
# The output is a MEASUREMENT of divergence. It asserts nothing about how large
# the divergence is, it is not a test, and this script exits 0 no matter how much
# it finds. The known deltas are deliberate content differences (Optimize ahead,
# Customize behind) and catalog content expansion is Out of Scope, so a gate would
# fail on intent rather than on defect.
#
# A non-zero exit is reserved for MALFORMED INPUT ONLY: an unreadable or
# unparseable data file, a classification sum that does not balance, a
# disjointness identity that does not balance, or an Akari domain with no
# counterpart. Those are defects in THIS script, not findings about the catalog.
#
# ---------------------------------------------------------------------------
# -- WHAT THIS READS, AND WHY THE ASYMMETRY IS CORRECT ------------------------
# ---------------------------------------------------------------------------
# The two sides are produced by deliberately different means, and the report says
# so in its own header rather than hiding it:
#   * AKARI is a REFLECTION over the already-built AkariTool.Core.dll: the
#     non-nested static catalog entry points (11 IReadOnlyList<SettingGroup>
#     Build() factories plus 4 top-level AppGroup Get*() factories), each invoked
#     and each emitted setting's Id read. SoftwareApps is reached through the
#     AppGroup return-type rule and has no Build() method at all, so it cannot be
#     missed without the count changing.
#     The 16 nested ExternalAppCatalog.* component factories are the COMPONENTS of
#     one aggregate, and their exclusion is PROVED lossless by a multiset equality
#     against ExternalAppCatalog.GetExternalApps() - so SoftwareApps is counted
#     once, not twice.
#   * WINHANCE is a TEXT PARSE of another repository's literal Id = "..." strings,
#     over a committed snapshot generated read-only by
#     tools/gen-winhance-id-snapshot.ps1. Reflection is impossible there: Akari has
#     no build, no assembly and no permission to reference a checkout it may not
#     modify.
# The asymmetry is therefore a property of the licensing and the deployment, not
# an inconsistency in method. It DOES have one consequence a reader must know: a
# Winhance ID expressed as a computed expression rather than a literal is reported
# as partial by the snapshot generator, so the Winhance side may be slightly short.
#
# ---------------------------------------------------------------------------
# -- NO TIMESTAMP ------------------------------------------------------------
# ---------------------------------------------------------------------------
# Nothing written here records a generation time, so an unchanged catalog
# regenerates a byte-identical report and `git diff --exit-code` is a real check.
# The Winhance provenance (commit SHA) lives in the snapshot header, not here.
#
# ---------------------------------------------------------------------------
# -- WRITES ------------------------------------------------------------------
# ---------------------------------------------------------------------------
# Exactly one path is written: -ReportPath. Nothing is globbed, no directory is
# cleaned, and no data file is rewritten - a wrong -ReportPath can overwrite only
# the file the operator named.
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$DataDir,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolved after the param block: $PSScriptRoot is not populated where a param
# default is evaluated under Windows PowerShell 5.1.
if ([string]::IsNullOrWhiteSpace($DataDir)) { $DataDir = Join-Path $PSScriptRoot 'data' }
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path (Split-Path -Parent $PSScriptRoot) '.planning\phases\01-baseline-spikes-test-harness\01-SETTING-ID-DIFF.md'
}
$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)

# A literal backtick in a double-quoted PowerShell string needs doubling, which is
# easy to get wrong while writing markdown code spans. These two helpers let the
# render section stay readable.
$TICK = [string][char]96
function Code { param([string]$Text) return ($TICK + $Text + $TICK) }

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, ($Content -replace "`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
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

function New-OrdinalSet {
    param([string[]]$Items)
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($i in $Items) { [void]$set.Add($i) }
    # The leading comma is load-bearing: PowerShell unrolls an IEnumerable written
    # to the output stream, so a bare `return $set` would hand back the strings (or
    # nothing at all for an empty set) instead of the set itself.
    return , $set
}

function Get-Sum {
    param($Values)
    $s = @($Values | Measure-Object -Sum).Sum
    if ($null -eq $s) { return 0 }
    return [int]$s
}

# â”€â”€ 1. Read the Akari side â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
$akariJsonPath = Join-Path $DataDir 'akari-setting-ids.json'
if (-not (Test-Path -LiteralPath $akariJsonPath -PathType Leaf)) {
    Stop-With "Akari ID set not found at '$akariJsonPath'. Run tools\SettingIdDiff first."
}
$akari = $null
try { $akari = Get-Content -LiteralPath $akariJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json }
catch { Stop-With "could not parse '$akariJsonPath': $($_.Exception.Message)" }

# The eight domain names are DERIVED from the Akari side - never spelled here.
$akariDomainList = New-Object System.Collections.Generic.List[string]
foreach ($p in $akari.domains.PSObject.Properties) { $akariDomainList.Add($p.Name) }
$akariDomains = @(Sort-Ordinal ($akariDomainList.ToArray()))
if ($akariDomains.Count -eq 0) { Stop-With 'the Akari ID set contains no domains.' }
$akariDomainLower = @(Sort-Ordinal ($akariDomains | ForEach-Object { $_.ToLowerInvariant() }))

# â”€â”€ 2. Read the Winhance side â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# The combined file's '#' header is stripped before parsing; the per-domain files
# are the authoritative per-directory sets. Only ID-BEARING domains have a file,
# which is why the non-ID-bearing ones are reported from the header instead.
$winhanceCombined = Join-Path $DataDir 'winhance-setting-ids.txt'
if (-not (Test-Path -LiteralPath $winhanceCombined -PathType Leaf)) {
    Stop-With "Winhance snapshot not found at '$winhanceCombined'. Run tools\gen-winhance-id-snapshot.ps1 first."
}

$snapshotLines = @(Get-Content -LiteralPath $winhanceCombined -Encoding UTF8)
$bodyLines = @($snapshotLines | Where-Object { -not $_.StartsWith('#') })
$winhanceSha = '(not recorded)'
$winhanceRootLine = '(not recorded)'
$bearingLine = ''
$domainHeaderLines = @()
foreach ($line in $snapshotLines) {
    if ($line -match '^# winhanceCommit:\s*(\S+)') { $winhanceSha = $Matches[1] }
    elseif ($line -match '^# winhanceRoot:\s*(.+)$') { $winhanceRootLine = $Matches[1].Trim() }
    elseif ($line -match '^# idBearingDomains:\s*(.+)$') { $bearingLine = $Matches[1].Trim() }
    elseif ($line -match '^# domain:\s*(\S+)\s+files=(\d+)\s+catalogShapedFiles=(\d+)\s+idCount=(\d+)\s+idBearing=(\w+)') {
        $domainHeaderLines += [pscustomobject]@{
            Domain        = $Matches[1]
            Files         = [int]$Matches[2]
            CatalogShaped = [int]$Matches[3]
            IdCount       = [int]$Matches[4]
            IdBearing     = $Matches[5]
        }
    }
}

$winhanceBearing = @()
if ($bearingLine) { $winhanceBearing = @($bearingLine -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
if ($winhanceBearing.Count -eq 0) { Stop-With 'the Winhance snapshot header names no ID-bearing domain.' }
$winhanceNonBearing = @(Sort-Ordinal (@($domainHeaderLines | Where-Object { $winhanceBearing -notcontains $_.Domain } | ForEach-Object { $_.Domain })))

$winhanceIds = @{}
foreach ($w in $winhanceBearing) {
    $path = Join-Path $DataDir ('winhance-{0}.ids.txt' -f $w.ToLowerInvariant())
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Stop-With "the snapshot header names '$w' as ID-bearing but '$path' is missing. Regenerate the snapshot."
    }
    $winhanceIds[$w] = @(Get-Content -LiteralPath $path -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 })
}

$winhanceAll = New-Object System.Collections.Generic.List[string]
foreach ($w in $winhanceBearing) { foreach ($id in $winhanceIds[$w]) { $winhanceAll.Add($id) } }
$winhanceAllDistinct = New-OrdinalSet $winhanceAll.ToArray()
if ($winhanceAllDistinct.Count -eq 0) { Stop-With 'the Winhance snapshot contains no IDs.' }

# Guard C, re-checked against the files THIS script reads.
$declaredTotal = 0
foreach ($d in $domainHeaderLines) { if ($winhanceBearing -contains $d.Domain) { $declaredTotal += $d.IdCount } }
if ($declaredTotal -ne $bodyLines.Count) {
    Stop-With ("the snapshot header accounts for $declaredTotal IDs but the combined file body holds " +
        "$($bodyLines.Count). Regenerate the snapshot; this report will not reconcile a broken snapshot.")
}

# Duplicate IDs on the Winhance side are a real defect in the reference data, not
# noise: the snapshot preserves them, so they are reported rather than hidden by
# set semantics.
$winhanceDuplicates = @()
foreach ($g in ($winhanceAll | Group-Object -CaseSensitive | Where-Object { $_.Count -gt 1 })) {
    $winhanceDuplicates += [pscustomobject]@{ Id = $g.Name; Count = $g.Count }
}
$winhanceDuplicates = @($winhanceDuplicates | Sort-Object -Property Id)

# â”€â”€ 3. Normalisation, printed in the header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
#   1. lower-case the ID
#   2. if its FIRST '-' delimited token equals one of the Akari domain names
#      (derived above, compared case-insensitively), drop that token
#   3. drop any remaining token equal to 'customization'
#   4. join the remaining tokens with ':'
$domainTokenSet = New-OrdinalSet $akariDomainLower

function ConvertTo-Normalised {
    param([string]$Id)
    $tokens = @($Id.ToLowerInvariant() -split '-')
    if ($tokens.Count -gt 0 -and $domainTokenSet.Contains($tokens[0])) { $tokens = @($tokens | Select-Object -Skip 1) }
    $kept = @($tokens | Where-Object { $_ -ne 'customization' })
    return ($kept -join ':')
}

function Get-NormalisedTokens {
    param([string]$Id)
    $key = ConvertTo-Normalised $Id
    if ($key.Length -eq 0) { return @() }
    return @($key -split ':' | Where-Object { $_.Length -gt 0 })
}

# â”€â”€ 4. Akari per-domain normalised index, and EXCLUSIVE attribution â”€â”€â”€â”€â”€â”€â”€â”€
# A normalised key -> the Akari domains that claim it. This is what makes
# attribution exclusive rather than a greedy first-match.
$akariIndex = @{}
$akariTokensPerDomain = @{}
foreach ($d in $akariDomains) {
    $ids = @($akari.domains.$d.ids)
    if ($ids.Count -eq 0) { Stop-With "Akari domain '$d' has no IDs. Regenerate the Akari ID set." }
    $akariIndex[$d] = @{}
    $tokenSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($id in $ids) {
        $key = ConvertTo-Normalised $id
        if (-not $akariIndex[$d].ContainsKey($key)) { $akariIndex[$d][$key] = @() }
        $akariIndex[$d][$key] += $id
        foreach ($t in (Get-NormalisedTokens $id)) { [void]$tokenSet.Add($t) }
    }
    $akariTokensPerDomain[$d] = $tokenSet
}

$attributed = @{}
$unattributedReasons = @{}
$unattributedList = New-Object System.Collections.Generic.List[string]
foreach ($id in $winhanceAllDistinct) {
    $key = ConvertTo-Normalised $id
    $claimants = @()
    foreach ($d in $akariDomains) { if ($akariIndex[$d].ContainsKey($key)) { $claimants += $d } }
    if ($claimants.Count -eq 1) {
        $attributed[$id] = $claimants[0]
    } else {
        $unattributedList.Add($id)
        if ($claimants.Count -gt 1) {
            $unattributedReasons[$id] = ('normalised-equal to an ID in ' + ($claimants -join ' and ') +
                ' - the same setting appearing in more than one Akari domain (duplicate-ID defect), so it is attributed to neither')
        } else {
            $unattributedReasons[$id] = 'no Akari domain claims this ID after normalisation'
        }
    }
}
$unattributed = @(Sort-Ordinal ($unattributedList.ToArray()))

# â”€â”€ 5. Counterpart selection, printed in the header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# An Akari domain's counterpart is the Winhance ID-bearing directory maximising
# the Jaccard overlap of their normalised TOKEN SETS; ties break to the
# lexicographically smallest Winhance domain name.
$winhanceTokensPerDomain = @{}
foreach ($w in $winhanceBearing) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($id in $winhanceIds[$w]) { foreach ($t in (Get-NormalisedTokens $id)) { [void]$set.Add($t) } }
    $winhanceTokensPerDomain[$w] = $set
}

$counterparts = @{}
$overlapPct = @{}
foreach ($d in $akariDomains) {
    $best = $null
    $bestScore = -1.0
    foreach ($w in $winhanceBearing) {
        $inter = 0
        foreach ($t in $akariTokensPerDomain[$d]) { if ($winhanceTokensPerDomain[$w].Contains($t)) { $inter++ } }
        $total = $akariTokensPerDomain[$d].Count + $winhanceTokensPerDomain[$w].Count - $inter
        $score = if ($total -eq 0) { 0.0 } else { $inter / [double]$total }
        if ($score -gt $bestScore -or ($score -eq $bestScore -and $null -ne $best -and $w -lt $best)) {
            $best = $w
            $bestScore = $score
        }
    }
    if ($null -eq $best) { Stop-With "Akari domain '$d' has no Winhance counterpart to select." }
    $counterparts[$d] = $best
    $overlapPct[$d] = [int][Math]::Round(100.0 * $bestScore)
}

# â”€â”€ 6. Per-domain classification â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# Five mechanical buckets. Every ID on both sides lands in exactly ONE of them.
#
#   raw-equal                     the exact ID string is on both sides
#   normalised-equal              not raw-equal, but the two IDs normalise to the
#                                  same key and neither side has a rival at it
#   both-but-normalised-conflict  the key is claimed by more than one ID on either
#                                  side, so the normalised diff is ambiguous there;
#                                  every claiming ID is counted here rather than
#                                  being called equal or one-sided
#   Akari-only                    no Winhance ID shares this normalised key
#   Winhance-only                 no Akari ID shares this normalised key
#
# Counts are in IDs, not in pairs, so the identity
#   raw-equal + normalised-equal + conflict + Akari-only + Winhance-only
#     == |Akari distinct| + |Winhance attributed distinct| - |raw-equal|
# is exact: the union of both sides counts a raw-equal ID once.
function Get-DomainBuckets {
    param([string]$Domain)
    $aDistinct = @(Sort-Ordinal (@(New-OrdinalSet @($akari.domains.$Domain.ids)) | ForEach-Object { $_ }))
    $wIds = @(Sort-Ordinal (@($attributed.Keys) | Where-Object { $attributed[$_] -eq $Domain }))
    $wSet = New-OrdinalSet $wIds

    $rawEqual = @()
    $aRemain = New-Object System.Collections.Generic.List[string]
    $rawEqualSet = New-OrdinalSet @()
    foreach ($id in $aDistinct) {
        if ($wSet.Contains($id)) { $rawEqual += $id; [void]$rawEqualSet.Add($id) } else { $aRemain.Add($id) }
    }
    $wRemain = New-Object System.Collections.Generic.List[string]
    foreach ($id in $wIds) { if (-not $rawEqualSet.Contains($id)) { $wRemain.Add($id) } }

    $aByKey = @{}
    foreach ($id in $aRemain) {
        $k = ConvertTo-Normalised $id
        if (-not $aByKey.ContainsKey($k)) { $aByKey[$k] = @() }
        $aByKey[$k] += $id
    }
    $wByKey = @{}
    foreach ($id in $wRemain) {
        $k = ConvertTo-Normalised $id
        if (-not $wByKey.ContainsKey($k)) { $wByKey[$k] = @() }
        $wByKey[$k] += $id
    }

    $normalised = @()
    $conflict = @()
    $akariOnly = @()
    $winhanceOnly = @()
    $allKeys = @(Sort-Ordinal (@($aByKey.Keys) + @($wByKey.Keys) | Select-Object -Unique))
    foreach ($k in $allKeys) {
        $aClaim = @(); if ($aByKey.ContainsKey($k)) { $aClaim = @($aByKey[$k]) }
        $wClaim = @(); if ($wByKey.ContainsKey($k)) { $wClaim = @($wByKey[$k]) }
        if ($aClaim.Count -eq 1 -and $wClaim.Count -eq 1) {
            $normalised += [pscustomobject]@{ Key = $k; Akari = $aClaim[0]; Winhance = $wClaim[0] }
        } elseif ($aClaim.Count -gt 0 -and $wClaim.Count -gt 0) {
            $conflict += [pscustomobject]@{ Key = $k; Akari = $aClaim; Winhance = $wClaim }
        } elseif ($aClaim.Count -gt 0) { $akariOnly += $aClaim } else { $winhanceOnly += $wClaim }
    }

    $conflictIds = Get-Sum ($conflict | ForEach-Object { $_.Akari.Count + $_.Winhance.Count })
    $lhs = $rawEqual.Count + ($normalised.Count * 2) + $conflictIds + $akariOnly.Count + $winhanceOnly.Count
    $rhs = $aDistinct.Count + $wIds.Count - $rawEqual.Count
    if ($lhs -ne $rhs) {
        Stop-With ("domain '$Domain': bucket counts ($lhs) do not balance against the union of both sides " +
            "($rhs). This is a defect in the generator, not a finding about the catalog.")
    }

    return [pscustomobject]@{
        Domain         = $Domain
        Counterpart    = $counterparts[$Domain]
        OverlapPct     = $overlapPct[$Domain]
        AkariIds       = $aDistinct
        WinhanceIds    = $wIds
        RawEqual       = $rawEqual
        Normalised     = $normalised
        Conflict       = $conflict
        ConflictIds    = $conflictIds
        AkariOnly      = $akariOnly
        WinhanceOnly   = $winhanceOnly
    }
}

$results = @($akariDomains | ForEach-Object { Get-DomainBuckets $_ })

# Disjointness identity: because attribution is exclusive, the per-domain
# Winhance-side counts are pairwise disjoint and sum to the whole Winhance ID set
# minus the unattributed remainder, which is reported once in its own section.
$attributedTotal = Get-Sum ($results | ForEach-Object { $_.WinhanceIds.Count })
$expectedAttributed = $winhanceAllDistinct.Count - $unattributed.Count
if ($attributedTotal -ne $expectedAttributed) {
    Stop-With ("disjointness identity broken: the per-domain Winhance sides total $attributedTotal but " +
        "$expectedAttributed was attributed ($($winhanceAllDistinct.Count) Winhance IDs minus " +
        "$($unattributed.Count) unattributed).")
}

# â”€â”€ 7. Render â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# Every line below is a SINGLE-QUOTED PowerShell string or an explicit
# concatenation, never a double-quoted string containing a markdown code span:
# a backtick inside a double-quoted PowerShell string is an ESCAPE, so a literal
# one has to be doubled and silently drops its partner when it is not. The Code
# helper exists for the same reason.
$L = New-Object System.Collections.Generic.List[string]
function Add-Line { param([string]$Text = '') [void]$L.Add($Text) }
function Add-CodeList {
    param([string[]]$Items)
    if ($null -eq $Items -or $Items.Count -eq 0) { Add-Line '  (none)'; return }
    foreach ($i in (Sort-Ordinal $Items)) { Add-Line ('  - ' + (Code $i)) }
}

$T = $TICK   # local alias so each rendered line stays readable

Add-Line '# Setting-ID divergence: Akari vs Winhance'
Add-Line ''
Add-Line '**This report is a MEASUREMENT. It asserts nothing, it gates nothing, and its generator exits 0**'
Add-Line '**regardless of how large the divergence is.** It is not a test, not a build gate and not a'
Add-Line '**pass/fail bar.** The known deltas are deliberate content differences (Optimize ahead, Customize'
Add-Line 'behind) and catalog content expansion is Out of Scope, so failing on them would fail on intent'
Add-Line 'rather than on defect. The generator exits non-zero only on malformed input.'
Add-Line ''
Add-Line '## How each side was obtained'
Add-Line ''
Add-Line 'The two sides are produced by deliberately different means. That asymmetry is a property of the'
Add-Line 'licensing and the deployment, not an inconsistency in method, and it is stated here rather than'
Add-Line 'hidden:'
Add-Line ''
Add-Line ('* **Akari** is a reflection over the already-built ' + $T + 'AkariTool.Core.dll' + $T + ': the **' + $akari.entryPoints + ' non-nested**')
Add-Line ('  static catalog entry points (' + $T + 'IReadOnlyList<SettingGroup> Build()' + $T + ' plus top-level')
Add-Line ($T + 'AppGroup Get*()' + $T + '), each invoked and each emitted setting''s ' + $T + 'Id' + $T + ' read. The Akari side therefore')
Add-Line ('  cannot drift from the catalog. ' + $T + 'SoftwareApps' + $T + ' is reached through the ' + $T + 'AppGroup' + $T + ' return-type rule')
Add-Line ('  and has **no ' + $T + 'Build()' + $T + ' method at all**, so it cannot go missing without the entry-point')
Add-Line ('  count changing - which is why an 11-entry enumeration would omit roughly 55% of Akari''s IDs.')
Add-Line ('* The **' + $akari.componentCoverage.nestedFactories + ' nested ' + $T + 'ExternalAppCatalog.*' + $T + ' component factories** are')
Add-Line '  the parts of one aggregate, not sixteen catalogs. They are excluded, and their exclusion is'
Add-Line ('  **proved lossless** by an ID-multiset equality against ' + $T + 'ExternalAppCatalog.GetExternalApps()' + $T + ' (' + $T + 'componentCoverage.equal = ' +
    $akari.componentCoverage.equal + $T + ', ' + $akari.componentCoverage.componentIds + ' IDs on each side), so')
Add-Line '  SoftwareApps is counted once rather than twice.'
Add-Line ('* **Winhance** is a text parse of another repository''s literal ' + $T + 'Id = "..."' + $T + ' strings, over a')
Add-Line ('  committed snapshot generated read-only by ' + $T + 'tools\gen-winhance-id-snapshot.ps1' + $T + '. Reflection is')
Add-Line '  impossible there: Akari has no build, no assembly and no permission to reference a checkout it may'
Add-Line '  not modify. One consequence: a Winhance ID expressed as a computed expression rather than a literal'
Add-Line '  is reported as partial by the snapshot generator rather than expanded, so the Winhance side may be'
Add-Line '  slightly short.'
Add-Line ''
Add-Line ('**The Akari-side namespaces will move in Phase 3.** All eleven ' + $T + 'Build()' + $T + ' types live in')
Add-Line ($T + 'src/AkariTool.Core/Features/<Domain>/Catalogs/' + $T + ' but declare ' + $T + 'AkariTool.Tabs.*' + $T + '. When Phase 3''s')
Add-Line 'namespace alignment moves them, the generator must be updated **at its namespace-derivation rule** -'
Add-Line 'never by adding an entry to a list. The ' + $T + 'AppGroup' + $T + ' arm is derived by return type and survives the move.'
Add-Line ''
Add-Line '## Winhance provenance'
Add-Line ''
Add-Line ('* Snapshot commit: ' + (Code $winhanceSha))
Add-Line ('* Snapshot source root: ' + (Code $winhanceRootLine))
Add-Line ('* Only the **' + $winhanceBearing.Count + ' ID-bearing directories** contributed: ' +
    (($winhanceBearing | ForEach-Object { Code $_ }) -join ', '))
Add-Line ('* The other ' + $winhanceNonBearing.Count + ' discovered domain directories (' +
    (($winhanceNonBearing | ForEach-Object { Code $_ }) -join ', ') + ') **structurally hold no IDs** at this')
Add-Line ('  commit and were deliberately given no ' + $T + '.ids.txt' + $T + '; each is recorded with its ' + $T + 'files' + $T + ',')
Add-Line ('  ' + $T + 'catalogShapedFiles' + $T + ' and ' + $T + 'idCount' + $T + ' counts in the ' + $T + 'tools/data/winhance-setting-ids.txt' + $T + ' header,')
Add-Line '  so their absence is **evidenced** rather than assumed. See that header rather than concluding'
Add-Line '  they were omitted by mistake.'
Add-Line ''
Add-Line '## Normalisation rule'
Add-Line ''
Add-Line 'Divergence is reported in two passes: a raw set difference **and** a normalised one. Without the'
Add-Line 'second pass a prefix-convention rename reads as wholesale divergence - arithmetically true,'
Add-Line 'semantically useless, and worse than no report at all. The rule, verbatim:'
Add-Line ''
Add-Line '1. lower-case the ID;'
Add-Line ('2. if its first ' + $T + '-' + $T + '-delimited token equals one of the **' + $akariDomains.Count + ' Akari domain names derived')
Add-Line ('   from the Akari side** (' + (($akariDomainLower | ForEach-Object { Code $_ }) -join ', ') + '), drop that token;')
Add-Line ('3. drop any remaining token equal to ' + (Code 'customization') + ';')
Add-Line ('4. join the remaining tokens with ' + (Code ':') + '.')
Add-Line ''
Add-Line 'Worked examples, one from each side, so any classification can be reproduced by hand:'
Add-Line ''
Add-Line ('* ' + (Code 'customize-explorer-show-file-extensions') + ' (Akari) -> ' + (Code (ConvertTo-Normalised 'customize-explorer-show-file-extensions')))
Add-Line ('* ' + (Code 'explorer-customization-shortcut-suffix') + ' (Winhance) -> ' + (Code (ConvertTo-Normalised 'explorer-customization-shortcut-suffix')))
Add-Line ''
Add-Line '## Counterpart rule'
Add-Line ''
Add-Line ('The two sides do not have the same number of domains - ' + $akariDomains.Count + ' derived Akari domains against')
Add-Line ($winhanceBearing.Count + ' ID-bearing Winhance directories - so "the counts on the other side" needs a')
Add-Line 'stated rule, or this report would either print one Winhance total six times or drop the per-side'
Add-Line 'counts entirely. The rule, verbatim:'
Add-Line ''
Add-Line ('1. **Select.** An Akari domain''s counterpart is the Winhance ID-bearing directory maximising the')
Add-Line ('   normalised-token overlap ' + (Code '|tokens(d) and tokens(w)| / |tokens(d) or tokens(w)|') + '. Ties break')
Add-Line ('   to the lexicographically smallest Winhance domain name. Worked example: ' + (Code 'Gaming') + ' selects ' +
    (Code $counterparts['Gaming']) + ', ' + (Code 'Customize') + ' selects ' + (Code $counterparts['Customize']) +
    ', ' + (Code 'SoftwareApps') + ' selects ' + (Code $counterparts['SoftwareApps']) + '.')
Add-Line '2. **Attribute exclusively.** A Winhance ID is attributed to an Akari domain iff it is'
Add-Line '   normalised-equal to some ID in that domain **and** to no ID in any other Akari domain. A Winhance ID'
Add-Line '   matching more than one Akari domain - which happens exactly when the same ID exists in two of'
Add-Line ('   Akari''s own domains, the duplicate-ID defect - is attributed to **none**, and is listed by name in')
Add-Line ('   the ' + $T + 'Winhance-only (unattributed)' + $T + ' section together with the competing domains.')
Add-Line '3. **Disjointness is the guarantee.** Because attribution is exclusive, the per-side counts in the'
Add-Line '   domain sections are pairwise disjoint and satisfy this identity, which the generator asserts:'
Add-Line ''
Add-Line '```'
Add-Line ('sum(per-domain Winhance-side counts) + unattributed == ' + $winhanceAllDistinct.Count + '   (the whole Winhance ID set)')
Add-Line '```'
Add-Line ''
Add-Line ('   That identity is what makes six Optimize-side Akari domains safe to print: each shows only the')
Add-Line ('   Winhance IDs attributed to it, and the ' + $T + 'Optimize' + $T + ' directory''s own total appears once,')
Add-Line '   in the unattributed accounting - never six times.'
Add-Line ''
Add-Line 'No hand-written Akari-domain-to-Winhance-domain table exists anywhere in this generator. The header'
Add-Line 'already claims everything here is derived mechanically, and a table would make that claim false - the'
Add-Line 'same second-place-to-update failure the enumeration rule exists to prevent.'
Add-Line ''
Add-Line '## Classification buckets'
Add-Line ''
Add-Line 'Every ID on both sides lands in exactly one of five buckets, and the per-domain counts balance:'
Add-Line ''
Add-Line '```'
Add-Line 'raw-equal + normalised-equal + conflict + Akari-only + Winhance-only'
Add-Line '  == |Akari distinct| + |Winhance attributed distinct| - |raw-equal|'
Add-Line '```'
Add-Line ''
Add-Line ('* ' + (Code 'raw-equal') + ' - the exact ID string is present on both sides.')
Add-Line ('* ' + (Code 'normalised-equal') + ' - not raw-equal, but the two IDs normalise to the same key and')
Add-Line '  neither side has a rival at that key. This is the prefix-convention rename class, and it is the'
Add-Line '  bucket that keeps Customize from being reported as wholesale divergence.'
Add-Line ('* ' + (Code 'both-but-normalised-conflict') + ' - the normalised key is claimed by **more than one** ID on')
Add-Line '  either side, so the normalised diff is ambiguous at that key. Every claiming ID is counted here'
Add-Line '  rather than being called equal or one-sided.'
Add-Line ('* ' + (Code 'Akari-only') + ' - no Winhance ID shares this normalised key.')
Add-Line ('* ' + (Code 'Winhance-only') + ' - no Akari ID shares this normalised key.')
Add-Line ''
Add-Line '## Summary'
Add-Line ''
Add-Line '| Akari domain | Winhance counterpart | Token overlap | raw-equal | normalised-equal | conflict | Akari-only | Winhance-only | Akari IDs | Attributed Winhance IDs |'
Add-Line '|---|---|---|---|---|---|---|---|---|---|'
foreach ($r in $results) {
    Add-Line ('| ' + (Code $r.Domain) + ' | ' + (Code $r.Counterpart) + ' | ' + $r.OverlapPct + '% | ' +
        $r.RawEqual.Count + ' | ' + $r.Normalised.Count + ' | ' + $r.ConflictIds + ' | ' +
        $r.AkariOnly.Count + ' | ' + $r.WinhanceOnly.Count + ' | ' + $r.AkariIds.Count + ' | ' + $r.WinhanceIds.Count + ' |')
}
Add-Line ''
Add-Line ('Totals: Akari ' + (Get-Sum ($results | ForEach-Object { $_.AkariIds.Count })) + ' distinct IDs across ' +
    $results.Count + ' domains; Winhance ' + $winhanceAllDistinct.Count + ' distinct IDs across ' +
    $winhanceBearing.Count + ' ID-bearing directories, of which ' + $attributedTotal +
    ' were attributed to an Akari domain and ' + $unattributed.Count + ' were not.')
Add-Line ''
Add-Line 'A raw-diff-only reading of this table would report Customize as almost entirely divergent. The'
Add-Line ($T + 'normalised-equal' + $T + ' column is what separates "renamed by prefix convention" from "missing')
Add-Line 'setting", and it is the column a config-format fork decision should be based on.'
Add-Line ''

foreach ($r in $results) {
    Add-Line ('## ' + $r.Domain)
    Add-Line ''
    Add-Line ('* Winhance counterpart: ' + (Code $r.Counterpart) + ' (normalised-token overlap ' + $r.OverlapPct + '%)')
    Add-Line ('* Akari IDs: ' + $r.AkariIds.Count + ' distinct | Winhance IDs attributed to this domain: ' + $r.WinhanceIds.Count + ' distinct')
    Add-Line ('* Buckets: ' + (Code 'raw-equal') + ' ' + $r.RawEqual.Count + ', ' + (Code 'normalised-equal') + ' ' +
        $r.Normalised.Count + ' pair(s), ' + (Code 'both-but-normalised-conflict') + ' ' + $r.ConflictIds + ', ' +
        (Code 'Akari-only') + ' ' + $r.AkariOnly.Count + ', ' + (Code 'Winhance-only') + ' ' + $r.WinhanceOnly.Count)
    Add-Line ''
    Add-Line ('### ' + $T + 'raw-equal')
    Add-Line ''
    Add-CodeList $r.RawEqual
    Add-Line ''
    Add-Line ('### normalised-equal (prefix-convention rename, not a missing setting)')
    Add-Line ''
    if ($r.Normalised.Count -eq 0) {
        Add-Line '  (none)'
    } else {
        Add-Line '| Normalised key | Akari ID | Winhance ID |'
        Add-Line '|---|---|---|'
        foreach ($n in ($r.Normalised | Sort-Object -Property Key)) {
            Add-Line ('| ' + (Code $n.Key) + ' | ' + (Code $n.Akari) + ' | ' + (Code $n.Winhance) + ' |')
        }
    }
    Add-Line ''
    Add-Line ('### both-but-normalised-conflict')
    Add-Line ''
    if ($r.Conflict.Count -eq 0) {
        Add-Line '  (none)'
    } else {
        Add-Line '| Normalised key | Akari IDs claiming it | Winhance IDs claiming it |'
        Add-Line '|---|---|---|'
        foreach ($c in ($r.Conflict | Sort-Object -Property Key)) {
            Add-Line ('| ' + (Code $c.Key) + ' | ' + (($c.Akari | ForEach-Object { Code $_ }) -join '<br>') +
                ' | ' + (($c.Winhance | ForEach-Object { Code $_ }) -join '<br>') + ' |')
        }
    }
    Add-Line ''
    Add-Line ('### ' + $T + 'Akari-only')
    Add-Line ''
    Add-CodeList $r.AkariOnly
    Add-Line ''
    Add-Line ('### ' + $T + 'Winhance-only')
    Add-Line ''
    Add-CodeList $r.WinhanceOnly
    Add-Line ''
}

Add-Line ('## Winhance-only (unattributed)')
Add-Line ''
Add-Line ('These ' + $unattributed.Count + ' Winhance ID(s) are **excluded from every domain section**, which')
Add-Line 'is what keeps the per-side counts pairwise disjoint. Each is normalised-equal to an ID in **more than'
Add-Line ('one** Akari domain (the duplicate-ID defect) or to none at all.')
Add-Line ''
if ($unattributed.Count -eq 0) {
    Add-Line '  (none)'
} else {
    Add-Line '| Winhance ID | Reason |'
    Add-Line '|---|---|'
    foreach ($u in $unattributed) { Add-Line ('| ' + (Code $u) + ' | ' + $unattributedReasons[$u] + ' |') }
}
Add-Line ''
Add-Line ('## Duplicate IDs')
Add-Line ''
Add-Line ('Duplicates are reported, never collapsed: a set would hide a real defect that Phase 2''s BUG-02')
Add-Line 'uniqueness validator has to be able to see.'
Add-Line ''
Add-Line ('### Akari side')
Add-Line ''
$akariDupes = @($akari.duplicates)
if ($akariDupes.Count -eq 0) {
    Add-Line ('  (none - all ' + $akari.totals.ids + ' Akari IDs are distinct across all ' + $akari.totals.domains + ' domains)')
} else {
    Add-Line '| Domain | ID | Occurrences |'
    Add-Line '|---|---|'
    foreach ($d in $akariDupes) { Add-Line ('| ' + (Code $d.Domain) + ' | ' + (Code $d.Id) + ' | ' + $d.Count + ' |') }
}
Add-Line ''
Add-Line ('### Winhance side')
Add-Line ''
if ($winhanceDuplicates.Count -eq 0) {
    Add-Line '  (none)'
} else {
    Add-Line '| ID | Occurrences in the snapshot |'
    Add-Line '|---|---|'
    foreach ($d in $winhanceDuplicates) { Add-Line ('| ' + (Code $d.Id) + ' | ' + $d.Count + ' |') }
}
Add-Line ''
Add-Line ('## Disjointness identity')
Add-Line ''
Add-Line '```'
Add-Line ('sum(per-domain Winhance-side counts) = ' + $attributedTotal)
Add-Line ('+ unattributed                     = ' + $attributedTotal + ' + ' + $unattributed.Count + ' = ' + ($attributedTotal + $unattributed.Count))
Add-Line ('total distinct Winhance IDs       = ' + $winhanceAllDistinct.Count)
Add-Line '```'
Add-Line ''
Add-Line 'Both numbers agree, which is what lets a reader add the per-domain "other side" columns and get the'
Add-Line 'whole reference set rather than a multiple of it.'
Add-Line ''
Add-Line '---'
Add-Line ''
Add-Line ('Regenerate with: ' + (Code 'powershell -ExecutionPolicy Bypass -File tools\gen-setting-id-diff.ps1') + '.')
Add-Line 'This report asserts nothing; regenerating it is how a reader re-measures, never a gate.'
Add-Line ''
$reportDir = Split-Path -Parent $ReportPath
if (-not (Test-Path -LiteralPath $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
Write-Utf8NoBom -Path $ReportPath -Content (($L -join "`n") + "`n")

$akariTotal = Get-Sum ($results | ForEach-Object { $_.AkariIds.Count })
Write-Output ("domains: $($results.Count)  akariIds: $akariTotal  winhanceIds: $($winhanceAllDistinct.Count)")
Write-Output ("attributed: $attributedTotal  unattributed: $($unattributed.Count)  (identity holds: " +
    ($attributedTotal + $unattributed.Count -eq $winhanceAllDistinct.Count) + ')')
Write-Output "report: $ReportPath"
