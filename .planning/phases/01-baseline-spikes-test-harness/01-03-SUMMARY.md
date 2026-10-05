---
phase: 01-baseline-spikes-test-harness
plan: 03
subsystem: ui
tags: [winui3, windowsappsdk, communitytoolkit, spike, control-compatibility, msbuild]

requires:
  - phase: 01-baseline-spikes-test-harness
    provides: "plan 01-01 — tools/run-tests.ps1 and its Resolve-VSToolchain vswhere discovery, reused here as the single MSBuild source of truth"
provides:
  - "tools/spike/WinUiControlCompat/ — a clean-room WinUI probe built against the five exact-pinned packages, and still on screen awaiting the human render check"
  - ".planning/phases/01-baseline-spikes-test-harness/01-SPIKE-02-VERDICT.md — the durable finding Phases 4, 5 and 9 plan against, opened with both automated gates green and Verdict = PENDING"
  - "Three verbatim configuration findings (CS0234 / CS0426 / WMC0001) recording that all three XAML namespaces resolved differently from what the plan and 01-RESEARCH.md predicted"
  - "A proven launch gate: the spike launches, survives 5s, and presents a visible un-minimized responding window"
affects: [04-domain-file-moves, 05-shared-row-primitives, 09-software-apps-table-view, phase-09-table-view]

actuals:
  tokens: 8700
  tasks: 2
  commits: 2

tech-stack:
  added:
    - "CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219 (spike-only, deleted in Task 3)"
    - "CommunityToolkit.WinUI.Controls.Primitives 8.2.251219 (spike-only, deleted in Task 3)"
    - "CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2 (spike-only, deleted in Task 3)"
    - "Microsoft.Windows.CsWinRT 2.3.1 (spike-only, deleted in Task 3)"
  patterns:
    - "Throwaway spike with zero ProjectReferences and no solution membership, so D-05's 'app package list stays byte-for-byte identical' is machine-checkable rather than asserted"
    - "Restore and Build as two separate MSBuild invocations — a combined /t:Restore,Rebuild breaks WinUI XAML codegen"

key-files:
  created:
    - "tools/spike/WinUiControlCompat/WinUiControlCompat.csproj — throwaway probe; its comments are the only record that survives deletion"
    - "tools/spike/WinUiControlCompat/MainWindow.xaml — the single render-checkable page carrying all three controls"
    - "tools/spike/WinUiControlCompat/MainWindow.xaml.cs — window sizing and the DataGrid's two rows; no DI, no ServiceLocator"
    - "tools/spike/WinUiControlCompat/App.xaml, App.xaml.cs — minimal entry point; XamlControlsResources only"
    - ".planning/phases/01-baseline-spikes-test-harness/01-SPIKE-02-VERDICT.md — the durable deliverable (D-15)"
  modified: []

key-decisions:
  - "Every XAML namespace prefix was read out of each pinned package's own XML doc file (its T: member list) rather than guessed, and each superseded prefix was allowed to fail once so the compiler would name the type it looked for. All three controls resolved DIFFERENTLY from the plan's prediction, and the verbatim errors are recorded as configuration findings C-1..C-3 — namespace-resolution facts, explicitly not rendering verdicts (D-07)."
  - "The rendered-output path carries an x64 segment (bin\\x64\\Debug\\<tfm>\\win-x64\\). The plan's verify block names bin\\Debug\\… without it. The project declares Platforms=x64 as the plan itself specifies, so the plan's path is the wrong one, not the build. Recorded rather than worked around by dropping Platforms."
  - "The launch-survival gate launched the .exe directly with Start-Process rather than dotnet run, because dotnet run does not surface WinUI XAML-load failures the way a direct launch does — and D-06 requires observing a real launch."

patterns-established:
  - "Configuration failure and rendering verdict are kept in separate document sections, each finding explicitly labelled, so a namespace or restore error can never be read as evidence about rendering."
  - "Window-liveness is proven with IsWindowVisible / IsIconic / rect bounds alongside HasExited — a process that is alive has not necessarily put a usable window on screen."

