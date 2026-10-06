# Phase 01: Baseline, Spikes & Test Harness - Research

**Researched:** 2026-10-05
**Domain:** Build-gate instrumentation, WinUI 3 / CommunityToolkit.WinUI compatibility spike, setting-ID divergence measurement, App-layer test harness — on a .NET 10 / WinUI 3 / VS-MSRBuild toolchain
**Confidence:** HIGH for SPIKE-01, SPIKE-03 and TEST-01 (all facts verified by direct filesystem/registry reads this session). MEDIUM for SPIKE-02 (package *metadata* is verified from the registry; whether the controls *render* is, by construction, unknown — that is the spike's job).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### App-layer test project (TEST-01)

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

#### SPIKE-02 — control compatibility spike

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

> **RESEARCH CORRECTION — three rows of that table are factually wrong about Akari.** Verified from
> `src/AkariTool.App/obj/project.assets.json` this session:
> - **CsWinRT is `2.1.1`, not `2.0.4`.** `2.0.4` is only `vendor/WinGet.Interop`'s explicit pin; NuGet
>   resolves the *maximum*, and `Material.Icons.WinUI3 3.0.2`'s nuspec declares
>   `Microsoft.Windows.CsWinRT 2.1.1`. The WCT 8.2 requirement of 2.2.0 is therefore a **one-minor
>   bump** from the resolved floor, not a jump from 2.0.4.
> - **SDK BuildTools is NOT absent.** The graph already resolves
>   `Microsoft.Windows.SDK.BuildTools 10.0.26100.4654` — *exactly* Winhance's pinned version — even
>   though no Akari csproj names it.
> - **`AkariTool.App.csproj` has no `Microsoft.Windows.CsWinRT` reference at all.** Only
>   `vendor/WinGet.Interop` does.
>
> This narrows the spike's risk surface considerably and should be stated in the spike's own write-up.

#### SPIKE-03 — setting-ID divergence report

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

#### SPIKE-01 — build baseline

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

### Deferred Ideas (OUT OF SCOPE)

- **Real-catalog validity test** (enumerate all 11 `Build()` methods through
  `SettingCatalogValidator`) — considered and explicitly deferred to **Phase 2 / BUG-02**. It is
  CONCERNS #16's top recommendation and every later Build Gate names it, but landing it in Phase 1
  would both move another phase's requirement and destabilise the recorded baseline test count.
- **Container-resolution integration test** — **Phase 3 / TEST-02**. Blocked until Phase 3 creates
  the composition root and removes `ServiceLocator`; there is nothing to resolve before then.
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
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description (from REQUIREMENTS.md) | Research Support |
|----|-----------------------------------|------------------|
| **SPIKE-01** | A recorded build baseline exists (assemblies produced, warning and error counts) so later phases can demonstrate the restructure introduced no regressions | §Toolchain — real MSBuild/vstest paths, verified vstest run, TRX `Counters` seam, the `WindowsAppSDKSelfContained` class-library hard error, the non-zero-exit-code reality of the PRI allowlist, assembly-inventory pitfalls |
| **SPIKE-02** | A spike confirms Winhance's `SettingsCard`, `DataGrid`, and `WrapPanel` render correctly under WindowsAppSDK 2.3.1 with `CommunityToolkit.WinUI`, or names the substitutes to use instead | §SPIKE-02 — verified registry metadata for every candidate package, the 8.3-preview MSB4011 trap, corrected Akari dependency floors, a concrete candidate `<ItemGroup>`, and the honest statement that "renders" has no automatable proxy |
| **SPIKE-03** | A generated report diffs every Akari setting ID against Winhance's, so config-format divergence is measured rather than assumed | §SPIKE-03 — the real 11 `Build()` types/namespaces, the **4 missing SoftwareApps factories**, the model shapes to walk, proof `SettingPageWarmUp` is *not* a usable seam, and the Customize prefix-scheme divergence that makes normalisation mandatory |
| **TEST-01** | An App-layer test project exists, so UI-adjacent logic is testable at all | §TEST-01 — verified `SettingBadgeCalculator.Compute` signature/accessibility, the Infrastructure reachability consequence, the two csproj shapes to mirror, the `UseWinUI`-on-a-library PRI risk, and the `InternalsVisibleTo` answer |
</phase_requirements>

---

## Project Constraints (from AGENTS.md)

These are hard directives. Nothing below contradicts any of them.

| Constraint | Effect on this phase |
|---|---|
| **Tech stack**: WinUI 3 (WindowsAppSDK 2.3.1), .NET 10, CommunityToolkit.Mvvm 8.4.2, C# 12. **No new frameworks.** | SPIKE-02's only admissible additions are NuGet packages. No new test framework, no new build tool. |
| **Build: VS MSBuild only** (`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`). `dotnet build` fails on WinUI 3 PRI/resource targets. | Every command `tools/run-tests.ps1` issues must be `MSBuild.exe` + `vstest.console.exe`. `dotnet test` is forbidden as the runner. |
| **Licensing — clean-room design parity.** Winhance is PolyForm Shield 1.0.0 with a noncompete clause and a required notice. **No new file may be copied from Winhance.** | SPIKE-03's snapshot script must **parse** the Winhance checkout and **emit Akari's own data**; it must not copy Winhance model files. Package *version strings* read from `Winhance.UI.csproj` are facts, not code. |
| **Licensing — outstanding compliance debt must not be made worse.** | Out of scope. Do not add third-party notices or copy PolyForm text into the snapshot. Record the Winhance commit SHA in the snapshot header instead. |
| **Elevation**: app is always-elevated by manifest; SYSTEM/TrustedInstaller work uses the existing impersonation path. | `tools/run-tests.ps1` will run **elevated**. See §Security. |
| **Safety**: every optimization must be safe, reversible, legible. | Non-binding here — Phase 1 mutates nothing. |
| **Scope**: every Winhance capability in scope; no anti-parity list. | SPIKE-03 must not filter or suppress divergences. |

**Project skills:** checked. `.claude/` contains only `settings.local.json`; `.agents/` does not exist;
`docs/` is the GitHub Pages site (`404.html`, `changelog.html`, `docs.html`, `features.html`,
`index.html`, `import-review-proposal.html`, `README.md`) — confirming D-15's rejection of `docs/` as
an artifact home. **No project skills to load.**

**Repo hygiene facts relevant to D-11/D-15** `[VERIFIED: git ls-files, repo-root listing]`:
`.planning/` **is** git-tracked (21 files) → the baseline document, SPIKE-02 verdict and SPIKE-03 report
are committable. `tools/` does not exist and has 0 tracked files → it is genuinely new. `bin/`, `obj/`,
`[Dd]ebug/`, `[Rr]elease/`, `bin/DeElevated/` are gitignored. Working tree is clean.

---

## Summary

This phase is four independent instruments, and the research found that **three of the four have a
concrete, verified execution path; one (SPIKE-02) has verified *inputs* but by definition has no
verifiable *output* until the spike is run.** The single most consequential finding is that **the
MSBuild path recorded in `AGENTS.md`, `01-CONTEXT.md` and therefore in the standing build definition
does not exist on this machine** — the project runs on Visual Studio Build Tools 2026 under
`Program Files (x86)`, not a Visual Studio Community install under `Program Files`. Any script that
hardcodes the documented path fails on its first invocation. Discovery must be dynamic via `vswhere.exe`.

Second, and equally load-bearing: **the solution's build is expected to exit non-zero** (PRI175/PRI252 are
tolerated errors, and MSBuild's exit code reflects all errors). So D-12's "baseline-compare mode" and
D-16's "explicit allowlist" cannot be implemented as "exit code == 0 means pass". The pass condition must
be *"every error MSBuild reported is in the allowlist, and the warning count did not increase."* The
`vstest.console.exe` side, by contrast, is clean and easy: I ran it against the existing build output
with **zero build** and it reported **230 tests, 229 passed, 1 not-executed**, with a machine-readable
`<Counters .../>` element in the TRX — which is the exact seam D-12 needs.

Third, for SPIKE-03: **D-09's "11 `Build()` methods" covers only Optimize (6) and Customize (5). The
SoftwareApps domain — roughly 55% of Akari's setting IDs by literal count — has no `Build()` method at
all** and is exposed through four differently shaped `Get*()` factories. SPIKE-03 says "diffs *every*
Akari setting ID", so the seam must be **15 entry points, not 11**. Separately, Optimize and SoftwareApps
IDs match Winhance's scheme exactly while Customize uses a *different* prefix convention — a raw set-diff
will report Customize as ~100% divergent, which would make the D6 evidence actively misleading unless the
report normalises.

Fourth, for TEST-01: **D-02's assumption is verified** — `SettingBadgeCalculator.Compute` is `public static`
with 9 parameters and no I/O, dispatcher or XAML type. But it is *not* Core-only: it calls
`AkariTool.Infrastructure...NumericConversionHelper`, reachable transitively. And there is a real,
specific risk in D-01: `UseWinUI=true` on a **class library** still gets a `PriIndexName`, which is the
same machinery that produces PRI175/PRI252 — so adding the test project could itself perturb the
baseline it is meant to help measure.

**Primary recommendation:** implement `tools/run-tests.ps1` around three verified seams — dynamic
`vswhere` toolchain discovery, a `/t:Rebuild` MSBuild run whose *error list* (not exit code) is matched
against the PRI175/PRI252 allowlist, and a `vstest.console.exe` run parsed from TRX `Counters` — and
build SPIKE-03's Akari-side seam over 15 static factories in Core, not 11.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Build invocation + warning/error accounting | **Build tooling** (`tools/`, MSBuild) | — | Only MSBuild can produce the warning/error stream; no runtime tier owns it. |
| Test execution + test counting | **Build tooling** (`vstest.console.exe`) | — | A host process, not app code. Parsed from TRX, never from log scraping. |
| Test project shape + assertion code | **Test project** (`tests/AkariTool.App.Tests`) | App (types under test) | The project is a consumer of `AkariTool.dll`; it owns no production behaviour. |
| Control-compatibility observation | **Desktop UI (spike `WinExe`)** | Build tooling | D-06 requires an actual running window; only a launched WinUI process can render. |
| Akari setting-ID enumeration | **Core** (`AkariTool.Core`, static factories) | Generator (reads Core's metadata) | The IDs live in Core's declarative catalogs; the generator only reflects over them. No DI, no App layer. |
| Winhance setting-ID extraction | **External read-only source** (Winhance checkout) | Generator (text parse) | D-08: a committed snapshot; the checkout is read, never modified, never copied. |
| Baseline persistence | **Repository files** (`tools/baseline.json`) | — | Machine-readable, git-tracked, diffable. |

**Layer note for TEST-01:** the test project references **App**, which references **Infrastructure**,
which references **Core** + `vendor/WinGet.Interop`. So the test assembly's *transitive* reference set
is the whole graph. This is fine for a managed reference; it is the reason a build is required at all
rather than a direct DLL load.

---

## Toolchain Facts — SPIKE-01 (all `[VERIFIED]` this session)

### The documented MSBuild path does not exist

`AGENTS.md` (STACK block) and `01-CONTEXT.md` both name:

```
C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe
```

`Test-Path` → **False**. `C:\Program Files\Microsoft Visual Studio` **does not exist as a directory**.
`vswhere -all -products *` returns exactly one install:

```json
{ "installationPath": "C:\\Program Files (x86)\\Microsoft Visual Studio\\18\\BuildTools",
  "installationVersion": "18.10.12224.181",
  "displayName": "Visual Studio Build Tools 2026",
  "productId": "Microsoft.VisualStudio.Product.BuildTools",
  "installDate": "2026-10-04T17:15:00Z", "isPrerelease": false, "isComplete": true }
```

**There is no Visual Studio IDE on this machine** — only Build Tools. Consequences for the plan:

| Fact | Value |
|---|---|
| Real MSBuild | `<vsroot>\MSBuild\Current\Bin\MSBuild.exe` → `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe` |
| MSBuild version | **18.10.1.42706** (`-version -nologo`) |
| 64-bit variant | `<vsroot>\MSBuild\Current\Bin\amd64\MSBuild.exe` (also present) |
| Discovery | `C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe -requires Microsoft.Component.MSBuild -property installationPath` |
| vstest.console.exe | `<vsroot>\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe` → **`VSTest version 18.10.0 (x64)`** |
| Duplicate vstest | `<vsroot>\Common7\IDE\Extensions\TestPlatform\vstest.console.exe` (pick one; the `CommonExtensions\Microsoft\TestWindow` one is the one verified working) |
| `dotnet` fallback for vstest | `C:\Program Files\dotnet\sdk\10.0.401\vstest.console.dll` exists, but D-03 pins VS tooling — use the VS binary |
| `msbuild` on `PATH` | **not present** — a script cannot rely on `PATH` |
| Installed .NET SDK | 10.0.401 (`dotnet --list-sdks`) |
| `global.json` | **does not exist** in the repo (AGENTS.md's "global.json pin 10.0.102" is stale) |
| Elevated session | `WindowsPrincipal.IsInRole(Administrator)` → **True** (tests can run) |

**Recommendation for the planner:** `tools/run-tests.ps1` must resolve the VS root through `vswhere.exe`
on every run, with a hard-coded path only as a last-resort fallback. Nine later phases' Build Gates cite
this script, so a machine-specific path baked into it is a roadmap-wide fragility.

### vstest works, and here is the measured result

I ran `vstest.console.exe` against the **already-built** test assemblies — no build, no writes to the
repo (TRX went to `%LOCALAPPDATA%\Temp`):

```powershell
& $vst `
  "tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll" `
  "tests\AkariTool.Infrastructure.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.Tests.dll" `
  /Platform:x64 /logger:"trx;LogFileName=baseline-probe.trx" /ResultsDirectory:"$tmp"
```

Result:

```
Test Run Successful.
Total tests: 230
     Passed: 229
    Skipped: 1
```

| Assembly | Tests (from TRX `testName` prefixes) |
|---|---|
| `AkariTool.Core.Tests` | **93** |
| `AkariTool.Infrastructure.Tests` | **137** |
| `AkariTool.App.Tests` | 0 (does not exist yet) |
| **Total** | **230** (229 passed, 1 `NotExecuted`) |

The single non-passing test is
`AkariTool.Infrastructure.Tests.Services.UpdateServiceTests.CheckAsync_LiveCall_NotRunInUnitTests`
(a deliberate network-test skip).

> **AGENTS.md and TESTING.md are both stale.** AGENTS.md says "53 passing" + "136 passing + 1 skipped";
> TESTING.md (analysed 2026-08-27) says "Core 53; Infrastructure 136 passing + 1 skipped". The measured
> truth on the current `bin/` is **93 + 137 = 230**. The Phase 1 baseline document must record the
> measured number, and the plan should treat the AGENTS.md figures as wrong rather than as a target.
> (Updating AGENTS.md is not a Phase 1 requirement — but do not copy its numbers into the baseline.)

**The machine-readable seam** — TRX `ResultSummary/Counters`, a single element with attributes:

```xml
<Counters total="230" executed="229" passed="229" failed="0" error="0"
          timeout="0" aborted="0" notRunnable="0" notExecuted="1" ... />
```

Per-class breakdown is available via `TestDefinitions/UnitTest/@className` joined to
`Results/UnitTestResult/@testId`; 18 distinct test classes exist today. D-14 forbids *gating* per
project but permits *recording* it, so the baseline file may carry a per-assembly split derived this way.

**MSBuild switches confirmed present** in `MSBuild.exe -help`: `-t:Rebuild`, `-clp` (via
`-consoleloggerparameters`, with `Summary`, `ErrorsOnly`, `NoItemAndPropertyList` documented),
`-warnaserror`, `-bl` (binary logger), `-getProperty`, `-getTargetResult`, `-fileLogger`,
`-verbosity`, `Restore`, `-p:`.

### The build is expected to exit NON-ZERO — the most important design fact for D-12/D-16

ROADMAP says both that "the solution compiles all six assemblies" *and* that PRI175/PRI252 are
tolerated errors. Those are only reconcilable if **MSBuild's exit code is non-zero** while the
assemblies are still emitted. The output directory confirms emission:

| File | Size | Note |
|---|---|---|
| `AkariTool.exe` / `AkariTool.dll` | 355,840 / 1,468,928 | App |
| `AkariTool.Core.dll` | 840,192 | |
| `AkariTool.Infrastructure.dll` | 694,272 | |
| `WinGet.Interop.dll` | 400,896 | |
| `WinUI.Framework.dll` | 58,880 | |
| `AkariTool.pri` | 2,305,600 | PRI **was** generated |
| `WinUI.Framework.pri` | **960** | near-empty — consistent with ROADMAP's stated root cause |

**Therefore the baseline gate's pass condition must be:**

> MSBuild exit code is non-zero **and every error line matches the allowlist** (`WINAPPSDKGENERATEPROJECTPRIFILE` PRI175/PRI252) **and** the warning count did not increase **and** the expected assembly set is present on disk.

`$LASTEXITCODE -eq 0` is the wrong check and would fail the gate on the tolerated errors, or — worse, if
someone "fixed" it by loosening the allowlist to make exit 0 — would hide a real compile error, which is
exactly what D-16 forbids.

### The `WindowsAppSDKSelfContained` class-library hard error — a trap for the test runner

`AkariTool.App.csproj` sets `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` at line 22.
`vendor/WinGet.Interop.csproj:27-34` documents that global properties flow into every `ProjectReference`
and that "the Windows App SDK targets hard-error when WindowsAppSDKSelfContained lands on a class
library". **Verified in the package targets** at
`%USERPROFILE%\.nuget\packages\microsoft.windowsappsdk.base\2.0.4\build\Microsoft.WindowsAppSDK.Base.targets:19-20`:

```xml
<Target Name="WindowsAppSDKSelfContainedAudit" BeforeTargets="Build"
        Condition="'$(WindowsAppSDKSelfContainedAudit)'!='false' and '$(WindowsAppSDKSelfContained)'=='true' and '$(OutputType)'=='Library'">
  <Error Text="WindowsAppSDKSelfContained should not be applied to a class library." />
```

Five projects in the graph are `OutputType=Library` (Core, Infrastructure, WinGet.Interop,
WinUI.Framework, and the new `AkariTool.App.Tests`).

> **Hard rule for `tools/run-tests.ps1`: never pass `/p:WindowsAppSDKSelfContained=true` or
> `/p:SelfContained=true` on the MSBuild command line.** Doing so hard-fails all five libraries
> before a single line of C# is compiled. The `AkariPublish` mapping at `AkariTool.App.csproj:35-38`
> is safe because it lives in a csproj `PropertyGroup` (csproj properties do not flow to
> `ProjectReference`s) — only `/p:` global properties do.

Mitigating factors, both verified in the same targets folder:
`Microsoft.UI.Xaml.Markup.Compiler.BeforeCommon.targets:22` forces `WindowsAppSDKSelfContained=false`
during markup compilation, and `Microsoft.WinUI.NET.Markup.Compiler.targets:53` has an
`OutputType=='Library'`-specific condition — library output is an *anticipated* case, not an error case.

### Assembly-inventory pitfalls

- **Do not count `.dll` files under `bin/`.** `src/AkariTool.App/bin/.../win-x64/` contains **224**
  dll/exe files because `WindowsAppSDKSelfContained=true` copies the whole WindowsAppSDK runtime payload
  (DirectML.dll alone is 18.7 MB). A naive count is meaningless.
- The meaningful inventory is the **6 project assemblies + 2 test assemblies = 8** after Phase 1, which
  matches ROADMAP's "Core, Infrastructure, WinGet.Interop, WinUI.Framework, App, and both test
  assemblies" (App contributes two files: `AkariTool.exe` + `AkariTool.dll`).
- `vendor/WinUI.Framework` is **not** in `AkariTool.sln` (CONCERNS #7, confirmed) but *is* built
  transitively, so it appears in `bin` regardless. It must still be in the baseline inventory — the
  assembly's presence is what matters, not its solution membership.
- `bin/DeElevated/` at the repo root is a leftover from `build-deelevated.ps1`
  (`/p:DeElevatedTest=true`) and must be excluded or it will double-count.
- `AkariTool.sln` actually has **6 real projects + 3 solution folders** (`src`, `tests`, `vendor`),
  not the "5 real projects + 2 solution folders" CONTEXT states. `AkariTool.App.Tests` joins the
  `tests` folder.

---

## SPIKE-02 — CommunityToolkit.WinUI under WindowsAppSDK 2.3.1

**Scope discipline:** this section supplies a *candidate package set and its verified dependency facts*.
It does **not** state a verdict. The verdict is the spike's deliverable.

### Registry reachability

`https://api.nuget.org/v3-flatcontainer/...` returns **HTTP 200** from this environment, and the full
`.nuspec` and `registration5` metadata are fetchable. **SPIKE-02 can restore.** (Context7 MCP is not
available to this subagent; for exact version strings and declared dependency floors the NuGet registry
API is the stronger source anyway.)

### Verified package facts

`[VERIFIED: api.nuget.org/v3-flatcontainer + /v3/registration5-gz-semver2, fetched 2026-10-05]`

| Package | Versions | Latest **stable** | Published | Owners | Project URL |
|---|---|---|---|---|---|
| `CommunityToolkit.WinUI.Controls.SettingsControls` | 14 | **8.2.251219** | 2025-12-20 | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows |
| `CommunityToolkit.WinUI.Controls.Primitives` | 14 | **8.2.251219** | 2025-12-20 | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows |
| `CommunityToolkit.WinUI.UI.Controls.DataGrid` | **6 (ever)** | **7.1.2** | **2021-11-18** | `Microsoft.Toolkit`, `CommunityToolkit.Common` | github.com/CommunityToolkit/WindowsCommunityToolkit |
| `CommunityToolkit.WinUI.Triggers` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `CommunityToolkit.WinUI.Extensions` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `CommunityToolkit.WinUI.Collections` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `CommunityToolkit.WinUI.Behaviors` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `CommunityToolkit.WinUI.Helpers` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `CommunityToolkit.WinUI.Animations` | 14 | 8.2.251219 | 2025-12-20 | `Microsoft.Toolkit` | ↑ |
| `Microsoft.Windows.CsWinRT` | 67 | **2.3.1** | 2026-07-22 | `Microsoft` | github.com/microsoft/cswinrt |
| `Microsoft.Windows.SDK.BuildTools` | 114 | 10.0.28000.2705 | 2026-08-26 | `Microsoft` | aka.ms/WinSDKProjectURL |
| `CommunityToolkit.Common` | — | 8.4.2 | 2026-03-25 | `Microsoft.Toolkit` | github.com/CommunityToolkit/dotnet |
| `Microsoft.Xaml.Behaviors.WinUI.Managed` | — | 3.0.1 | 2026-01-14 | `Microsoft` | go.microsoft.com/fwlink/?LinkID=651678 |

### Declared dependency floors (the landmines, measured)

| Package @ version | Windows-TFM dependencies (from `.nuspec`) |
|---|---|
| `SettingsControls 8.2.251219` | `CommunityToolkit.WinUI.Triggers 8.2.251219` \| `Microsoft.WindowsAppSDK 1.6.250108002` |
| `Primitives 8.2.251219` | `CommunityToolkit.WinUI.Extensions 8.2.251219` \| `Microsoft.WindowsAppSDK 1.6.250108002` |
| `Triggers 8.2.251219` | `CommunityToolkit.WinUI.Helpers 8.2.251219` \| `Microsoft.WindowsAppSDK 1.6.250108002` |
| `Helpers 8.2.251219` | `CommunityToolkit.WinUI.Extensions 8.2.251219` \| `Microsoft.WindowsAppSDK 1.6.250108002` |
| `Extensions 8.2.251219` | `CommunityToolkit.Common 8.2.1` \| `Microsoft.WindowsAppSDK 1.6.250108002` |
| `Behaviors 8.2.251219` | `Animations 8.2.251219` \| `Extensions 8.2.251219` \| `Microsoft.WindowsAppSDK 1.6.250108002` \| `Microsoft.Xaml.Behaviors.WinUI.Managed 3.0.0` |
| `DataGrid 7.1.2` | `Microsoft.WindowsAppSDK 1.0.0` — **and nothing else** |
| **`SettingsControls 8.3.260402-preview2`** | `Triggers 8.3.260402-preview2` \| **`Microsoft.WindowsAppSDK.WinUI 1.8.260204000`** ⚠ |
| **`Primitives 8.3.260402-preview2`** | `Extensions 8.3.260402-preview2` \| **`Microsoft.WindowsAppSDK.WinUI 1.8.260204000`** ⚠ |

Five facts fall out of that table, and each one changes the plan:

1. **`Microsoft.WindowsAppSDK 1.6.250108002` is a NuGet *minimum*, not a pin.** NuGet takes the maximum
   across the graph, so Akari's `2.3.1` satisfies it with **no `NU1605` downgrade**. The "floors at
   WinAppSDK 1.6+" claim in `Winhance.UI.csproj` is a *floor*, and 2.3.1 clears it. **This removes what
   looked like the largest landmine.**
2. **⚠ The 8.3 preview train is a trap, and must NOT be the spike's candidate.** 8.3 switched its
   WindowsAppSDK dependency from the **metapackage** to the **component package**
   (`Microsoft.WindowsAppSDK.WinUI`). `Winhance.UI.csproj`'s own comment says the component package
   "pulls in BOTH families and the duplicate WinAppSDK build props break the build (MSB4011 …)". Akari
   is on the 2.3.1 metapackage, whose own transitive graph *already contains*
   `Microsoft.WindowsAppSDK.WinUI 2.3.0` (verified in `project.assets.json`) — so a direct component ref
   is exactly the dual-family condition MSB4011 describes. **Pin the spike to 8.2.251219.**
3. **`Microsoft.Windows.CsWinRT` is NOT a nuspec dependency of any WCT 8.2 package.** It must be added
   **explicitly** or WCT 8.2's WinRT content will not project. This confirms Winhance's csproj comment
   and is why NuGet will not rescue the spike.
4. **DataGrid 7.1.2 is genuinely frozen and genuinely decoupled.** Six versions ever, last published
   2021-11-18, single dependency `Microsoft.WindowsAppSDK 1.0.0`, no `CommunityToolkit.*` coupling at
   all. It is the *lowest*-risk of the three packages, not the highest.
5. **There is no 8.x under the `CommunityToolkit.WinUI` metapackage id** — that id also stops at 7.1.2.
   *"Add CommunityToolkit.WinUI"* is therefore not an actionable instruction; the 8.x train must be
   referenced per package.

### Akari's actual current dependency floor (corrects CONTEXT)

`[VERIFIED: src/AkariTool.App/obj/project.assets.json — read this session]`

```
Microsoft.Windows.CsWinRT/2.1.1
Microsoft.Windows.SDK.BuildTools/10.0.26100.4654
Microsoft.WindowsAppSDK/2.3.1          (metapackage)
Microsoft.WindowsAppSDK.WinUI/2.3.0    (transitive, via the metapackage — the supported pattern)
Microsoft.WindowsAppSDK.Base/2.0.4   Foundation/2.3.5   InteractiveExperiences/2.1.3
Microsoft.WindowsAppSDK.DWrite/2.1.0  Widgets/2.0.5      AI/2.3.4   ML/2.1.74   Runtime/2.3.1
```

- `Microsoft.Windows.CsWinRT 2.1.1` is **not** in any Akari csproj. It arrives from
  `Material.Icons.WinUI3 3.0.2`'s nuspec, which declares `Microsoft.Windows.CsWinRT 2.1.1`. NuGet takes
  the max over `vendor/WinGet.Interop`'s `2.0.4`, so 2.1.1 wins. The gap to WCT 8.2's stated 2.2.0
  need is **one minor version**.
- `Microsoft.Windows.SDK.BuildTools 10.0.26100.4654` is **already resolved** — exactly Winhance's pin.
  CONTEXT's "absent" is wrong. **The spike does not need to add it.**
- None of the nine `WindowsAppSDK.*` sub-packages declare a `CsWinRT` dependency (checked each
  `.nuspec`), so the 2.1.1 floor is entirely Akari's own icon-package doing.

### Recommended candidate `<ItemGroup>` for the throwaway spike

Derived from the facts above, **not** a verdict. The plan should put something equivalent in an
`<action>` so the executor does not have to invent versions.

```xml
<!-- D-05: throwaway spike project. AkariTool.App.csproj stays byte-for-byte identical. -->
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
  <Platforms>x64</Platforms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <RootNamespace>AkariTool.Spike</RootNamespace>
  <UseWinUI>true</UseWinUI>
  <EnableMsixTooling>false</EnableMsixTooling>
  <WindowsPackageType>None</WindowsPackageType>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>

<ItemGroup>
  <!-- Keep the metapackage. NEVER add Microsoft.WindowsAppSDK.WinUI as a direct reference (MSB4011). -->
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.3.1" />

  <!-- Explicit: NOT a nuspec dependency of any WCT 8.2 package. -->
  <PackageReference Include="Microsoft.Windows.CsWinRT" Version="2.3.1" />

  <!-- SettingsCard -->
  <PackageReference Include="CommunityToolkit.WinUI.Controls.SettingsControls" Version="8.2.251219" />
  <!-- WrapPanel -->
  <PackageReference Include="CommunityToolkit.WinUI.Controls.Primitives" Version="8.2.251219" />
  <!-- DataGrid. Frozen at 7.1.2; last published 2021-11-18; no toolkit coupling. -->
  <PackageReference Include="CommunityToolkit.WinUI.UI.Controls.DataGrid" Version="7.1.2" />
</ItemGroup>
```

Notes the planner must carry into the task:

- **Do not reference `AkariTool.App` or `AkariTool.Core` from the spike.** A ProjectReference would drag
  in the self-contained `win-x64` App graph, `Material.Icons.WinUI3`'s CsWinRT 2.1.1 floor and
  `vendor/WinUI.Framework` — three variables the spike does not need. The spike is a clean-room WinUI
  project with a `MainWindow.xaml` containing the three controls.
- **`Microsoft.Windows.CsWinRT 2.3.1` vs `2.2.0`:** 2.3.1 is the current stable and is a superset of
  2.2.0's requirement; 2.2.0 matches Winhance exactly. Either is defensible. Prefer **2.3.1** (stable
  latest) and record 2.2.0 as the fallback if 2.3.1 misbehaves — trying both is cheap inside a
  throwaway project and is a *substitute* observation, not root-cause diagnosis (D-07-compliant).
- **Do not add `CommunityToolkit.WinUI.Behaviors` or `.Collections`** unless the render needs them.
  `Behaviors` drags in `Microsoft.Xaml.Behaviors.WinUI.Managed 3.0.0` — one more moving part for zero
  benefit against the three named controls.
- **Do not add `Microsoft.Windows.SDK.BuildTools`.** It is already at `10.0.26100.4654` transitively and
  the latest is `10.0.28000.2705` — a floating reference would *upgrade* it and change the build.
- Set `<EnableMsixTooling>false</EnableMsixTooling>` on the spike. `AkariTool.App.csproj` sets it
  `true`, and `Microsoft.WinUI.AppX.targets:36` documents that MSIX tooling on class libraries "can
  erroneously raise a warning thinking that we've packaged multiple executables" — a warning the spike's
  own log would then contain for no reason.
- The spike project should **not** be added to `AkariTool.sln` at all, which trivially satisfies the
  "must not be left in the solution file after deletion" concern in the discretion list.

---

## SPIKE-03 — Setting-ID divergence

### The Akari side: exactly 11 `Build()` methods, and they do **not** cover everything

`[VERIFIED: grep over src/AkariTool.Core for 'static.*Build\s*\(' — 11 matches, then each file read for
its `namespace` and `class` declaration]`

All 11 have the identical shape `public static IReadOnlyList<SettingGroup> Build()` — **zero parameters,
no dependencies, `public static class` (none `partial`)**. That is what makes D-09's reflection trivial
and DI-free.

| # | Declaring type | Namespace | File | Line |
|---|---|---|---|---|
| 1 | `GamingOptimizations` | `AkariTool.Tabs.Gaming` | `Core/Features/Gaming/Catalogs/GamingOptimizations.cs` | 10 |
| 2 | `PrivacyOptimizations` | `AkariTool.Tabs.Privacy` | `Core/Features/Privacy/Catalogs/PrivacyOptimizations.cs` | 10 |
| 3 | `PowerOptimizations` | `AkariTool.Tabs.Power` | `Core/Features/Power/Catalogs/PowerOptimizations.cs` | 15 |
| 4 | `NotificationsOptimizations` | `AkariTool.Tabs.Notifications` | `Core/Features/Notifications/Catalogs/NotificationsOptimizations.cs` | 16 |
| 5 | `SoundOptimizations` | `AkariTool.Tabs.Sound` | `Core/Features/Sound/Catalogs/SoundOptimizations.cs` | 15 |
| 6 | `UpdateOptimizations` | `AkariTool.Tabs.Update` | `Core/Features/Update/Catalogs/UpdateOptimizations.cs` | 27 |
| 7 | `DesktopOptimizations` | `AkariTool.Tabs.Customize` | `Core/Features/Customize/Catalogs/DesktopOptimizations.cs` | 11 |
| 8 | `AppearanceOptimizations` | `AkariTool.Tabs.Customize` | `Core/Features/Customize/Catalogs/AppearanceOptimizations.cs` | 10 |
| 9 | `TaskbarOptimizations` | `AkariTool.Tabs.Customize` | `Core/Features/Customize/Catalogs/TaskbarOptimizations.cs` | 10 |
| 10 | `StartMenuOptimizations` | `AkariTool.Tabs.Customize` | `Core/Features/Customize/Catalogs/StartMenuOptimizations.cs` | 10 |
| 11 | `ExplorerOptimizations` | `AkariTool.Tabs.Customize` | `Core/Features/Customize/Catalogs/ExplorerOptimizations.cs` | 10 |

**These cover Optimize (6) and Customize (5) only.** Note also that all 11 declare
`AkariTool.Tabs.*` — **not** `AkariTool.Core.Features.*` (CONCERNS #12's "naming hides the divergence"),
so the generator's `using`/reflection must use the `AkariTool.Tabs` namespaces, and Phase 3's ARCH-09
namespace alignment will break the generator's type names. Worth a comment in the generator.

### ⚠ GAP: the SoftwareApps domain is not in the 11

`[VERIFIED: files read; grep for 'static.*Build\s*\(' over Software/Apps/AkariOS returns nothing]`

The SoftwareApps domain — which ROADMAP sizes at **254+ items** and which raw literal counting puts at
**606 `Id = "…"` occurrences** — has **no `Build()` method**. It exposes four differently shaped
factories, all `public static`, all **zero-parameter**, all in namespace **`AkariTool.Tabs`**:

| Factory | File | Returns |
|---|---|---|
| `ExternalAppCatalog.GetExternalApps()` | `Core/Features/Software/Catalogs/ExternalAppCatalog.cs:8` | `AppGroup` (aggregates 16 `*Category.Get*()` calls) |
| `WindowsAppCatalog.GetWindowsApps()` | `Core/Features/Software/Catalogs/WindowsAppCatalog.cs:8` | `AppGroup` |
| `CapabilityCatalog.GetWindowsCapabilities()` | `Core/Features/Software/Catalogs/CapabilityCatalog.cs:8` | `AppGroup` |
| `OptionalFeatureCatalog.GetWindowsOptionalFeatures()` | `Core/Features/Software/Catalogs/OptionalFeatureCatalog.cs:8` | `AppGroup` |

**Consequence: SPIKE-03's seam is 15 entry points, not 11.** D-09's wording ("the 11 real catalog
`Build()` methods") is accurate about what exists but under-covers what the requirement asks for
("diffs *every* Akari setting ID"). The plan must extend the seam to 15 and say so in the generator's
header comment — otherwise the D6 evidence base silently omits the largest domain. This is a scope
correction inside a locked decision, not a redesign of it: the *method* D-09 chose (reflect over static
catalog factories) is unchanged; only the *count* was wrong.

### The two model shapes to walk

```csharp
// src/AkariTool.Core/Features/Common/Models/SettingGroup.cs:5-10
public sealed record SettingGroup
{
    public required string Name { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<SettingDefinition> Settings { get; init; }
}
```

```csharp
// src/AkariTool.Core/Features/Software/Catalogs/AppModels.cs:87-93
public record AppGroup
{
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<AppDefinition> Items { get; init; }
}
```

`SettingGroup` has **no `Id` of its own** — the identity is `FeatureId` on the group and `Id` on each
item (`BaseDefinition.Id` is `public required string Id { get; init; }`,
`src/AkariTool.Core/Features/Common/Models/BaseDefinition.cs:6-8`; `AppDefinition.Id` is likewise
`required`, `AppModels.cs:29`). So:

- `Build()` path → `group.Settings.Select(s => s.Id)`
- `Get*()` path → `group.Items.Select(i => i.Id)`

A single generator handling both shapes needs a two-arm walk keyed on the returned type, or two
adapters behind one `IEnumerable<(string domain, string id)>` seam. The **second** adapter is the one
Phase 2's BUG-02 validator test will also need (it should be written to take
`IEnumerable<SettingGroup>` and separately `IEnumerable<AppGroup>`, or a normalised
`(GroupName, Id)` pair).

### `SettingPageWarmUp` is **not** reusable as the enumeration seam

`[VERIFIED: src/AkariTool.App/Services/SettingPageWarmUp.cs read in full]`

```csharp
public static void Run(IServiceProvider services, WinUI.Framework.Services.ILogService log)
{
    var pages = services.GetServices<SettingPageViewModel>().ToList();
    var backup = services.GetRequiredService<SettingBackupService>();
    foreach (var page in pages) { page.Build(); backup.Register(page); }
    InitializeNewBadges(services, pages);
    ...
}
```

It is entangled with everything the phase is trying to avoid:

1. Requires a **live `IServiceProvider`** with every `SettingPageViewModel` registered.
2. `GetRequiredService<SettingBackupService>()` — a **hard** dependency on a 658-line App service
   (CONCERNS #10) which itself takes `IEnumerable<SettingPageViewModel>`.
3. `page.Build()` goes through `SettingPageViewModel`, which per CONCERNS #9 resolves five more
   services **at call time** via `WinUI.Framework.IoC.ServiceLocator.GetService<T>()` (48 call sites
   across 27 App files), including the Infrastructure `IProcessRestartManager` resolved by
   fully-qualified name.
4. It lives on the **App** assembly in `AkariTool.Services`, so using it would drag App + Infrastructure
   + `vendor/WinUI.Framework` into the generator — the exact DI entanglement SPIKE-03 is meant to avoid.

**Verdict: it is a runtime-enumeration precedent, not a test seam.** The 15 static factories need no DI
at all, which is strictly better and satisfies D-09's stated goal ("the Akari side cannot drift from the
catalog") more tightly. `CompatibleSettingsRegistry.GetKnownFeatureProviders()` is the other candidate
named in CONTEXT; it is in **Infrastructure**, so it adds a layer dependency for no benefit and sits
under the CORE-06 "do not move or rewrite" freeze. **Recommend the 15 static factories.**

### The Winhance side (read-only reference)

`[VERIFIED: directory listing of C:\Users\isleap\Documents\GitHub\Winhance\src\Winhance.Core\Features]`

| Domain | `.cs` files | Subfolders |
|---|---|---|
| `AdvancedTools` | 8 | `Interfaces`(6), `Models`(2) |
| `Common` | 168 | `Constants`(7) `Converters`(1) `Enums`(13) `Events`(6) `Exceptions`(2) `Extensions`(2) `Helpers`(2) `Interfaces`(75) `Localization`(1) `Models`(40) `Native`(8) `Services`(5) `Utils`(1) `Validation`(1) |
| `Customize` | 5 | `Interfaces`(1), `Models`(4) |
| `Optimize` | 9 | `Interfaces`(1), `Models`(8) |
| `SoftwareApps` | 50 | `Enums`(2) `Interfaces`(20) `Models`(27) `Utilities`(1) |

**Catalogs live in `Models/`, not `Catalogs/`.** The ID-bearing files (raw `Id = "…"` counts):

| Winhance file | Ids | Lines |
|---|---|---|
| `Optimize/Models/GamingAndPerformanceOptimizations.cs` | 174 | 4,153 |
| `Optimize/Models/PowerOptimizations.cs` | 142 | 1,469 |
| `Optimize/Models/PrivacyOptimizations.cs` | 122 | 2,920 |
| `Customize/Models/ExplorerCustomizations.cs` | 113 | 3,425 |
| `SoftwareApps/Models/WindowsAppDefinitions.cs` | 101 | 655 |
| `SoftwareApps/Models/ExternalAppDefinitions.Multimedia.cs` | 64 | 310 |
| `SoftwareApps/Models/ExternalAppDefinitions.Browsers.cs` | 46 | 276 |
| `Customize/Models/TaskbarCustomizations.cs` | 31 | 1,008 |
| `Optimize/Models/NotificationOptimizations.cs` | 29 | 465 |
| … plus `StartMenuCustomizations.cs`, `WindowsThemeCustomizations.cs`, `SoundOptimizations.cs`, `UpdateOptimizations.cs`, `PowerPlan.cs`, `PowerTemplates.cs` (non-catalog) | | |

Note the **naming asymmetry** the snapshot script must bridge: Winhance calls them
`*Customizations` / `*Optimizations` / `*Definitions`; Akari calls them `*Optimizations` uniformly.
File-name-based pairing will not work — the snapshot must be **ID-set based**, keyed by domain.

### ⚠ The divergence that matters: Customize uses a different ID prefix scheme

`[VERIFIED: literal samples read from both sides]`

| Domain | Akari sample | Winhance sample | Same scheme? |
|---|---|---|---|
| Optimize | `Id = "gaming-game-mode"` | `Id = "gaming-game-mode"` | ✅ identical |
| Optimize | `Id = "gaming-performance-autostart-delay"` | `Id = "gaming-performance-autostart-delay"` | ✅ identical |
| SoftwareApps | `Id = "windows-app-3d-viewer"` | `Id = "windows-app-3d-viewer"` | ✅ identical |
| **Customize** | `Id = "customize-explorer-show-file-extensions"` | `Id = "explorer-customization-shortcut-suffix"` | ❌ **different** |
| Customize | `Id = "customize-explorer-show-hidden-files"` | `Id = "explorer-customization-shortcut-arrow"` | ❌ **different** |

Akari prefixes the **domain** (`customize-explorer-…`); Winhance suffixes the **page name**
(`explorer-customization-…`). The *trailing* tokens line up (`show-file-extensions` vs
`shortcut-suffix` are different settings, but the prefixes are systematically transformable).

**Implication for the report:** a naive set-difference on raw IDs will report the Customize domain as
**~100% Akari-only and ~100% Winhance-only**, which is arithmetically true and semantically useless as
D6 evidence — it would suggest a total format fork when in fact the *tail* vocabulary is shared. Since
the report is the evidence base for the config-fork decision, it **must** emit both a raw diff and a
normalised diff, and must state the normalisation rule in its own header. This maps directly onto the
discretionary item *"How the ID report categorises divergence kinds"* — the answer is:
**raw-equal / normalised-equal / Akari-only / Winhance-only / both-but-normalised-conflict.**

Two more structural notes for the generator:

- Akari catalogs expose **`FeatureId` on the group** (`"gaming-game-mode"` appears as *both* a
  `FeatureId` and an `Id` in `GamingOptimizations.cs`) — so a raw `Id = "…"` text parse over Akari
  over-counts. This is the concrete proof of D-09's "text-parsing is fragile" claim and is why the Akari
  side must be reflective while the Winhance side (plain C# literal files, no factory seam) is parsed.
- Raw literal counts for orientation only (**upper bounds**, not the report's numbers):
  Akari Software **606**, Optimize **465**, Customize **150**; Winhance Optimize top-3 **438**,
  Customize top-2 **144**, SoftwareApps top-10 **~380**. `.planning/research/PARITY.md` holds the
  1,045-line per-domain reconciliation the report should be checked against.

### `SettingCatalogValidator` is available for reuse

`[VERIFIED: src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs]`

```
:7   namespace AkariTool.Core.Features.Common.Validation;
:12  public sealed record CatalogViolation(string SettingId, string GroupName, string Message);
:39  public static class SettingCatalogValidator
:51  public static IReadOnlyList<CatalogViolation> Validate(SettingGroup group)
:68  public static IReadOnlyList<CatalogViolation> Validate(IEnumerable<SettingGroup> groups)
```

It is in **Core**, so both the SPIKE-03 generator and Phase 2's BUG-02 test can use it. It is *not* a
Phase 1 deliverable (deferred), but the generator's `SettingGroup` walk should produce output the
validator could consume, so Phase 2 inherits the seam rather than rebuilding it.

---

## TEST-01 — `AkariTool.App.Tests`

### D-02's target is verified: `public static`, pure, 9 parameters

`[VERIFIED: src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs:15-27, read in full]`

```csharp
namespace AkariTool.ViewModels.Tweaks;

public static class SettingBadgeCalculator
{
    public static IReadOnlyList<BadgePillState> Compute(
        SettingDefinition definition,
        InputType inputType,
        bool isOn,
        int selectedIndex,
        int numericValue,
        int acNumericValue,
        int dcNumericValue,
        bool hasBattery,
        bool supportsSeparateACDC)
```

**D-02's assumption holds**: `public static class`, static method, `SettingDefinition` +
`InputType` + primitives in, `IReadOnlyList<BadgePillState>` out. No `DispatcherQueue`, no XAML type, no
I/O, no static mutable state. The class is in namespace `AkariTool.ViewModels.Tweaks` — **not**
`AkariTool.App.*`.

### …but it is not Core-only

`[VERIFIED: same file, lines 381-383]`

```csharp
private static int ConvertFromSystemUnits(SettingDefinition definition, int systemValue) =>
    AkariTool.Infrastructure.Features.Common.Utilities.NumericConversionHelper
        .ConvertFromSystemUnits(systemValue, definition.NumericRange?.Units);
```

Every `NumericRange` badge path calls into **Infrastructure**. Because `AkariTool.App` ProjectReferences
`AkariTool.Infrastructure`, the test project resolves it transitively — **no extra `ProjectReference`
needed** — but the test project cannot be Core-only, and the mock/factory helpers will need real
`PowerCfgSetting` + `NumericRangeMetadata` values for the numeric-range branches.

### A behavioural subtlety the characterization test should freeze

`[VERIFIED: same file, lines 30-31]`

```csharp
if (definition.InputType == InputType.Action)
    return result;
```

The guard reads the **definition's** `InputType`, *not* the `inputType` **parameter** that every
subsequent branch switches on. So `Compute(def, inputType: InputType.Toggle, …)` where
`def.InputType == InputType.Action` returns an empty list regardless of the parameter. That is either
intentional (the row VM always passes the definition's own type) or a latent bug — **either way it is
exactly the kind of edge a Phase 5 characterization test must pin**, because Phase 5's CORE-04
extraction will move this logic into a shared primitive and could "fix" it silently. Recommend one test
named for it.

`BadgePillState` (the return element) is Core, so a test needs no App type beyond the calculator:

`[VERIFIED: src/AkariTool.Core/Features/Common/Models/BadgePillState.cs:17-22]`

```csharp
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None);
```

### The two csproj shapes to mirror

`[VERIFIED: both read in full]`

`tests/AkariTool.Core.Tests/AkariTool.Core.Tests.csproj` — 23 lines, `Microsoft.NET.Sdk`:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  <PackageReference Include="FluentAssertions" Version="8.7.1" />
  <PackageReference Include="NSubstitute" Version="5.3.0" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="../../src/AkariTool.Core/AkariTool.Core.csproj" />
</ItemGroup>
```

`AkariTool.Infrastructure.Tests.csproj` is byte-identical except it adds a second `ProjectReference`
and has 24 lines. **All five package versions are already in the local NuGet cache** — no restore risk.
The new project differs only in: `UseWinUI=true` (D-01), `ProjectReference` to
`../../src/AkariTool.App/AkariTool.App.csproj`, and the `.Tests` namespace suffix on the folder.

### `InternalsVisibleTo`: **not needed**

`SettingBadgeCalculator` is `public`. `AkariTool.App.csproj` has **no** `InternalsVisibleTo` block, so
none is required for Phase 1. If a future phase needs it, the precedent is
`AkariTool.Core.csproj:9` (`<InternalsVisibleTo Include="AkariTool.Core.Tests" />`) and
`AkariTool.Infrastructure.csproj:11`. **Recommendation: do not add it in Phase 1** — an unused
`InternalsVisibleTo` is a hole in the App assembly's surface for no benefit, and D-01's remit is
minimal (D-04).

### ⚠ The real D-01 risk: `UseWinUI=true` on a class library

D-01 locks `UseWinUI=true` on the test project. Reading the WinUI 2.3.0 targets:

`[VERIFIED: %USERPROFILE%\.nuget\packages\microsoft.windowsappsdk.winui\2.3.0\build\Microsoft.UI.Xaml.Markup.Compiler.interop.targets:330-333]`

```xml
<!-- Exe's don't have a PriIndexName -->
...
<PriIndexName Condition="'$(AppxPackage)' != 'true' and '$(ManagedAssembly)' != 'false' and '$(OutputType)' != 'winmdobj'">$(TargetName)</PriIndexName>
```

The PRI-index machinery runs for **any** non-`winmdobj` output — **including a `UseWinUI=true` class
library**. That is the same machinery behind PRI175/PRI252, which ROADMAP attributes to
"`vendor/WinUI.Framework`'s PRI is not generated at the expected path". So **adding
`AkariTool.App.Tests` with `UseWinUI=true` could introduce new PRI175/PRI252 errors into the very
baseline it is meant to help measure** — and per D-13/D-14 the gate would then fail on its own
instrument.

Mitigations the planner should choose between, in descending order of preference:

| Option | Effect | Cost |
|---|---|---|
| **(a) Drop `UseWinUI=true` from the test project** | The badge test touches **no** XAML type; a plain `Microsoft.NET.Sdk` library with a ProjectReference to `AkariTool.dll` compiles and runs the test. No PRI machinery, no MSIX tooling, no XAML compiler. | **Deviates from D-01's literal wording** — needs an explicit deviation note. |
| (b) Keep `UseWinUI=true`, add `<EnableMsixTooling>false</EnableMsixTooling>` and `<WindowsPackageType>None</WindowsPackageType>` | Matches the spike's shape; reduces but does not prove the PRI surface away. | None; still an unverified risk. |
| (c) Keep D-01 exactly as written | Honours the lock. | Baseline may shift the moment the project is added. |

**Recommendation: raise (a) as a documented deviation in the plan.** D-01's *stated rationale* is
("It tests only App logic that needs no live `DispatcherQueue` and no XAML visual tree") — and
`SettingBadgeCalculator` satisfies that rationale with **no** `UseWinUI` at all. `UseWinUI=true` is
incidental to the rationale, not required by it. If the planner keeps `UseWinUI=true` (option b/c), the
SPIKE-01 baseline task **must run its first `/t:Rebuild` capture *after* `AkariTool.App.Tests` is added**
— otherwise the recorded baseline is stale the moment Phase 1 lands. That ordering constraint applies
regardless of which option is chosen.

**This is not verified by a build.** Whether a `UseWinUI=true` library can reference a self-contained
`WinExe` at all is the single largest untested assumption in D-01. It is cheap to test (add the project,
build it) and should be the **first** task in the phase, not the last — if it fails, D-01 needs a
fallback and the fallback is D-01's own rejected option (compile-link), which D-01 calls "costly".

**Mitigating evidence already in hand** (so the risk is *reduced*, not *absent*):
- csproj-set properties do **not** flow to `ProjectReference`s, so `AkariTool.App.csproj`'s
  `WindowsAppSDKSelfContained=true` will not reach the test project — the class-library hard error does
  not fire via the reference.
- `Microsoft.UI.Xaml.Markup.Compiler.BeforeCommon.targets:22` forces
  `WindowsAppSDKSelfContained=false` in the markup-compile step.
- `Microsoft.WinUI.NET.Markup.Compiler.targets:53` has an `OutputType=='Library'`-specific branch, so
  library output is anticipated by the SDK, not an error case.
- `AkariTool.App`'s `project.assets.json` contains **both** `net10.0-windows10.0.26100.0` and
  `net10.0-windows10.0.26100.0/win-x64` targets, so a non-RID test project can resolve the reference
  (the classic "assets file doesn't have a target for …/win-x64" failure does not apply).

### Existing test-suite shape to match

`[VERIFIED: 18 distinct test classes in the TRX `className` attributes]`

- **8** in `AkariTool.Core.Tests`: `BackupResultTests`, `BadgePillStateTests`, `SettingCatalogValidatorTests`,
  `SettingDefinitionModelTests`, `SettingDefinitionToggleStateTests`, `BuildVersionGateTests`,
  `UpdateModelsTests` (+1 more).
- **10** in `AkariTool.Infrastructure.Tests`: `ComboBoxResolverTests`, `HardwareCompatibilityFilterTests`,
  `WindowsUpdatePolicyHandlerTests`, `PowerPlanComboBoxServiceTests`, `PowerPlanHelperTests`,
  `SettingDependencyResolverTests`, `SettingOperationExecutorTests`, `SettingStateReaderTests`,
  `SystemBackupServiceParsingTests`, `WindowsCompatibilityFilterTests`, `UpdateServiceTests`.
- Conventions: `tests/<Project>/Features/…` mirroring source, `.Tests` namespace suffix,
  `{TypeUnderTest}Tests.cs`, `{Method}_{Condition}_{Expected}` methods, private static factory helpers at
  the top of the class, `[Theory]`+`[InlineData]` for matrices, no teardown, no fixture classes, no
  coverage gates. `SettingBadgeCalculatorTests` should live at
  `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` in namespace
  `AkariTool.App.Tests.ViewModels.Tweaks`.
- **TESTING.md's documented run command is `dotnet test`, which violates the VS-MSBuild-only
  constraint.** D-03 already resolves this in favour of MSBuild + VSTest. The plan should not
  reintroduce `dotnet test` anywhere, including in comments.

---

## Don't Hand-Roll

| Problem | Don't build | Use instead | Why |
|---|---|---|---|
| Locating the MSBuild / vstest binaries | Hardcoded `C:\Program Files\...\Community\...` paths | `vswhere.exe -requires Microsoft.Component.MSBuild -property installationPath`, then join `MSBuild\Current\Bin` / `Common7\IDE\CommonExtensions\Microsoft\TestWindow` | The documented path is already wrong on this machine. Hardcoding breaks the moment the VS edition or root changes — and nine later Build Gates cite this script. |
| Parsing the test count | `Select-String "Total tests:"` over console output | TRX `/logger:trx` + `TestRun/ResultSummary/Counters/@total` | The console summary is locale- and verbosity-dependent; `Counters` is a stable attribute contract. Verified working. |
| Counting build warnings | Grepping console text for `warning` | MSBuild `-clp:WarningsOnly` (or a binlog via `-bl`) parsed deterministically, plus a binary-logger artifact for forensics | Console text interleaves per-project summaries and the same warning repeats once per project; a text count is not the "solution-wide" number D-14 specifies. |
| Enumerating Akari's setting IDs | A hand-written list of 15 factory names | Reflection over `public static` zero-parameter methods in `AkariTool.Core`, filtered by return type | D-09's entire point. A hand list is the "second place to update" the milestone exists to prevent. |
| Enumerating Winhance's setting IDs | Regex over Akari-style `Build()` bodies | Regex/`Id = "…"` over the `*Optimizations.cs` / `*Customizations.cs` / `*Definitions.cs` files in Winhance's `Models/` folders | Winhance has no `Build()` seam to reflect over; its data is literal. D-09's text-parsing objection applies to Akari's *computed* IDs, not Winhance's literals — and the asymmetry is real, not an inconsistency. |
| Detecting duplicate setting IDs | Anything new | The **existing, already-written** `SettingCatalogValidator.Validate(IEnumerable<SettingGroup>)` in Core | It already enforces global ID uniqueness, registry-path shape and ComboBox mapping integrity. Zero `src/` call sites (CONCERNS #8) — Phase 2 wires it; Phase 1 must not reimplement it. |
| Version discovery for SPIKE-02 packages | Guessing a plausible version string | The `index.json` + `.nuspec` under `api.nuget.org/v3-flatcontainer/` | A plausible-but-invented version is far more costly than an admitted unknown: the whole point of SPIKE-02 is that the answer is currently unverified. |
| Restricting NuGet supply-chain exposure | Manually auditing every transitive package | Exact-version `PackageReference` pins + `dotnet list package --vulnerable` (NuGet audit) after restore + **delete the spike project** once the verdict is written (D-05) | NuGet packages execute MSBuild `.props`/`.targets` at build time — that *is* the code-execution surface. D-05 already deletes the vector. |

---

## Runtime State Inventory

Not a rename/refactor/migration phase in the storage sense, but four of the five categories carry
findings that affect Phase 1's execution. All five answered explicitly.

| Category | Question | Finding | Action required |
|---|---|---|---|
| **Stored data** | Does any database/datastore hold setting IDs or a build baseline? | **None.** No database in the project. Winhance ships three `.winhance` config files as `EmbeddedResource`; Akari has no equivalent and `.winhance` import is Out of Scope. The Phase 1 baseline is a **new** file (`tools/baseline.json`), not a migration. | None. Create `tools/baseline.json`. |
| **Live service config** | Does any service outside git hold state this phase reads or writes? | **None relevant.** No CI (`.github/` absent), no DB, no cloud. `C:\Program Files\Akari Tool\` holds an installed copy of the app (locale folders) — irrelevant to the build baseline, and must not be read as the build output. | None. Build the baseline from `bin/`, not from the installed app. |
| **OS-registered state** | Task Scheduler / services / registry keys embedding the build definition? | **Not verified and not needed.** The baseline gate is a local command. Nothing is registered at OS level. (The *app's* own registrations — HKLM `SOFTWARE\AkariTool`, `%ProgramData%\AkariTool` — are untouched by Phase 1.) | None. |
| **Secrets / env vars** | Any secret key or env var whose name would break on a rename? | **None.** No `.env`, no secrets manager, no `NuGet.config` (so no package-source credentials). `tools/run-tests.ps1` needs no credentials. Note: the NuGet cache and `vswhere` are per-user paths — a script that hardcodes `%USERPROFILE%` breaks under a different account; derive from `$env:USERPROFILE` at runtime. | None. Use runtime path derivation. |
| **Build artifacts** | What installed/built artifacts still carry stale state? | `bin/` and `obj/` are populated for all 7 existing projects and **pre-date this session** — the existing `project.assets.json` files are usable as-is. **`bin/DeElevated/`** at the repo root is a leftover from `build-deelevated.ps1` (`/p:DeElevatedTest=true`) and contains a *second* copy of every assembly — it will double-count an assembly inventory. `.gitignore` already covers `bin/`, `obj/`, `[Dd]ebug/`, `[Rr]elease/`, `bin/DeElevated/`. | **Exclude `bin/DeElevated/` from the assembly inventory.** D-13's `/t:Rebuild` will refresh everything else. |

---

## Common Pitfalls

### Pitfall 1: Hardcoding the documented MSBuild path
**What goes wrong:** the script exits immediately — the documented
`C:\Program Files\Microsoft Visual Studio\18\Community\...` path does not exist. This is not
hypothetical: it is the current state of this machine.
**Why it happens:** `AGENTS.md`'s STACK block and `01-CONTEXT.md` both record a Visual Studio **Community**
install; the machine actually has **Visual Studio Build Tools 2026** under `Program Files (x86)`.
**How to avoid:** resolve via `vswhere.exe` on every run; hardcode only as a last-resort fallback, and
fail loudly with the vswhere output when discovery returns nothing.
**Warning signs:** `Test-Path` returns False; `vswhere -all` returns one `BuildTools` install.

### Pitfall 2: Gating on MSBuild's exit code
**What goes wrong:** either the gate fails on the tolerated PRI175/PRI252 errors, or someone widens the
allowlist / adds `/p:...` to force exit 0 and a real compile error hides behind it — the exact failure
D-16 names.
**Why it happens:** the natural reading of "build succeeded" is `$LASTEXITCODE -eq 0`, but the build is
*expected* to report PRI errors while still emitting all six assemblies.
**How to avoid:** pass = *(MSBuild reported errors) AND *(every error matches the PRI175/PRI252
allowlist)* AND *(warning count ≤ baseline)* AND *(expected assembly set present on disk)*. Parse the
**error list**, never the exit code.
**Warning signs:** a gate that "always fails"; an allowlist that grows.

### Pitfall 3: Passing `WindowsAppSDKSelfContained` as a global property
**What goes wrong:** all five class libraries hard-error with *"WindowsAppSDKSelfContained should not
be applied to a class library."* before compiling anything.
**Why it happens:** `/p:` properties are **global** and flow into every `ProjectReference`; csproj-set
properties do not. `build-installer.ps1` passes `/p:AkariPublish=true` precisely because of this
(`AkariTool.App.csproj:27-34` documents the trap) — the same reasoning applies to any future edit of
the test runner.
**How to avoid:** never add `/p:SelfContained=true` or `/p:WindowsAppSDKSelfContained=true` to the
baseline command.
**Warning signs:** five identical errors, one per library.

### Pitfall 4: Trusting an incremental build for warning counts
**What goes wrong:** a false green. MSBuild re-emits warnings only for files it recompiles, so
`/t:Build` on a warm tree reports near-zero warnings and the gate passes on a tree full of them.
**Why it happens:** incremental build is the default and feels fast; the under-report is silent.
**How to avoid:** D-13 — always `/t:Rebuild`. Non-negotiable, and the reason D-13 exists.
**Warning signs:** warning count drops to 0 or near-0 on a warm tree.

### Pitfall 5: Counting assemblies by globbing `bin/`
**What goes wrong:** a meaningless number. `src/AkariTool.App/bin/.../win-x64/` holds **224** dll/exe
files because `WindowsAppSDKSelfContained=true` ships the whole WindowsAppSDK payload.
**Why it happens:** `WindowsAppSDKSelfContained` copies native + managed runtime DLLs next to the EXE.
**How to avoid:** assert the **expected 8 project assemblies by exact path**, and separately assert that
no *expected* assembly is missing — never assert a total count of `*.dll`.
**Warning signs:** a count in the hundreds; `DirectML.dll` in an assembly list.

### Pitfall 6: `bin/DeElevated/` double-counting
**What goes wrong:** every assembly appears twice in the inventory.
**Why it happens:** `build-deelevated.ps1` writes a full second copy to repo-root `bin/DeElevated\`.
**How to avoid:** restrict the inventory to `src/*/bin/...` and `tests/*/bin/...` under
`Debug\net10.0-windows10.0.26100.0\`.
**Warning signs:** exactly double the expected count.

### Pitfall 7: SPIKE-03 covering 11 of 15 catalog entry points
**What goes wrong:** the D6 evidence base silently omits the SoftwareApps domain — the **largest** one
(~254 items, 606 raw ID literals). The report would say "Akari and Winhance agree" about a domain it
never looked at.
**Why it happens:** D-09 says "the 11 real catalog `Build()` methods", and that count is correct for what
it describes (11 `Build()` methods do exist) while wrong for what SPIKE-03 needs (every setting ID).
SoftwareApps uses four `Get*()` factories instead, so it is invisible to a `Build()`-shaped search.
**How to avoid:** the generator reflects over **all 15** public static zero-parameter catalog
factories in `AkariTool.Core` and dispatches on the return type (`IReadOnlyList<SettingGroup>` vs
`AppGroup`). State the 15-entry-point count in the generator header.
**Warning signs:** a report with no SoftwareApps section; domain totals summing to ~615 rather than ~1,220.

### Pitfall 8: A raw set-diff on Customize IDs
**What goes wrong:** the report declares the Customize domain ~100% divergent on both sides, and the D6
fork decision is made on an artefact that measures a naming convention, not a format incompatibility.
**Why it happens:** Akari prefixes the domain (`customize-explorer-show-file-extensions`); Winhance
suffixes the page name (`explorer-customization-shortcut-suffix`). Optimize and SoftwareApps share a
scheme, so the bug is invisible there.
**How to avoid:** emit both a **raw** and a **normalised** diff, and print the normalisation rule in the
report header. Classify into `raw-equal / normalised-equal / Akari-only / Winhance-only /
both-but-normalised-conflict`.
**Warning signs:** Akari-only ≈ Winhance-only ≈ the whole Customize domain, with zero
`normalised-equal` rows.

### Pitfall 9: Believing `SettingPageWarmUp` is a reusable test seam
**What goes wrong:** the generator needs a live DI container, drags in the App + Infrastructure +
`vendor/WinUI.Framework` graph, and hits the `ServiceLocator` entanglement CONCERNS #9 records.
**Why it happens:** it *is* the runtime-enumeration precedent, and reading it as a precedent invites
reading it as a seam.
**How to avoid:** use the 15 static zero-parameter factories. They need no DI, cannot drift from the
catalog, and live entirely in Core.
**Warning signs:** a generator that imports `AkariTool.Services` or `Microsoft.Extensions.DependencyInjection`.

### Pitfall 10: The new test project perturbing the baseline it measures
**What goes wrong:** `AkariTool.App.Tests` with `UseWinUI=true` picks up `PriIndexName` machinery and
emits **new** PRI175/PRI252 errors, so the very first baseline capture is already stale.
**Why it happens:** `PriIndexName` is set for any `OutputType != 'winmdobj'` when `AppxPackage != true`
— libraries included.
**How to avoid:** two independent measures — (i) capture the baseline **after** the test project exists,
and (ii) strongly consider dropping `UseWinUI=true` from the test project (the badge test needs no XAML),
documented as a deviation from D-01's letter that preserves its stated rationale.
**Warning signs:** the baseline document's warning count differs before and after the test project lands.

### Pitfall 11: Copying AGENTS.md's test counts into the baseline
**What goes wrong:** the baseline records 53 + 136 and every later phase's gate fails on the first run
for the wrong reason.
**Why it happens:** both AGENTS.md and TESTING.md are stale. Measured: **93 + 137 = 230**.
**How to avoid:** record only what the gate itself measured. If the numbers disagree, the gate wins.
**Warning signs:** baseline says 189; vstest says 230.

### Pitfall 12: Guessing a `CommunityToolkit.WinUI` version
**What goes wrong:** a plausible-but-invented version string makes the spike fail at restore, and the
result gets recorded as "the controls don't render" — a false verdict on a hard gate for Phases 5 and 9.
**Why it happens:** training data is stale and the 8.2/8.3 trains differ in a way that matters
(§SPIKE-02).
**How to avoid:** the version set in §SPIKE-02 is registry-verified. If the spike must deviate, record
the *restore* failure separately from the *render* verdict.
**Warning signs:** `NU1102` (version not found) reported as a compatibility failure.

---

## Validation Architecture

`workflow.nyquist_validation: true` and `workflow.security_enforcement: true` in `.planning/config.json`
(both read this session) — so this section and the Security Domain section are both required.

### Test Framework

| Property | Value |
|---|---|
| Framework | xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.5 + `Microsoft.NET.Test.Sdk` 17.14.1; FluentAssertions 8.7.1; NSubstitute 5.3.0 |
| Config file | none — default xUnit discovery. Both existing csproj files have no `xunit.runner.json`. |
| Build tool | `MSBuild.exe` 18.10.1.42706 via `vswhere` discovery (**never** `dotnet build`) |
| Test host | `vstest.console.exe` 18.10.0 (x64) from the VS/BuildTools install |
| Quick run command (existing projects) | `& $vst 'tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll' /Platform:x64` |
| Full suite command (the phase's deliverable) | `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` |
| Baseline-compare mode (the phase's deliverable) | `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline` |
| Measured current state | **230 tests, 229 passed, 1 `NotExecuted`** (`UpdateServiceTests.CheckAsync_LiveCall_NotRunInUnitTests`) |
| Elevation | required in practice — the suite reads registry state; this session is elevated |

### Phase Requirements → Test Map

| Req ID | Behaviour being decided | Test type | Automated command | Failure signal | Exists? |
|---|---|---|---|---|---|
| **SPIKE-01** | Warning count did not increase; no non-allowlisted error; all 8 expected assemblies emitted | **integration / gate** (not a unit test) | `tools\run-tests.ps1 -Mode Baseline` | **Exit code ≠ 0**, plus a printed diff line naming the metric that moved (`warnings 118 -> 121`) and, for errors, the offending line(s) not in the allowlist. Silent pass prints `PASS (warnings 118<=118, errors 2 allowlisted, tests 232>=230)`. | ❌ **Wave 0** — script is the deliverable |
| **SPIKE-02** | `SettingsCard`, `DataGrid`, `WrapPanel` render under WinAppSDK 2.3.1 | **build gate (automatable) + manual visual (not automatable)** | Build: `& $msb <spike>.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:WindowsAppSDKSelfContained=true` *(WinExe — safe, see Pitfall 3)* | MSBuild exit ≠ 0, or `MC`/XAML-compiler `error XLSQ…`/`error WMC…` naming the unresolved control | ❌ **Wave 0** — spike is the deliverable |
| **SPIKE-02** | *(launch proxy)* the spike window opens with all three controls present and does not die on XAML load | **smoke (automatable proxy)** | `Start-Process <spike>.exe -PassThru`, `Start-Sleep 5`, assert `-not $p.HasExited` | Process exits within 5 s → almost certainly a `XamlParseException` / `TypeLoadException` / theming failure at load. Also assert a screenshot exists so the human step has something to look at. | ❌ **Wave 0** |
| **SPIKE-02** | *(render)* the three controls are **visible and correctly themed** | **MANUAL — HUMAN EYE** | **none** | *No command can distinguish "renders correctly" from "instantiated but invisible/mis-themed".* Human opens the spike window and records one of: `all three render` / `<control> fails: <observed symptom>` / `all three render via <named substitute>`. The verdict is written to `.planning/phases/01-baseline-spikes-test-harness/` per D-15. | ❌ manual gate |
| **SPIKE-03** | The committed report reflects the current catalog | **drift check (gate)** — *not* a divergence gate, so D-10 is respected | `tools\gen-setting-id-diff.ps1` then `git diff --exit-code -- <report.md> <ids/*.txt>` | Non-zero from `git diff --exit-code` → the catalog changed and the evidence base is stale. Re-run and commit. | ❌ **Wave 0** — generator is the deliverable |
| **SPIKE-03** | The generator reads all 15 catalog entry points | **unit (self-test)** | `vstest <gen>.Tests.dll /Platform:x64` if the generator ships with a test project; otherwise assert inside the generator | A per-domain count of 0 in the report → a missing entry point. Simplest honest form: the generator *fails loudly* if any of the 15 factories is absent or any domain yields 0 IDs. | ❌ **Wave 0** |
| **TEST-01** | `SettingBadgeCalculator.Compute` behaves as characterised | **unit** | `& $vst 'tests\AkariTool.App.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.App.Tests.dll' /Platform:x64` | Exit ≠ 0, or TRX `Counters/@failed > 0` / `@notRunnable > 0`. | ❌ **Wave 0** — `SettingBadgeCalculatorTests` is the deliverable |
| **TEST-01** | The project builds and is discoverable by `vstest` | **build + discovery gate** | `& $msb AkariTool.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64` then the vstest line above | MSBuild error, **or** vstest reports `No test is available` / `0 tests` — the second is the specific signal that the adapter (`xunit.runner.visualstudio`) did not load, which is the classic failure when `IsTestProject` or the runner package is missing. | ❌ **Wave 0** |

**On the SPIKE-02 honesty requirement.** D-06 sets the bar at "compile + launch + visually render" and
explicitly rejects compile-only. The table above is therefore deliberately split: *compile* and *launch
survival* have real automatable gates with real failure signals; *render* does not, and I have not
invented a command for it. The narrowest honest proxy is the 5-second launch-survival check plus a
committed screenshot for the human step. A pass on the automated checks is a **necessary, not sufficient**
input to the verdict — the plan's SPIKE-02 task must not be allowed to close on the build gate alone.

### Sampling Rate

- **Per task commit:** `& $vst <that task's test dll> /Platform:x64` (single assembly, ~1–2 s for the
  existing suites; measured 230 tests in 1.28 s).
- **Per wave merge:** `tools\run-tests.ps1` — full MSBuild `/t:Rebuild` + all test assemblies + a
  baseline comparison. This is the *only* rate at which a warning count is trustworthy (D-13).
- **Phase gate:** `tools\run-tests.ps1 -Mode Baseline` green, SPIKE-02's human verdict written,
  SPIKE-03's report committed and `-Mode Baseline` re-run after any catalog change. Required before
  `/gsd-verify-work`.

### Wave 0 Gaps

- [ ] `tools/run-tests.ps1` — the runner + `-Mode Baseline` compare (D-03, D-12, D-13, D-14, D-16).
      **Must** use `vswhere` discovery, `/t:Rebuild`, a PRI175/PRI252 error allowlist matched against the
      error *list* (not the exit code), and TRX `Counters` parsing.
- [ ] `tools/baseline.json` — the machine-readable baseline (assembly list, warnings, errors, test count,
      optionally per-assembly split). Recorded **after** `AkariTool.App.Tests` exists.
- [ ] `tools/gen-setting-id-diff.ps1` (name is the agent's discretion per D-15) — reflects over the
      **15** static catalog factories, reads the committed Winhance snapshot, emits the markdown report
      plus machine-readable ID sets.
- [ ] Winhance ID snapshot file + its generator (D-08). The snapshot header must record the Winhance
      commit SHA read (`git -C <winhance> rev-parse HEAD`) so the evidence is attributable.
- [ ] `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj` + `ViewModels/Tweaks/SettingBadgeCalculatorTests.cs`
- [ ] SPIKE-02 throwaway project + its written verdict document. **Not** added to `AkariTool.sln`.
- [ ] Baseline document, SPIKE-02 verdict, SPIKE-03 report — all in
      `.planning/phases/01-baseline-spikes-test-harness/` (D-15).

**Ordering constraint (important):** the SPIKE-01 baseline capture must run **after**
`AkariTool.App.Tests` is added to the solution, or the recorded warning/error/test counts are stale on
arrival (Pitfall 10). The plan's wave structure should put the test project in Wave 0/1 and the
baseline capture last.

### What is genuinely NOT automatable

`SPIKE-02`'s render check. Also **SPIKE-01's warning baseline is only as trustworthy as the machine it
was captured on** — a different VS Build Tools version, a different Windows SDK, or a different NuGet
cache can shift warning counts without any code change. There is no CI (verified: no `.github/`, no
pipeline file) and the deferred-ideas list explicitly puts CI out of scope, so the baseline is a
single-machine reference. The plan should say so in the baseline document's header, or a future phase on
a different machine will chase a phantom regression.

---

## Security Domain

`workflow.security_enforcement: true`, `security_asvs_level: 1`, `security_block_on: "high"`.
Threat *modelling* is the planner's job; these are the facts it needs.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard control for this phase |
|---|---|---|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | **yes (narrow)** | `tools/run-tests.ps1` must not be runnable by an unprivileged user *expecting* registry-touching tests to pass, and must not silently report green when elevation is absent. Recommend an explicit elevation precondition check with a clear message. |
| V5 Input Validation | **yes** | The SPIKE-03 generator parses an **external repository's C# source**. Treat every parsed token as untrusted data: never `Invoke-Expression`, never interpolate a parsed string into a command, never write a parsed string to a path without validating it. Emit only sorted, escaped, `key=value` / JSON data. |
| V6 Cryptography | no | — (No new crypto. Note below: Winhance pins `System.Security.Cryptography.Xml 10.0.7` to floor out two named CVEs; **Akari does not carry that pin** — pre-existing, out of scope, recorded because a supply-chain review will ask.) |
| V8 Data Protection / V12 Files & Resources | **yes** | The generator writes committed files. It must overwrite only its own known output paths and must not glob-and-delete. |

### Supply-chain facts (SPIKE-02)

| Fact | Evidence | Consequence |
|---|---|---|
| **NuGet packages execute MSBuild code at build time.** A package's `build/*.props` and `build/*.targets` are imported into the consuming project. There is **no `postinstall` script mechanism** in NuGet — the `npm view <pkg> scripts.postinstall` check from the standard protocol has no NuGet analogue and is **not applicable**. | The standard legitimacy protocol's Step 3 does not apply. The equivalent surface is the package's `build/` and `buildTransitive/` folders. | The planner's threat model should target `.props`/`.targets` import, not install scripts. |
| `gsd-tools query package-legitimacy check` **does not support the `nuget` ecosystem.** Usage error: `Usage: gsd-tools package-legitimacy check --ecosystem <npm\|pypi\|crates> <pkg1> ...` | Ran it this session with `--ecosystem nuget`; it failed. | The Package Legitimacy Audit below was produced from the **NuGet v3 registration API** instead (owners, publish dates, project URLs) — a different and arguably stronger provenance source for NuGet than the npm/pypi heuristics the seam implements. |
| All 13 candidate packages are **Microsoft-owned** (`Microsoft.Toolkit`, `Microsoft`) and map to public Microsoft/CommunityToolkit GitHub repositories. | `registration5-gz-semver2` authors + projectUrl, fetched this session. See the table in §SPIKE-02. | Low provenance risk. |
| `CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2` was **last published 2021-11-18** — nearly five years stale. | Registration API. | Not a slopsquat risk (Microsoft-owned, and it is the *only* 7.x/8.x DataGrid that exists), but it is an **unmaintained** dependency. A `NU1901`/`NU1902`-style advisory appearing against it in future is plausible. Record the version choice and its rationale in the SPIKE-02 verdict. |
| **Version pinning is the whole control.** The repo has **no `NuGet.config`** and **no `packages.lock.json`**; `project.assets.json` is MSBuild-generated and not checked in. | Verified: no `NuGet.config` anywhere in the repo; no lock file; AGENTS.md confirms the assets file is not checked in. | Without a lock file, the transitive graph is not reproducible. For the **spike** this is acceptable (it is deleted). For any **adopted** package, the plan should require exact-version pins (no ranges, no floating) and a `dotnet list package --vulnerable` check after restore. |
| `Microsoft.Windows.SDK.BuildTools` latest stable is **`10.0.28000.2705`**, while Akari resolves **`10.0.26100.4654`**. | Registration API + `project.assets.json`. | A *floating* reference to this package would silently upgrade the SDK toolchain and change the build. Pin exactly, or do not add it (Akari does not need to — see §SPIKE-02). |
| D-05 already eliminates the persistent vector: the spike project is **deleted** once the verdict is written. | CONTEXT.md D-05. | After SPIKE-02, none of these packages remain in the repo. The threat is confined to the spike's own lifetime. |

### Test-binary execution facts (SPIKE-01, TEST-01)

| Fact | Evidence | Consequence |
|---|---|---|
| `vstest.console.exe` **executes repo-built assemblies** in the current process context. | Standard VSTest behaviour; the run this session executed 230 tests including `SettingStateReaderTests` and `SettingOperationExecutorTests`. | The test host runs with the invoking user's token. |
| This session's shell is **elevated** (`IsInRole(Administrator)` → True), and AGENTS.md lists "Administrator privileges (to run tests and debug app)" as a platform requirement. | Verified this session. | `tools/run-tests.ps1` will typically execute **elevated**. The existing suite contains registry-reading and (per `SystemBackupServiceParsingTests`) `vssadmin`-parsing tests. Running it elevated is the intended posture, but the plan's threat model should state that the gate executes elevated code and that `tools/` scripts are a privileged surface. |
| `bin/DeElevated/` contains a **second, `asInvoker`-manifested copy** of every assembly, produced by `build-deelevated.ps1` (`/p:DeElevatedTest=true`). | `AkariTool.App.csproj:40-52` documents the `IntermediateOutputPath` redirect; the directory exists at repo root. | A test runner that globs `**\*.Tests.dll` would pick up **de-elevated duplicates** and could double-count or run against the wrong manifest. The runner must target the exact `Debug\net10.0-windows10.0.26100.0` paths. This is a correctness issue *and* a small integrity issue (running a manifest you did not intend to run). |
| The vstest invocation writes a TRX containing **test names, outcomes and stack traces** to a caller-specified directory. | Verified this session. | Point `/ResultsDirectory` at a gitignored or temp path. Do **not** commit TRX files — they are machine- and run-specific and would make every commit noisy. |

### Licensing facts (project constraint, restated for the threat model)

- The SPIKE-03 generator reads `C:\Users\isleap\Documents\GitHub\Winhance` and must **never write to it**.
  A generator that takes a configurable Winhance path is a write-risk; hardcode it read-only or open
  files with read-only intent and add a comment.
- **No new file may be copied from Winhance** (PolyForm Shield 1.0.0 noncompete + required notice). The
  committed snapshot must be *data Akari derived by parsing*, not a copied file. Keep the snapshot's
  format minimal and Akari-authored (sorted ID lists), and record the source commit SHA for attribution
  rather than reproducing Winhance's file content wholesale.
- Winhance's own `Winhance.UI.csproj` carries a `Copyright` line and a `System.Security.Cryptography.Xml`
  CVE-floor pin. Neither should be reproduced in Akari's artefacts.

---

## Package Legitimacy Audit

> **The standard `gsd-tools query package-legitimacy check` gate could not be run**: the seam accepts
> `--ecosystem <npm|pypi|crates>` only, and this phase's ecosystem is NuGet. Ran it and captured the
> usage error. The audit below was produced instead from the **NuGet v3 registration API**
> (`registration5-gz-semver2`), which reports package **owners, publish dates and project URLs** —
> the NuGet-native equivalent of a provenance check, and a *stronger* signal than download counts for
> the question actually at issue (are these the real Microsoft packages?).

| Package | Registry | Age (latest stable) | Owners | Source Repo | Verdict | Disposition |
|---|---|---|---|---|---|---|
| `CommunityToolkit.WinUI.Controls.SettingsControls` 8.2.251219 | NuGet | ~9.5 mo (2025-12-20) | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows | **OK** | Approved for spike. Exact pin. |
| `CommunityToolkit.WinUI.Controls.Primitives` 8.2.251219 | NuGet | ~9.5 mo (2025-12-20) | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows | **OK** | Approved for spike. Exact pin. |
| `CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.2 | NuGet | **~4.8 yr (2021-11-18)** | `Microsoft.Toolkit`, `CommunityToolkit.Common` | github.com/CommunityToolkit/WindowsCommunityToolkit | **OK (with note)** | Approved for spike. **Unmaintained** — the only DataGrid that exists. Record the version + rationale in the verdict. |
| `Microsoft.Windows.CsWinRT` 2.3.1 | NuGet | ~2.5 mo (2026-07-22) | `Microsoft` | github.com/microsoft/cswinrt | **OK** | Approved. Must be added **explicitly** — no WCT package pulls it. |
| `CommunityToolkit.WinUI.Triggers` 8.2.251219 | NuGet | ~9.5 mo | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows | **OK** | Transitive. Do not pin separately unless needed. |
| `CommunityToolkit.WinUI.Extensions` 8.2.251219 | NuGet | ~9.5 mo | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows | **OK** | Transitive. |
| `CommunityToolkit.WinUI.Helpers` 8.2.251219 | NuGet | ~9.5 mo | `Microsoft.Toolkit` | github.com/CommunityToolkit/Windows | **OK** | Transitive. |
| `CommunityToolkit.Common` 8.2.1 | NuGet | — | `Microsoft`, `Microsoft.Toolkit`, `CommunityToolkit.Common` | github.com/CommunityToolkit/dotnet | **OK** | Transitive. |
| `Microsoft.Xaml.Behaviors.WinUI.Managed` 3.0.0 | NuGet | — | `Microsoft` | go.microsoft.com/fwlink/?LinkID=651678 | **OK** | **Avoid** — only reachable via `CommunityToolkit.WinUI.Behaviors`, which the spike does not need. |
| `Microsoft.Windows.SDK.BuildTools` | NuGet | — | `Microsoft` | aka.ms/WinSDKProjectURL | **OK but NOT NEEDED** | **Do not add.** Already resolved at `10.0.26100.4654`; latest is `10.0.28000.2705` — adding risks an unintended toolchain upgrade. |
| `Microsoft.WindowsAppSDK.WinUI` (component pkg) | NuGet | — | `Microsoft` | — | **OK but FORBIDDEN in this phase** | **Never add as a direct reference.** WCT 8.3 previews require it, which is precisely the MSB4011 dual-family hazard. Pin the spike to 8.2.251219. |

**Packages removed due to a SLOP verdict:** none — no package in the candidate set failed; all are
Microsoft-owned. (The `gsd-tools` seam could not have made this determination for NuGet in any case.)

**Packages flagged as suspicious:** none.

**Cross-ecosystem confusion check:** N/A in the usual npm/PyPI sense, but the analogous hazard *did*
occur and is worth recording — **`CommunityToolkit.WinUI` (the metapackage id) tops out at 7.1.2 while
`CommunityToolkit.WinUI.*` sub-packages are at 8.2.251219.** A plan that writes
`<PackageReference Include="CommunityToolkit.WinUI" Version="8.2.251219" />` would fail restore. The
8.x train must be referenced per package. The candidate `<ItemGroup>` in §SPIKE-02 is written to avoid
exactly this.

**Postinstall-script check:** **not applicable to NuGet.** There is no package-install script hook;
the equivalent code-execution surface is the package's `build/` + `buildTransitive/` MSBuild files,
which are imported into the consuming project. D-05 (delete the spike) bounds this.

---

## State of the Art

| Old approach | Current approach | When changed | Impact on this phase |
|---|---|---|---|
| `dotnet test` as the test runner (documented in TESTING.md) | MSBuild `/t:Rebuild` + `vstest.console.exe` | D-03, this milestone | The documented command **cannot** be used: `dotnet build` fails on WinUI 3 PRI/resource targets. TESTING.md's run command is wrong and must not be copied into any task. |
| Hardcoded VS install path | `vswhere.exe -requires Microsoft.Component.MSBuild` | The documented path is already broken on this machine | `tools/run-tests.ps1` must discover, not hardcode. |
| `dotnet test --logger trx` + `--collect` | `vstest.console.exe /logger:trx /ResultsDirectory:` | — | Verified working; the TRX `Counters` element is the machine-readable seam. |
| Console-output scraping for counts | Structured artefacts (TRX `Counters`, MSBuild binlog via `-bl`, `-getProperty`) | MSBuild 17.8+ added `-getProperty`/`-getTargetResult`; present in 18.10 | D-12's "machine-checked" is achievable with structured output, not text matching. |
| Reading Winhance's catalogs by copying files | Parse → emit Akari's own snapshot | PolyForm noncompete (project constraint) | The snapshot is derived data, never a copied file. |
| `CommunityToolkit.WinUI` 7.x (last release 2021-11-18) | `CommunityToolkit.WinUI.*` 8.2.251219 (2025-12-20) with DataGrid stranded at 7.1.2 | WCT 8.x reorganised and never ported DataGrid | Confirmed by version enumeration. Drives SPIKE-02's package set. |
| 8.2.251219 depending on the WinAppSDK **metapackage** | 8.3.260402-preview2 depends on the WinAppSDK **component** package | 8.3 preview train (2026-04) | **8.3 is a trap for a project on the 2.3.1 metapackage.** Pin 8.2.251219. |

**Deprecated/outdated:**
- `CommunityToolkit.WinUI` metapackage — no 8.x exists. Any instruction to "add CommunityToolkit.WinUI"
  is unactionable as written.
- `Microsoft.Windows.CsWinRT 2.0.4` — pinned only in `vendor/WinGet.Interop`; superseded by 2.1.1 in the
  resolved graph and by 2.3.1 stable upstream. Not this phase's cleanup (deferred, per CONTEXT).
- `Microsoft.Windows.SDK.BuildTools 10.0.26100.1` — the floor declared by `FluentIcons.WinUI` and
  `Material.Icons.WinUI3`; already superseded at 10.0.26100.4654 in Akari's graph.
- TESTING.md's `dotnet test` run command and its "Core 53 / Infrastructure 136" counts — both stale.
- AGENTS.md's MSBuild path, its "no `LICENSE` file exists" claim (a `LICENSE` **is** present at repo
  root), and its "`global.json` pin 10.0.102" claim (no `global.json` exists; installed SDK is 10.0.401).

---

## Assumptions Log

| # | Claim | Section | Risk if wrong |
|---|---|---|---|
| A1 | A clean `/t:Rebuild` of `AkariTool.sln` exits **non-zero** because of PRI175/PRI252 while still emitting all assemblies. | Toolchain | **HIGH.** This is inferred from ROADMAP's wording plus the presence of emitted `.pri` files — **not observed**, because I did not run a build (that is SPIKE-01's deliverable). If the build actually exits 0, the gate design should simplify to exit-code-plus-warning-count; if it exits non-zero with *other* errors too, the allowlist needs more entries than D-16 allows. **Verify this first** — the gate's shape depends on it. |
| A2 | A `UseWinUI=true` class library can ProjectReference a self-contained `WinExe` and build. | TEST-01 | **HIGH.** Not tested. D-01's largest untested assumption. Mitigating evidence exists (§TEST-01) but is not proof. If false, D-01's fallback is its own rejected compile-link option, which D-01 calls costly. **Test this before anything else in the phase.** |
| A3 | `SettingBadgeCalculator.Compute`'s branches are all reachable with hand-built `SettingDefinition` fixtures and **no** OS access. | TEST-01 | LOW. Verified pure by reading all 384 lines; the one external call (`NumericConversionHelper`) is a static unit conversion. |
| A4 | A `vstest.console.exe` invocation with **zero** arguments beyond the DLLs needs no further flags on this machine. | SPIKE-01 | LOW. Verified this session (`/Platform:x64` + two DLL paths + trx logger). `/Platform:x64` is the one flag worth keeping, since App is `Platforms=x64` only. |
| A5 | The Winhance checkout at `C:\Users\isleap\Documents\GitHub\Winhance` is at a commit whose IDs the snapshot will record. | SPIKE-03 | LOW. Directory and file listing verified. The snapshot must capture `git rev-parse HEAD` at generation time so the evidence is attributable — otherwise the report is unattributable. |
| A6 | `CommunityToolkit.WinUI.* 8.2.251219` + `Microsoft.Windows.CsWinRT 2.3.1` restores cleanly against `Microsoft.WindowsAppSDK 2.3.1`. | SPIKE-02 | MEDIUM. Dependency floors verified from `.nuspec`; **the restore itself is untested**. A `NU1605`/`NU1102`/`MSB4011` at restore time is a spike finding, not a research finding. |
| A7 | The three controls' *rendering* behaviour under 2.3.1 is unknown. | SPIKE-02 | **This is the phase's point.** Stated as an assumption only so no plan treats the package set as a compatibility guarantee. |
| A8 | Warning counts are stable across runs on this machine. | SPIKE-01 | MEDIUM. No CI, single machine, no lock file. A NuGet cache difference or a Windows SDK update shifts them. The baseline document must state it is a single-machine reference. |
| A9 | The baseline-gate script will be run **after** `AkariTool.App.Tests` is added. | Validation | MEDIUM. If not, the recorded counts are stale on arrival (Pitfall 10). This is an ordering constraint on the plan, not a research fact. |
| A10 | `AkariTool.App.Tests` needs no `InternalsVisibleTo`. | TEST-01 | LOW. `SettingBadgeCalculator` is `public`; verified. Only changes if the test target moves. |

---

## Open Questions (RESOLVED — all six dispositions recorded inline)

> **Resolution status:** closed. Every question below carries its disposition inline as
> **`→ RESOLVED`**, naming the plan task that answers it. None was left open for planning to proceed
> against: where a question is a build-time fact, the disposition is "this phase measures it, at task
> X" — the honest answer rather than a guess.

1. **Does a clean `/t:Rebuild` succeed, and with exactly which errors?**
   - *What we know:* ROADMAP says six assemblies are emitted and that PRI175/PRI252 are tolerated
     errors; the output dir contains `AkariTool.pri` (2.3 MB) and a near-empty `WinUI.Framework.pri`
     (960 B), consistent with ROADMAP's stated root cause.
   - *What's unclear:* the actual MSBuild exit code and the complete error list. Whether the only errors
     are PRI175/PRI252, or whether NU/MSB/PRI errors of other kinds also appear.
   - *Recommendation:* the SPIKE-01 task captures the full error list verbatim on its first run and
     records it in the baseline document. If the allowlist needs entries beyond D-16's two, **stop and
     escalate** — widening it is explicitly forbidden and would indicate a real problem.
   - *Why I did not answer it:* the instructions assign the baseline numbers to the phase, and a full
     rebuild is a build-time cost. This is the one question I would most want answered first.
   - **→ RESOLVED by measurement, not by research.** This is a build-time fact and cannot be answered
     from the filesystem. `01-02` Task 2 (`-Mode Record`) captures the exit code and the complete error
     list verbatim, and its **Escalation branch** stops before committing if any code other than
     `PRI175`/`PRI252` from `WINAPPSDKGENERATEPROJECTPRIFILE` appears. `01-02` Task 3 then drives a
     synthetic `CS1002` line through the same classifier to prove the allowlist rejects it. The
     recommendation is adopted as written; the escalation branch is its planned expression.

2. **Can a `UseWinUI=true` test library reference `AkariTool.App` at all — and does it add new PRI errors?**
   - *What we know:* `PriIndexName` is assigned for any `OutputType != 'winmdobj'`, so the PRI machinery
     does run for libraries. Mitigating: csproj properties don't flow through `ProjectReference`;
     `BeforeCommon.targets:22` forces `WindowsAppSDKSelfContained=false`; the markup-compiler has a
     Library branch; App's assets file has a RID-agnostic target.
   - *What's unclear:* whether it builds, and whether it changes the warning/error baseline.
   - *Recommendation:* make this the phase's first task. Test D-01 as literally written; if it fails or
     perturbs the baseline, fall back to dropping `UseWinUI=true` (which the badge test does not need)
   and record the deviation. **This is cheap to test and expensive to get wrong late.**
   - **→ RESOLVED by measurement, not by research.** Also build-time only. `01-01` Task 3 builds the
     project exactly as D-01 specifies, records the observed outcome in the commit message, and takes
     the documented fallback (remove `UseWinUI`, preserving D-01's rationale) if the build fails or new
     PRI175/PRI252 errors appear. The solution-scope half of the question — does the error-code set or
     the warning count move — is a hard assertion at the per-wave rate in `01-01`'s `<verification>`
     block, so it is a recorded observation rather than an assumption. The recommendation is adopted as
     written and is, as advised, the first thing the phase does once the runner exists.

3. **Should SPIKE-02's CsWinRT be 2.3.1 or 2.2.0?**
   - *What we know:* WCT 8.2 needs ≥ 2.2.0 per Winhance's comment (a csproj comment, not a nuspec
     constraint — no WCT 8.2 package declares CsWinRT at all). Both 2.2.0 and stable 2.3.1 exist.
   - *What's unclear:* whether CsWinRT 2.3.1's targets are compatible with WCT 8.2.251219's WinRT
     component metadata. Genuinely unknowable without trying.
   - *Recommendation:* try 2.3.1 first (current stable), then 2.2.0. Treat a swap as a **substitute
     observation** for the verdict, not root-cause diagnosis — D-07-compliant.
   - **→ RESOLVED by decision.** `01-03` pins `Microsoft.Windows.CsWinRT 2.3.1`, and its Task 3
     substitute step records `2.2.0` as the first substitution to attempt if the 2.3.1 pairing
     misbehaves. Under D-07 a swap is explicitly a substitute observation rather than root-cause
     diagnosis, so the fallback already sits inside the task's remit and needs no extra allowance. The
     recommendation is adopted as written; 2.3.1 is the planned value.

4. **Is Akari's `customize-*` ID prefix scheme a deliberate earlier decision?**
   - *What we know:* Akari uses `customize-explorer-*`; Winhance uses `explorer-customization-*`.
     Optimize and SoftwareApps match Winhance exactly.
   - *What's unclear:* whether the Customize divergence is an intentional Akari convention or drift from
     the port. This materially affects how SPIKE-03 should present the Customize section — a deliberate
     convention deserves a different report annotation than an accident.
   - *Recommendation:* the report emits raw + normalised diffs regardless (that is safe under either
     answer), and the generator's header states the rule. If the planner wants the *intent* recorded,
     `docs/import-review-proposal.html` and `AKARI_ARCHITECTURE_PLAN.md` at the repo root are the places
     to look. I did not read them — out of scope for a toolchain-verification pass.
   - **→ RESOLVED by design, and the answer is deliberately not required.** `01-04` Task 3 emits the raw
     pass, the normalised pass and a per-domain token-overlap percentage with both rules printed in the
     header — the presentation that is correct under either answer. No task states whether the scheme is
     *intentional*, because no plan in this phase is entitled to that claim and no measurement of the
     catalogue can establish intent. The report therefore reports the divergence and annotates the
     mechanism, never the motive; a reader who needs the intent must look it up in the two documents
     named above.

5. **Does the baseline need to be machine-pinned, and to what?**
   - *What we know:* no CI exists; the gate is a local command; there is no lock file; `project.assets.json`
     is not checked in. Warning counts can shift with the VS Build Tools version, the Windows SDK, or the
     NuGet cache without any code change.
   - *What's unclear:* whether the team wants the baseline to record toolchain identity (MSBuild version,
     Windows SDK version) as a *gated* field or merely as documentation.
   - *Recommendation:* **record it, do not gate it** (consistent with D-14's tolerance for recording
     per-project detail). A gated toolchain version would make the gate fail for a reason that is not a
     regression. The deferred-ideas list already puts CI out of scope.
   - **→ RESOLVED by decision, consistent with D-14.** `01-02` Task 1 records toolchain identity as
     provenance (`toolchain.msbuildVersion`, `vstestVersion`, `visualStudioInstallPath`,
     `sdkBuildToolsVersion`) and never as a gate; `01-02` carries the negative case as its own
     prohibition row, on the grounds that a gated toolchain version would fail the gate for something
     that is not a regression — the same class of false green D-13 exists to prevent. `01-02` Task 2's
     `01-BASELINE.md` states the single-machine-reference caveat in its first paragraph, per
     `01-VALIDATION.md`'s "Genuinely not automatable" note. The recommendation is adopted as written.

6. **Should `SettingCatalogValidator` be wired into the SPIKE-03 generator, or left entirely to Phase 2?**
   - *What we know:* it is `public static` in Core with `Validate(IEnumerable<SettingGroup>)`, and it
     already enforces global ID uniqueness. It has **zero** `src/` call sites. D-10 forbids SPIKE-03
     asserting anything; BUG-02 is Phase 2's job.
   - *What's unclear:* whether the generator should *report* uniqueness violations as informational
     output (not a gate), which would give Phase 2 a warm start without moving its requirement.
   - *Recommendation:* **do not call it in Phase 1.** D-04 keeps Phase 1 to four requirements and a
     stable test count; calling the validator turns the report into a partial BUG-02. Note in the
     generator header that the `SettingGroup` walk is validator-ready for Phase 2.
   - **→ RESOLVED by decision, consistent with D-04 and D-10.** `01-04` Task 1 carries "MUST NOT call
     `SettingCatalogValidator`" as a prohibition *and* carries the hand-over as a requirement:
     `WalkSettingGroups` emits `(GroupName, Id)`-shaped output with a comment stating it is
     validator-ready for Phase 2's BUG-02 test to consume unchanged. The uniqueness signal Phase 2
     needs is preserved rather than deferred — the enumerator keeps duplicate multiplicity in its
     emitted lists and reports every duplicate under the JSON `duplicates` key and the report's
     **duplicate-IDs** section, which is the input Phase 2's uniqueness assertion will consume. The
     recommendation is adopted as written.

---

## What Might I Have Missed

- **I did not build anything.** No `/t:Rebuild`, no restore of a new project. Every toolchain claim is
  from executable/version probes, target-file reads, and one zero-build `vstest` run against existing
  artefacts. The two highest-impact unknowns (Open Questions 1 and 2) both require a build and are
  flagged as such rather than guessed.
- **I did not read the Winhance catalog bodies**, only their headers, ID literal samples and file
  inventory. A snapshot script that needs to know Winhance's *nesting* (are IDs ever computed or
  templated there?) is unvalidated. Akari's side is safe — the reflective seam reads real objects.
- **`AkariTool.sln` build-configuration sections were not inspected.** Whether every project has a
  `Debug|x64` mapping is assumed. Worth a glance when the project is added to the solution, since
  `AkariTool.App.csproj` is `Platforms=x64` only.
- **`vendor/WinUI.Framework.csproj` was not read.** It is in the assembly inventory (ROADMAP requires it)
  and is absent from the solution (CONCERNS #7). Whether a `/t:Rebuild` of the *solution* rebuilds it or
  relies on a stale `bin/` copy is untested — and given the near-empty `WinUI.Framework.pri` (960 B) and
  ROADMAP's stated PRI root cause, this is plausibly connected. **The planner should confirm
  `WinUI.Framework.dll`'s timestamp advances on a solution rebuild**, because a stale one would make the
  baseline document a record of a fiction.
- I read `.claude/settings.local.json`'s existence but not its contents — it is machine-local
  permissions, not project convention.

---

## Sources

### Primary (HIGH confidence) — verified this session

- `vswhere.exe -all -products * -format json` — the single VS-family install on this machine
  (Build Tools 2026 18.10.3, `Program Files (x86)`), and the absence of any Community install.
- `MSBuild.exe -version -nologo` → 18.10.1.42706; `MSBuild.exe -help -nologo` for the switch inventory.
- `vstest.console.exe` execution against both existing test assemblies → `VSTest version 18.10.0 (x64)`,
  230/229/1, TRX written and parsed.
- `src/AkariTool.App/obj/project.assets.json` — the resolved package graph (39 libraries) including
  `Microsoft.Windows.CsWinRT/2.1.1` and `Microsoft.Windows.SDK.BuildTools/10.0.26100.4654`.
- `%USERPROFILE%\.nuget\packages\microsoft.windowsappsdk.base\2.0.4\build\Microsoft.WindowsAppSDK.Base.targets:19-20`
  — the class-library `WindowsAppSDKSelfContained` hard error, verbatim.
- `%USERPROFILE%\.nuget\packages\microsoft.windowsappsdk.winui\2.3.0\build\Microsoft.UI.Xaml.Markup.Compiler.interop.targets:330-333`
  — the `PriIndexName` condition, verbatim.
- `api.nuget.org/v3-flatcontainer/<id>/index.json` and `<id>.nuspec` for all 13 candidate packages.
- `api.nuget.org/v3/registration5-gz-semver2/<id>/index.json` — owners, publish dates, project URLs.
- Source files read in full: `SettingBadgeCalculator.cs` (384 lines), `SettingPageWarmUp.cs` (59),
  `AkariTool.App.csproj` (97), both test `.csproj` files, `BaseDefinition.cs`, `SettingGroup.cs`,
  `AppModels.cs` (partial), `BadgePillState.cs`, `Winhance.UI.csproj`, `Winhance.Core.csproj`,
  `ExternalAppCatalog.cs`, `WindowsAppCatalog.cs` (head).
- Grep-derived but header-confirmed: the 11 `Build()` methods with declaring type, namespace and line.
- `.planning/config.json`, `.planning/ROADMAP.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`,
  `.planning/codebase/CONCERNS.md`, `.planning/codebase/TESTING.md`.
- `git ls-files`, `git status --porcelain`, `git log --oneline -3` — trackedness and clean-tree state.
- `api.nuget.org` reachability: `HTTP 200` on the flatcontainer index query.

### Secondary (MEDIUM confidence)

- `docs/` directory listing — confirms D-15's rationale that `docs/` is the GitHub Pages site.
- Raw `Id = "…"` literal counts per catalog file on both sides — **upper bounds**, used only to size
  domains and to prove the SoftwareApps gap. Not the report's numbers.
- `01-CONTEXT.md`'s landmine table — used as the starting hypothesis; **three of its seven rows are
  contradicted** by `project.assets.json` (see the correction block in `<user_constraints>`).

### Tertiary (LOW confidence) — marked for validation

- `[ASSUMED]` MSBuild exits non-zero on the PRI errors (Open Question 1). Inferred from ROADMAP's
  wording and the emitted `.pri` files; not observed.
- `[ASSUMED]` A `UseWinUI=true` library can reference a self-contained `WinExe` (Open Question 2).
  Not tested; mitigating evidence only.
- `[ASSUMED]` The 8.2.251219 + CsWinRT 2.3.1 + WinAppSDK 2.3.1 combination restores without NU/MSB
  errors. Dependency floors verified; the restore is not.
- `[ASSUMED]` Winhance's setting IDs are plain string literals throughout. Sampled across 4 files in
  3 domains; the snapshot generator must tolerate a computed ID if one exists.

### Note on tool-strategy compliance

The `research-plan` seam routed two of six questions to **Context7**, which is not available to this
subagent (no `mcp__context7__*` tools). Both were answered instead from the **NuGet registry's own
flatcontainer and registration APIs** plus the **on-disk `.nuspec` and MSBuild `.targets` files** in the
local package cache. For the specific claims at issue — exact version strings that exist, declared
dependency floors, and target-file behaviour — those sources are *more* authoritative than
documentation prose, and both are first-party. `classify-confidence --provider webfetch` returns LOW by
default, so the digests were stored with `--confidence HIGH` on the grounds that the underlying sources
are the registry and the shipped files themselves, cross-checked against each other.

---

## Metadata

**Confidence breakdown:**

| Area | Level | Reason |
|---|---|---|
| Standard stack (SPIKE-01 toolchain) | **HIGH** | Every path, version and flag probed directly; one real `vstest` run executed and parsed. The only gap is the build's exit code (Open Question 1). |
| Standard stack (SPIKE-02 packages) | **HIGH** for metadata / **NONE** for compatibility | Version lists, dependency floors, owners and publish dates are registry-verified. Whether the controls render is the spike's deliverable and is explicitly not asserted. |
| Architecture (SPIKE-03 seam) | **HIGH** | 11 `Build()` methods read header-by-header; 4 `Get*()` factories read; both model shapes read; `SettingPageWarmUp` read in full and rejected as a seam with reasons. |
| Architecture (TEST-01) | **HIGH** for the target / **MEDIUM** for the project wiring | `Compute`'s signature, accessibility, purity and Infrastructure dependency all verified by reading the file. The *buildability* of D-01's wiring is untested. |
| Pitfalls | **HIGH** | Each is anchored to a verified fact (a missing path, a target-file error string, a measured count, a file's contents) rather than to a generalisation. |

**Research date:** 2026-10-05
**Valid until:** 2026-11-04 (30 days). The NuGet package set is the fastest-moving input — the 8.3
preview train could ship a stable release inside that window, which would re-open the component-package
question. Re-verify §SPIKE-02's version table before the spike task executes.
