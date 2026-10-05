---
gsd_state_version: "1.0"
milestone: v1.0
current_phase: 01
current_phase_name: Baseline, Spikes & Test Harness
status: executing
stopped_at: Completed 01-01-PLAN.md - the build+test loop, the App test project, and the badge characterization suite. Next: 01-02.
last_updated: "2026-10-05T21:52:00.000Z"
last_activity: 2026-10-05
last_activity_desc: Plan 01-01 complete (3 commits, 4 files)
state_head: e9e5aae8f0a88f53f5798f19b589557a88e027bb
progress:
  total_phases: 10
  completed_phases: 0
  total_plans: 4
  completed_plans: 1
milestone_name: Winhance Parity
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-05)

**Core value:** Every new capability must be addable as one self-contained vertical slice — a Core
contract, an Infrastructure service, and a UI page — without touching code outside its own feature
folder.

**Current focus:** Phase 01 — Baseline, Spikes & Test Harness

## Current Position

Phase: 01 (Baseline, Spikes & Test Harness) — EXECUTING
Plan: 2 of 4
Status: Executing Phase 01
Last activity: 2026-10-05 — Plan 01-01 complete (runner + App test project + badge characterization)

Progress: [███░░░░░░░] 25%

**The committed build gate:** `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1`
discovers MSBuild and `vstest.console.exe` through `vswhere` (never a hardcoded path — the VS
Community path named in AGENTS.md does not exist on this machine; the only install is Build Tools
2026 under `Program Files (x86)`), restores then builds as two separate MSBuild invocations, and
prints a measured test count from the TRX `Counters`. Exit 0 green / 1 real failure / 2
precondition. Build verdict comes from the error list, never `$LASTEXITCODE`.

**Measured test state (2026-10-05): 243 tests, 242 passed, 1 notExecuted** (Core 93,
Infrastructure 137, App.Tests 13). AGENTS.md's "53 + 136" is stale — never copy those numbers.

**Build definition:** VS MSBuild only —
`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64` — must emit every solution
assembly with zero compile errors and no new warnings beyond the Phase 1 baseline. The
`WINAPPSDKGENERATEPROJECTPRIFILE` PRI175/PRI252 error is pre-existing and Out of Scope; it is
tolerated but must never excuse a real compile error hiding behind it. **Note:** that error pair
is not reproducible run-to-run — it appears on a cold build and disappears once
`WinUI.Framework.pri` exists on disk, so the gate must *tolerate* these codes when present rather
than *require* them.

## Performance Metrics

**Velocity:**
- Total plans completed: 1
- Average duration: 47 min
- Total execution time: 47 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 1 | 47 min | 47 min |

**Recent Trend:**
- Last 5 plans: 01-01 (47 min)
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table. Recent decisions affecting current work:

- **Runner path is locked: `tools/run-tests.ps1`**, one script, `-Mode Build | Record | Baseline |
  Allowlist`. Chosen because 100+ existing references across nine planning documents already name
  this exact path — renaming would invalidate every later Build Gate line. Do not rename or split
  it.
- **`UseWinUI` is NOT usable on a test project** (measured): it stops the five
  `Microsoft.TestPlatform.*` assemblies from being copied, so every vstest run aborts with
  `FileNotFoundException` and zero tests. D-01's letter is deliberately not honoured; its rationale
  is preserved. Recorded in the csproj so the property is not re-added.
- **`AkariTool.App.Tests` is mapped to x64 in the solution, not AnyCPU**, because `AkariTool.App`
  is x64-only and an MSIL reference to it fails with MSB3270. Its output path therefore carries an
  `x64` segment while the other two test projects' does not.
- Localization sequenced at Phase 6 — at the **top** of the feature stage, above every phase that
  adds a page or setting (Phases 7-10).
- Configuration stays forked (D6); no `.winhance` import or shared config format in any phase.
- AkariOS is a sixth mirrored slice (D1); Settings/Home/Backup are UI-only slices (D7/ARCH-07).
- Builder-mode numeric-range and AC/DC recording is **fixed**, not replicated (D10).
- `CompatibleSettingsRegistry.GetKnownFeatureProviders()` is not moved or rewritten (CORE-06).

### Pending Todos

- Plan 01-02 must record the baseline against **243** tests, not 230 (230 was the pre-plan count).
- Plan 01-02's gate must tolerate PRI175/PRI252 when present, not require them.

### Blockers/Concerns

- SPIKE-02 is a hard gate on Phase 5 and Phase 9: if `SettingsCard`/`DataGrid`/`WrapPanel` do not
  resolve under WindowsAppSDK 2.3.1, those phases plan against substitutes.
- SPIKE-03 is the evidence base for the D6 fork decision.
- `ElevationService` impersonation is thread-affine — any change making an impersonated path async
  silently breaks elevated writes. Audit at every call site touched.
- Phase 5's CORE-04 badge-primitive extraction must leave `SettingBadgeCalculatorTests` passing
  unchanged. That suite is the guard; six of its assertions encode behaviour that reads like a
  defect (the definition-vs-parameter `Action` guard, the always-added `Custom` pill, AND-folded
  evidence). They are frozen deliberately — do not "fix" them to tidy the extraction.

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Localization | I18N-06, I18N-07 (29 locales, RTL) | Deferred | Requirements | v1.0 |
| Architecture | ARCH-13 (full `vendor/WinUI.Framework` removal) | Deferred | Requirements | v1.0 |
| Modes | MODE-06 (staged changes persist across restarts) | Deferred | Requirements | v1.0 |
| Packaging | WinUI 3 PRI175/PRI252 build failure | Out of Scope | PROJECT.md | v1.0 |

## Session Continuity

Last session: 2026-10-05T21:52:00.000Z
Stopped at: Completed 01-01-PLAN.md - the build+test loop, the App test project, and the badge characterization suite. Next: 01-02.
Resume file: .planning/phases/01-baseline-spikes-test-harness/01-02-PLAN.md
