---
gsd_state_version: "1.0"
milestone: v1.0
current_phase: 01
current_phase_name: Baseline, Spikes & Test Harness
status: executing
stopped_at: "Completed 01-03 (SPIKE-02) - Verdict: `all three render`, recorded from a human visual render confirmation. Phase 1 continues with 01-04 (SPIKE-03), which never depended on this verdict."
last_updated: "2026-10-06T00:40:00.000Z"
last_activity: 2026-10-06
last_activity_desc: "Plan 01-03 complete - SPIKE-02 resolved as `all three render`; tools/spike/ deleted per D-05; Phases 4, 5 and 9 unblocked"
state_head: 34fabe2
progress:
  total_phases: 10
  completed_phases: 0
  total_plans: 4
  completed_plans: 3
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
Plan: 3 of 4 complete — next is 01-04 (SPIKE-03)
Status: On track. SPIKE-02 is resolved and its hard gate on Phases 5 and 9 is cleared.
Last activity: 2026-10-06 — Plan 01-03 complete: SPIKE-02 resolved, the spike instrument deleted

Progress: [███████░░░] 75%

**SPIKE-02 is RESOLVED: `all three render`.** `SettingsCard`, `DataGrid` and `WrapPanel` all
render under WindowsAppSDK 2.3.1 with `CommunityToolkit.WinUI`. `01-SPIKE-02-VERDICT.md` carries all
eight sections with the Verdict section filled with one of the three permitted strings.

The gate was closed by **a human visual confirmation of the spike window**, at the whole-page
level — which is the only thing D-06 accepts. Both automated gates (clean two-pass compile; a
launch that survived 5s with a visible, responding 720×640 window) were green throughout and were
**explicitly not sufficient** on their own; a version-skewed toolkit resolves at compile time and
fails at XAML-load or theming time. No automated proxy for "renders correctly" was invented, and
none should be: asserting a control type is in the visual tree passes on an invisible or mis-themed
control, which is the exact failure the render gate exists to catch. **Phases 4, 5 and 9 must read
the Verdict section — not the Automated gates section.**

**`tools/spike/WinUiControlCompat/` has been deleted** per D-05, in its own commit made *before* the
verdict document so the record of what was tried survives the thing that was tried. No canary page,
placeholder, `.gitkeep` or dangling reference remains anywhere in the repository. The probe was
never a solution member and had zero `ProjectReference`s, so `git diff --exit-code -- AkariTool.sln
src/` stayed clean for the probe's entire life. **Do not recreate it as a permanent canary** — D-05
rejects one explicitly, because a later package bump would silently re-open this question.

