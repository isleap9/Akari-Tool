# Phase 01 — Build Baseline (SPIKE-01)

> **This baseline is a SINGLE-MACHINE REFERENCE. Read this before you compare anything to it.**
>
> There is no CI in this repository — verified: no `.github/`, no pipeline file — and CI is an
> explicitly deferred idea (D-15, `01-CONTEXT.md` § Deferred). Every number below was measured on
> **one** machine, with **one** toolchain, on **one** date. A different Visual Studio Build Tools
> version, a different Windows SDK, a different NuGet cache, or a different antivirus definition set
> will move the warning count with **no code change whatsoever**. If you are running on another
> machine, or your SDK has been patched since, this file is not your baseline: record your own with
> `-Mode Record` and compare against that. A red gate on a second machine is a phantom regression, and
> this paragraph exists so nobody wastes a phase chasing one.

Recorded by: `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Record`
Machine-readable form: [`tools/baseline.json`](../../../tools/baseline.json) — **written only by
`-Mode Record`**, never hand-edited. `-Mode Baseline` refuses to compare against a reference file
with no `recordedOn` provenance stamp, so a hand-authored baseline cannot masquerade as a measured
one.

---

## 1. Measured toolchain identity

Recorded **for provenance only. The gate never reads any of it** — a gated toolchain version would
fail the gate for a reason that is not a regression, which is the same class of false green that
D-13 exists to prevent.

| Item | Measured value |
|------|----------------|
| MSBuild | `18.10.1-1.26427.6+3cd27c13eb422ceea9de6fb84d12684e3bcf3188` |
| `vstest.console.exe` | `18.0.11915.232` |
| Visual Studio install path | `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools` |
| Visual Studio edition | **Build Tools 2026** — there is **no Visual Studio IDE** on this machine |
| `Microsoft.Windows.SDK.BuildTools` | `10.0.26100.4654` |
| Configuration / Platform | `Debug` / `x64` |
| Build target used for the capture | `/t:Rebuild` (forced — see §4) |
| Capture timestamp | `2026-10-05T20:09:45Z` |

Both executables are discovered through `vswhere.exe` on **every** run. `tools/run-tests.ps1`
contains no Visual Studio install literal at all, deliberately.

> ⚠ **The MSBuild path in `AGENTS.md` does not exist.** `AGENTS.md:81` names
> `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` — a
> **Community** install under Program Files rather than Program Files (x86). The only VS-family
> install here is Build Tools under `Program Files (x86)`. If you ever "fix" the runner by pasting
> that path in as a constant, every later phase's Build Gate breaks on this machine at once.

---

## 2. The emitted-assembly inventory — twelve paths

`Test-Path`-asserted by the runner on every `-Mode Baseline` run. Twelve, not three: the three test
assemblies prove tests *ran*; the other nine prove the **shipping output emitted**, which is what
"the solution builds" has to mean when a phase's real deliverable is an App-layer change.

| # | Repo-relative path | Present |
|---|--------------------|---------|
| 1 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll` | ✅ |
| 2 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.exe` | ✅ |
| 3 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.Core.dll` | ✅ |
| 4 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.Infrastructure.dll` | ✅ |
| 5 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\WinGet.Interop.dll` | ✅ |
| 6 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\WinUI.Framework.dll` | ✅ |
| 7 | `src\AkariTool.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.pri` — 2,305,600 bytes | ✅ |
| 8 | `src\AkariTool.Core\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.dll` | ✅ |
| 9 | `src\AkariTool.Infrastructure\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.dll` | ✅ |
| 10 | `tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll` | ✅ |
| 11 | `tests\AkariTool.Infrastructure.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.Tests.dll` | ✅ |
| 12 | `tests\AkariTool.App.Tests\bin\x64\Debug\net10.0-windows10.0.26100.0\AkariTool.App.Tests.dll` | ✅ |

