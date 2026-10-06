---
phase: 01-baseline-spikes-test-harness
plan: 03
subsystem: ui
tags: [winui3, windowsappsdk, communitytoolkit, spike, control-compatibility, msbuild]

requires:
  - phase: 01-baseline-spikes-test-harness
    provides: "plan 01-01 — tools/run-tests.ps1 and its Resolve-VSToolchain vswhere discovery, reused here as the single MSBuild source of truth"
provides:
  - "01-SPIKE-02-VERDICT.md — SPIKE-02 RESOLVED: `all three render`. SettingsCard, DataGrid and WrapPanel all render under WindowsAppSDK 2.3.1 with CommunityToolkit.WinUI. Recorded from a human visual confirmation, not from the automated gates."
  - "A cleared hard gate for Phase 4, Phase 5 (SettingsCard row primitives) and Phase 9 (DataGrid table view) — all three may plan against the real controls, not substitutes"
  - "Three verbatim configuration findings (CS0234 / CS0426 / WMC0001) recording that all three XAML namespaces resolved differently from what the plan and 01-RESEARCH.md predicted"
  - "Proof of the two automated halves of D-06: a clean two-pass MSBuild compile and a launch-survival assertion with a visible, un-minimized, responding window"
  - "tools/spike/WinUiControlCompat/ created and deleted inside this plan — the instrument leaves no trace, no canary and no placeholder behind (D-05)"
affects: [04-domain-file-moves, 05-shared-row-primitives, 09-software-apps-table-view, phase-09-table-view]

actuals:
  tokens: 14370
  tasks: 3
  commits: 8

tech-stack:
  added:
    - "CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219 (spike-only, added and deleted inside this plan)"
    - "CommunityToolkit.WinUI.Controls.Primitives 8.2.251219 (spike-only, added and deleted inside this plan)"
    - "CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2 (spike-only, added and deleted inside this plan)"
    - "Microsoft.Windows.CsWinRT 2.3.1 (spike-only, added and deleted inside this plan)"
  patterns:
    - "Throwaway spike with zero ProjectReferences and no solution membership, so D-05's 'app package list stays byte-for-byte identical' is machine-checkable rather than asserted"
    - "Restore and Build as two separate MSBuild invocations — a combined /t:Restore,Rebuild breaks WinUI XAML codegen"

key-files:
  created:
    - ".planning/phases/01-baseline-spikes-test-harness/01-SPIKE-02-VERDICT.md — the durable deliverable (D-15), now carrying the resolved verdict"
  modified: []
  deleted:
    - "tools/spike/WinUiControlCompat/ (5 tracked files: WinUiControlCompat.csproj, App.xaml, App.xaml.cs, MainWindow.xaml, MainWindow.xaml.cs) — deleted in Task 3 per D-05"

key-decisions:
  - "The render gate closed on a WHOLE-PAGE human answer ('all three render'), and the verdict document records it at that granularity — naming each control, stating what it was required to show, and attributing the meeting of those criteria to the human's confirmation rather than to any agent inspection. No finer-grained per-control detail was invented, because no agent had screen access."
  - "A configuration failure stays a configuration finding. C-1..C-3 (CS0234 / CS0426 / WMC0001) are statements about where a type is declared and say nothing about rendering; they were left exactly as recorded in Task 2 rather than folded into the verdict."
  - "Root-cause diagnosis was excluded in BOTH directions (D-07): no theory for why a control is incompatible, and equally no theory for why the controls did render. A passing result is not an invitation to explain the passing."
  - "No substitute was constructed, because no control failed — and D-07's 'prove the substitute renders on the same page' obligation was therefore never entered rather than discharged with a guess."
  - "The spike project was deleted BEFORE the verdict document was committed, in a separate earlier commit, so the record of what was tried survives even though the thing that was tried does not."

patterns-established:
  - "Configuration failure and rendering verdict are kept in separate document sections, each finding explicitly labelled, so a namespace or restore error can never be read as evidence about rendering."
  - "Window-liveness is proven with IsWindowVisible / IsIconic / rect bounds alongside HasExited — a process that is alive has not necessarily put a usable window on screen."
  - "A human gate that must be discharged in a live process window is a session-boundary hazard: the instrument has to be relaunchable, and the record has to name the fact that it was relaunched, or a later reader ties the verdict to a PID that no longer exists."

