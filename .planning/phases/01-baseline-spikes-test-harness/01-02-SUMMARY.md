---
phase: 01-baseline-spikes-test-harness
plan: 02
subsystem: testing
tags: [baseline-gate, msbuild, vstest, trx, vswhere, winui, pri175, d-12, d-13, d-14, d-16]

requires:
  - phase: 01-baseline-spikes-test-harness
    plan: 01-01
    provides: "tools/run-tests.ps1 reporting loop (vswhere discovery, error-list verdict, TRX counters, 243 tests), AkariTool.App.Tests, SettingBadgeCalculatorTests"
provides:
  - "tools/run-tests.ps1 -Mode Record|Baseline|Allowlist - the machine-checked gate nine later phases' Build Gate lines quote verbatim (per D-12, D-13, D-14, D-16)"
  - "tools/baseline.json - the recorded Phase 1 reference: 16 distinct warnings, 0 errors, 243 tests, 12 emitted assemblies (SPIKE-01)"
  - "tools/fixtures/allowlist-self-test.txt - the committed fixture that proves the PRI allowlist rejects a real compile error"
  - "01-BASELINE.md - the human-readable baseline record with its single-machine caveat and the five recorded gate drills (per D-15)"
  - "A measured finding: the PRI175/PRI252 pair is not cosmetic - on a cold tree it stops AkariTool.pri being emitted and makes AkariTool.App.Tests fail CS0234, which is correctly NOT allowlisted"
  - "A measured finding: a solution /t:Rebuild never regenerates vendor\\WinUI.Framework\\bin\\x64\\, so a fresh clone hits the PRI cascade on its first gate run"
affects: [01-03, 01-04, phase-02, phase-03, phase-05, all-later-build-gates, CI-future-work]

actuals:
  tokens: 23822
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Gate verdict computed from structured artefacts (error list, warning list, Test-Path inventory, TRX counters) - never from a process exit code"
    - "Allowlist as a closed two-entry literal with no parameter, env var or sidecar file, plus a structural tripwire that fails loudly if a future edit widens it"
    - "Reference-file validation precedes the expensive command: a missing baseline exits 2 instead of burning a /t:Rebuild on a typo"
    - "Warning counting de-duplicates by (location, code, message) so the compared number is solution-wide, not per-project"
    - "A committed fixture that exercises the gate's own predicate over a file, so the filter is proven rather than assumed"

key-files:
  created:
    - tools/baseline.json
    - tools/fixtures/allowlist-self-test.txt
    - .planning/phases/01-baseline-spikes-test-harness/01-BASELINE.md
  modified:
    - tools/run-tests.ps1

key-decisions:
  - "The PRI175/PRI252 pair is TOLERATED-WHEN-PRESENT and NEVER-REQUIRED. Measured: it appears only when vendor\\WinUI.Framework\\bin\\x64\\...\\WinUI.Framework.pri is absent, so errors.allowlistedCount is recorded for provenance and the comparison never reads it. A gate asserting 'errors == 2' would flip on whether a build artifact happened to be on disk."
  - "The emitted-assembly inventory is TWELVE paths, not three - the three test assemblies prove tests ran, the other nine prove the shipping output emitted."
  - "The App's output path carries the x64 platform segment, matching App.Tests. Read out of the build's own '<Project> -> <output>' lines, not inferred - a stale un-segmented bin\\Debug\\ tree exists and would have made a wrong assertion Test-Path true forever."
  - "vstest's process exit code is no longer captured or reported. The TRX Counters are the signal; keeping one fewer exit-code number keeps one fewer number available for somebody to gate on later."
  - "-Mode Record refuses to record over a run with a missing assembly - the inventory is one of the four recorded metrics, so recording it while entries are missing records a regression as the new normal."
  - "The failing metric renders 'recorded->measured' on the verdict line while passing metrics keep their documented shape, so the failing metric AND both its values appear on one line."

patterns-established:
  - "PASS (warnings <r><=<m>, errors <n> allowlisted, tests <m>>=<r>) on green; the failing metric's slot switches to <r>-><m> on red"
  - "An explicit '<unclassified>' bucket wherever a measurement is attributed, so a join that silently matches nothing shows up instead of looking like a working split"
  - "Provenance block (recordedOn + toolchain identity) on any committed reference file; -Mode Baseline refuses a reference with no recordedOn"

