# Phase 1: Baseline, Spikes & Test Harness - Context

**Gathered:** 2026-10-05
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase delivers **measurement instruments, not product capability**. Nothing produced here is
referenced by production code, so the app stays byte-for-byte unaffected and keeps working.

Four instruments:

1. **SPIKE-01** — a recorded build baseline: assemblies emitted, warning and error counts, test count.
2. **SPIKE-02** — a spike establishing whether `SettingsCard`, `DataGrid` and `WrapPanel` render under
   WindowsAppSDK 2.3.1 with `CommunityToolkit.WinUI`. A **hard gate** for Phases 5 and 9.
3. **SPIKE-03** — a generated report diffing every Akari setting ID against Winhance's. The evidence
   base for the deliberate config-format fork (D6).
4. **TEST-01** — an `AkariTool.App.Tests` project in the solution that builds and runs passing tests
   against App-layer logic.

Plus the mechanism that makes all of it checkable: a committed test-runner and baseline-comparison
script, which every later phase's Build Gate cites.

**Out of scope:** catalog content expansion, `.winhance` import, fixing PRI175/PRI252, and any real
App-layer test coverage beyond the single characterization test named below.

</domain>

<decisions>
## Implementation Decisions

### App-layer test project (TEST-01)

- **D-01:** `AkariTool.App.Tests` is wired by **referencing `AkariTool.App` directly** — the test
  project sets `UseWinUI=true` and ProjectReferences `AkariTool.App`, consuming the DLL that already
  builds successfully today. It tests only App logic that needs **no live `DispatcherQueue` and no
  XAML visual tree**. Chosen over compile-linking App sources (which breaks every code-behind with
  `InitializeComponent` and turns the file list into a hand-maintained exclusion set) and over
  pulling App logic down into Core/Infrastructure (which belongs to Phase 3/4's namespace alignment).
  — **Reversibility:** costly — switching to compile-link or an extract-first strategy means
  re-authoring the project's references and, in the extract case, physically moving App code ahead of
  the Phase 3/4 namespace-alignment turn it is scheduled for.
- **D-02:** The Phase 1 test target is **`SettingBadgeCalculator.Compute`**
  (`src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs`). It is pure, static, no I/O and no
  side effects — it takes a snapshot of a row's values and returns the
  Recommended/Default/Custom/Preference pills. It also **doubles as the characterization test** that
  guards Phase 5's CORE-04 shared badge-primitive extraction from changing behaviour. Preferred over
  `UnattendTweakCatalog` (data assertions, not behaviour), over `SettingBackupService` (takes
  `IEnumerable<SettingPageViewModel>`, which cannot be constructed without a live container — the
  `ServiceLocator` problem CONCERNS #9 records), and over a deliberately trivial test.
- **D-03:** Tests are built and run by a **committed wrapper script in `tools/`**: MSBuild builds the
  solution, then `vstest.console.exe` runs the produced test assemblies. This honours the standing
  "VS MSBuild only" constraint (plain `dotnet build` fails on WinUI 3 PRI/resource targets) while
  giving every later phase **one command**. The same script is the runner the SPIKE-01 baseline gate
  uses. — **Reversibility:** costly — all nine later phases' Build Gate lines cite this script by
  path, so relocating or renaming it invalidates documented gates across the roadmap.
- **D-04:** `AkariTool.App.Tests`' Phase 1 remit is **minimal — `SettingBadgeCalculatorTests` only**.
  Deliberately *not* in Phase 1: the real-catalog validity test (Phase 2 / BUG-02), the container
  resolution integration test (Phase 3 / TEST-02), and the Core-references-neither-layer test
  (Phase 3 / TEST-03). Keeps Phase 1 to its four requirements and keeps the recorded baseline test
  count stable for the nine phases that follow.

### SPIKE-02 — control compatibility spike

- **D-05:** The spike lives in a **throwaway project inside the repo, deleted once the verdict is
  written**. `AkariTool.App.csproj`'s package list stays byte-for-byte identical, so a failing
  package set cannot contaminate the app build. Chosen over a scratch XAML page inside
  `AkariTool.App` (a failing package set breaks the whole app build until reverted) and over keeping
  a permanent control-compat canary page (a later package bump would silently re-open this question).
- **D-06:** The bar for "all three render" is **compile against WindowsAppSDK 2.3.1 + launch +
  visually render all three controls**. A compile-only pass is explicitly *not* sufficient — the
  requirement says the controls "render correctly", and a version-skewed toolkit typically resolves at
  compile time and fails at XAML-load or theming time, which is the case a compile-only check misses.
- **D-07:** For **any** control that fails, the verdict names a **concrete substitute and proves that
  substitute renders in the same spike page** (a render-checkable XAML snippet). Phases 5 and 9 then
  plan against an observed substitute rather than a guess. Root-cause diagnosis of *why* a control
  is incompatible is **not** in scope — it risks turning a bounded spike into an open-ended
  compatibility project.

**Known landmines the spike must test against** (from `Winhance.UI.csproj` comments, `v26.06.12` /
Release 27) — these are recorded so the spike does not have to rediscover them:

| Landmine | Winhance's value | Akari's value |
|---|---|---|
| WinUI Toolkit train | `CommunityToolkit.WinUI.* 8.2.251219`, floors at WinAppSDK 1.6+ | none |
| CsWinRT (required for WCT 8.2+) | `Microsoft.Windows.CsWinRT 2.2.0` | `2.0.4` |
| `DataGrid` | `CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2` — WCT 8.x never ported it | none |
| WindowsAppSDK package | **metapackage** `Microsoft.WindowsAppSDK 1.8.260416003`; the component package pulls both families and breaks the build with MSB4011 | metapackage `2.3.1` ✓ |
| SDK BuildTools | `Microsoft.Windows.SDK.BuildTools 10.0.26100.4654` | absent |
| `WrapPanel` | ships in `CommunityToolkit.WinUI.Controls.Primitives 8.2.251219` | none |
| `SettingsCard` | ships in `CommunityToolkit.WinUI.Controls.SettingsControls 8.2.251219` | none |

### SPIKE-03 — setting-ID divergence report

- **D-08:** The Winhance side comes from a **committed snapshot of Winhance setting IDs**, generated
  once by a committed script that reads the Winhance checkout at
  `C:\Users\isleap\Documents\GitHub\Winhance` (read-only — never modified). The report reads the
  snapshot, so it runs anywhere with no dependency on a path outside this repo and cannot silently
  change when Winhance is updated.
- **D-09:** The Akari side is obtained by **reflecting over the 11 real catalog `Build()` methods** and
  reading each `SettingDefinition.Id`. The Akari side therefore cannot drift from the catalog, and
  this becomes the enumeration seam Phase 2's BUG-02 uniqueness test reuses. Chosen over a hand-written
  catalog list (a second place to update — the exact failure mode this milestone exists to prevent) and
  over text-parsing both sides (regex over 3,000-line catalogs is fragile to formatting and misses
  computed or templated IDs).
- **D-10:** The report **measures and reports only — it asserts nothing about the divergence**. No gate.
  The known deltas are deliberate content differences (Optimize ahead by 13, Customize behind by 9),
  and catalog content expansion is Out of Scope, so a gate would fail on intent rather than on defect.
- **D-11:** The output is a **committed generator writing a committed markdown report plus the
  machine-readable ID sets**. Re-runnable at any time, human-readable as the evidence base for the D6
  fork decision, and git-diffable so the evidence evolves as the catalog changes. Preferred over a
  test-only variant (the D6 evidence would live in a test nobody reads) and over adding an
  up-to-dateness assertion (which fails whenever the catalog legitimately changes).

### SPIKE-01 — build baseline

- **D-12:** The baseline is **machine-checked, not merely recorded.** `tools/run-tests.ps1` gains a
  baseline-compare mode that forces a rebuild, captures the MSBuild warning/error summary and the
  VSTest test count, compares them against a machine-readable baseline, and **exits non-zero on any
  increase**. Every later phase's "baseline bar" Build Gate therefore becomes one command instead of a
  judgement call. Preferred over a document-only baseline (each phase re-derives the judgement by eye)
  and over mechanising only the test count (leaves warning counting to review, which is the part that
  actually catches regressions).
- **D-13:** The baseline gate **always runs `/t:Rebuild`.** MSBuild only re-emits warnings for files it
  actually recompiles, so an incremental build under-reports and the gate would pass on stale numbers —
  a false green is worse than no gate.
- **D-14:** Gate tolerance is **"no increase, solution-wide."** Reductions pass silently; only an
  increase fails. This matches ROADMAP's exact wording ("no new warnings beyond the counts recorded in
  Phase 1") and Phase 2's expected deletions of dead code. Explicitly *not* exact-match-and-rebaseline
  (which turns a warning reduction into extra work) and *not* per-project (which adds baseline
  maintenance cost without changing what the gate catches).
- **D-15:** **Generator and runner code goes in a new repo-root `tools/`.** The baseline document, the
  SPIKE-02 verdict, and the SPIKE-03 report live in
  `.planning/phases/01-baseline-spikes-test-harness/`. Reason: `tools/` code is durable and
  unmistakably code, whereas GSD archives phase directories on milestone close — which would take the
  runner script with it while nine later phases still depend on it. (`docs/` was rejected as the
  artifact home: it is the GitHub Pages website, not a documentation folder.)
- **D-16:** *(carried from ROADMAP.md — not a new choice)* The baseline gate must tolerate **exactly**
  the `WINAPPSDKGENERATEPROJECTPRIFILE` **PRI175 / PRI252** error and nothing else, via an explicit
  allowlist. Those errors are pre-existing (verified 2026-10-05) and Out of Scope. The allowlist must
  never be widened to excuse any other compile error hiding behind them.

### the agent's Discretion

The user did not constrain these; the agent should pick and document a choice:

- The internal shape of `tools/` — file names, and whether the test runner, the baseline gate and the
  ID-diff generator are one script or several.
- The machine-readable baseline file's format (JSON vs. simple `key=value`) and which fields it holds
  beyond warnings, errors and test count — e.g. the emitted-assembly list and a per-project breakdown.
  (D-14 rules out *gating* per project; it does not forbid *recording* it.)
- Whether `AkariTool.App.Tests` needs `InternalsVisibleTo` — `SettingBadgeCalculator` is already
  `public`, so it probably does not.
- Whether the throwaway spike project is added to `AkariTool.sln` (it must not be left in the solution
  after deletion), and whether the baseline run interacts with `vendor/WinUI.Framework` being absent
  from the solution (CONCERNS #7).
- How the ID report categorises divergence kinds (present-in-both / Akari-only / Winhance-only, or a
  finer split such as prefix-related renames).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and locked requirements
- `.planning/ROADMAP.md` — Phase 1 entry (goal, 4 success criteria, Build Gate, rationale); the
  "What counts as a passing build" section defining the baseline bar and the tolerated PRI175/PRI252
  error; the "Carried uncertainty" table naming SPIKE-02 a hard gate and SPIKE-03 the D6 evidence base.
- `.planning/REQUIREMENTS.md` — SPIKE-01, SPIKE-02, SPIKE-03, TEST-01 definitions; the Out of Scope
  table (catalog content expansion, PRI fix, `.winhance` import).
- `.planning/PROJECT.md` — Constraints section: VS-MSBuild-only build, WinAppSDK 2.3.1 retained,
  clean-room parity, elevation, and the unverified 2.3.1 + `CommunityToolkit.WinUI` compatibility that
  SPIKE-02 exists to answer.
- `.planning/STATE.md` — Build definition and the Blockers/Concerns list (SPIKE-02 gates Phases 5 and
  9; SPIKE-03 underwrites D6; `ElevationService` thread-affinity warning).

### Codebase maps
- `.planning/codebase/CONCERNS.md` — §16 (no App test project; no test asserts the real catalogs are
  valid; the enumerated fix ordering), §9 (why ViewModels cannot be constructed without a live
  container — the constraint on D-02), §8 (the unwired `SettingCatalogValidator`), §7
  (`vendor/WinUI.Framework` absent from `AkariTool.sln`), §12 (the 11 catalog-only Core domains and
  the enumeration seam), §14 (dead package references in Core).
- `.planning/codebase/TESTING.md` — existing test conventions: xUnit 2.9.3, FluentAssertions 8.7.1,
  NSubstitute 5.3.0, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit.runner.visualstudio` 3.1.5;
  `{TypeUnderTest}Tests.cs` naming; `{Method}_{Condition}_{Expected}` method naming; `.Tests`
  namespace suffix; mirrored `tests/` structure. **Note the documented `dotnet test` run command
  conflicts with the VS-MSBuild-only constraint — D-03 resolves this in favour of MSBuild + VSTest.**
- `.planning/codebase/CONVENTIONS.md` — one public type per file, filename == type name, Allman
  braces, 4-space indent, `// ── Section ──` banners.

### Winhance reference (read-only — never modified, never copied)
- `C:\Users\isleap\Documents\GitHub\Winhance\src\Winhance.UI\Winhance.UI.csproj` — **the SPIKE-02
  landmine list in comments** (metapackage mandate + MSB4011, CsWinRT 2.2.0 for WCT 8.2+, DataGrid
  pinned at 7.1.2, `WrapPanel` in Primitives, `SettingsCard` in SettingsControls).
- `C:\Users\isleap\Documents\GitHub\Winhance\src\Winhance.Core\Features\Optimize\Models\GamingAndPerformanceOptimizations.cs` —
  setting IDs are string literals (`Id = "gaming-game-mode"`); note catalogs live in `Models/`, not
  `Catalogs/`, and this one file is the ~4,151-line single-file catalog ROADMAP flags as an anti-pattern.
- `C:\Users\isleap\Documents\GitHub\Winhance\src\Winhance.Core\Features\` — the five domains
  (`AdvancedTools`, `Common`, `Customize`, `Optimize`, `SoftwareApps`) the SPIKE-03 snapshot must cover.
- `.planning/reference/winhance/ARCHITECTURE.md`, `.planning/reference/winhance/CONVENTIONS.md`,
  `.planning/reference/winhance/FEATURES.md`, `.planning/reference/winhance/STACK.md`,
  `.planning/reference/winhance/STRUCTURE.md` — the existing 5-document reference map.
- `.planning/research/PARITY.md` — the 1,045-line parity analysis; its per-domain counts are the
  reconciliation target for the SPIKE-03 report.

### Code this phase touches or depends on
- `src/AkariTool.App/AkariTool.App.csproj` — must stay byte-identical in its package list (D-05).
  `WinExe`, `UseWinUI=true`, `WindowsAppSDKSelfContained=true`, `RuntimeIdentifier=win-x64`,
  `Platforms=x64`, `TargetPlatformMinVersion=10.0.17763.0`.
- `src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs` — the Phase 1 test target (D-02).
- `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs` — exists, zero `src/`
  call sites; reusable for ID uniqueness, not a Phase 1 deliverable.
- `src/AkariTool.Core/AkariTool.Core.csproj` — `InternalsVisibleTo AkariTool.Core.Tests` precedent for
  the internals-visible question in agent discretion.
- `tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` and
  `tests/AkariTool.Core.Tests/AkariTool.Core.Tests.csproj` — the two csproj shapes to mirror.
- `AkariTool.sln` — 5 real projects + 2 solution folders; `vendor/WinUI.Framework` is **not** among
  them.
- `src/AkariTool.App/Services/SettingPageWarmUp.cs` — enumerates all `SettingPageViewModel`s and calls
  `Build()`; the runtime enumeration precedent behind D-09.
- `src/AkariTool.App/Services/SettingBackupService.cs` — the 658-line untested App class D-02
  deliberately did *not* choose, and why (needs a live container).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`SettingBadgeCalculator.Compute`** — pure, static, no I/O, no side effects, already public in App.
  The one App-layer target that is testable today without a container or a UI thread (D-02).
- **`SettingCatalogValidator.Validate(IEnumerable<SettingGroup>)`** — already enforces globally unique
  setting Ids, registry-path shape, and ComboBox value-mapping integrity. Zero `src/` call sites;
  Phase 2's BUG-02 wires it. Relevant to Phase 1 only as the place uniqueness is *already* checked.
- **`SettingPageWarmUp.Run(IServiceProvider, ILogService)`** — enumerates every
  `SettingPageViewModel` from the container and calls `page.Build()`. The existing runtime-enumeration
  precedent behind D-09's reflection approach.
- **`CompatibleSettingsRegistry.GetKnownFeatureProviders()`** — the single, explicit,
  reflection-free, per-feature-degrading catalog aggregation dictionary. The anchor the restructure
  must not move (CORE-06) and a second candidate for the SPIKE-03 enumeration.
- **11 `public static Build()` catalog methods** in Core — statically enumerable, which is what makes
  D-09's reflection practical.
- **Two existing test projects** to mirror: 24-line csproj, 5 package references, `IsTestProject`,
  `.Tests` namespace suffix, mirrored `tests/` folder structure.
- **`AkariTool.Core.csproj`'s `InternalsVisibleTo`** — the precedent if the App test project needs to
  reach non-public types.

### Established Patterns
- **xUnit + FluentAssertions + NSubstitute**, `{Method}_{Condition}_{Expected}` method naming,
  private static factory helpers at the top of each test class (`Setting()`, `MakeExecutor()`), no
  teardown, no separate fixture classes, no coverage gates.
- **`sealed record` models with `init`**, `InputType` enum, `BadgePillState` — the vocabulary the
  SPIKE-03 report and the badge test both speak.
- **Long architectural doc comments** are the house style; the codebase already annotates Winhance
  parity inline, and that is where the SPIKE-02 landmine notes belong.
- **VS MSBuild only** — `AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`. Plain
  `dotnet build` fails on WinUI 3 PRI/resource targets, so D-03 routes everything through MSBuild.

### Integration Points
- **`AkariTool.sln`** — where `AkariTool.App.Tests` must be added (alongside the existing two test
  projects and the `tests` solution folder). The throwaway SPIKE-02 project may join temporarily but
  must not survive deletion in the solution file.
- **`tools/`** — new repo-root directory created by D-15, holding the test runner and baseline gate
  and the SPIKE-03 generator.
- **`.planning/phases/01-baseline-spikes-test-harness/`** — where the baseline document, the SPIKE-02
  verdict and the SPIKE-03 report are written.
- **MSBuild at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`**
  and `vstest.console.exe` from the VS install — the two executables `tools/run-tests.ps1` invokes.
- **Winhance checkout at `C:\Users\isleap\Documents\GitHub\Winhance`** — read-only input to the
  SPIKE-03 regeneration script, never modified.

</code_context>

<specifics>
## Specific Ideas

- **The badge test is a Phase 5 instrument, not just a Phase 1 deliverable.** `SettingBadgeCalculator`
  is the pure logic Phase 5's CORE-04 work extracts into a shared row primitive. Characterising it now
  means Phase 5 can prove the extraction preserved behaviour. Whoever plans Phase 5 should be told
  this test exists.
- **The incremental-warning trap is the reason D-13 exists.** A baseline gate built on an incremental
  MSBuild invocation reports fewer warnings than actually exist, because MSBuild only re-reports
  warnings for files it recompiles. Any verification machinery in this milestone must force a rebuild
  or it produces false greens.
- **SPIKE-02's landmine list is already written down** — Winhance's own csproj comments record
  exactly which package versions and which architectural choices (metapackage over component package,
  CsWinRT 2.2.0, DataGrid frozen at 7.1.2) its toolkit usage depends on. The spike should start from
  that list rather than discovering the constraints by trial.
- **Phase 1 is the only phase allowed to change the definition of "did it build" without
  consequence** (ROADMAP's own words). That is the licence for D-12's machine-checked gate, and also
  the reason Phase 1 must not quietly absorb later phases' test obligations (D-04).

</specifics>

<deferred>
## Deferred Ideas

- **Real-catalog validity test** (enumerate all 11 `Build()` methods through
  `SettingCatalogValidator`) — considered and explicitly deferred to **Phase 2 / BUG-02**. It is
  CONCERNS #16's top recommendation and every later Build Gate names it, but landing it in Phase 1
  would both move another phase's requirement and destabilise the recorded baseline test count.
- **Container-resolution integration test** — **Phase 3 / TEST-02**. Blocked until Phase 3 creates the
  composition root and removes `ServiceLocator`; there is nothing to resolve before then.
- **Core-references-neither-layer assembly test** — **Phase 3 / TEST-03**. Cheap, but assigned to
  Phase 3 in the traceability table and not this phase's requirement.
- **Root-cause diagnosis of a SPIKE-02 incompatibility** — if the controls fail, Phase 1 records the
  substitute and moves on. Diagnosing whether the cause is the WinAppSDK version, the
  CsWinRT 2.0.4-vs-2.2.0 gap, or the WCT 8.x reorg is not this phase's job; Phases 5 and 9 plan
  against the substitutes.
- **Adding `vendor/WinUI.Framework` to `AkariTool.sln`** (CONCERNS #7) — belongs with the Phase 3/4
  restructure. Phase 1 must not depend on it being there, but may note its absence as a build risk.
- **Deleting the dead `Microsoft.WindowsAppSDK` / `CommunityToolkit.Mvvm` package references from
  `AkariTool.Core.csproj`** (CONCERNS #14) — unrelated cleanup, not a Phase 1 requirement. Note this
  would change the recorded warning baseline if done in the same phase.
- **A CI pipeline that runs the baseline gate** — no CI exists in the repo (verified: no `.github/`,
  no pipeline file). The gate is a local committed command for now; wiring it to CI is future work.

</deferred>

---

*Phase: 1-Baseline, Spikes & Test Harness*
*Context gathered: 2026-10-05*