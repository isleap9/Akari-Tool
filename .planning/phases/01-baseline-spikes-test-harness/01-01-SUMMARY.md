---
phase: 01-baseline-spikes-test-harness
plan: 01
subsystem: testing
tags: [xunit, vstest, msbuild, vswhere, winui, pri175, trx, characterization-test]

requires:
  - phase: none
    provides: "Pre-existing: 230 tests across two test projects (Core 93 / Infrastructure 137), a solution that builds with two tolerated PRI errors."
provides:
  - "tools/run-tests.ps1 - the committed one-command build+test runner nine later phases cite by path (per D-03, per D-15)"
  - "tests/AkariTool.App.Tests - the first App-layer test project, seventh project in AkariTool.sln (per TEST-01, per D-01)"
  - "SettingBadgeCalculatorTests - a 12-test characterization baseline for Phase 5's CORE-04 extraction (per D-02)"
  - "A measured answer to D-01's UseWinUI question: the property is NOT usable on a test project"
affects: [01-02, 01-03, 01-04, 05-common-machinery, phase-02, phase-05, all-later-build-gates]

# Actuals (#2632) - same scale as the plan's estimate (chars/4 over the realized diff).
actuals:
  tokens: 24000
  tasks: 4
  commits: 3

tech-stack:
  added: []
  patterns:
    - "vswhere toolchain discovery - never a hardcoded VS install path"
    - "Build verdict computed from the error LIST, never from $LASTEXITCODE"
    - "Restore and Build as two separate MSBuild invocations (combined breaks XAML codegen)"
    - "Explicit assembly paths resolved from the csproj TFM; never a recursive test-DLL glob"
    - "Characterization tests freeze today's behaviour rather than assert what is ideal"

key-files:
  created:
    - tools/run-tests.ps1
    - tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj
    - tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs
  modified:
    - AkariTool.sln

key-decisions:
  - "Runner fixed at tools/run-tests.ps1, one script, -Mode Build|Record|Baseline|Allowlist (Task 1 option-a). Chosen because 100+ existing references across nine planning documents already name this exact path; renaming would have meant rewriting every Build Gate in the roadmap."
  - "UseWinUI REMOVED from the test project - D-01's letter deliberately not honoured, its stated rationale fully preserved (documented deviation, see below)."
  - "AkariTool.App.Tests mapped to the real x64 platform in the solution, not AnyCPU - the plan's prescribed mapping provably cannot work."
  - "Error and warning counts sourced from a single WarningsOnly file logger plus a captured console log, because MSBuild silently honours only the LAST /flp on a command line."

patterns-established:
  - "Failure reporting names the metric that moved: 'warnings <n>', 'errors <n> not allowlisted', 'tests <n> failed'"
  - "Three documented exit codes branches every later phase relies on: 0 green, 1 real failure, 2 precondition"
  - "Run artifacts (TRX, build logs) default to a per-run directory under %TEMP%, never the repository"

requirements-completed: [TEST-01]
requirements-partial:
  - id: SPIKE-01
    note: >-
      Declared by both 01-01 and 01-02, and NOT genuinely complete here. Its text is "a recorded
      build baseline exists (assemblies produced, warning and error counts)" - the recording is
      plan 01-02's job (tools/baseline.json + 01-BASELINE.md). What 01-01 delivers is the
      instrument that produces those numbers, plus the first measured values (243 tests,
      28 warnings on a forced rebuild, the PRI code set). Marking it complete now would assert a
      recorded baseline that does not exist on disk yet.