requirements-completed: [SPIKE-01]

coverage:
  - id: D1
    description: "-Mode Baseline exits 0 when nothing regressed and non-zero when any warning count, error count or test count moves the wrong way - one command instead of nine phases' hand-derived judgement."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "tools\\run-tests.ps1 -Mode Baseline -> exit 0, 'PASS (warnings 16<=16, errors 0 allowlisted, tests 243>=243)'"
        status: pass
      - kind: integration
        ref: "scratch baseline warnings=15 -> exit 1, 'FAIL (warnings 15->16, ...)'"
        status: pass
      - kind: integration
        ref: "scratch baseline tests.total=244 -> exit 1, 'FAIL (... tests 243->244)'"
        status: pass
    human_judgment: false
  - id: D2
    description: "The gate always forces /t:Rebuild; no incremental path and no skip-rebuild switch exists."
    requirement: SPIKE-01
    verification:
      - kind: other
        ref: "Select-String buildTarget -> 'Rebuild' assigned in both the Record and the Baseline branch; 'Build' only for the other two modes"
        status: pass
      - kind: integration
        ref: "five /t:Rebuild runs performed in this plan; measured 16 distinct warnings each, vs 12 on an incremental /t:Build of the same tree"
        status: pass
    human_judgment: false
  - id: D3
    description: "Tolerance is 'no increase, solution-wide': reductions pass silently and nothing is gated per-project."
    requirement: SPIKE-01
    verification:
      - kind: other
        ref: "Compare-ToBaseline warnings metric -> WarningsOk = (Measured -le Baseline); no per-project branch exists"
        status: pass
      - kind: integration
        ref: "measured 16 distinct vs 28 raw log lines - the compared number is the de-duplicated solution-wide one"
        status: pass
    human_judgment: false
  - id: D4
    description: "The error allowlist is exactly two entries, PRI175 and PRI252 from WINAPPSDKGENERATEPROJECTPRIFILE, and any error line lacking both the target and the code fails the gate. Nothing can widen it."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "tools\\run-tests.ps1 -Mode Allowlist -> exit 1; PRI175 TOLERATED, PRI252 TOLERATED, CS1002 REJECTED"
        status: pass
      - kind: integration
        ref: "cold run with vendor WinUI.Framework.pri deleted -> real PRI175/PRI252 both tolerated, run exits 0"
        status: pass
      - kind: other
        ref: "Select-String allowlist literal -> exactly 2 entries, both paired with WINAPPSDKGENERATEPROJECTPRIFILE; no parameter or env-var read adds a third"
        status: pass
    human_judgment: false
  - id: D5
    description: "No branch derives the verdict from $LASTEXITCODE; the sole executable read is the restore-failure check."
    requirement: SPIKE-01
    verification:
      - kind: other
        ref: "Select-String LASTEXITCODE -> 5 hits: 3 comments, 2 inside the restore-failure check; none in Compare-ToBaseline or Write-VerdictLine"
        status: pass
    human_judgment: false
  - id: D6
    description: "The gate never passes /p:SelfContained=true or /p:WindowsAppSDKSelfContained=true, and never uses dotnet build/test."
    requirement: SPIKE-01
    verification:
      - kind: other
        ref: "Select-String 'SelfContained=true' -> no match; Select-String 'dotnet build|dotnet test' -> no match"
        status: pass
    human_judgment: false
  - id: D7
    description: "tools/baseline.json records only numbers this plan's own run measured, never the stale AGENTS.md / TESTING.md figures, and carries toolchain identity plus capture date."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "-Mode Record -> tools/baseline.json; tests.total=243 (vs AGENTS.md 53+136=189, vs plan context 230); recordedOn=2026-10-05T20:09:45Z"
        status: pass
      - kind: other
        ref: "Get-Content -Encoding Byte -TotalCount 3 -> 7B 0D 0A (no UTF-8 BOM); ConvertFrom-Json parses; all 9 required top-level keys present"
        status: pass
    human_judgment: false
  - id: D8
    description: "All twelve expected assemblies are asserted present by exact repo-relative path; no *.dll glob under bin/, and no bin\\DeElevated\\ entry."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "-Mode Record exit 0 -> 12/12 emitted, 0 missing (Record refuses to record over a missing assembly)"
        status: pass
      - kind: other
        ref: "Select-String '@{ Dir = ' -> 12 entries; every 'DeElevated' occurrence in the script is a comment"
        status: pass
    human_judgment: false
  - id: D9
    description: "The gate has been observed failing: a non-allowlisted error line, a warning increase and a test-count decrease each drive a non-zero exit with the offending metric and both its values printed."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "three red drills recorded in 01-BASELINE.md section 8; all exited 1 and named the metric with both values"
        status: pass
      - kind: other
        ref: "git diff --exit-code -- tools/baseline.json -> empty after all drills; only scratch copies under %TEMP% were edited"
        status: pass
    human_judgment: false
  - id: D10
    description: "01-BASELINE.md states in its opening paragraph that the counts are a single-machine reference, and cross-references the stale upstream figures without editing them."
    requirement: SPIKE-01
    verification:
      - kind: other
        ref: "01-BASELINE.md first paragraph -> 'SINGLE-MACHINE REFERENCE'; section 5 tabulates AGENTS.md 53/136 and TESTING.md 53/136 against the measured 93/137/13"
        status: pass
      - kind: other
        ref: "git diff --name-status -> AGENTS.md and .planning/codebase/TESTING.md absent from the range"
        status: pass
    human_judgment: false