**Why the platform segment appears on some rows and not others.** Only the two x64-only projects get
it. `AkariTool.App` declares `Platforms=x64`; `AkariTool.App.Tests` ProjectReferences it, and an MSIL
test assembly referencing an AMD64 one is rejected with `error MSB3270`. Everything else is mapped to
Any CPU. These shapes were read out of the build's own `<Project> -> <output>` console lines, not
inferred.

> ⚠ **Stale sibling trees — a real false green found while building this baseline.**
> `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\` **also exists**, holding a
> complete, plausible-looking `AkariTool.dll` (1,468,928 bytes) last written at 15:13 by an earlier
> *direct-csproj* build that carried no platform. `src\AkariTool.Core\bin\x64\`, and the `bin\x64\`
> variants of Infrastructure and both vendored projects, are the same kind of leftover. Asserting
> the un-segmented path `Test-Path`s true against a stale tree **forever**. The runner asserts the
> `x64`-segmented paths above because those are the ones `/t:Rebuild` on the solution actually writes.
> Do not "simplify" the inventory into a glob or into the un-segmented path.

**`vendor\WinUI.Framework` is absent from `AkariTool.sln` yet emitted.** It — and
`vendor\WinGet.Interop` — reach the output through the App's `ProjectReference`s. **Assembly presence
is what this baseline records, not solution membership.** If you move either project into the
solution (Phase 3/4 territory, CONCERNS #7), this baseline stays valid; if you stop emitting either
one, it correctly goes red.

**`bin\DeElevated\` is excluded, deliberately.** It holds an `asInvoker`-manifested duplicate of
every assembly, produced by `build-deelevated.ps1 /p:DeElevatedTest=true`. Any recursive glob over
`bin\` would assert on — or worse, *execute* — a manifest the runner never intended. No path in the
inventory contains `DeElevated`, and none ever may.

---

## 3. Error and warning counts

| Metric | Measured | Gated? |
|--------|----------|--------|
| Errors parsed from the console log | **0** | see §3.1 |
| Errors allowlisted | **0** | no |
| Errors **not** allowlisted | **0** | **yes — any non-zero fails** |
| Warnings, distinct solution-wide | **16** | **yes — increase fails, reduction passes** |
| Warnings, raw log lines | 28 | no |

### 3.1 `PRI175` / `PRI252` — pre-existing, tolerated, and **not reproducible**

Per D-16 the allowlist tolerates **exactly two** error codes, and only from the
`WINAPPSDKGENERATEPROJECTPRIFILE` target:

```
{ Target = 'WINAPPSDKGENERATEPROJECTPRIFILE'; Code = 'PRI175' }
{ Target = 'WINAPPSDKGENERATEPROJECTPRIFILE'; Code = 'PRI252' }
```

**These are pre-existing and Out of Scope.** They are a known WinUI 3 PRI packaging fragility
(DEFERRED in STATE.md as "WinUI 3 PRI175/PRI252 build failure"), not a code defect, and fixing them
is not a Phase 1 requirement.

> 🔍 **The pair is NOT reproducible run-to-run, and this is load-bearing for how the gate reads.**
> Both errors appear on a **cold** build and are **absent** once
> `vendor\WinUI.Framework\bin\...\WinUI.Framework.pri` (960 bytes) exists on disk — which is exactly
> the state the tree was in when this baseline was captured, which is why the measured error count is
> **zero**. The gate therefore **TOLERATES these codes when present and NEVER REQUIRES them**. A gate
> asserting `errors == 2` would flip between green and red depending on whether a PRI file happened
> to be sitting in the working tree, which is a false signal in both directions. `tools/baseline.json`
> records `errors.allowlistedCount` for provenance and the comparison **never reads it**. The only
> error signal the gate acts on is `errors.notAllowlistedCount`, which must be **0**.

Verbatim text of the tolerated pair, captured from a real build on this machine by deleting
`vendor\WinUI.Framework\bin\**\WinUI.Framework.pri` and rebuilding (see §8, drill 5):

```
WINAPPSDKGENERATEPROJECTPRIFILE : error : PRI175: 0x80070002 - Processing Resources failed with error: The system cannot find the file specified. [C:\Users\isleap\Documents\GitHub\Akari-Tool\src\AkariTool.App\AkariTool.App.csproj]
WINAPPSDKGENERATEPROJECTPRIFILE : error : PRI252: 0xdef00071 - File C:\Users\isleap\Documents\GitHub\Akari-Tool\vendor\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.pri not found. [C:\Users\isleap\Documents\GitHub\Akari-Tool\src\AkariTool.App\AkariTool.App.csproj]
```

Note the shape: the target sits at the **start** of the line, `error` is followed by a **colon**,
and the trailing `[...]` names the **project only** — there is no `::target` suffix. Both PRI lines
were **tolerated**, and the run still exited 0.

> 🔍 **On a genuinely cold tree these two errors are not cosmetic — they CASCADE into a real compile
> error.** Reproduced deliberately (§8, drill 5). `PRI252` reports the vendor PRI missing, which
> makes `WINAPPSDKGENERATEPROJECTPRIFILE` fail, which means **`AkariTool.pri` is never emitted**,
> which means MSBuild does not deliver the App's output to its `ProjectReference` consumers, which
> makes `AkariTool.App.Tests` fail to compile:
>
> ```
> tests\AkariTool.App.Tests\ViewModels\Tweaks\SettingBadgeCalculatorTests.cs(5,17): error CS0234:
> The type or namespace name 'ViewModels' does not exist in the namespace 'AkariTool'
> ```
>
> `CS0234` is **not** on the allowlist, so a cold-tree run goes **RED** — correctly. This is the
> single most important thing to understand about this baseline: **the allowlist tolerates the PRI
> pair, it does not tolerate what the PRI pair causes.** The pair is not "fine"; it is "these two
> specific codes, and everything downstream of them still fails the gate".
>
> ⚠ **Why this tree measures zero errors, and what that means for a fresh clone.** A plain
> `/t:Rebuild` of `AkariTool.sln` builds `vendor\WinUI.Framework` as **Any CPU** into
> `vendor\WinUI.Framework\bin\Debug\…\WinUI.Framework.pri`. The App's PRI step reads the **x64**
> path — `vendor\WinUI.Framework\bin\x64\Debug\…\WinUI.Framework.pri` — which a solution rebuild does
> **not** produce. That file exists on this machine only because plan 01-01's diagnostics built the
> csproj directly with `/p:Platform=x64`. **On a freshly cloned tree it will not exist, and
> `-Mode Baseline` will go red with the cascade above on its very first run.** That red is correct and
> must not be silenced by widening the allowlist. Restoring it is one command:
>
> ```powershell
> & $msb 'vendor\WinUI.Framework\WinUIFramework.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64
> ```
>
> Fixing the underlying PRI fragility is **Out of Scope** for this milestone (DEFERRED in STATE.md as
> "WinUI 3 PRI175/PRI252 build failure"). Recorded here so no later phase mistakes it for a
> regression it introduced.

If **any other error code** ever appears from that target, or from any other target, it is a
**finding to escalate**, not a tolerance to add: record it verbatim here and stop. D-16 forbids
widening the allowlist.

### 3.2 The 16 warnings, by code

| Code | Count | What it is |
|------|-------|-----------|
| `CS0436` | 9 | `AkariPaths` type shadowing in `src/AkariTool.App/Features/Software/SoftwareAppService.cs` — the CS0436 ambiguity Phase 2 is scheduled to resolve |
| `CS8604` | 3 | Possible null reference argument in `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` — the unwired `ILocalizationService` / `SettingStatusBannerManager` / `TechnicalDetailsManager` injection |
| `CS9113` | 3 | Unread parameter — `ChangeHistoryService.cs` ×1, `SystemSettingsDiscoveryService.cs` ×2 |
| `CA2024` | 1 | `WingetPackageInstaller.cs:231` uses `process.StandardOutput.EndOfStream` in an async method |
| **Total** | **16** | |

**These 16 are distinct instances, not log lines.** MSBuild re-emits the same diagnostic once per
consuming project; the raw `WarningsOnly` logger holds **28** lines that collapse to these 16. A raw
line count is *not* the solution-wide number D-14 specifies and would double-count every
transitive-consumer warning, so the gate counts **distinct** `(source location, code, message)` with
the trailing `[<project>]` attribution stripped — that suffix is precisely the part that differs
between duplicate emissions.

> 🔍 **Per-project warning counts are deliberately NOT gated** (D-14). Phase 2 is expected to delete
> dead code; gating per project would turn a legitimate reduction into extra work. The per-code split
> above is recorded so a *change* in composition is legible without being a failure.

---

## 4. Test counts

| Metric | Measured |
|--------|----------|
| Total | **243** |
| Executed | 242 |
| Passed | **242** |
| Failed | **0** |
| Not runnable | **0** |
| Not executed (`NotExecuted` attribute) | 0 — the `Counters` element does not expose it |
| "Ran nothing" (`total − executed`, the measured skip count) | **1** |

`vstest` prints `Skipped: 1` and records that result with outcome `NotExecuted`, but the TRX
`Counters` element has **no `skipped`/`notExecuted` attribute** — so the measured "ran nothing"
figure is derived as `total − executed`. It is derived from the run, never typed in.

### Per-assembly split

| Assembly | Tests |
|----------|-------|
| `AkariTool.Core.Tests` | **93** |
| `AkariTool.Infrastructure.Tests` | **137** |
| `AkariTool.App.Tests` | **13** |
| `<unclassified>` | **0** |
| **Total** | **243** |

The single deliberately-skipped test:

```
AkariTool.Infrastructure.Tests.Services.UpdateServiceTests.CheckAsync_LiveCall_NotRunInUnitTests
```

It performs a live network call and is skipped in unit runs **by design**. `@notExecuted` is
**recorded, never gated** — skipping is not a defect.

> 🔍 **The split is a real join, and it is not the join the script's own comment used to claim.**
> It comes from `Results/UnitTestResult/@testId` + `TestDefinitions/UnitTest[@id]/@storage`, where
> `@storage` is the declaring assembly's output path. There is **no `className` attribute anywhere**
> in the TRX that `vstest.console.exe` 18.10 writes — joining on one silently matched nothing and put
> all 243 results in `<unclassified>` while still looking like a working split. `<unclassified>` is
> kept as a visible bucket precisely so that failure mode shows up instead of hiding.

---

## 5. Stale upstream figures — do not copy these

Neither document was edited; correcting them is not a Phase 1 requirement. Their numbers are
recorded here so they do not propagate into any later artefact.

| Source | It says | Measured |
|--------|---------|----------|
| `AGENTS.md:106` | Core.Tests — **53** passing | **93** |
| `AGENTS.md:107` | Infrastructure.Tests — **136** passing + 1 skipped | **137** + 1 not-executed |
| `AGENTS.md:81` | MSBuild at a **Community** path under `Program Files` | Build Tools under `Program Files (x86)`; that path does not exist |
| `.planning/codebase/TESTING.md:331` | "Core 53 passing; Infrastructure 136 passing + 1 skipped" | same as above |
| `.planning/codebase/TESTING.md:18` | `dotnet test` is the run command | it is not — it fails on WinUI 3 PRI/resource targets; the command is `tools\run-tests.ps1` |
| `AGENTS.md` / solution description | "5 projects + 2 solution folders" | **7 projects** + 2 folders — `AkariTool.App.Tests` was added in plan 01-01 |

### 🔍 The three corrections a future reader is most likely to reintroduce

1. **The non-existent MSBuild path.** `C:\Program Files\Microsoft Visual Studio\18\Community\…`
   does not exist on this machine. `tools/run-tests.ps1` contains **no** Visual Studio path at all, on
   purpose — do not put one back.
2. **The stale test counts.** `53 + 136 = 189`. The measured figure is **243** (93 + 137 + 13). Any
   document recording 189, 230 or 231 is stale; only **243** is current.
3. **The "5 projects + 2 folders" solution description.** It is **7 projects** + 2 folders since plan
   01-01 added `AkariTool.App.Tests`.

---

## 6. What this baseline does **not** cover

- **No CI.** There is no pipeline, and none is added in this phase. The gate is a local committed
  command; a trigger would make a single-machine baseline authoritative on machines it was never
  measured on.
- **Per-project counts are recorded, not gated** (D-14). Only the solution-wide warning total and the
  test total are gated, and only in the directions stated above.
- **Toolchain identity is recorded, not gated.** See §1.
- **`AkariTool.Core`'s dead package references are untouched in this phase** — deliberately.
  Removing the unused `Microsoft.WindowsAppSDK` / `CommunityToolkit.Mvvm` references (CONCERNS #14)
  would move the very warning counts recorded here, and destabilising the baseline for an unrelated
  cleanup is precisely what this phase must not do.
- **`vendor\WinUI.Framework` is still not in `AkariTool.sln`** (CONCERNS #7). The baseline records that
  it emits; it does not record it as a solution member, and Phase 1 does not depend on it being added.
- **No freshness guarantee on assembly timestamps.** The inventory asserts *presence* at exact paths,
  not that each file was rewritten by the current run. See the stale-sibling-tree warning in §2 for
  why the paths, not a glob, are what matter.
- **Nothing about runtime behaviour.** This is a compile-and-test baseline. It says nothing about
  whether the app launches, renders, or applies a setting.

---

## 7. Re-verification

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline
```

Exit `0` and one `PASS (warnings 16<=16, errors 0 allowlisted, tests 243>=243)` line means "nothing
regressed against this baseline". Exit `1` means the failing metric and **both** its values are on
the line. Exit `2` means a precondition failed and no verdict was possible.

The mode **always** forces `/t:Rebuild`. There is deliberately no switch to skip it: MSBuild re-emits
a warning only for a file it actually recompiles, so an incremental gate would report near-zero
warnings on a warm tree and pass on a tree full of them — a false green, which is worse than no gate.

To re-capture (overwrites this baseline with fresh measurements — do this only when the change is
intentional and understood):

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Record
```

To prove the PRI allowlist still rejects a real compile error:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Allowlist
```

This mode **exits non-zero by design**, because the committed fixture deliberately contains one
non-allowlisted `CS1002` line. A *rejected* line is the desired outcome. See §8.

---

## 8. Gate self-verification — the gate was observed red, then green

A gate that has never been observed failing is not a gate. Every drill below was run against this
baseline; results are recorded here rather than only in a commit message so a later phase can trust
the gate without re-deriving that it can be trusted.

<!-- GATE-SELF-VERIFICATION -->

Five drills, run against the committed `tools/baseline.json`. The first four are the plan's own
proof obligations; the fifth was added because §3.1's claim about the PRI pair's behaviour needed
real evidence rather than an assumption.

| # | Drill | Command | Exit | Verdict printed |
|---|-------|---------|------|-----------------|
| 1 | Allowlist has teeth | `tools\run-tests.ps1 -Mode Allowlist` | **1** | `ALLOWLIST SELF-TEST: 1 of 3 error line(s) correctly REJECTED.` |
| 2 | Warning increase fails | `tools\run-tests.ps1 -Mode Baseline -BaselinePath <scratch: warnings 15>` | **1** | `FAIL (warnings 15->16, errors 0 allowlisted, tests 243>=243)` |
| 3 | Test-count decrease fails | `tools\run-tests.ps1 -Mode Baseline -BaselinePath <scratch: tests.total 244>` | **1** | `FAIL (warnings 16<=16, errors 0 allowlisted, tests 243->244)` |
| 4 | Green against the committed baseline | `tools\run-tests.ps1 -Mode Baseline` | **0** | `PASS (warnings 16<=16, errors 0 allowlisted, tests 243>=243)` |
| 5 | Tolerated when **present** | `tools\run-tests.ps1 -Mode Build` (cold: vendor `WinUI.Framework.pri` deleted) | **0** | `tests 243 total, …; errors 2 [PRI175=1 PRI252=1]; warnings 12 distinct` |

**Drill 1 — the allowlist rejects a real compile error.** `-Mode Allowlist` applies the gate's *own*
`Test-ErrorAllowlisted` predicate to `tools/fixtures/allowlist-self-test.txt` instead of a build log.
Read the exit code carefully: it is **non-zero because the fixture deliberately contains one
non-allowlisted line**. A *rejected* line is the desired outcome. The dangerous direction — a
non-allowlisted line being *tolerated* — would be reported as a separately-labelled
`ALLOWLIST BREACH` row, and a third allowlist entry or an entry missing its code half would be
reported by the same tripwire. Neither fired.