coverage:
  - id: D1
    description: "One committed command rebuilds the solution through vswhere-discovered MSBuild, runs every test assembly through vstest.console.exe, and prints a measured test count from the TRX Counters element."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "powershell -ExecutionPolicy Bypass -File tools\\run-tests.ps1"
        status: pass
      - kind: integration
        ref: "tools/run-tests.ps1 -> exit 0, 'tests 243 total, 242 passed, 0 failed, 1 notExecuted'"
        status: pass
    human_judgment: false
  - id: D2
    description: "MSBuild and vstest.console.exe are discovered through vswhere on every run; no Visual Studio installation path is hardcoded anywhere in tools/."
    requirement: SPIKE-01
    verification:
      - kind: integration
        ref: "grep 'Program Files\\Microsoft Visual Studio' tools/run-tests.ps1 -> no match"
        status: pass
      - kind: integration
        ref: "Test-Path on the AGENTS.md Community path -> False; script does not reference it"
        status: pass
    human_judgment: false
  - id: D3
    description: "AkariTool.App.Tests exists, is a member of AkariTool.sln, builds, and its tests are discovered and executed by vstest."
    requirement: TEST-01
    verification:
      - kind: integration
        ref: "vstest tests\\AkariTool.App.Tests\\bin\\x64\\Debug\\...\\AkariTool.App.Tests.dll -> 13/13 passed"
        status: pass
      - kind: integration
        ref: "Select-String AkariTool.sln 'AkariTool.App.Tests' -> Project entry + NestedProjects entry; new GUID x14"
        status: pass
    human_judgment: false
  - id: D4
    description: "SettingBadgeCalculator.Compute is characterised by real assertions, including the definition.InputType == InputType.Action guard read from the definition rather than the parameter."
    requirement: TEST-01
    verification:
      - kind: unit
        ref: "tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs#ActionDefinition_NonActionInputType_ReturnsNoPills"
        status: pass
      - kind: unit
        ref: "tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs#NumericRange_MinutesUnits_ConvertsSystemValueToDisplayUnits"
        status: pass
    human_judgment: false
  - id: D5
    description: "SettingBadgeCalculatorTests is the only test class in the new project - no real-catalog validity test, container-resolution test or Core-references test landed."
    requirement: TEST-01
    verification:
      - kind: other
        ref: "git ls-files tests/AkariTool.App.Tests -> exactly one .cs; one class declaration in it"
        status: pass
    human_judgment: false
  - id: D6
    description: "D-01's UseWinUI question is answered by an actual build, with the deviation recorded rather than assumed away."
    requirement: TEST-01
    verification:
      - kind: integration
        ref: "clean rebuild with UseWinUI -> 0 of 5 Microsoft.TestPlatform.* DLLs copied, 'Test Run Aborted', 0 tests"
        status: pass
      - kind: integration
        ref: "clean rebuild without UseWinUI -> 5 of 5 copied, smoke test passes"
        status: pass
    human_judgment: false

# Metrics
duration: 47min
completed: 2026-10-05
status: complete
commits: 3
plan_head_before: 3a99bab20544a22654cf4a2cb739694679e2ccd4
plan_head_after: e9e5aae8f0a88f53f5798f19b589557a88e027bb
---

# Phase 01 Plan 01: The Measurement Loop Summary

**One command (`tools/run-tests.ps1`) that discovers the VS toolchain through vswhere, builds the
solution, runs all three test assemblies through `vstest.console.exe`, and prints a test count
measured from the TRX `Counters` element — plus the first App-layer test project and a
12-test characterization baseline for `SettingBadgeCalculator.Compute`.**

## Performance

- **Duration:** ~47 min
- **Started:** 2026-10-05T19:11:43Z
- **Completed:** 2026-10-05T21:50:00Z
- **Tasks:** 4 (1 decision checkpoint, 1 tracer, 2 auto)
- **Files modified:** 4

## Accomplishments

- **Task 1 — runner name locked, no rewriting needed.** `tools/run-tests.ps1`, one script,
  `-Mode Build | Record | Baseline | Allowlist`. Option-a selected. **The measured reason the name
  was worth locking before nine phases cite it: 100+ existing references** across `01-VALIDATION.md`,
  `SKELETON.md`, `01-CONTEXT.md`, `01-02/03/04-PLAN.md`, `01-PATTERNS.md`, `01-RESEARCH.md`,
  `01-DISCUSSION-LOG.md` and `ROADMAP.md` already name this exact path. Had option-b
  (`build-and-test.ps1`) been taken, every one of those — plus every remaining phase's Build Gate
  line — would have needed rewriting in the same pass as the file, with the recorded Phase 1
  baseline re-measured against the new invocation. No planning document was edited to record this,
  because the name was already correct everywhere.
- **Task 2 — the walking skeleton is proven end to end.** The loop runs green on a machine with no
  Visual Studio IDE (Build Tools 2026 only) and reports a measured `230 total / 229 passed /
  1 notExecuted` with the PRI175/PRI252 breakdown and a warning count that comes from the run.
- **Task 3 — the first App-layer test project exists and runs**, as the seventh solution project,
  nested under `tests`. The whole-solution total moved 230 → 231 exactly as predicted.