requirements-completed: [SPIKE-02]

coverage:
  - id: D1
    description: "A clean-room WinUI probe compiles against the five exact-pinned packages (WindowsAppSDK 2.3.1 metapackage, CsWinRT 2.3.1, SettingsControls/Primitives 8.2.251219, DataGrid 7.1.2) with zero ProjectReferences and no solution membership."
    requirement: SPIKE-02
    verification:
      - kind: other
        ref: "MSBuild /t:Restore then /t:Rebuild on tools/spike/WinUiControlCompat/WinUiControlCompat.csproj — Restore exit 0, Rebuild exit 0, no 'error ' line, no XLSQ/WMC/MC"
        status: pass
      - kind: other
        ref: "git diff --exit-code -- src/AkariTool.App/AkariTool.App.csproj AkariTool.sln — clean (D-05)"
        status: pass
      - kind: other
        ref: "Select-String over the csproj: exactly 5 PackageReference lines, all exact versions, no '[' or '*' range, no CommunityToolkit.WinUI\" metapackage, no Microsoft.WindowsAppSDK.WinUI, no Microsoft.Windows.SDK.BuildTools, no ProjectReference"
        status: pass
    human_judgment: false
  - id: D2
    description: "The spike launches, survives five seconds, and presents a visible, un-minimized, responding window."
    requirement: SPIKE-02
    verification:
      - kind: other
        ref: "Start-Process -PassThru + Start-Sleep 5 + assert -not HasExited — printed 'PASS: spike still running after 5s'"
        status: pass
      - kind: other
        ref: "IsWindowVisible=True, IsIconic=False, rect 286,286-1006,926 (720x640 on a 3640x1920 screen), MainWindowTitle='SPIKE-02 - WinUI control compatibility probe', Responding=True"
        status: pass
    human_judgment: false
  - id: D3
    description: "The three XAML namespaces resolve, and the resolved mapping is recorded verbatim rather than guessed."
    requirement: SPIKE-02
    verification:
      - kind: other
        ref: "Package XML doc T: member lists read for SettingsControls, Primitives and DataGrid; prefixes corrected until Rebuild exited 0 with no WMC0001/XLSQ/CS0234/CS0426"
        status: pass
      - kind: other
        ref: "01-SPIKE-02-VERDICT.md section 3 records C-1 CS0234, C-2 CS0426 and C-3 WMC0001 verbatim, each labelled a namespace-resolution finding and not a rendering verdict"
        status: pass
    human_judgment: false
  - id: D4
    description: "THE SPIKE-02 VERDICT ITSELF: SettingsCard, DataGrid and WrapPanel are visible and correctly themed under WindowsAppSDK 2.3.1 — recorded as `all three render`."
    requirement: SPIKE-02
    verification:
      - kind: manual_procedural
        ref: "Human visual confirmation of the open 'SPIKE-02 - WinUI control compatibility probe' window, answered at the whole-page level: `all three render`. Recorded in 01-SPIKE-02-VERDICT.md section 5."
        status: pass
    human_judgment: true
    rationale: "D-06 sets the bar at compile PLUS launch PLUS visual render and explicitly rejects compile-only, because a version-skewed toolkit resolves at compile time and fails at XAML-load or theming time. No command distinguishes 'renders correctly' from 'instantiated but invisible or mis-themed' — an assertion that the control type is in the visual tree passes on an invisible or mis-themed control, which is the exact failure D-06 exists to catch. No automated proxy was invented. The confirmation is a human visual observation and no agent had screen access, so it can never be upgraded to an automated pass. Recorded as manual-only in 01-VALIDATION.md's Manual-Only Verifications table."
  - id: D5
    description: "The spike project is deleted from the working tree, leaving no canary page, placeholder, .gitkeep or dangling reference behind (D-05)."
    requirement: SPIKE-02
    verification:
      - kind: other
        ref: "git status --porcelain -- tools/spike — five 'D' entries, nothing else"
        status: pass
      - kind: other
        ref: "Test-Path tools\\spike — False; tools/ contains only fixtures, baseline.json, run-tests.ps1"
        status: pass
      - kind: other
        ref: "git grep -i 'WinUiControlCompat' -- src tests tools AkariTool.sln vendor — no match"
        status: pass
    human_judgment: false