requirements-completed: []

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
    description: "THE SPIKE-02 VERDICT ITSELF: SettingsCard, DataGrid and WrapPanel are visible and correctly themed under WindowsAppSDK 2.3.1, or a failing control has a substitute observed rendering on the same page."
    requirement: SPIKE-02
    verification: []
    human_judgment: true
    rationale: "D-06 sets the bar at compile PLUS launch PLUS visual render and explicitly rejects compile-only, because a version-skewed toolkit resolves at compile time and fails at XAML-load or theming time. No command distinguishes 'renders correctly' from 'instantiated but invisible or mis-themed' — an assertion that the control type is in the visual tree passes on an invisible or mis-themed control, which is the exact failure D-06 exists to catch. No automated proxy was invented, because inventing one is worse than the gap. The probe window is OPEN on screen awaiting the human. Recorded as manual-only in 01-VALIDATION.md's Manual-Only Verifications table."
  - id: D5
    description: "The spike project is deleted from the working tree, leaving no canary page, placeholder, .gitkeep or dangling reference behind (D-05)."
    requirement: SPIKE-02
    verification: []
    human_judgment: false
    rationale: "Deliberately NOT performed in this run. Task 3's deletion is sequenced AFTER the render check, and deleting the project while its window is still on screen would destroy the very instrument the human is looking at. This deliverable is BLOCKED on D4, not on anything the human must judge."

duration: 32min
completed: 2026-10-05
status: halted
commits: 2
plan_head_before: f493c9c2b75c5fc63a8bdd3ed08caa0ced6c1d40
plan_head_after: 5fe614770a6e71704f41474d1c9b5a39cc37aaf0
---

# Phase 01 Plan 03: SPIKE-02 control-compatibility spike Summary

**A throwaway WinUI probe built and launched against Winhance's exact toolkit package set under WindowsAppSDK 2.3.1 — proving the compile and launch gates, recording three verbatim namespace-configuration findings, and leaving the window OPEN on screen because the render check is the one thing no command can answer.**

> **This plan is HALTED at Task 3's human gate, deliberately.** Task 3 cannot be closed by an
> agent. The spike window is on screen right now, waiting. D-06 requires compile **plus**
> launch **plus** visual render and explicitly rejects compile-only; the automated gates here
> are green and are still **not sufficient**. Closing SPIKE-02 on them would silently violate
> D-06, so the Verdict section reads `PENDING` and the instrument is deliberately **not**
> deleted yet.

## Performance

- **Duration:** 32 min
- **Started:** 2026-10-05T23:20:00Z
- **Completed:** 2026-10-05T23:52:00Z
- **Tasks:** 2 of 3 complete (Task 3 halted at its human gate)
- **Files created:** 6
- **Commits:** 2

## Accomplishments

- **The probe builds clean.** `/t:Restore` then `/t:Rebuild` — two separate invocations, because
  a combined `/t:Restore,Rebuild` breaks WinUI XAML codegen — both exit 0 with no `error ` line and
  no `XLSQ`/`WMC`/`MC`. MSBuild was discovered through the `vswhere` logic reused from
  `tools/run-tests.ps1`, not a third hardcoded copy of it.
- **`AkariTool.App.csproj` and `AkariTool.sln` are byte-for-byte unchanged.** Verified by
  `git diff --exit-code` at every step. The spike has zero `ProjectReference`s and was never
  added to the solution, so D-05 is machine-checked rather than merely asserted.
- **The launch gate is proven with a real window, not just a live process.** `PASS: spike still
  running after 5s`, plus `IsWindowVisible=True`, `IsIconic=False`, a 720×640 rect on a
  3640×1920 screen, and `Responding=True`. Launched directly via `Start-Process`, never
  `dotnet run`, which would not surface a XAML-load failure the same way.
- **All three XAML namespaces turned out to be different from what the plan predicted**, and the
  finding is recorded verbatim rather than quietly patched around. This is the plan's own stated
  expectation about namespace resolution, and it paid off.
- **The verdict document exists with all eight sections**, `Verdict: PENDING`, and a bold
  necessary-but-not-sufficient statement on the automated gates.

## Task Commits

1. **Task 1: Build the throwaway spike with the pinned package set (D-05)** — `a09b30e` (feat)
2. **Task 2: Prove the spike survives launch (the automatable half of D-06)** — `5fe6147` (docs)

Task 3 is **not committed** — it is halted mid-task at the human gate, and its work is
correctly *not* done (see below).

## Files Created/Modified

- `tools/spike/WinUiControlCompat/WinUiControlCompat.csproj` — the probe. Five exact pins, zero
  `ProjectReference`s, no `ApplicationManifest`. Its comments are the only record that survives
  Task 3's deletion, so every non-obvious choice is annotated in place.