- **Task 4 — `SettingBadgeCalculator.Compute` is characterised** by 12 tests, including the
  `InputType.Action` guard, pill ordering, the AC/DC `considerDc` branch, and the `"Minutes"`
  integer-division boundary at both 3600 and 3599. Whole-suite total 243.

## Task Commits

Each task was committed atomically:

1. **Task 1: Confirm the runner's path and file name** — no commit; a decision, recorded here.
2. **Task 2: Prove the loop (tracer)** - `e42d181` (feat)
3. **Task 3: Add `AkariTool.App.Tests` and prove vstest discovers it** - `183130f` (feat)
4. **Task 4: Characterise `SettingBadgeCalculator.Compute`** - `e9e5aae` (feat)

**Measured:** 3 commits between `plan_head_before` `3a99bab` and `plan_head_after` `e9e5aae`.
No files were deleted.

## Files Created/Modified

- `tools/run-tests.ps1` - the committed build+test runner. Discovers MSBuild and
  `vstest.console.exe` via vswhere, restores then builds as two separate invocations, classifies
  build errors against a two-entry PRI allowlist, runs three exact test-assembly paths, parses the
  TRX `Counters`, and exits 0/1/2.
- `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj` - mirrors the Infrastructure.Tests csproj;
  one `ProjectReference` to `AkariTool.App`; no `InternalsVisibleTo`.
- `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` - the 12-test
  characterization suite.
- `AkariTool.sln` - seventh project entry, 12-line config block under a freshly generated GUID
  `{AD856391-9292-437F-90B0-AC127F174034}`, nested under `tests`.

## Decisions Made

1. **Runner path `tools/run-tests.ps1`, one script, four `-Mode` values** (Task 1, option-a).
   Rationale in Accomplishments above: 100+ pre-existing references.
2. **`UseWinUI` removed from the test project** — a deliberate deviation from D-01's *letter*.
   See Deviations #1.
3. **`AkariTool.App.Tests` mapped to x64 in the solution, not AnyCPU** — the plan's prescribed
   mapping provably cannot work. See Deviations #2.
4. **Error/warning sourcing uses one `/flp` plus a captured console log.** MSBuild silently honours
   only the *last* `/flp` on a command line (measured: two loggers → one file). Two loggers cannot
   be relied on for a number that nine phases will gate on.
5. **Six characterization assertions were corrected against the real implementation** rather than
   the implementation being touched. See Issues Encountered — these are the task's main findings.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug / documented fallback] Removed `<UseWinUI>true</UseWinUI>` from the test project**
- **Found during:** Task 3 (D-01 probe)
- **Issue:** With `UseWinUI=true` the project compiles and copies the `AkariTool.App` reference
  fine, but the five `Microsoft.TestPlatform.*` assemblies that `vstest`'s testhost loads at run
  time are **not** copied to the output. Every test run aborts:
  `System.IO.FileNotFoundException: Could not load file or assembly
  'Microsoft.TestPlatform.CoreUtilities, Version=15.0.0.0'`, and vstest reports `Test Run Aborted`
  with zero tests.
- **Fix:** Removed the one property — the plan's own documented fallback. `<EnableMsixTooling>`
  and `<WindowsPackageType>` were **not** added; the markup targets never demanded them.
- **Verification:** Clean rebuild in both states. With `UseWinUI`: 0 of 5 TestPlatform DLLs
  copied, run aborted. Without it: 5 of 5 copied, 13/13 tests pass.
- **Impact on D-01:** D-01's *letter* (`UseWinUI=true`) is **not honoured** — this is a real,
  deliberate deviation. D-01's *stated rationale* ("tests only App logic that needs no live
  `DispatcherQueue` and no XAML visual tree") is **fully preserved**, because
  `SettingBadgeCalculator` needs no XAML at all. D-01's chosen *strategy* (direct
  `ProjectReference` to `AkariTool.App`, no compile-linking, no moving App logic into Core) is
  unchanged. The csproj carries a comment recording the measurement so nobody re-adds the property.
- **Files modified:** `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj`
- **Committed in:** `183130f`

**2. [Rule 1 - Bug] Mapped `AkariTool.App.Tests` to x64 instead of the plan's prescribed AnyCPU**
- **Found during:** Task 3 (solution wiring)
- **Issue:** The plan prescribed copying the Infrastructure.Tests block's
  `Debug|x64.ActiveCfg = Debug|Any CPU` shape. That provably cannot work here:
  `AkariTool.App` is x64-only (`Platforms=x64`, `RuntimeIdentifier=win-x64`), so an MSIL test
  assembly referencing it fails with
  `error MSB3270: There was a mismatch between the processor architecture of the project being
  built "MSIL" and the reference "AMD64"`. The two existing test projects can be AnyCPU only
  because Core and Infrastructure are AnyCPU libraries.
- **Fix:** Mapped the new project's configurations to `Debug|x64` / `Release|x64`. This is why its
  output path carries an `x64` segment while the other two test projects' does not;
  `tools/run-tests.ps1` now declares the path shape per project with the reason inline.
- **Verification:** `MSB3270` absent after the change; the project builds and its 13 tests run.
- **Files modified:** `AkariTool.sln`, `tools/run-tests.ps1`
- **Committed in:** `183130f`

**3. [Rule 3 - Blocking] Fixed the repo-root anchor in the runner**
- **Found during:** Task 2
- **Issue:** The house pattern `$root = $PSScriptRoot` is copied from `build-deelevated.ps1`, which
  sits at the repo root. This script lives in `tools\`, so `$PSScriptRoot` resolved to
  `tools\` and the first run failed looking for `tools\tests\...csproj`.
- **Fix:** `$root = Split-Path -Parent $PSScriptRoot`, with a comment explaining why the analog's
  version does not transfer.
- **Files modified:** `tools/run-tests.ps1`
- **Committed in:** `e42d181`

**4. [Rule 3 - Blocking] Corrected a PowerShell format-string precedence bug**
- **Found during:** Task 2
- **Issue:** `("a {0}" + "b {1}") -f x` — `-f` binds tighter than `+`, so only the second literal
  was formatted and the first printed `{0}` verbatim. The summary line was unreadable.
- **Fix:** Bound the concatenated format string to a variable before applying `-f`.
- **Files modified:** `tools/run-tests.ps1`
- **Committed in:** `e42d181`

**5. [Rule 3 - Blocking] Extracted `Invoke-Vstest` as a named helper**
- **Found during:** Task 2
- **Issue:** The vstest call was inlined in `MAIN`, but the plan's `artifacts` list names
  `Invoke-Vstest` among the script's internal helpers.
- **Fix:** Extracted it as a function returning the process exit code, with a comment that the exit
  code is reported but never used as the verdict.
- **Files modified:** `tools/run-tests.ps1`
- **Committed in:** `e42d181`

**6. [Rule 2 - Missing Critical] Removed the Visual Studio install literal from the runner's header**
- **Found during:** Task 2 (acceptance-criteria gate)
- **Issue:** The header comment explaining *why* the toolchain is discovered quoted AGENTS.md's
  Community path verbatim — which the plan's own acceptance criterion forbids anywhere in the
  script, precisely so a future reader cannot "correct" the discovery back to a constant.
- **Fix:** Rewrote the comment to describe the path's *shape* (a Community install under Program
  Files rather than Program Files (x86)) without embedding the literal.
- **Files modified:** `tools/run-tests.ps1`
- **Committed in:** `e42d181`

---

**Total deviations:** 6 auto-fixed (4 × Rule 3 blocking, 1 × Rule 1 bug with a documented
fallback, 1 × Rule 1 bug, 1 × Rule 2 missing-critical — Rules 1 and 2 overlap in #1/#2's
classification).
**Impact on plan:** Deviations #1 and #2 are the substantive ones and both are *consequences of the
plan asking this task to build and observe rather than assume* — that is the task working as
designed, and both are recorded in commit messages and in the csproj itself. #3–#6 are mechanical.

## Issues Encountered

- **Six characterization assertions were wrong on first run** — and that is the substantive finding
  of Task 4, not a nuisance. The tests were written from reading the source; reading was not enough.
  All six were corrected **against the real implementation**, with production code never touched:
  1. **The NumericRange non-perMode path emits two pills, not one.** A `Custom` pill is added
     unconditionally whenever `hasRecData || hasDefData` (`SettingBadgeCalculator.cs:203-204`), so
     `Recommended` and `Custom` both appear, with `Custom` dimmed when the value matches. The plan's
     behaviour 9/10 phrasing implied a single pill.
  2. **`matchesRec` / `matchesDef` are AND-folded across every evidence source.** A *matching*
     registry source cannot un-dim a pill the toggle-level state already dimmed — the first draft
     assumed it could.
  3. **For a Selection, the highlight comes from the flag on the option *at* the selected index.**
     Selecting the recommended option highlights `Recommended` and dims `Default`; the plan's
     behaviour 6 said "both highlighted".
  These are now pinned by the suite with comments recording the mechanism, which is exactly the
  baseline Phase 5 needs.

- **The PRI175/PRI252 error set is not reproducible run-to-run.** Both errors appeared on the
  initial cold build and are **absent** once `vendor\WinUI.Framework\bin\...\WinUI.Framework.pri`
  (960 bytes) exists on disk. Pre-existing and Out of Scope per D-16, but it has a direct
  consequence for plan 01-02: **the baseline gate must tolerate these two codes when present, not
  require them**, and a gate that asserts "errors == 2" would pass or fail depending on whether a
  PRI file happened to be on disk. Flagged for 01-02.

- **`/flp` is honoured once, not twice.** Two file loggers on one MSBuild command line silently
  produce one file. Measured directly. This is why the runner takes its warning count from a
  `WarningsOnly` logger and its error list from a captured console log.

## Known Stubs

None. No stub, placeholder, or mock data was introduced. Every assertion runs against the real
`SettingBadgeCalculator.Compute`; the only substituted value is `vstest`'s, which is real.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| threat_flag: elevated-execution | `tools/run-tests.ps1` | The runner deliberately executes repo-built test assemblies with the invoking token and refuses to report green when not elevated (exit 2). Mitigated by `Assert-Elevated` and by addressing three exact paths with no recursive glob, per `T-01-02`. |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for 01-02** (`-Mode Baseline` / `Record` / `Allowlist`), which has everything it needs:
- the loop exists and is proven, so the gate can be layered on top of it;
- `Resolve-VSToolchain`, `Get-MsbuildErrors`, `Get-MsbuildWarnings`, `Read-TrxCounters` are all
  factored as named helpers to extend;
- the two-entry PRI allowlist already exists as two literals, with no parameter through which a
  third code could be admitted, per D-16;
- the per-assembly TRX split source is documented in the script so `baseline.json` need not
  re-derive it;
- **and the measured facts it must respect**: the PRI error set is not reproducible, so tolerate
  rather than require; the build is incremental here so warning counts under-report on a warm tree,
  which is exactly what 01-02's forced `/t:Rebuild` fixes; `App.Tests` output carries an `x64`
  path segment.

**Test count moved 230 → 243 across this plan** (231 after Task 3, plus 12 in Task 4). Any document
recording 230 as "current" is now stale, and the baseline captured in 01-02 must be 243.

## Self-Check: PASSED

- Files created and present on disk: `tools/run-tests.ps1`,
  `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj`,
  `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` — all three verified.
- Commits exist and are ancestors of HEAD: `e42d181`, `183130f`, `e9e5aae` — all three verified via
  `git log`; `commits: 3` is measured, not narrated.
- `git diff --name-status` over the plan's range shows exactly 4 files: 3 added, 1 modified, 0 deleted.
- Task 2 acceptance criteria: all pass (exit 0; both discovered paths printed and both under
  `...\Visual Studio\18\BuildTools`; AGENTS.md Community path `Test-Path` → False and unreferenced;
  measured `230/229/1`; 0 `.trx` under the repo root; no `SelfContained=true`; no recursive glob).
- Task 3 acceptance criteria: all pass (exactly one `ProjectReference` to `AkariTool.App`; no
  `InternalsVisibleTo`; `git diff -- src/AkariTool.App/AkariTool.App.csproj` empty; sln matches in
  both sections; non-zero test count with no `No test is available`; commit message records the
  `UseWinUI` outcome).
- Task 4 acceptance criteria: all pass (`failed=0`, `notRunnable=0`; 12 distinct methods covering
  all ten named behaviours; a test named for the `Action` guard; 7 `because:` arguments; 9
  full-sequence assertions; both 3600 and 3599 present; exactly one tracked `.cs` file declaring
  only `SettingBadgeCalculatorTests`; runner reports 243 = 231 + 12).
- Plan-level verification: all five bullets pass.

---
*Phase: 01-baseline-spikes-test-harness*
*Completed: 2026-10-05*