duration: 32min + the render gate, held open across two sessions
completed: 2026-10-06
status: complete
commits: 8
plan_head_before: f493c9c2b75c5fc63a8bdd3ed08caa0ced6c1d40
plan_head_after: "the terminal 01-03 commit is the one carrying this file's own ledger, and a commit cannot contain its own hash. Measured span f493c9c2..HEAD = 8 commits; the terminal hash is reported in the executor's completion block."
---

# Phase 01 Plan 03: SPIKE-02 control-compatibility spike Summary

**A throwaway WinUI probe built and launched against Winhance's exact toolkit package set under WindowsAppSDK 2.3.1, closed out by a human visual render check: `all three render` — `SettingsCard`, `DataGrid` and `WrapPanel` all work on WindowsAppSDK 2.3.1 with `CommunityToolkit.WinUI`, and the probe was then deleted, leaving the document and nothing else.**

> **SPIKE-02 is RESOLVED.** Verdict: `all three render`. Phases 5 and 9 may plan against the real
> controls; no substitute is needed anywhere. The gate was open only between 2026-10-05 and the
> human render confirmation — a later phase reading a stale planning document must not conclude
> it is still open.

## Performance

- **Duration:** 32 min of automated work, plus a render gate held open across **two sessions**
- **Started:** 2026-10-05T23:20:00Z
- **Automated work completed:** 2026-10-05T23:52:00Z
- **Render gate discharged:** 2026-10-06
- **Tasks:** 3 of 3
- **Files created:** 6 (5 of them since deleted) · **Files deleted:** 5 tracked spike files
- **Commits:** 8 — 4 task commits (`a09b30e`, `5fe6147`, `ad675e6`, `34fabe2`), 3 halted-state
  records written while the gate was open, and this plan's metadata commit

## Accomplishments

- **SPIKE-02 has a verdict in one of the three permitted strings: `all three render`.**
  `SettingsCard`, `DataGrid` and `WrapPanel` all render under WindowsAppSDK 2.3.1 with
  `CommunityToolkit.WinUI`. This unblocks the two phases the spike was a hard gate for.
- **The render gate was closed by a human looking at the screen, which is the only thing D-06
  accepts.** The plan sat at `PENDING` for two sessions even though both automated gates were
  green, because a compile-only or launch-only pass is explicitly not sufficient — a
  version-skewed toolkit resolves at compile time and fails at XAML-load or theming time. The
  verdict records the confirmation as a *human visual confirmation* and does not dress it up as
  anything an agent observed.
- **The instrument is gone.** `tools/spike/WinUiControlCompat/` — csproj, `App.xaml(.cs)`,
  `MainWindow.xaml(.cs)`, plus its `bin/`, `obj/` and every build artifact — is deleted. No
  canary page, no placeholder project, no `.gitkeep`, no comment pointing at a deleted project.
  The four pinned packages' `build/` and `buildTransitive/` import surface leaves the
  repository with the deletion, which is the whole point of D-05.
- **The shipping app was never touched.** `git diff --exit-code -- AkariTool.sln src/` is clean.
  The spike had zero `ProjectReference`s and was never a solution member, so D-05's "the app's
  package list stays byte-for-byte identical" was machine-checked rather than asserted.
- **All three XAML namespaces resolved differently from what the plan predicted**, and the
  finding is recorded verbatim rather than quietly patched around.

## Task Commits

