---
gsd_state_version: "1.0"
milestone: v1.0
current_phase: 01
current_phase_name: Baseline, Spikes & Test Harness
status: executing
stopped_at: Completed 01-02-PLAN.md - the machine-checked build gate (-Mode Record|Baseline|Allowlist), tools/baseline.json, and 01-BASELINE.md. Next: 01-03.
last_updated: "2026-10-05T23:05:00.000Z"
last_activity: 2026-10-05
last_activity_desc: Plan 01-02 complete (3 commits, 4 files)
state_head: a759a799032d6096bfb19dae88cee90379c425b7
progress:
  total_phases: 10
  completed_phases: 0
  total_plans: 4
  completed_plans: 2
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
Plan: 3 of 4
Status: Executing Phase 01
Last activity: 2026-10-05 — Plan 01-02 complete (the machine-checked build gate + recorded baseline)

Progress: [█████░░░░░] 50%

**The committed build gate:** `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline`
is the command every remaining phase's Build Gate line quotes verbatim. It forces `/t:Rebuild`, matches
the parsed error **list** against a closed two-entry `PRI175`/`PRI252` allowlist, counts **distinct**
solution-wide warnings, `Test-Path`s twelve exact assembly paths, reads the TRX `Counters`, and prints
one `PASS (...)`/`FAIL (...)` line in which the failing metric and **both** its values appear inline.
Exit 0 green / 1 regression / 2 precondition. **No branch reads `$LASTEXITCODE`** — the only executable
read is the restore-failure check. The gate has been observed red three ways and green once; all five
drills are recorded in `01-BASELINE.md` §8.

**Recorded baseline (2026-10-05, `tools/baseline.json`, written only by `-Mode Record`):**
16 distinct warnings (28 raw log lines), **0 errors**, **243 tests** / 242 passed / 1 not-executed,
split Core 93 / Infrastructure 137 / App.Tests 13, and 12 emitted assemblies. **243** is the current
figure — not the plan context's 230 and never AGENTS.md's 189.

**The PRI tolerance is asymmetric and must stay that way.** `PRI175`/`PRI252` are tolerated **when
present** and never **required**: they appear only when
`vendor\WinUI.Framework\bin\x64\…\WinUI.Framework.pri` is absent, so `errors.allowlistedCount` is
recorded for provenance and the comparison reads only `errors.notAllowlistedCount` (must be 0).

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
- Total plans completed: 2
- Average duration: 55 min
- Total execution time: 109 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 2 | 109 min | 55 min |

**Recent Trend:**
- Last 5 plans: 01-01 (47 min), 01-02 (62 min)
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table. Recent decisions affecting current work:

- **`PRI175`/`PRI252` are tolerated-when-present, never-required** (measured 2026-10-05). The pair is
  present only when `vendor\WinUI.Framework\bin\x64\…\WinUI.Framework.pri` is absent, so the gate
  compares `errors.notAllowlistedCount` (must be 0) and never `allowlistedCount`. A gate asserting
  "errors == 2" would flip on whether a build artifact happened to be on disk. Do not tighten this
  into a required count, and do not widen it.
- **The assembly inventory is twelve exact paths, and the App's output carries the `x64` segment**
  (`bin\x64\Debug\<tfm>\win-x64\`, matching `App.Tests`). Read out of the build's own
  `<Project> -> <output>` lines. A stale un-segmented `bin\Debug\…\win-x64\` tree exists in the
  working copy and would make a wrong assertion `Test-Path` true forever — never glob, never glob.
- **`tools/baseline.json` is written only by `-Mode Record`** and carries a `recordedOn` stamp;
  `-Mode Baseline` refuses to compare against a reference lacking one, so a hand-authored baseline
  cannot masquerade as a measured one. Re-record rather than edit.
- **The gate counts DISTINCT warnings, not log lines** — 28 raw lines collapse to 16. A raw count
  would double-count every transitive-consumer warning and make the gate trip for a non-regression.
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
- **The per-assembly test split joins `UnitTest[@id]/@storage`, not `@className`** — vstest 18.10
  writes no `className` anywhere; the old join silently put all 243 results in `<unclassified>`
  while still looking like a working split.
- Localization sequenced at Phase 6 — at the **top** of the feature stage, above every phase that
  adds a page or setting (Phases 7-10).
- Configuration stays forked (D6); no `.winhance` import or shared config format in any phase.
- AkariOS is a sixth mirrored slice (D1); Settings/Home/Backup are UI-only slices (D7/ARCH-07).
- Builder-mode numeric-range and AC/DC recording is **fixed**, not replicated (D10).
- `CompatibleSettingsRegistry.GetKnownFeatureProviders()` is not moved or rewritten (CORE-06).

### Pending Todos

- Plan 01-03 (SPIKE-02) must **re-record the baseline** if the spike project is ever added to
  `AkariTool.sln` — a new project moves the warning count and the twelve-path inventory.
- Plan 01-03 must NOT wire the gate into CI (deferred idea; a single-machine baseline must not become
  authoritative on machines it was never measured on).

### Blockers/Concerns

- **A freshly cloned tree will hit the PRI cascade on its first gate run.** A solution `/t:Rebuild`
  builds `vendor\WinUI.Framework` as Any CPU into `bin\Debug\…`, but the App's PRI step reads the
  **x64** path, which a solution rebuild never produces. With it absent, `AkariTool.pri` is never
  emitted and `AkariTool.App.Tests` fails `CS0234` — correctly RED, since `CS0234` is not allowlisted.
  The restore is one direct-csproj build (`/p:Platform=x64`); recorded in `01-BASELINE.md` §3.1. The
  underlying fragility is Out of Scope, but this must not be mistaken for a regression later.
- SPIKE-02 is a hard gate on Phase 5 and Phase 9: if `SettingsCard`/`DataGrid`/`WrapPanel` do not
  resolve under WindowsAppSDK 2.3.1, those phases plan against substitutes.
- SPIKE-03 is the evidence base for the D6 fork decision.
- `ElevationService` impersonation is thread-affine — any change making an impersonated path async
  silently breaks elevated writes. Audit at every call site touched.
- Phase 5's CORE-04 badge-primitive extraction must leave `SettingBadgeCalculatorTests` passing
  unchanged. That suite is the guard; six of its assertions encode behaviour that reads like a
  defect (the definition-vs-parameter `Action` guard, the always-added `Custom` pill, AND-folded
  evidence). They are frozen deliberately — do not "fix" them to tidy the extraction.
- `tools/fixtures/allowlist-self-test.txt` is **runner input, not a test.** It must never move into
  `tests/` — that would change the recorded baseline test count.

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Localization | I18N-06, I18N-07 (29 locales, RTL) | Deferred | Requirements | v1.0 |
| Architecture | ARCH-13 (full `vendor/WinUI.Framework` removal) | Deferred | Requirements | v1.0 |
| Modes | MODE-06 (staged changes persist across restarts) | Deferred | Requirements | v1.0 |
| Packaging | WinUI 3 PRI175/PRI252 build failure | Out of Scope | PROJECT.md | v1.0 |

## Session Continuity

Last session: 2026-10-05T23:05:00.000Z
Stopped at: Completed 01-02-PLAN.md - the machine-checked build gate (-Mode Record|Baseline|Allowlist), tools/baseline.json, and 01-BASELINE.md. Next: 01-03.
Resume file: .planning/phases/01-baseline-spikes-test-harness/01-03-PLAN.md