- `tools/spike/WinUiControlCompat/MainWindow.xaml` — the single render-checkable page: one
  `SettingsCard`, one `WrapPanel` with eight 88px children in a 420px strip (so wrapping is
  visually unambiguous — they *cannot* fit on one line), one `DataGrid` with two real rows so a
  headers-only render is distinguishable from no render at all.
- `tools/spike/WinUiControlCompat/MainWindow.xaml.cs` — window sizing and the DataGrid's two rows.
  No DI, no service registration, no `ServiceLocator`: a clean-room render is the whole point.
- `tools/spike/WinUiControlCompat/App.xaml`, `App.xaml.cs` — minimal entry point,
  `XamlControlsResources` and nothing else.
- `.planning/phases/01-baseline-spikes-test-harness/01-SPIKE-02-VERDICT.md` — the durable
  deliverable per D-15.

**Not modified:** `src/AkariTool.App/AkariTool.App.csproj`, `AkariTool.sln`, `tools/run-tests.ps1`,
and every other file under `src/`.

## The human gate — Task 3 is open, and the window is on screen

**A `WinUiControlCompat` window titled "SPIKE-02 - WinUI control compatibility probe" is open
right now** (PID 10320, visible, un-minimized, responding). It is deliberately **not** closed and
the spike project is deliberately **not** deleted.

Three things must be looked at, individually:

| Control | Look for | If it fails |
|---|---|---|
| `SettingsCard` | A visible bordered card showing header *"SPIKE-02 SettingsCard"*, its description, and body text *"SettingsCard body content"* — themed, not raw/default | record the symptom |
| `WrapPanel` | **Eight** coloured boxes inside a 420px strip. Eight 88px boxes cannot fit on one line — if they appear on a single line, the control did not lay out | record the symptom |
| `DataGrid` | Headers *Name*/*Value* **plus two painted rows** (`row-one`/`alpha`, `row-two`/`beta`) | headers-only is a distinct symptom from nothing |

Then each control is recorded as **visible-and-themed**, or as a named failure. For any failure, a
substitute must be named **and proved to render on this same page** before the verdict is final
(D-07) — added to `MainWindow.xaml`, rebuilt with the same two-pass invocation, re-launched, and
looked at again.

## Decisions Made

- **XAML namespaces were read, not guessed.** Each pinned package ships an XML doc file listing its
  `T:` members. Every prefix came from there, and each superseded prefix was allowed to fail once so
  the compiler would name the type it looked for. All three resolved differently from the plan and
  `01-RESEARCH.md`: the 8.2 train declares `SettingsCard` and `WrapPanel` **directly in the flattened
  namespace** `CommunityToolkit.WinUI.Controls` (the per-package names occur as *assembly* names, which
  is the trap — an `xmlns:using:` prefix must name a namespace), and frozen `DataGrid 7.1.2` sits in
  `CommunityToolkit.WinUI.UI.Controls`, not the legacy `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`.
  Recorded as configuration findings C-1..C-3 with the compiler's verbatim text.
- **The output-path discrepancy was recorded, not worked around.** The emitted binary is at
  `bin\x64\Debug\…\win-x64\`, because the project declares `<Platforms>x64</Platforms>` exactly as the
  plan specifies. The plan's verify block names `bin\Debug\…`. The plan's path is the wrong one — the
  same `x64`-segmented shape STATE.md already records for `AkariTool.App` — so dropping `Platforms`
  to satisfy the assertion would have been the wrong fix.
- **`/p:WindowsAppSDKSelfContained=true` was passed on the command line, not set in the csproj**,
  and only for this project. It is a `WinExe` with zero `ProjectReference`s, so the flag cannot reach a
  class library; `AkariTool.sln` would fail all five of its libraries if it were passed there.
- **`EnableMsixTooling` is `false` on the probe** (the app sets `true`), so the spike's own log carries
  no spurious "packaged multiple executables" warning that could be misread as a finding.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `SizeInt32` unresolvable in the spike's code-behind**
- **Found during:** Task 1
- **Issue:** `MainWindow.xaml.cs` referenced `SizeInt32` with `using Microsoft.UI;` and
  `using Microsoft.UI.Windowing;`. `SizeInt32` is declared in `Windows.Graphics`, producing
  `error CS0246: The type or namespace name 'SizeInt32' could not be found`.
- **Fix:** Swapped the unused `Microsoft.UI` import for `Windows.Graphics`.
- **Files modified:** `tools/spike/WinUiControlCompat/MainWindow.xaml.cs`
- **Verification:** Rebuild exits 0, no `error ` line.
- **Committed in:** `a09b30e`

**2. [Rule 3 - Blocking] All three XAML namespaces were wrong**
- **Found during:** Task 1
- **Issue:** The plan predicted `CommunityToolkit.WinUI.Controls.SettingsControls`,
  `CommunityToolkit.WinUI.Controls.Primitives` and `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid`.
  None exists. Three build attempts, each failing with the compiler naming the type it looked for
  (`CS0234`, then `CS0426`, then `WMC0001` × 2).
- **Fix:** Read each package's own XML doc file for its `T:` member list and used the namespaces
  actually declared. No prefix was guessed and no failure was suppressed.
- **Files modified:** `tools/spike/WinUiControlCompat/MainWindow.xaml`
- **Verification:** Rebuild exits 0 with no `XLSQ`/`WMC`/`MC`/`CS0234`/`CS0426`. The three verbatim
  errors are preserved in the verdict's Configuration findings section rather than discarded.
- **Committed in:** `a09b30e`

### Observed but deliberately NOT fixed

**`CS0234` on a fresh-clone solution build is a known pre-existing condition**, routed here from
plan 01-02: a solution `/t:Rebuild` never regenerates `vendor\WinUI.Framework\bin\x64\`, so a fresh
clone hits it on its first build. It did **not** occur here — this spike builds its own graph and
touches nothing under `vendor/`, and the build log contained no `PRI175`/`PRI252` at all. Recorded
because it remains true and must not later be mistaken for a regression.

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both are mechanical and confined to the throwaway probe. Neither changed a
package pin, the project shape, or any shipped file.

## Issues Encountered

- **Three build failures during namespace resolution**, each informative rather than mysterious. The
  plan anticipated exactly this and directed that the compiler's error be copied verbatim rather than
  silently retried — which is what happened, and the plan's expectation that namespace resolution
  "is itself part of the finding" was correct.
- **No `NU1102`, `NU1605`, `NU1608` or `MSB4011`.** Restore was clean first time, and no
  dependency-family conflict arose: `Microsoft.WindowsAppSDK 1.6.250108002` in the WCT 8.2 nuspecs
  proved to be a *minimum* that 2.3.1 satisfies, confirming 01-RESEARCH.md's reading that what looked
  like the largest landmine is not one.
- **No automated proxy exists for "renders correctly", and none was invented.** Writing one — asserting
  a control type is in the visual tree, say — would pass on an invisible or mis-themed control, which
  is precisely the failure D-06 exists to catch. The gap is left open on purpose.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

**Blocked, and correctly so.** SPIKE-02 is a hard gate on Phase 5 and Phase 9, and neither can be
planned against an unwritten verdict. Nothing downstream should treat the two green automated gates
as the finding.

**To finish this plan, a human must:**

1. Look at the open `SPIKE-02` window and record each of the three controls as visible-and-themed or
   as a named failure with its symptom (table above).
2. If any control failed, add its named substitute to `MainWindow.xaml`, rebuild with the same
   two-pass invocation, re-launch, and confirm the substitute renders on that same page.
3. Fill the Verdict, Render check and Substitutes sections of `01-SPIKE-02-VERDICT.md` with exactly
   one of the three permitted verdict strings.
4. Close the window, delete `tools/spike/WinUiControlCompat/`, and confirm
   `git status --porcelain -- tools/spike` is empty and `git diff --exit-code -- AkariTool.sln src/`
   is clean.
5. Commit the verdict document in its own commit **after** the deletion, so the record of what was
   tried survives even though the thing that was tried does not.

**Do not widen the build-gate allowlist** to accommodate anything here, and do not record a
configuration failure as a rendering verdict.

### Self-Check: PASSED

- Five spike files exist on disk; `01-SPIKE-02-VERDICT.md` exists.
- `a09b30e` and `5fe6147` are both ancestors of HEAD.
- `git diff --exit-code -- AkariTool.sln src/` clean — the shipping app was never touched.
- Verdict document has all eight sections; `PENDING` appears exactly once, in the Verdict section.
- No root-cause hypothesis appears anywhere in the verdict — the Scope note states that diagnosis was
  deliberately not performed, and no mechanism, version-gap or reorganisation theory is offered.
- The spike window is confirmed open, visible, and responding.

---

*Phase: 01-baseline-spikes-test-harness*
*Completed: 2026-10-05 (halted at Task 3's human gate — SPIKE-02 verdict still PENDING)*