1. **Task 1: Build the throwaway spike with the pinned package set (D-05)** — `a09b30e` (feat)
2. **Task 2: Prove the spike survives launch (the automatable half of D-06)** — `5fe6147` (docs)
3. **Task 3: Record the render verdict, then delete the spike (D-06, D-07, D-05)** — `ad675e6`
   (chore — the instrument's deletion) then `34fabe2` (docs — the verdict document), split into
   two commits so the record of what was tried survives the thing that was tried not doing so.

Intervening halted-state records: `0547140`, `96189d5`, `264266c` — written while the plan was
correctly parked at the human gate.

## Files Created/Modified

- `.planning/phases/01-baseline-spikes-test-harness/01-SPIKE-02-VERDICT.md` — **the durable
  deliverable** (D-15). Verdict filled, render check recorded as human confirmation, substitutes
  section retained and marked not-needed, configuration findings C-1..C-3 untouched, downstream
  section recording the gate as resolved in Phases 4/5/9's favour.
- `tools/spike/WinUiControlCompat/**` — **deleted.** The probe: five exact-pinned package
  references, zero `ProjectReference`s, no `ApplicationManifest`, and a page carrying one
  `SettingsCard`, one `WrapPanel` with eight 88px children in a 420px strip (so wrapping is
  visually unambiguous — they *cannot* fit on one line), and one `DataGrid` with two real rows so
  a headers-only render is distinguishable from no render at all.

**Not modified:** `src/AkariTool.App/AkariTool.App.csproj`, `AkariTool.sln`, `tools/run-tests.ps1`,
`tools/baseline.json`, and every other file under `src/`.

## The render gate — how it closed, and what it cost

Task 3's render check is the obligation D-06 exists for, and **the gate had to be held open
across two sessions**.

| | |
|---|---|
| Session 1 | Built the probe, proved compile and launch, wrote the verdict skeleton with `Verdict = PENDING`, parked the plan at the gate with the window open |
| Session 2 | The spike window did not survive the session rollover. Plan halted again; the probe was relaunched |
| Gate discharge | A human looked at the open window and answered `all three render` |

This is a real operational finding and it belongs in the record: **a human render gate that
depends on a live process window is a session-boundary hazard.** The mitigations that turned out
to matter were that the instrument was *rebuildable and relaunchable* (a one-command, two-invocation
rebuild plus a `Start-Process` launch), and that the verdict document was written down to
`PENDING` *before* the gate was presented — so the outstanding question was legible to a fresh
session even though the window was not. The second session had everything it needed except a
live window.

Two consequences recorded so a later reader is not misled:

- **The PID in the verdict's automated-gates section (10320) is not the PID the human answered
  about** (13320). The gate was re-presented on a freshly launched window.
- **No per-control detail was invented.** The human answered at the whole-page level, and the
  document records each control's *required* criteria with the meeting of those criteria
  attributed to that confirmation.

## Decisions Made

- **The verdict was recorded at the granularity it was actually given.** The human said
  "all three render" about the page. Each control is named individually with what it was
  required to show, and the row is explicit that the outcome rests on the human's confirmation
  and not on any agent inspection — no agent had screen access, so claiming otherwise would be
  a fabricated observation.
- **Configuration findings stayed configuration findings.** C-1..C-3 are left exactly as Task 2
  recorded them. A `CS0234` or `WMC0001` is a fact about where a type is declared; promoting it
  into a rendering verdict would send three phases planning around a problem that does not exist.
- **No substitute was built, because none was needed.** D-07 requires a substitute to be *named
  and proved rendering on the same page*; with no failing control that obligation was never
  entered rather than discharged with a plausible guess. The heading stays in the document so
  "none required" is distinguishable from "never considered".
- **Root-cause exclusion was applied symmetrically.** Not only is there no theory for *why* a
  control would be incompatible, there is no theory for *why the controls did render* either.
  A passing result is not an invitation to explain the passing, and a post-hoc mechanism would
  be the same unbounded work pointed the other way.
- **The XAML namespaces were read, not guessed.** Each pinned package ships an XML doc file
  listing its `T:` members. Every prefix came from there, and each superseded prefix was allowed
  to fail once so the compiler would name the type it looked for. All three resolved differently
  from the plan and `01-RESEARCH.md`: the 8.2 train declares `SettingsCard` and `WrapPanel`
  **directly in the flattened namespace** `CommunityToolkit.WinUI.Controls` (the per-package
  names occur as *assembly* names, which is the trap — an `xmlns:using:` prefix must name a
  namespace), and frozen `DataGrid 7.1.2` sits in `CommunityToolkit.WinUI.UI.Controls`, not the
  legacy `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`.
- **The output-path discrepancy was recorded, not worked around.** The emitted binary is at
  `bin\x64\Debug\…\win-x64\`, because the project declares `<Platforms>x64</Platforms>` exactly as
  the plan specifies. The plan's verify block names `bin\Debug\…`. The plan's path is the wrong
  one — the same `x64`-segmented shape already recorded for `AkariTool.App` — so dropping
  `Platforms` to satisfy the assertion would have been the wrong fix.
- **`/p:WindowsAppSDKSelfContained=true` was passed on the command line, not set in the csproj**,
  and only for this project. It is a `WinExe` with zero `ProjectReference`s, so the flag cannot
  reach a class library; `AkariTool.sln` would fail all five of its libraries if it were passed
  there.

## Downstream impact

| Phase | Status now |
|---|---|
| **Phase 5 — shared row primitives** | **Hard gate CLEARED.** Plan against the real `SettingsCard`, not a substitute row. Its badge / details / banner targets are unaffected. |
| **Phase 9 — Software & Apps table view** | **Hard gate CLEARED.** Plan against the real `DataGrid` (both rows render, not just headers) and the real `WrapPanel` for any filter-chip or tag row. |
| **Phase 4 — domain file moves** | Never formally gated, but should not have assumed availability. Now explicitly unblocked. |
| **Phase 2 / plan 01-04 (SPIKE-03)** | Unaffected throughout. |

The verdict settles *compatibility*, not *fit*: nothing here says these are the best controls for
Akari's UX, only that they render.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `SizeInt32` unresolvable in the spike's code-behind** (Task 1)
- **Issue:** `MainWindow.xaml.cs` referenced `SizeInt32` with `using Microsoft.UI;` and
  `using Microsoft.UI.Windowing;`. `SizeInt32` is declared in `Windows.Graphics`, producing
  `error CS0246: The type or namespace name 'SizeInt32' could not be found`.
- **Fix:** Swapped the unused `Microsoft.UI` import for `Windows.Graphics`.
- **Verification:** Rebuild exits 0, no `error ` line. · **Committed in:** `a09b30e`

**2. [Rule 3 - Blocking] All three XAML namespaces were wrong** (Task 1)
- **Issue:** The plan predicted `CommunityToolkit.WinUI.Controls.SettingsControls`,
  `CommunityToolkit.WinUI.Controls.Primitives` and `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`.
  None exists. Three build attempts, each failing with the compiler naming the type it looked for
  (`CS0234`, then `CS0426`, then `WMC0001` × 2).
- **Fix:** Read each package's own XML doc file for its `T:` member list and used the namespaces
  actually declared. No prefix guessed, no failure suppressed.
- **Verification:** Rebuild exits 0 with no `XLSQ`/`WMC`/`MC`/`CS0234`/`CS0426`. The three verbatim
  errors are preserved in the verdict's Configuration findings section rather than discarded.
  · **Committed in:** `a09b30e`

**3. [Rule 3 - Blocking] The render gate survived a session rollover with the instrument lost** (Task 3)
- **Issue:** The plan's Task 3 depends on a live process window remaining open between the
  automated half and the human answer. The window did not survive the session boundary; the
  second session resumed to find the gate still open and the instrument not running.
- **Fix:** Relaunched the already-built probe (no rebuild or code change was needed — the
  instrument was deliberately committed to git in Task 1, which is what made relaunching a
  one-command operation) and presented the gate again.
- **Verification:** The human answered against the relaunched window. The verdict's automated
  gates section still names the original PID, and the render section states plainly that the PID
  the human answered about is not the one recorded there.
- **Committed in:** `34fabe2` (the record), no code change.

### Observed but deliberately NOT fixed

**`CS0234` on a fresh-clone solution build is a known pre-existing condition**, routed here from
plan 01-02: a solution `/t:Rebuild` never regenerates `vendor\WinUI.Framework\bin\x64\`, so a fresh
clone hits it on its first build. It did **not** occur here — this spike builds its own graph and
touches nothing under `vendor/`, and the build log contained no `PRI175`/`PRI252` at all. Both
facts are now carried into the verdict's Configuration used section, because a later reader
comparing the two build logs will otherwise read the difference as a regression. Do not "fix" it
by widening the allowlist.

---

**Total deviations:** 3 auto-fixed (1 bug, 2 blocking)
**Impact on plan:** All three are confined to the throwaway probe or to the record. None changed a
package pin, the project shape, or any shipped file.

## Issues Encountered

- **Three build failures during namespace resolution**, each informative rather than mysterious.
  The plan anticipated exactly this and directed that the compiler's error be copied verbatim
  rather than silently retried — which is what happened, and the plan's expectation that
  namespace resolution "is itself part of the finding" was correct.
- **The render gate spanned two sessions.** See the dedicated section above; the instrument was
  rebuilt-and-relaunchable by design, which is what kept this a relaunch rather than a restart.
- **No `NU1102`, `NU1605`, `NU1608` or `MSB4011`.** Restore was clean first time, and no
  dependency-family conflict arose: `Microsoft.WindowsAppSDK 1.6.250108002` in the WCT 8.2 nuspecs
  proved to be a *minimum* that 2.3.1 satisfies, confirming 01-RESEARCH.md's reading that what
  looked like the largest landmine is not one.
- **No automated proxy exists for "renders correctly", and none was invented.** Writing one —
  asserting a control type is in the visual tree, say — would pass on an invisible or mis-themed
  control, which is precisely the failure D-06 exists to catch. The gap was closed by a human, not
  by a cleverer command.
- **The spike process was already gone when this continuation started** (PID 13320 not running, no
  `WinUiControlCompat` process present). Nothing needed closing; verified by name as well as by
  PID so a PID reuse could not hide a stray instance.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

**Unblocked.** Phase 1's remaining work is plan 01-04 (SPIKE-03), which never depended on this
verdict.

- **Phase 4, Phase 5 and Phase 9 may now be planned against the real controls.** The XAML
  namespaces they must use are in the verdict's section 3, and they are **not** the ones the
  package ids suggest.
- **Nothing about Phase 5's row-primitive design or Phase 9's table design changes shape**
  because of this spike.
- **One accepted risk travels with the verdict:** `DataGrid 7.1.2` is unmaintained — last
  published 2021-11-18, six versions ever, the only DataGrid in existence. It renders; whether it
  is still maintained is a separate question this spike does not answer.
- **`tools/spike/` is gone.** Do not recreate it as a canary (D-05 explicitly rejects a permanent
  one: a later package bump would silently re-open this question). If a future package bump needs
  re-testing, rebuild the probe from the recorded configuration in the verdict.

### Self-Check: PASSED

- `tools/spike/WinUiControlCompat/` does not exist; `Test-Path tools\spike` returns False.
- `git status --porcelain -- tools/spike` reports exactly five `D` entries and nothing else.
- `git diff --exit-code -- AkariTool.sln src/` exits 0 — the shipping app was never touched.
- `git grep -i WinUiControlCompat -- src tests tools AkariTool.sln vendor` returns no match; the
  only surviving mentions are in planning documents, which describe a project that was created
  and deleted inside this plan.
- `a09b30e`, `5fe6147`, `ad675e6` and `34fabe2` are all ancestors of HEAD.
- The verdict section reads exactly `all three render`; `PENDING` appears **nowhere** in the
  document, and no section still claims to be awaiting a human.
- All three controls are named individually in the Render check section, each attributed to the
  human's visual confirmation.
- The Substitutes section is present and states that none were needed.
- No root-cause hypothesis appears anywhere — neither for a failure nor for the success. The Scope
  note states that diagnosis was deliberately not performed and that the exclusion applies in both
  directions.
- Section 3 (Configuration findings C-1..C-3) is byte-identical to what Task 2 recorded.

---

*Phase: 01-baseline-spikes-test-harness*
*Completed: 2026-10-06*