Two measured facts routed here from 01-02 still travel with the verdict's context, and neither is
a regression: a solution `/t:Rebuild` **never regenerates `vendor\WinUI.Framework\bin\x64\`**, so a
fresh clone hits `CS0234` on its first build; and the `PRI175`/`PRI252` pair is **not reproducible
run-to-run** — present on a cold build, absent once `WinUI.Framework.pri` exists on disk, which is
why the gate *tolerates* those codes when present and never *requires* them.

**A human render gate that depends on a live window is a session-boundary hazard.** SPIKE-02's
gate had to be held open across **two sessions**: the spike window did not survive a session
rollover and had to be launched afresh to present the gate again, and the plan was correctly parked
(Verdict `PENDING`, instrument retained) in the meantime. What made recovery cheap was committing
the instrument to git in Task 1 — relaunching was a one-command `Start-Process`, not a rebuild. If
a future spike holds a gate open, **write the document down to `PENDING` first and keep the
instrument committed**, so the outstanding question survives even when the window does not.

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
a *minimum* 2.3.1 satisfies. **Phases 5 and 9 must copy that namespace mapping from section 3, not
from the package ids.**

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
- Total plans completed: 3
- Average duration: 47 min
- Total execution time: 141 min (109 min on 01-01/01-02 + 32 min of automated work on 01-03; its
  render gate added calendar time across two sessions, not agent time)

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01 | 3 complete | 141 min | 47 min |

**Recent Trend:**
- Last 3 plans: 01-01 (47 min), 01-02 (62 min), 01-03 (32 min automated + a two-session human gate)
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table. Recent decisions affecting current work:

- **SPIKE-02 closes on compile + launch + VISUAL RENDER, and the first two are not sufficient**
  (D-06, re-confirmed 2026-10-06 when the gate was actually discharged). A green build and a green
  launch are necessary inputs to the verdict, never the verdict — and this was not theoretical: the
  plan sat at `PENDING` for two sessions with both automated gates green because only a human
  looking at the window could close it. No automated proxy for "renders correctly" was invented,
  and none should be: asserting a control type is in the visual tree passes on an invisible or
  mis-themed control, which is the exact failure the render gate exists to catch. Phase 5 and
  Phase 9 must read `01-SPIKE-02-VERDICT.md`'s **Verdict section**, never its Automated gates.
- **SPIKE-02's answer is `all three render` — the gate is CLOSED, resolved in Phases 4/5/9's
  favour.** Recorded from a human visual confirmation of the spike window, given at the whole-page
  level. Phases 5 (`SettingsCard` row primitives) and 9 (`DataGrid` table view) may plan against
  the real controls; no substitute is needed anywhere. **The gate was open only from 2026-10-05
  until 2026-10-06 — a later phase reading a stale planning document must not conclude it is still
  open.**
- **A human render gate that depends on a live window is a SESSION-BOUNDARY HAZARD.** SPIKE-02's
  gate spanned two sessions because the spike window did not survive the session rollover. What
  made recovery cheap: **commit the instrument to git, and write the verdict document down to
  `PENDING` before presenting the gate**, so the outstanding question survives even when the
  window does not — the second session had the whole question on disk and needed only a relaunch.
  Do this for any future gate that needs a running app on screen.
- **Root-cause exclusion (D-07) is symmetric, and must stay that way.** SPIKE-02 recorded neither a
  theory for why a control would be incompatible **nor a theory for why the controls did render**.
  A passing result is not an invitation to explain the passing; a post-hoc mechanism is the same
  unbounded compatibility work pointed the other way.
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

- **Plan 01-04 (SPIKE-03) is the only remaining Phase 1 work.** It never depended on SPIKE-02 and is
  unblocked now that 01-03 is complete.
- **Phases 4, 5 and 9 may be planned — the SPIKE-02 gate is cleared.** They must read
  `01-SPIKE-02-VERDICT.md`'s **Verdict section** (`all three render`) and its **Configuration used /
  Configuration findings** for the XAML namespaces, and must NOT plan against the two green automated
  gates or assume any control was unavailable.
- **`tools/spike/` is deleted and must not come back as a canary** (D-05). A permanent control-compat
  page would silently re-open this question on every future package bump. If a package bump needs
  re-testing, rebuild the probe from the configuration recorded in the verdict.
- Plan 01-03 must **re-record the baseline** if anything from the spike is ever added to
  `AkariTool.sln` — a new project moves the warning count and the twelve-path inventory. (Nothing was;
  the spike had zero `ProjectReference`s, no solution membership, and is now deleted.)
- The gate must NOT be wired into CI (deferred idea; a single-machine baseline must not become
  authoritative on machines it was never measured on).

### Blockers/Concerns

- **RESOLVED: the SPIKE-02 render verdict exists.** `01-SPIKE-02-VERDICT.md` reads
  **`all three render`**, recorded from a human visual confirmation of the probe window on
  2026-10-06. `tools/spike/WinUiControlCompat/` has been **deleted** per D-05 in its own commit made
  before the verdict document, and no canary, placeholder or dangling reference survives. Phase 5 and
  Phase 9 plan against the **real** `SettingsCard` and `DataGrid`. This is no longer a blocker —
  if a planning document still says the gate is open, that document is stale.
- **Accepted risk that travels with the verdict:** `CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2`
  is **unmaintained** — last published 2021-11-18, six versions ever, the only DataGrid in existence.
  It renders; whether it still receives fixes is a separate question this spike does not answer.
- **The `CS0234` on a fresh clone is a known pre-existing condition, not a regression** (routed from
  01-02): a solution `/t:Rebuild` never regenerates `vendor\WinUI.Framework\bin\x64\`, so the App's
  PRI step finds nothing and `AkariTool.App.Tests` fails `CS0234` — correctly RED, since `CS0234` is
  not allowlisted. The restore is one direct-csproj build with `/p:Platform=x64`
  (`01-BASELINE.md` §3.1). It did **not** occur during 01-03: the spike built its own graph, touched
  nothing under `vendor/`, and its log carried no `PRI175`/`PRI252` at all. Do not "fix" it by widening
  the allowlist. Both facts are now also carried into the verdict's Configuration used section so a
  later reader comparing the two build logs does not read the difference as a regression.
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

Last session: 2026-10-06T00:40:00.000Z
Stopped at: Completed 01-03-PLAN.md (SPIKE-02). Verdict `all three render`, recorded from a human visual render confirmation; the probe window is closed and `tools/spike/` is deleted. Phase 1 next plan: 01-04 (SPIKE-03).
Resume file: .planning/phases/01-baseline-spikes-test-harness/01-04-PLAN.md
