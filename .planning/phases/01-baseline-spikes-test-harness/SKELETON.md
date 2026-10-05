# Walking Skeleton — Akari Tool

**Phase:** 01 — Baseline, Spikes & Test Harness
**Generated:** 2026-10-05
**Status:** Planning complete; execution begins with plan `01-01`.

---

## Capability Proven End-to-End

> One command rebuilds the solution with VS MSBuild, runs every test assembly through
> `vstest.console.exe`, and prints a **measured** test count that came back from the real toolchain.

This is a measurement-instrument project, so the skeleton is the measurement loop rather than a user
feature. Its thinnest slice is deliberately the smallest loop that returns a real number rather than a
canned one:

| Element | Choice |
|---|---|
| **Assembly under test** | `tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll` — already exists and already runs; the loop is proven against real output before anything new is added |
| **Command** | `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` |
| **Thin slice complete means** | MSBuild resolved through `vswhere`; `PRI175`/`PRI252` errors classified as tolerated while any *other* error fails; the expected assemblies confirmed present by exact path; `vstest.console.exe` run against exact `Debug\net10.0-windows10.0.26100.0` paths; TRX `TestRun/ResultSummary/Counters/@total` read and printed; exit 0 |

**Thin slice complete does *not* mean the gate exists.** The loop above is plan `01-01`'s tracer. The
machine-checked gate — rebuild-forced, baseline-compared, allowlisted, non-zero on any increase — is
plan `01-02`, and it is a separate slice precisely because a reporting loop and a gating loop have
different failure modes and must be provable independently.

**Every subsequent task in this phase is additive to that loop**, never parallel to it:

```
01-01  test project + runner  →  the loop exists
01-02  -Mode Baseline gate    →  the loop becomes the gate
01-03  spike + verdict        →  independent instrument; own project, own build
01-04  ID-diff generator      →  reads the loop's build output (Core.dll), writes its own artefacts
```

