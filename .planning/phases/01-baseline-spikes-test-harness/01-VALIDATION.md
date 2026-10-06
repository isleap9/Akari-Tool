---
phase: "01"
slug: "baseline-spikes-test-harness"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-05"
---

# Phase 01 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
>
> Derived from `01-RESEARCH.md` § Validation Architecture. Phase plans do not exist yet, so the
> **Task ID** column below holds *provisional* IDs bound to the verification obligation, not to a plan
> task. Re-bind them to real `{plan}-{task}` IDs at execution start.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.5 + `Microsoft.NET.Test.Sdk` 17.14.1; FluentAssertions 8.7.1; NSubstitute 5.3.0 |
| **Config file** | none — default xUnit discovery. Neither existing test csproj has an `xunit.runner.json`, and the new one must not add one. |
| **Build tool** | `MSBuild.exe` **18.10.1.42706**, discovered via `vswhere.exe` — **never** `dotnet build` (fails on WinUI 3 PRI/resource targets) |
| **Test host** | `vstest.console.exe` **18.10.0** (x64), from `<vsroot>\Common7\IDE\CommonExtensions\Microsoft\TestWindow\` |
| **Quick run command** | `& $vst 'tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll' /Platform:x64` |
| **Full suite command** | `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` |
| **Baseline-compare mode** | `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Baseline` |
| **Measured current state** | **230 tests, 229 passed, 1 `NotExecuted`** (Core 93 / Infra 137), measured in 1.28 s |
| **Estimated runtime** | ~5 s full suite; ~60–180 s for `-Mode Baseline` (dominated by `/t:Rebuild`) |
| **Elevation** | required in practice — the suite reads registry state; runner should assert elevation explicitly rather than silently reporting green without it |

> **Toolchain paths are discovered, never hardcoded.** `AGENTS.md` and `01-CONTEXT.md` both name a
> VS **Community** install under `Program Files`; this machine actually has **Visual Studio Build
> Tools 2026 (18.10.3)** under `Program Files (x86)`. `tools/run-tests.ps1` must resolve both
> executables through `vswhere.exe`.

---

## Sampling Rate

- **After every task commit:** Run `& $vst <that task's test dll> /Platform:x64` (single assembly, ~1–2 s)
- **After every plan wave:** Run `powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1` — the *only* rate at which a warning count is trustworthy (D-13: MSBuild only re-emits warnings for files it actually recompiles, so an incremental build under-reports and produces a false green)
- **Before `/gsd-verify-work`:** `tools\run-tests.ps1 -Mode Baseline` green, SPIKE-02's human verdict written, SPIKE-03's report committed
- **Max feedback latency:** ~5 s per task, ~180 s per wave