# Metrics
duration: 62min
completed: 2026-10-05
status: complete
commits: 3
plan_head_before: d18a4f8116a5d6b364ab8584cf1a03c8c688f91f
plan_head_after: a759a799032d6096bfb19dae88cee90379c425b7
---

# Phase 01 Plan 02: The Machine-Checked Build Gate Summary

**`-Mode Record | Baseline | Allowlist` on `tools/run-tests.ps1`, so "the restructure regressed
nothing" becomes one command with a trustworthy non-zero exit instead of a judgement call — with a
recorded baseline (16 distinct warnings, 0 errors, 243 tests, 12 assemblies) and five recorded gate
drills proving it goes red.**

## Performance

- **Duration:** ~62 min
- **Started:** 2026-10-05T21:57:00Z
- **Completed:** 2026-10-05T22:59:00Z
- **Tasks:** 3
- **Files modified:** 4 (3 created, 1 modified, 0 deleted)
- **Full rebuilds performed:** 8 — the phase's design cost, not an accident

## Accomplishments

- **The gate exists and is trustworthy in both directions.** `-Mode Baseline` forces `/t:Rebuild`,
  parses the error **list** against a closed two-entry PRI175/PRI252 allowlist, counts *distinct*
  solution-wide warnings, `Test-Path`s twelve exact assembly paths, reads the TRX `Counters`, and
  prints exactly one `PASS (...)`/`FAIL (...)` line in which the failing metric and **both** of its
  values appear inline. No branch anywhere reads `$LASTEXITCODE` — the only executable read is the
  restore-failure check.
- **The baseline is recorded from a real run and is not a transcription.** `-Mode Record` forced a
  full rebuild and wrote `tools/baseline.json` (UTF-8, no BOM): **16 distinct warnings** (28 raw log
  lines), **0 errors**, **243 tests** / 242 passed / 1 not-executed, split 93 / 137 / 13, and **12**
  emitted assemblies. It records **243**, not the plan context's 230 and not AGENTS.md's 189.
- **The gate was proven, not asserted.** Three red drills (non-allowlisted error line, warning
  increase 15→16, test-count decrease 243→244) each exited 1 naming the metric and both values; the
  final run against the committed baseline exited 0 with `PASS (warnings 16<=16, errors 0
  allowlisted, tests 243>=243)`. `git diff --exit-code -- tools/baseline.json` is **empty** — every
  drill edited only a scratch copy under `%TEMP%`.
- **The committed fixture found a live bug on its first run**, which is the entire reason such a
  fixture exists: it parsed **2 of 3** lines, because `Get-MsbuildErrorList` anchored its
  task-raised error pattern at line start while MSBuild prefixes the raising project's path. A real
  `CS0234` in that shape would have been *invisible* — never parsed, never rejected, silently
  passing the gate. Fixed, with both parser branches and both PRI codes now covered.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add `-Mode Record` and `-Mode Baseline` to the runner (D-12, D-13, D-14, D-16)** — `51f1a08` (feat)
2. **Task 2: Record the baseline and write `01-BASELINE.md` (D-12, D-15)** — `9f37f47` (docs)
3. **Task 3: Prove the gate actually goes red, then record green** — `a759a79` (test)

**Measured:** 3 commits between `plan_head_before` `d18a4f8` and `plan_head_after` `a759a79`. No
files were deleted. `actuals.tokens` = 23,822 (chars/4 over the 95,290-character realized diff).

## Files Created/Modified

- `tools/run-tests.ps1` — extended from a reporting loop into the gate. New/changed:
  `Get-MsbuildErrorList` (two-shape error parser, shared by the build and the fixture path),
  `Test-ErrorAllowlisted` (the single predicate), `Assert-ErrorAllowlistShape` (widening tripwire),
  `Get-MsbuildWarnings` / `Get-MsbuildWarningsByCode` (distinct-instance counting),
  `Get-CsprojElementValue` / `Get-TargetFramework` / `Get-RuntimeIdentifier`,
  `Get-ExpectedAssemblyPaths` (twelve paths), `Get-RepoRelativePath`, `Get-SdkBuildToolsVersion`,
  `Get-ToolVersionString`, `Get-TrxPerAssembly`, `Compare-ToBaseline`, `Write-VerdictLine`,
  `Write-BaselineFile`, `Read-BaselineFile`, `Write-AllowlistTable`; `-Mode` widened to
  `Build|Record|Baseline|Allowlist`; `-BaselinePath` and `-AllowlistSelfTestPath` added.
- `tools/baseline.json` — the recorded reference. Written only by `-Mode Record`; never hand-edited.
- `tools/fixtures/allowlist-self-test.txt` — three log-shaped lines: `PRI175` and `PRI252` on the
  `WINAPPSDKGENERATEPROJECTPRIFILE` target (canonical and task-raised shapes) plus one `CS1002`.
- `.planning/phases/01-baseline-spikes-test-harness/01-BASELINE.md` — the human-readable record.

## Decisions Made

1. **PRI175/PRI252 are tolerated-when-present and never-required** (measured — see Deviations #6).
2. **Twelve inventory paths, not three** — the three test assemblies prove tests ran; the other nine
   prove the shipping output emitted, which is what "the solution builds" must mean for an App-layer
   deliverable.
3. **The App's output path carries the `x64` segment**, matching `App.Tests` (Deviations #4).
4. **vstest's process exit code is no longer captured** (Deviations #5).
5. **`-Mode Record` refuses to record over a missing assembly** — the inventory is one of the four
   recorded metrics, so recording it incomplete would record a regression as the new normal.
6. **The failing metric's slot switches to `<recorded>-><measured>` on red** while passing metrics
   keep their documented shape, so one line carries the failing metric and both its values.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed a `$matches`-clobbering crash in the new warning-by-code helper**
- **Found during:** Task 1 (first `-Mode Build` run)
- **Issue:** `Get-MsbuildWarningsByCode` read `$matches['code']` *after* a second `-match` had
  overwritten the automatic variable, so `$code` was `$null` and `$counts.Contains($null)` threw
  `ArgumentNullException: Key cannot be null`, aborting the run.
- **Fix:** Capture `$matches` into a local immediately, before the next `-match`.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** `-Mode Build` exits 0; `warningsByCode` = `CS0436: 9, CS8604: 3, CS9113: 3,
  CA2024: 1`, summing to the 16 recorded.
- **Committed in:** `51f1a08`

**2. [Rule 1 - Bug] Corrected the per-assembly test split — the documented join matched nothing**
- **Found during:** Task 2 (first `ConvertFrom-Json` on the recorded baseline)
- **Issue:** The runner's own comment (left by plan 01-01) specified joining
  `TestDefinitions/UnitTest[@id]/@className`. Measured against the TRX `vstest.console.exe` 18.10
  writes, **there is no `className` attribute anywhere** — the join silently matched nothing and put
  all 243 results in `<unclassified>` while still looking like a working split.
- **Fix:** Join `Results/UnitTestResult/@testId` → `TestDefinitions/UnitTest[@id]/@storage`, which
  carries the declaring assembly's output path, and attribute by assembly file name. The stale
  comment was corrected in place so it cannot mislead the next reader.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** Split is now `Core.Tests 93 / Infrastructure.Tests 137 / App.Tests 13 /
  <unclassified> 0`, summing to 243 — identical to plan 01-01's documented split.
- **Committed in:** `9f37f47`

**3. [Rule 1 - Bug] Fixed a doubled test-assembly path handed to vstest**
- **Found during:** Task 1
- **Issue:** `Get-ExpectedAssemblyPaths` returns absolute paths; MAIN joined `$root` a second time,
  producing `…\Akari-Tool\C:\…\AkariTool.Core.Tests.dll` and `The test source file … was not found`.
- **Fix:** Pass the already-absolute subset straight through, with a comment naming the trap.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** vstest reports `A total of 3 test files matched the specified pattern`.
- **Committed in:** `51f1a08`

**4. [Rule 1 - Bug] Corrected the App's output path — a stale sibling tree would have passed forever**
- **Found during:** Task 1 (the plan's listed twelve paths were measured against disk)
- **Issue:** The plan listed the App output as
  `src\AkariTool.App\bin\Debug\<tfm>\win-x64\…`, but the solution build with `/p:Platform=x64`
  actually writes `src\AkariTool.App\bin\`**`x64`**`\Debug\<tfm>\win-x64\…` — confirmed from the
  build's own `<Project> -> <output>` console lines. The un-segmented `bin\Debug\…\win-x64\` tree
  **also exists**, holding a complete, plausible 1,468,928-byte `AkariTool.dll` last written at
  15:13 by an earlier *direct-csproj* build, so asserting the plan's path would have `Test-Path`
  true against a stale tree indefinitely — a false green, and the exact class of defect the twelve-path
  inventory exists to prevent. The plan's path also omitted the `x64` segment for `App.Tests`, whose
  real path is `bin\x64\Debug\<tfm>\`.
- **Fix:** Derived the platform segment per project from the measured build output, with an inline
  warning naming the stale sibling trees. Recorded in `01-BASELINE.md` §2.
- **Files modified:** `tools/run-tests.ps1`, `.planning/…/01-BASELINE.md`
- **Verification:** All 12 paths resolved from the build's own output lines; `Record` exits 0 with
  12/12 emitted and 0 missing.
- **Committed in:** `51f1a08`, documented `9f37f47`

**5. [Rule 3 - Blocking] Removed every `$LASTEXITCODE` outside the restore-failure check**
- **Found during:** Task 1 (plan-level verification)
- **Issue:** `Invoke-Vstest` returned `$LASTEXITCODE` and printed `vstest exit <n>` in the summary.
  The plan's verification requires the string to appear **only** inside the restore-failure check.
- **Fix:** `Invoke-Vstest` no longer captures or reports the process exit code, and `vstest exit <n>`
  is dropped from both summary formats. The TRX `Counters` plus TRX presence are the signals, and one
  fewer exit-code number is one fewer number available for a later reader to gate on.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** `Select-String LASTEXITCODE` → 5 hits: 3 in comments, 2 in the restore check;
  none in `Compare-ToBaseline` or `Write-VerdictLine`.
- **Committed in:** `51f1a08`

**6. [Rule 2 - Missing Critical] Made the PRI tolerance asymmetric, and proved it both ways**
- **Found during:** Task 2 / Task 3
- **Issue:** The plan's `must_haves` required the allowlist to *contain* PRI175/PRI252, and the
  baseline's `errors.allowlistedCount` would have been read as though the pair were expected. Measured:
  the pair is **absent** on any tree where `vendor\WinUI.Framework\bin\x64\…\WinUI.Framework.pri`
  exists, and **present** when it does not. An implementation that compared the count would flip
  green/red on whether a build artifact happened to be on disk.
- **Fix:** The comparison reads **only** `errors.notAllowlistedCount` (must be 0);
  `errors.allowlistedCount` is recorded for provenance and never compared. Documented in the script
  header and in `01-BASELINE.md` §3.1, and demonstrated in both states (drills 4 and 5).
- **Files modified:** `tools/run-tests.ps1`, `.planning/…/01-BASELINE.md`
- **Verification:** warm tree → `errors 0 allowlisted`, gate green; cold tree → `errors 2
  [PRI175=1 PRI252=1]`, both tolerated, run still exits 0.
- **Committed in:** `51f1a08`, documented `a759a79`

**7. [Rule 1 - Bug] Un-anchored the task-raised error pattern after the committed fixture caught it**
- **Found during:** Task 3, drill 1
- **Issue:** `Get-MsbuildErrorList` anchored the task-raised pattern with `^\s*`, requiring the
  target at position zero. MSBuild prefixes the raising project's path, so every task-raised line
  was dropped. The committed fixture parsed **2 of 3** lines. This is the shape real `PRI175`/`PRI252`
  lines take — so a real error in that shape was *invisible*: not reported, not tolerated, never
  parsed.
- **Fix:** Removed the anchor; the two patterns are mutually exclusive because the punctuation after
  `error` differs, so ordering is safe. The fixture now covers both branches and both PRI codes.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** All 3 fixture lines parsed; `PRI175` TOLERATED, `PRI252` TOLERATED, `CS1002`
  REJECTED, exit 1. Real cold-tree PRI lines also parse and are tolerated.
- **Committed in:** `a759a79`

**8. [Rule 1 - Bug] Replaced the XML-adapter csproj reader with a correct XPath reader**
- **Found during:** Task 1
- **Issue:** `$xml.Project.PropertyGroup.TargetFramework` is the PowerShell XML *adapter's*
  projection: it enumerates **every** `PropertyGroup` and yields an **empty string** for each one that
  does not declare the element. `AkariTool.App.csproj` has three `PropertyGroup`s and only the first
  declares `TargetFramework`, so the expression returned a 3-element `Object[]` whose string form is
  `"net10.0-windows10.0.26100.0  "` — two trailing spaces. Interpolated into a path it produced
  `…\net10.0-windows10.0.26100.0  \AkariTool.App.Tests.dll`, which `Test-Path` and vstest both
  rejected. The same expression was harmless for the test csprojs (one `PropertyGroup` each), which
  is why it survived plan 01-01.
- **Fix:** `Get-CsprojElementValue` uses `SelectNodes("//*[local-name()='X']")` and **throws** on
  conflicting values, because a csproj declaring the element conditionally cannot be resolved
  statically and guessing would point the inventory at a directory the build never writes.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** All inventory paths resolve cleanly; `-Mode Build` exits 0 with 243 tests.
- **Committed in:** `51f1a08`

**9. [Rule 1 - Bug] Corrected the inverted structural tripwire on the allowlist literal**
- **Found during:** Task 1 (first Allowlist run)
- **Issue:** The defensive "breach" check fired on a *correctly* tolerated `PRI175` line, because it
  asked only whether an entry named the target — which is exactly what a correct entry does. It
  reported a breach on the good case and would have stayed silent on the bad one.
- **Fix:** Replaced it with `Assert-ErrorAllowlistShape`, which checks the literal itself: every entry
  must name **both** a target and a code (a half-named entry is a wildcard), and the count must be
  exactly 2.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** No breach reported on the correct fixture; the check is unreachable with the
  committed literal and fails loudly if a future edit widens it.
- **Committed in:** `51f1a08`

**10. [Rule 1 - Bug] Fixed `$stamp` being undefined when `-ResultsDirectory` was supplied**
- **Found during:** Task 1
- **Issue:** `$stamp` was assigned inside the `if (-not $ResultsDirectory)` block but read later for
  the TRX name, so an explicitly supplied results directory produced a TRX named `-Name.trx`.
- **Fix:** Compute `$stamp` unconditionally.
- **Files modified:** `tools/run-tests.ps1`
- **Verification:** TRX written as `run-<stamp>.trx` on every observed run.
- **Committed in:** `51f1a08`

---

**Total deviations:** 10 auto-fixed (8 × Rule 1 bug, 1 × Rule 2 missing-critical, 1 × Rule 3
blocking; #4 is a correction to the plan's own measured facts).
**Impact on plan:** #1, #3, #8, #9 and #10 were latent defects in code this task wrote or extended,
caught by running it. #2 and #4 are corrections to upstream facts — the plan's `className` join and
its listed assembly paths — where the plan itself said "whatever the run reports is the truth".
#5, #6 and #7 tighten rather than loosen: each removes a way the gate could go falsely green.
No scope was added outside the plan's four files.

## Issues Encountered

- **The PRI175/PRI252 pair is not cosmetic — on a cold tree it cascades into a real compile error.**
  Reproduced deliberately in drill 5 by deleting `vendor\WinUI.Framework\bin\**\WinUI.Framework.pri`.
  `PRI252` reports the vendor PRI missing → `WINAPPSDKGENERATEPROJECTPRIFILE` fails →
  **`AkariTool.pri` is never emitted** (verified absent from the App output) → MSBuild does not
  deliver the App's output to its `ProjectReference` consumers →
  `AkariTool.App.Tests\ViewModels\Tweaks\SettingBadgeCalculatorTests.cs(5,17): error CS0234: The
  type or namespace name 'ViewModels' does not exist in the namespace 'AkariTool'`. `CS0234` is not
  allowlisted, so the cold-tree gate run goes **RED** — correctly. The allowlist tolerates the PRI
  pair; it does not tolerate what the PRI pair causes.
- **A solution `/t:Rebuild` never regenerates the artifact that pair depends on.** It builds
  `vendor\WinUI.Framework` as **Any CPU** into `bin\Debug\…\WinUI.Framework.pri`; the App's PRI step
  reads the **x64** path. That file exists on this machine only because plan 01-01's diagnostics built
  the csproj directly with `/p:Platform=x64`. **On a freshly cloned tree it will not exist and the
  gate will go red on its first run.** The red is correct and must not be silenced by widening the
  allowlist; the one-command restore is recorded in `01-BASELINE.md` §3.1. Fixing the underlying PRI
  fragility is Out of Scope for this milestone.
- **The tree was restored, not left broken.** After drill 5 the vendor PRI was regenerated by a
  direct-csproj `/t:Rebuild /p:Platform=x64`, and the final `-Mode Baseline` run returned green with
  `AkariTool.pri` back at 2,305,600 bytes. No tracked file was touched during the experiment — both
  deleted paths were untracked build outputs under `vendor\WinUI.Framework\bin\`.
- **`git rev-list --count` first reported `0`.** A shell artifact: reading the on-disk plan-head
  ledger without trimming embedded a newline in the revision range. Re-measured with `.Trim()`:
  **3**, matching the three visible commits, with the ledger SHA confirmed an ancestor of HEAD.

## Known Stubs

None. Every number in `tools/baseline.json` came from a `-Mode Record` run; nothing is
hand-authored, defaulted, or placeholder. No `TODO`/`FIXME` and no unimplemented branch was left.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: elevated-execution | `tools/run-tests.ps1` | The runner executes repo-built test assemblies with the invoking token and refuses to report green when not elevated (exit 2). Mitigated by `Assert-Elevated` and by addressing exact paths with no recursive glob, inherited from plan 01-01. |
| threat_flag: reference-file-trust | `tools/baseline.json` | A committed file in the working tree is the reference every later phase is judged against. Mitigated per `T-01-04`: written only by `-Mode Record`, carries a `recordedOn` provenance stamp, and `-Mode Baseline` **refuses to compare** against a reference lacking one — so a hand-authored baseline cannot masquerade as a measured one. Task 3's drills operated only on `%TEMP%` scratch copies, verified by `git diff --exit-code -- tools/baseline.json` being empty afterwards. |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for 01-03** (the SPIKE-02 control-compatibility spike) and **01-04** (the setting-ID diff
generator). What they have:

- `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline` — one command, exit 0
  green / 1 regression / 2 precondition. Every later Build Gate line quotes it verbatim.
- A recorded reference (`tools/baseline.json`) that is 243 tests, **not** 230 and **not** 189, with
  the per-assembly split and the warning-by-code breakdown.
- `01-BASELINE.md` as the record of what was measured, what is not gated, and the three upstream
  corrections most likely to be reintroduced.

**Two facts 01-03's author must carry:**
1. If the spike project is ever added to the solution, the baseline must be **re-recorded** — a new
   project moves the warning count and the inventory.
2. The gate's PRI tolerance is asymmetric and must stay that way; the cold-tree `CS0234` cascade is
   the evidence that the allowlist is a filter, not a suppressor.

**Carried into STATE.md as a concern:** a fresh clone has no
`vendor\WinUI.Framework\bin\x64\…\WinUI.Framework.pri` and will hit the PRI cascade on its first
gate run. Not a Phase 1 defect (the PRI fragility is Out of Scope), but it must not be mistaken for a
regression later.

## Self-Check: PASSED

- **Files created and present on disk:** `tools/baseline.json`,
  `tools/fixtures/allowlist-self-test.txt`,
  `.planning/phases/01-baseline-spikes-test-harness/01-BASELINE.md` — all three `Test-Path` verified.
- **Commits exist and are ancestors of HEAD:** `51f1a08`, `9f37f47`, `a759a79` — verified via
  `git log "$B..HEAD"`; `commits: 3` is **measured** from the on-disk plan-head ledger, not narrated.
- **`git diff --name-status "$B..HEAD"`** shows exactly 4 files: 3 added, 1 modified, **0 deleted**.
- **Task 1 acceptance criteria:** all 7 pass. Allowlist probe → exit 1 with `PRI175` TOLERATED /
  `CS1002` REJECTED; absent `-BaselinePath` → exit **2** naming the file with no MSBuild invocation
  line emitted; allowlist literal → exactly 2 entries both paired with
  `WINAPPSDKGENERATEPROJECTPRIFILE` with no parameter/env-var widening path; no `SelfContained=true`,
  no `dotnet build`/`dotnet test`; `Rebuild` in the `Record` and `Baseline` branches; 12 inventory
  entries with no `DeElevated` path; `-Mode Build` → exit 0 with **243 total / 242 passed / 1
  notExecuted**, identical to plan 01-01.
- **Task 2 acceptance criteria:** all 5 pass. JSON parses; all 9 top-level keys present; `assemblies`
  holds 12; first 3 bytes `7B 0D 0A` (**no BOM**); `tests.total` 243 ≠ 189; `01-BASELINE.md`'s first
  paragraph carries the single-machine warning and names `PRI175`/`PRI252` as pre-existing and
  tolerated; the exact re-verification command is present verbatim. **The escalation branch did not
  trigger**: the recorded error set contains **no code other than** `PRI175`/`PRI252` — in fact
  **zero** errors were recorded, so nothing was admitted to the allowlist.
- **Task 3 acceptance criteria:** all 6 pass. Allowlist → exit 1 with both PRI lines tolerated and
  `CS1002` rejected; scratch `warnings=15` → exit 1 printing `FAIL` + `warnings` + both numbers;
  scratch `tests.total=244` → exit 1 naming the test count; committed baseline → exit 0 with exactly
  one `PASS (warnings 16<=16, errors 0 allowlisted, tests 243>=243)`; `git diff --exit-code --
  tools/baseline.json` empty; `01-BASELINE.md` carries a "Gate self-verification" section with the
  command, exit code and verdict line for every drill.
- **Plan-level `<verification>`:** all 6 bullets pass, including
  `Select-String LASTEXITCODE` finding the string only in comments and the restore-failure check.
- **Prohibitions:** all 8 honoured — no `$LASTEXITCODE` verdict; the allowlist still holds exactly two
  entries and was never widened (the cold-tree `CS0234` was escalated and recorded rather than
  tolerated); `baseline.json` was never hand-edited and contains none of the stale figures; no
  per-project gate; no toolchain gate; no `-SkipRebuild` switch and every gate run was a `/t:Rebuild`;
  no CI, trigger or pipeline was added; the fixture lives in `tools/fixtures/`, never in `tests/`.
- **File-scope:** only the four planned files were touched, plus `STATE.md`/`ROADMAP.md`/this SUMMARY.
  `AGENTS.md` and `.planning/codebase/TESTING.md` were read and cross-referenced but **not edited**.

---
*Phase: 01-baseline-spikes-test-harness*
*Completed: 2026-10-05*