| ln | Code | Target | Verdict |
|----|------|--------|---------|
| 1 | `PRI175` | `WINAPPSDKGENERATEPROJECTPRIFILE` | **TOLERATED** (allowlisted) |
| 2 | `PRI252` | `WINAPPSDKGENERATEPROJECTPRIFILE` | **TOLERATED** (allowlisted) |
| 3 | `CS1002` | `CoreCompile` | **REJECTED** (not allowlisted) |

> 🔍 **This fixture found a live bug on its first run, which is the whole reason it exists.**
> Committed with three lines, it parsed only **two**: line 2 — the real task-raised shape — was
> silently dropped, because `Get-MsbuildErrorList` anchored the task-raised pattern with `^\s*` and
> MSBuild prefixes the raising project's path ahead of the target. A real `CS0234` in that shape
> would have been **invisible**: not reported, not tolerated, simply never parsed. The anchor was
> removed. The three fixture lines deliberately cover both parser branches (lines 1 and 3 canonical
> `<file>(<l>,<c>): error CODE:`, line 2 task-raised `… <Target> : error : CODE:`) and both PRI
> codes, so neither branch can regress unnoticed again.

**Drills 2 and 3 — a regression fails in both directions, naming both values.** Both used a scratch
copy of `tools/baseline.json` under `%TEMP%` with exactly **one** field edited. The committed file was
never touched: after drill 4, `git diff --exit-code -- tools/baseline.json` is **empty** — the
committed baseline is byte-identical to what `-Mode Record` wrote. Note the shape difference in the
verdict line: the metric that **failed** renders `recorded->measured` (`warnings 15->16`,
`tests 243->244`) while metrics that passed keep their documented shape (`warnings 16<=16`,
`tests 243>=243`). That is what puts the failing metric **and both of its values** on one line.

Drill 3 also proves the **decrease** direction is gated, which the plan required specifically: a
test count that drops means tests went missing, and that must not pass silently.

**Drill 5 — the PRI pair is tolerated when present, and cascades when it is not resolved.** Deleting
`vendor\WinUI.Framework\bin\**\WinUI.Framework.pri` and running the reporting mode reproduced both
real PRI errors (§3.1). Both were **tolerated**, the run still exited **0**, and the tests still ran
— proving the gate is not asserting "errors == 0 is required" and is not asserting "errors == 2 is
required" either.

The **same** deletion followed by a `/t:Rebuild` — i.e. the genuine cold-tree state — went **RED**,
because `AkariTool.pri` is then never emitted and `AkariTool.App.Tests` fails `CS0234`. That red is
**correct**: `CS0234` is not allowlisted. The tree was restored with the direct-csproj build quoted in
§3.1, after which drill 4 returned green. This drill is the reason the gate's allowlist is trusted to
be a *filter* rather than a *suppressor*.

---

*Phase: 01-baseline-spikes-test-harness* · *Recorded: 2026-10-05* · *Baseline: SPIKE-01 / D-12, D-13, D-14, D-15, D-16*