---

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Build tool | **VS `MSBuild.exe` only**, discovered through `vswhere.exe` on every run | `dotnet build` fails on WinUI 3 PRI/resource targets. The path named in `AGENTS.md` (`…\Visual Studio\18\Community\…`) **does not exist** on this machine — the only install is Visual Studio Build Tools 2026 under `Program Files (x86)`. Nine later phases' Build Gate lines cite `tools/run-tests.ps1`, so a hardcoded path would be a roadmap-wide fragility. |
| Test host | **`vstest.console.exe`** from the same VS install (`Common7\IDE\CommonExtensions\Microsoft\TestWindow\`) | Counts come from the TRX `Counters` element, never from scraping `Total tests:` console text, which is verbosity- and locale-dependent. |
| Runner location and name | **`tools/run-tests.ps1`** | `tools/` because the runner is durable code that nine later phases still cite after the phase directory is archived on milestone close; the runner is one script with a `-Mode` parameter rather than two scripts, so a Build Gate names exactly one command. Confirmed at `01-01` Task 1 before the name is cited anywhere. |
| Build verdict semantics | The build is **expected to exit non-zero**; pass = *no non-allowlisted error* ∧ *warning count did not increase* ∧ *assemblies emitted* ∧ *tests green* | PRI175/PRI252 are tolerated pre-existing errors (per D-16) while every assembly still emits. A `$LASTEXITCODE -eq 0` gate would either fail on the tolerated errors or invite someone to widen the allowlist until the exit code is zero — which is the precise failure D-16 forbids. |
| Error allowlist | Exactly two entries — `WINAPPSDKGENERATEPROJECTPRIFILE` + `PRI175`, + `PRI252`. A literal, with no parameter and no sidecar file | D-16. Bounded on both sides: an unexpected code is a finding to escalate, not a tolerance to add. |
| Assembly inventory | Twelve named repo-relative paths, `Test-Path`-asserted | The self-contained App output holds **224** dll/exe files because the whole WinAppSDK runtime payload is copied; a `*.dll` count is meaningless, and `bin\DeElevated\` holds a fourth duplicate set. |
| Rebuild policy | `/t:Rebuild`, always, for every baseline capture | MSBuild re-emits a warning only for files it actually recompiles, so an incremental build reports near-zero warnings on a warm tree — a false green worse than no gate (per D-13). |
| Gate tolerance | "No increase, solution-wide"; reductions pass silently; not per-project | D-14. Phase 2's expected deletions of dead code must not become extra work. |
| Test-assembly wiring | `AkariTool.App.Tests` ProjectReferences `AkariTool.App` directly, `UseWinUI=true` | Per D-01. Chosen over compile-linking App sources (breaks every `InitializeComponent`, makes the file list a hand-maintained exclusion set) and over pulling App logic down into Core/Infrastructure (belongs to Phase 3/4). The `UseWinUI` property is **under observation**: `PriIndexName` is assigned for any non-`winmdobj` output, so the first task builds it and records the outcome; the documented fallback is to drop the one property, which the badge test does not need and which preserves D-01's stated rationale. |
| Test scope in this phase | `SettingBadgeCalculatorTests` only | Per D-04. The real-catalog validity test is Phase 2's BUG-02; the container-resolution test is Phase 3's TEST-02; the Core-references test is Phase 3's TEST-03. Landing any of them here would move another phase's requirement and destabilise the baseline test count nine phases compare against. |
| SPIKE-02 isolation | A throwaway project under `tools/spike/`, **never** added to `AkariTool.sln`, **deleted** once the verdict is written | Per D-05. A failing package set inside `AkariTool.App` would break the shipping build; a permanent canary would silently re-open the question on every package bump. Isolation is structural, not disciplinary — the same lesson as the existing `build-deelevated.ps1`. |
| SPIKE-02 package set | Exact pins: `Microsoft.WindowsAppSDK` 2.3.1 (metapackage), `Microsoft.Windows.CsWinRT` 2.3.1 (explicit), `CommunityToolkit.WinUI.Controls.SettingsControls` 8.2.251219, `CommunityToolkit.WinUI.Controls.Primitives` 8.2.251219, `CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.2 | Registry-verified 2026-10-05. The 8.3 preview train depends on the component package `Microsoft.WindowsAppSDK.WinUI` instead of the metapackage — the MSB4011 dual-family hazard. `CommunityToolkit.WinUI` (the metapackage id) has no 8.x. `Microsoft.Windows.SDK.BuildTools` is already resolved transitively at 10.0.26100.4654 and must not be added. |
| SPIKE-02 verdict bar | Compile **+** launch survival **+** human visual render; any failing control gets a substitute **proved to render on the same page**; root-cause diagnosis explicitly out of scope | Per D-06, per D-07. A version-skewed toolkit resolves at compile time and fails at XAML-load or theming time — exactly what a build gate cannot see. `workflow.human_verify_mode = end-of-phase`, so the render step is an `<auto>` task carrying `<verify><human-check>` rather than a mid-flight checkpoint. |
| SPIKE-03 Akari-side seam | Reflection over **15** static catalog factories in `AkariTool.Core` — 11 `Build()` returning `IReadOnlyList<SettingGroup>` plus 4 `Get*()` returning `AppGroup` | Per D-09. The Akari side therefore cannot drift from the catalog. The count is 15, not the 11 in D-09's wording: SoftwareApps has no `Build()` method at all and is the largest domain, so an 11-entry diff would silently omit roughly 55% of Akari's IDs. D-09's *method* is unchanged; only its count was wrong. |
| SPIKE-03 Winhance-side seam | A **committed snapshot** of sorted ID lists, generated by a committed read-only script that records the Winhance commit SHA | Per D-08. The report then runs anywhere and cannot silently change when the Winhance checkout is updated. The snapshot is data Akari derived by **parsing** — never a copied file — because Winhance is PolyForm Shield 1.0.0 with a noncompete clause and a required notice. |
| SPIKE-03 host language | A tiny .NET 10 `Exe` (`tools/SettingIdDiff`) with **no** `ProjectReference`, loading `AkariTool.Core.dll` by path via `Assembly.LoadFrom` | Windows PowerShell 5.1 cannot load a `net10.0-windows` assembly and `pwsh` is not installed, so a compiled host is the only way to run .NET 10 code from a committed script. No `ProjectReference` means no rebuild of Core, so the tool cannot race the solution build and cannot drift from the assembly the gate measures. |
| SPIKE-03 report shape | Raw **and** normalised diff, five mechanical buckets, per-domain token-overlap percentage, no timestamp | D-10 makes it a measurement, not a gate. The normalised pass exists because Akari prefixes the domain (`customize-explorer-*`) where Winhance suffixes the page name (`explorer-customization-*`); a raw diff alone would report Customize as near-total divergence and mislead the D6 fork decision. No timestamp keeps the artefact git-diffable (per D-11). |
| Artefact home | Durable code in `tools/`; this phase's records (baseline, SPIKE-02 verdict, SPIKE-03 report) in `.planning/phases/01-baseline-spikes-test-harness/` | Per D-15. Phase directories are archived on milestone close, which would take the runner with it while nine phases still depend on it. `docs/` was rejected: it is the GitHub Pages site. |
| TRX handling | `/ResultsDirectory` defaults to a per-run directory under `$env:TEMP` | TRX files carry machine- and run-specific test names, outcomes and stack traces; committing them would make every commit noisy and mix run state into the tree. |
| CI | **None.** The gate is a local committed command | Verified: no `.github/`, no pipeline file. CI is an explicitly deferred idea, so the baseline is a **single-machine reference** — a different VS Build Tools, Windows SDK or NuGet cache shifts the counts with no code change. `01-BASELINE.md` says so in its first paragraph. |

---

## Stack Touched in Phase 1

- [x] Project scaffold — `tools/run-tests.ps1` (the runner) and `tests/AkariTool.App.Tests/` (the
      seventh project, wired into `AkariTool.sln` under the `tests` folder)
- [x] Test runner — xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.5 + `Microsoft.NET.Test.Sdk` 17.14.1,
      executed by `vstest.console.exe`; FluentAssertions 8.7.1 and NSubstitute 5.3.0 available
- [x] One real read/write of App-layer logic — `SettingBadgeCalculator.Compute`, characterised by real
      assertions including the `definition.InputType == InputType.Action` guard, pill ordering, the
      AC/DC branch, and the `"Minutes"` integer-division boundary at 3600 and 3599
- [x] One real UI interaction — the SPIKE-02 spike window putting `SettingsCard`, `DataGrid` and
      `WrapPanel` on screen, plus the human render check that produces the verdict
- [x] Deployment — *not applicable*: this is a local desktop app. The documented local run command
      **is** the deliverable: `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1`, with
      `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline` as the gate
      nine later phases cite

---

## Out of Scope (Deferred to Later Slices)

Everything below is explicitly **not** in this phase. The list exists so no later phase re-litigates
Phase 1's minimalism.

- **Real-catalog validity test** (enumerate the catalog factories through
  `SettingCatalogValidator.Validate(IEnumerable<SettingGroup>)`) → **Phase 2 / BUG-02**. It is
  CONCERNS §16's top recommendation and every later Build Gate names it, but landing it here would move
  another phase's requirement and destabilise the recorded baseline test count.
- **Container-resolution integration test** → **Phase 3 / TEST-02**. Blocked until Phase 3 creates the
  composition root and removes `ServiceLocator`; there is nothing to resolve before then.
- **Core-references-neither-layer assembly test** → **Phase 3 / TEST-03**.
- **Root-cause diagnosis of a SPIKE-02 incompatibility** → never in this milestone. Phase 1 records the
  substitute and moves on; diagnosing the WinAppSDK version, the CsWinRT gap or the WCT 8.x reorg is
  not this phase's job.
- **Adding `vendor/WinUI.Framework` to `AkariTool.sln`** → Phase 3/4. Phase 1 must not depend on it
  being there, but the baseline document must note its absence as a build risk — and confirm whether
  its assembly timestamp advances on a solution rebuild, since a stale copy would make the baseline a
  record of a fiction.
- **Deleting the dead `Microsoft.WindowsAppSDK` / `CommunityToolkit.Mvvm` package references from
  `AkariTool.Core.csproj`** → unrelated cleanup, **and deliberately excluded here because it would move
  the very warning counts this phase records**.
- **A CI pipeline that runs the baseline gate** → future work. No CI exists and none is created.
- **Catalog content expansion** → out of scope for the whole milestone. The report measures divergence;
  it never closes it.
- **Any `.winhance` import or shared config format** → out of scope by design. Configuration stays
  forked (D6) in Phases 6 and 8.

---

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural
decisions — in particular, without ever hardcoding a toolchain path, without ever gating on
`$LASTEXITCODE`, and without ever replacing the runner.

- **Phase 2** — Dead subsystems & defect repairs. Make Verify actually verify, enforce setting-ID
  uniqueness (the duplicate-reporting seam this phase's generator is shaped to hand over), resolve the
  `AkariPaths` CS0433 ambiguity, delete the dead subsystems, add OTS elevation detection.
- **Phase 3** — Namespace alignment and composition root. Every namespace matches its folder, one
  composition root replaces `ServiceLocator`, and **this phase's SPIKE-03 generator must be updated at
  its namespace-derivation rule** — not by adding a catalog name to a list.
- **Phase 4** — Vertical slices across all three layers. Six domains mirrored in Core, Infrastructure
  and UI; eight legacy top-level App directories retired. Its SPIKE-02 verdict is an input.
- **Phase 5** — Common machinery and shared rendering. **Its first job is to prove
  `SettingBadgeCalculatorTests` still passes unchanged after the CORE-04 badge-primitive extraction** —
  that is why the characterization suite exists.
- **Phase 6** — Preferences and localization. Above every phase that adds a page or a setting, because
  the key surface grows with the catalog.
- **Phases 7–10** — Advanced Tools split; Builder and Config Review modes; Software & Apps machinery
  (a second consumer of the SPIKE-02 `DataGrid` finding); Customize slice completion.

---

## Spec-less probe disposition

This phase has **no SPEC**, so `## Edge Coverage` and `## Prohibitions` were both absent and the
spec-less probe protocol ran. **All nine edge-probe items came back `unresolved`** and one arrived in
the `unclassified` bucket; per protocol none was auto-resolved and none was auto-dismissed. The
no-silent-drop equality holds: **all 9 surfaced items carry an authored `must_haves.truths` entry** —
2 of them as a *narrow* reading of an otherwise category-missed probe — and **3 flagged assumptions are
surfaced on top**: one standalone (item 3, flagged-only, with no acceptance criterion manufactured for
it) and 2 generic readings attached to the two items that also carry a narrow authored resolution.

The honest diagnosis: **the one-line requirement texts in `REQUIREMENTS.md` are what defeated the
probe.** "A generated report diffs every Akari setting ID against Winhance's" admits no edge probe in
the abstract; the edge had to be recovered from the architecture, not from the sentence.

| # | Requirement | Category | Disposition | Where |
|---|---|---|---|---|
| 1 | SPIKE-01 | `unclassified` | **Authored, narrow** + generic reading flagged | Narrow reading — a capture-time/mutation-time adjacency that is load-bearing: the gate must fail when a warning count rises even though every assembly still emits, and the first capture must happen *after* the test project joins the solution or it is stale on arrival. Authored in `01-02` truths. **Generic reading flagged:** the `unclassified` bucket is an artifact of a one-line requirement; a real resolution would need `REQUIREMENTS.md` to state which inputs to the baseline are edge-bearing (e.g. "measured on a clean rebuild", "captured after all test projects exist"). |
| 2 | SPIKE-02 | `empty` | **Authored, narrow** + generic reading flagged | Narrow reading — the spike page must stay launchable with a failing control so the other two remain judgeable, and a degenerate page is a real failure mode. Authored in `01-03` truths (launch-survival is a required automated input to the verdict). **Generic reading flagged:** "empty or null input" is a category-miss for a requirement about whether three named control *types* resolve; a real resolution would need the requirement to state what "renders" means for a control with zero items. |
| 3 | SPIKE-02 | `encoding` | **Flagged — category-miss** | No real question. There is no string length or equality semantics in "renders correctly under WindowsAppSDK 2.3.1"; the axis is visual presence and theming, which the probe taxonomy does not reach. A real resolution would need a rendering-correctness clause in the requirement (visual? themed? correct DPI? correct font?). **Not resolved, and deliberately not manufactured into an acceptance criterion.** |
| 4 | SPIKE-03 | `adjacency` | **Authored** | Real and load-bearing on two counts: a duplicate setting ID must stay **visible** rather than collapsed by set semantics (emitted lists preserve multiplicity and a `duplicates` key records them), and a normalised key claimed by more than one ID on either side is reported as `both-but-normalised-conflict` so the normalised pass is never ambiguous. Authored in `01-04` truths. |
| 5 | SPIKE-03 | `empty` | **Authored** | Real: an empty domain is the exact failure the instrument exists to prevent. The enumerator fails loudly if fewer than 15 factories are found or any domain yields zero IDs, naming what is missing; the diff generator fails on an empty domain. Authored in `01-04` truths and as acceptance criteria on Tasks 1 and 3. |
| 6 | SPIKE-03 | `ordering` | **Authored** | Real: the report and the committed ID sets must use a documented **ordinal** sort, and duplicates must be preserved in a defined order, so `git diff` shows real catalog changes only. A culture-aware sort would make the artefact locale-dependent. Authored in `01-04` truths. |
| 7 | TEST-01 | `adjacency` | **Authored** | Real at the integer boundary: the numeric-range path divides by 60 for `"Minutes"`, so the characterisation suite pins `3600 → 60` **and** the adjacent `3599 → 59`. This is the boundary a Phase 5 CORE-04 refactor of the unit-conversion path would silently move. Authored in `01-01` truths and as behaviour case 9. |
| 8 | TEST-01 | `empty` | **Authored** | Real on two paths: `Compute` returns an empty list when no badge data exists at all (the `hasBadgeData` gate — this is the tracer's smoke test), and a `NumericRange` with null `Units` degrades to an identity conversion rather than throwing. Authored in `01-01` truths and as behaviour cases 2 and 10. |
| 9 | TEST-01 | `ordering` | **Authored** | Real and load-bearing: the order of the returned `BadgePillState` sequence determines what a user sees first, and Phase 5's shared row primitive must not reorder it. The suite asserts the full sequence — kind, `IsHighlighted`, order — not just a count. Authored in `01-01` truths and in behaviour cases 3, 5 and 6. |

### Prohibition recall disposition

`## Prohibitions` was also absent, so the recall protocol ran: *"what could this phase silently become
that the author would **not** want, but nothing forbids?"* Stage 1 over-produced; stage 2 dropped the
routine-engineering items and kept the intent constraints. **28 prohibitions were kept** and every one
is authored into a plan's `must_haves.prohibitions`, descriptor-less so each disposes
flagged-unverified. None was auto-dismissed.

| # | Requirement | Statement (abbreviated) | Plan |
|---|---|---|---|
| 1 | TEST-01 | Phase 1's App test project is `SettingBadgeCalculatorTests` only | `01-01` |
| 2 | TEST-01 | No `InternalsVisibleTo` added to the App assembly "just in case" | `01-01` |
| 3 | TEST-01 | Do not "repair" the `definition.InputType == Action` guard the suite characterises | `01-01` |
| 4 | SPIKE-01 | No `/p:SelfContained` / `/p:WindowsAppSDKSelfContained` global property | `01-01` |
| 5 | SPIKE-01 | No hardcoded Visual Studio path | `01-01` |
| 6 | SPIKE-01 | No committed TRX files | `01-01` |
| 7 | SPIKE-01 | Never derive the verdict from `$LASTEXITCODE` | `01-02` |
| 8 | SPIKE-01 | Never widen the PRI175/PRI252 allowlist | `01-02` |
| 9 | SPIKE-01 | Never hand-edit `baseline.json` or record the stale AGENTS.md counts | `01-02` |
| 10 | SPIKE-01 | Never gate per-project | `01-02` |
| 11 | SPIKE-01 | Never gate on recorded toolchain identity | `01-02` |
| 12 | SPIKE-01 | Never run the gate incrementally or add a skip-rebuild switch | `01-02` |
| 13 | SPIKE-01 | Never wire the gate into CI | `01-02` |
| 14 | SPIKE-02 | Never let the spike's packages touch `AkariTool.App.csproj` | `01-03` |
| 15 | SPIKE-02 | Never leave the spike or a canary page in the repository | `01-03` |
| 16 | SPIKE-02 | Never record a restore/version failure as a rendering verdict | `01-03` |
| 17 | SPIKE-02 | Never close SPIKE-02 on the automated gates alone | `01-03` |
| 18 | SPIKE-02 | Never reference `Microsoft.WindowsAppSDK.WinUI` or use the 8.3 preview train | `01-03` |
| 19 | SPIKE-02 | Never add a floating or explicit `Microsoft.Windows.SDK.BuildTools` | `01-03` |
| 20 | SPIKE-02 | Never write root-cause diagnosis into the verdict | `01-03` |
| 21 | SPIKE-03 | Never enumerate only the 11 `Build()` methods or from a hand-written list | `01-04` |
| 22 | SPIKE-03 | Never report a raw set difference alone | `01-04` |
| 23 | SPIKE-03 | Never turn the report into a gate | `01-04` |
| 24 | SPIKE-03 | Never copy a Winhance file, or write under the Winhance checkout | `01-04` |
| 25 | SPIKE-03 | Never feed a parsed token to `Invoke-Expression`, a command, or a path | `01-04` |
| 26 | SPIKE-03 | Never glob-and-delete; write only generator-owned paths | `01-04` |
| 27 | SPIKE-03 | Never wire `SettingCatalogValidator` into Phase 1 | `01-04` |
| 28 | SPIKE-03 | Never use `SettingPageWarmUp` or the frozen settings registry as the seam | `01-04` |

*(28 kept prohibitions, 28 authored. Duplicate-ID preservation was also surfaced by the edge probe as
item 4 above and is authored as a `truths` entry in `01-04`, not as a prohibition — it is a correctness
obligation rather than an intent constraint.)*

**Canon breadcrumbs** (surfaced, deliberately **not** minted as prohibitions):
path traversal / command injection from untrusted parsed input is canon and is owned by
`/gsd-secure-phase` — though it is *also* given a concrete implementation here via `T-01-09` in
`01-04-PLAN.md`, because the specific input is another repository's C# source and the generic canon
reference would not tell an implementer where the risk actually is.

---

## How to read this skeleton later

- **Plan `01-01`** establishes the loop. **Plan `01-02`** makes it a gate. Neither changes the
  toolchain-discovery or error-classification decisions above.
- The **corrections table** in `01-01-PLAN.md` is the list of upstream facts that are wrong. Any later
  plan or phase that reintroduces the AGENTS.md MSBuild path, the `53 + 136` test counts, the
  "5 projects + 2 folders" solution description, or `dotnet test` as the run command has regressed from
  this document.
- `01-VALIDATION.md` holds the per-requirement verification map and the single **Manual-Only**
  verification (SPIKE-02's render check). Its provisional Task IDs are re-bound to real `{plan}-{task}`
  IDs at execution start.
