---
gsd_state_version: "1.0"
milestone: v1.0
current_phase: 01
current_phase_name: Baseline, Spikes & Test Harness
status: executing
stopped_at: "HALTED at 01-03 Task 3's human gate - the SPIKE-02 render check. The spike window (PID 10320) is OPEN on screen awaiting a human verdict; tools/spike/ is deliberately NOT deleted until then."
last_updated: "2026-10-06T00:05:00.000Z"
last_activity: 2026-10-06
last_activity_desc: "Plan 01-03 halted at the render gate (3 commits, 6 files) - compile and launch gates green, Verdict PENDING"
state_head: 0547140
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
Plan: 3 of 4 — **HALTED at the SPIKE-02 render gate**
Status: Blocked on a human render check; 01-04 is not blocked by it
Last activity: 2026-10-06 — Plan 01-03 halted: the probe builds and launches, the verdict awaits a human

Progress: [█████░░░░░] 50%

**SPIKE-02 is at its human gate, and this is the only thing standing between it and a verdict.**
The throwaway probe at `tools/spike/WinUiControlCompat/` was built against the five exact-pinned
packages and **launched**: `PASS: spike still running after 5s`, window visible, un-minimized,
720×640, responding. **Both automated gates are green and both are explicitly NOT sufficient**
(D-06 rejects compile-only: a version-skewed toolkit resolves at compile time and fails at
XAML-load or theming time). `01-SPIKE-02-VERDICT.md` carries all eight sections with
**Verdict = `PENDING`**. No automated proxy for "renders correctly" was invented, because one would
pass on an invisible or mis-themed control — the exact failure D-06 exists to catch.

**The spike window is still OPEN on screen (PID 10320) and `tools/spike/` is deliberately NOT
deleted.** Deleting the instrument while the human is looking at it would destroy the evidence.
Task 3's remaining obligations — record each control's outcome, prove any substitute renders on the
same page, fill the verdict, close the window, delete the project, commit the verdict after the
deletion — are enumerated in `01-03-SUMMARY.md`.

**All three XAML namespaces resolved DIFFERENTLY from the plan's prediction**, and this is a
finding, not a mishap. The 8.2 train declares `SettingsCard` and `WrapPanel` **directly in the
flattened namespace** `CommunityToolkit.WinUI.Controls`; the per-package names that appear in those
assemblies are *assembly* names, and an `xmlns:using:` prefix must name a namespace. Frozen
`DataGrid 7.1.2` sits in `CommunityToolkit.WinUI.UI.Controls`, not the legacy
`Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`. Every prefix was read from each package's own XML doc
`T:` member list, never guessed, and each superseded prefix's verbatim `CS0234` / `CS0426` /
`WMC0001` is preserved in the verdict's Configuration findings section. These are
**namespace-resolution findings, explicitly not rendering verdicts** — no `NU1102`/`NU1605`/
`NU1608`/`MSB4011` occurred, and the WCT nuspec `Microsoft.WindowsAppSDK 1.6.250108002` proved to be
a *minimum* 2.3.1 satisfies. **Whatever Phase 5 and Phase 9 write, they must read the Verdict
section — not these gates.**

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
- Total plans completed: 2 (01-03 halted, not complete)
- Average duration: 55 min
- Total execution time: 109 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 2 complete + 1 halted | 109 min + 32 min (halted) | 55 min |

**Recent Trend:**
- Last 5 plans: 01-01 (47 min), 01-02 (62 min), 01-03 (32 min, HALTED at the human gate)
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table. Recent decisions affecting current work:

- **SPIKE-02 closes on compile + launch + VISUAL RENDER, and the first two are not sufficient**
  (D-06, re-confirmed 2026-10-06). A green build and a green launch are necessary inputs to the
  verdict, never the verdict. No automated proxy for "renders correctly" was invented for 01-03,
  and none should be: asserting a control type is in the visual tree passes on an invisible or
  mis-themed control, which is the exact failure the render gate exists to catch. Phase 5 and
  Phase 9 must read `01-SPIKE-02-VERDICT.md`'s **Verdict section**, never its Automated gates.
- **WCT 8.2 XAML prefixes are a FLATTENED namespace, and the per-package names are a trap.**
  `SettingsCard` and `WrapPanel` are both declared directly in `CommunityToolkit.WinUI.Controls`.
  `…Controls.SettingsControls` and `…Controls.Primitives` appear in those assemblies as *assembly*
  names only, and an `xmlns:using:` prefix must name a namespace. Frozen `DataGrid 7.1.2` is in
  `CommunityToolkit.WinUI.UI.Controls`, not the legacy `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`.
  Verified from each package's own XML doc `T:` list. Read the assembly; do not trust the package id.
- **A throwaway spike with ZERO `ProjectReference`s and no solution membership makes D-05
  machine-checkable.** `git diff --exit-code` on `AkariTool.App.csproj` and `AkariTool.sln` is the
  real assertion, and it held clean throughout 01-03. Never add a spike to the solution "temporarily" —
  that is what moves the recorded warning count and the twelve-path inventory.
- **`/p:WindowsAppSDKSelfContained=true` is safe for exactly one shape in this repo**: a `WinExe` with
  zero `ProjectReference`s. Global properties flow into `ProjectReference`s, and
  `Microsoft.WindowsAppSDK.Base.targets:19-20` hard-errors on a class library — so it must never be
  passed to `AkariTool.sln`, which has five libraries.
- **Launch the spike with `Start-Process -PassThru` on the `.exe`, never `dotnet run`.** `dotnet run`
  does not surface WinUI XAML-load failures the way a direct launch does, and D-06 requires observing
  a real launch. Also assert `IsWindowVisible`/`IsIconic`, not just `HasExited`: an alive process has
  not necessarily put a usable window on screen.
- **Restore and Build are two separate MSBuild invocations for any WinUI project** — a combined
  `/t:Restore,Rebuild` breaks XAML codegen (same rule as `build-deelevated.ps1:52-53`).
- **An x64-platform WinUI project emits under `bin\x64\Debug\…`, not `bin\Debug\…`.** A `Platforms=x64`
  project built with `/p:Platform=x64` puts an `x64` segment in the output path. Plan 01-03's verify
  block named the un-segmented path; the build was right and the plan's path was wrong. Same shape
  already recorded for `AkariTool.App` — never assert an un-segmented `bin\Debug\…` path for an
  x64 project, and never drop `Platforms` to satisfy such an assertion.
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

- **SPIKE-02 is HALTED at its render gate and must not be closed by an agent.** Record each control's
  outcome from the open window, prove any substitute renders on the same page, fill the Verdict, close
  the window, delete `tools/spike/WinUiControlCompat/`, then commit the verdict document **after** the
  deletion. Full checklist in `01-03-SUMMARY.md`.
- Plan 01-04 (SPIKE-03) does **not** depend on SPIKE-02's verdict and may proceed. But Phase 4, Phase 5
  and Phase 9 all must, and none of them may be planned against the two green automated gates.
- Plan 01-03 (SPIKE-02) must **re-record the baseline** if the spike project is ever added to
  `AkariTool.sln` — a new project moves the warning count and the twelve-path inventory. (It never was;
  the spike has zero `ProjectReference`s and no solution membership.)
- Plan 01-03 must NOT wire the gate into CI (deferred idea; a single-machine baseline must not become
  authoritative on machines it was never measured on).

### Blockers/Concerns

- **OPEN: the SPIKE-02 render verdict does not exist yet.** `01-SPIKE-02-VERDICT.md` reads
  `PENDING`. A probe window titled "SPIKE-02 - WinUI control compatibility probe" is **open on screen**
  (PID 10320) and `tools/spike/WinUiControlCompat/` is deliberately **still present** — deleting it
  before the human looks would destroy the instrument. Phase 5 and Phase 9 plan against substitutes
  if `SettingsCard`/`DataGrid`/`WrapPanel` do not render; neither may start until this reads a verdict.
- **The `CS0234` on a fresh clone is a known pre-existing condition, not a regression** (routed from
  01-02): a solution `/t:Rebuild` never regenerates `vendor\WinUI.Framework\bin\x64\`, so the App's
  PRI step finds nothing and `AkariTool.App.Tests` fails `CS0234` — correctly RED, since `CS0234` is
  not allowlisted. The restore is one direct-csproj build with `/p:Platform=x64`
  (`01-BASELINE.md` §3.1). It did **not** occur during 01-03: the spike builds its own graph, touches
  nothing under `vendor/`, and its log carried no `PRI175`/`PRI252` at all. Do not "fix" it by widening
  the allowlist.
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

Last session: 2026-10-06T00:05:00.000Z
Stopped at: HALTED at 01-03 Task 3's human gate - the SPIKE-02 render check. Probe window OPEN (PID 10320); Verdict PENDING; tools/spike/ intentionally still present.
Resume file: .planning/phases/01-baseline-spikes-test-harness/01-03-SUMMARY.md