> **Never glob for test DLLs.** `bin\DeElevated\` holds a second, `asInvoker`-manifested copy of every
> assembly (produced by `build-deelevated.ps1` /p:DeElevatedTest=true). A `**\*.Tests.dll` glob would
> double-count and could execute a manifest the runner did not intend to.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-01-01 (prov.) | — | 0 | SPIKE-01 | — | — | gate | `tools\run-tests.ps1 -Mode Baseline` | ❌ W0 | ⬜ pending |
| 01-01-02 (prov.) | — | 0 | SPIKE-01 | — | error allowlist matches the error **list**, never `$LASTEXITCODE` | gate | `tools\run-tests.ps1 -Mode Baseline` | ❌ W0 | ⬜ pending |
| 01-01-03 (prov.) | — | 0 | SPIKE-02 | T-01-SB* | exact-version pins; `Microsoft.WindowsAppSDK.WinUI` never directly referenced | build gate | `& $msb <spike>.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:WindowsAppSDKSelfContained=true` | ❌ W0 | ⬜ pending |
| 01-01-04 (prov.) | — | 0 | SPIKE-02 | — | — | smoke proxy | `Start-Process <spike>.exe -PassThru`; `Start-Sleep 5`; assert `-not $p.HasExited` | ❌ W0 | ⬜ pending |
| 01-01-05 (prov.) | — | 0 | SPIKE-02 | — | — | **MANUAL** | *none — see Manual-Only Verifications* | ❌ W0 | ⬜ pending |
| 01-01-06 (prov.) | — | 0 | SPIKE-03 | T-01-SV* | parsed tokens never reach `Invoke-Expression`, a command line, or an unvalidated path | drift check | `tools\gen-setting-id-diff.ps1` then `git diff --exit-code -- <report.md> <ids/*.txt>` | ❌ W0 | ⬜ pending |
| 01-01-07 (prov.) | — | 0 | SPIKE-03 | — | — | unit self-test | `vstest <gen>.Tests.dll /Platform:x64`, or an in-generator assertion | ❌ W0 | ⬜ pending |
| 01-01-08 (prov.) | — | 0 | TEST-01 | T-01-EX* | runner executes elevated code by design; `tools/` is a privileged surface | unit | `& $vst 'tests\AkariTool.App.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.App.Tests.dll' /Platform:x64` | ❌ W0 | ⬜ pending |
| 01-01-09 (prov.) | — | 0 | TEST-01 | — | — | build + discovery gate | `& $msb AkariTool.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64` then the vstest line above | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Failure signals (the observable, per obligation)

| Obligation | Fails when |
|---|---|
| 01-01-01 | **Exit code ≠ 0**, or a printed diff line naming the metric that moved. The line must name the metric and both values, e.g. the shape `warnings <before> -> <after>` — **never** literal example numbers; the real figures are whatever `/t:Rebuild` produces, and they are recorded for the first time by this phase. |
| 01-01-02 | An error line appears that is **not** in the allowlist. Note: **do not** gate on `$LASTEXITCODE -eq 0` — the build is *expected* to exit non-zero while PRI175/PRI252 are tolerated and assemblies still emit. |
| 01-01-03 | MSBuild exit ≠ 0, or `MC`/XAML-compiler `error XLSQ…` / `error WMC…` naming the unresolved control. |
| 01-01-04 | Process exits within 5 s — almost certainly a `XamlParseException` / `TypeLoadException` / theming failure at load. |
| 01-01-05 | No automated signal exists. Human step only. |
| 01-01-06 | `git diff --exit-code` non-zero → the catalog changed and the evidence base is stale. Re-run and commit. |
| 01-01-07 | Any of the **15** catalog factories absent, or any domain yielding 0 IDs — the generator must fail loudly. |
| 01-01-08 | vstest exit ≠ 0, or TRX `Counters/@failed > 0` / `@notRunnable > 0`. |
| 01-01-09 | MSBuild error, **or** vstest reports `No test is available` / `0 tests` — the second is the specific signal that the `xunit.runner.visualstudio` adapter did not load (the classic failure when `IsTestProject` or the runner package is missing). |

*`T-01-SB*` supply-chain / `T-01-SV*` untrusted-parsed-input / `T-01-EX*` elevated-execution IDs are
provisional placeholders. The planner assigns the real `T-01-NN` numbers in each plan's
`<threat_model>`; re-bind these cells at execution start.*

---

## Wave 0 Requirements

Everything below is produced **by this phase**, so all of it is Wave 0. None exists yet.

- [ ] `tools/run-tests.ps1` — runner + `-Mode Baseline` compare (D-03, D-12, D-13, D-14, D-16).
      **Must** use `vswhere` discovery, `/t:Rebuild`, a PRI175/PRI252 error allowlist matched against
      the error **list** (not the exit code), and TRX `Counters` parsing. Must **not** pass
      `/p:WindowsAppSDKSelfContained=true` globally — the verbatim hard error
      `WindowsAppSDKSelfContained should not be applied to a class library.`
      (`Microsoft.WindowsAppSDK.Base.targets:19-20`) fails all five libraries.
- [ ] `tools/baseline.json` — machine-readable baseline (assembly list, warnings, errors, test count,
      optionally a per-assembly split). Recorded **after** `AkariTool.App.Tests` exists.
- [ ] `tools/gen-setting-id-diff.ps1` (name is agent discretion per D-15) — reflects over the **15**
      static catalog factories, reads the committed Winhance snapshot, emits the markdown report plus
      machine-readable ID sets.
- [ ] Winhance setting-ID snapshot + its generator (D-08). The snapshot header must record the
      Winhance commit SHA read (`git -C <winhance> rev-parse HEAD`) so the evidence is attributable.
- [ ] `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj` +
      `ViewModels/Tweaks/SettingBadgeCalculatorTests.cs`
- [ ] SPIKE-02 throwaway project + its written verdict document. **Not** added to `AkariTool.sln`.
- [ ] Baseline document, SPIKE-02 verdict, SPIKE-03 report — all in
      `.planning/phases/01-baseline-spikes-test-harness/` (D-15).

### Ordering constraint (load-bearing)

The SPIKE-01 baseline capture **must run after `AkariTool.App.Tests` is added to the solution**, or
the recorded warning/error/test counts are stale on arrival. Wave structure must put the test project
in Wave 0/1 and the baseline capture last.

### Highest-risk Wave 0 item, per open question 2

D-01's buildability is untested: can a `UseWinUI=true` class library reference the self-contained
`WinExe` without perturbing the PRI baseline it is measuring? `PriIndexName` is assigned for *any*
non-`winmdobj` output, so this could move warning/error counts. **Resolve this first.** Documented
fallback: drop `UseWinUI` — the badge test needs no XAML.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `SettingsCard`, `DataGrid`, `WrapPanel` are **visible and correctly themed** under WinAppSDK 2.3.1 | SPIKE-02 | No command distinguishes "renders correctly" from "instantiated but invisible / mis-themed". D-06 sets the bar at compile + launch + **visually render** and explicitly rejects compile-only, because a version-skewed toolkit typically resolves at compile time and fails at XAML-load or theming time. | Open the spike window. Record exactly one of: `all three render` / `<control> fails: <observed symptom>` / `all three render via <named substitute>`. Per D-07 a failing control's substitute must be named **and proved to render in the same spike page**. Write the verdict to the phase directory. |

> **The SPIKE-02 task must not close on the automated build gate alone.** Rows 01-01-03 and 01-01-04
> are *necessary, not sufficient* inputs to the verdict. The research was explicit that it did not
> invent a command for the render check, and any plan that closes SPIKE-02 on a green build gate
> has silently violated D-06.

### Genuinely not automatable, and why it matters

- **SPIKE-02's render check** — above.
- **SPIKE-01's baseline is single-machine.** A different VS Build Tools version, Windows SDK, or NuGet
  cache shifts warning counts with no code change. There is no CI (verified: no `.github/`, no
  pipeline) and CI is explicitly deferred. The baseline document's header must say so, or a future
  phase on a different machine will chase a phantom regression.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5 s per task
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending