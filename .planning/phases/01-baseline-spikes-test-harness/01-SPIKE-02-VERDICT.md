<!--
  SPIKE-02 VERDICT — do Winhance's SettingsCard, DataGrid and WrapPanel render under
  WindowsAppSDK 2.3.1 with CommunityToolkit.WinUI?

  ► This check is recorded as MANUAL-ONLY, and must stay that way. See
    .planning/phases/01-baseline-spikes-test-harness/01-VALIDATION.md §
    "Manual-Only Verifications" for the row and the reason: no command distinguishes
    "renders correctly" from "instantiated but invisible or mis-themed". The two
    automated gates below are NECESSARY INPUTS to the verdict, never the verdict
    itself (D-06).
-->

# SPIKE-02 — WinUI control compatibility verdict

> **Status: RESOLVED — `all three render`.** All three D-06 gates are satisfied: compile,
> launch survival, and the human visual render check. The Verdict below is filled from that
> human confirmation, not from the two automated gates.

## 1. Verdict

`all three render`

`SettingsCard`, `DataGrid` and `WrapPanel` all render correctly under WindowsAppSDK 2.3.1
with `CommunityToolkit.WinUI`. No substitute was needed, so none was built or proved — see
section 6.

This is the verdict recorded from the **human's visual confirmation** of the spike window.
The two automated gates in section 4 were green as well, but they were not what closed this:
D-06 requires compile **plus** launch **plus** visual render, and rejects compile-only
explicitly.

## 2. Configuration used

Exactly five package references, all **exact pins** — no version ranges, no floating
references, no `Microsoft.Windows.SDK.BuildTools` (already resolved transitively at
`10.0.26100.4654`; the latest published is `10.0.28000.2705`, so adding it would have
silently upgraded the SDK toolchain).

| Package | Version | Role |
|---|---|---|
| `Microsoft.WindowsAppSDK` | `2.3.1` | **metapackage** — `Microsoft.WindowsAppSDK.WinUI` was never referenced directly (MSB4011 dual-family hazard) |
| `Microsoft.Windows.CsWinRT` | `2.3.1` | **explicit** — no WCT 8.2 package declares it as a nuspec dependency |
| `CommunityToolkit.WinUI.Controls.SettingsControls` | `8.2.251219` | `SettingsCard` |
| `CommunityToolkit.WinUI.Controls.Primitives` | `8.2.251219` | `WrapPanel` |
| `CommunityToolkit.WinUI.UI.Controls.DataGrid` | `7.1.2` | `DataGrid` |

Project shape, copied from `src/AkariTool.App/AkariTool.App.csproj:2-25` with the app's
own identity removed:

| Property | Value |
|---|---|
| `OutputType` | `WinExe` |
| `TargetFramework` | `net10.0-windows10.0.26100.0` |
| `TargetPlatformMinVersion` | `10.0.17763.0` |
| `Platforms` / `RuntimeIdentifier` | `x64` / `win-x64` |
| `UseWinUI` | `true` |
| `Nullable` / `ImplicitUsings` | `enable` / `enable` |
| `EnableMsixTooling` | `false` (the app sets `true`; MSIX tooling on library output raises a spurious "multiple executables" warning) |
| `WindowsPackageType` | `None` |
| `ApplicationManifest` / `ApplicationIcon` / `Version` | **omitted** — the spike carries no `requireAdministrator` manifest and so runs unelevated with the developer's normal token, with no registry-write or SYSTEM capability (threat T-01-07, accepted) |
| `ProjectReference`s | **zero** — see below |

`WindowsAppSDKSelfContained` was **not** set in the csproj. It was passed as
`/p:WindowsAppSDKSelfContained=true` on the build command line, and **only** because
this project is a `WinExe` with zero `ProjectReference`s.
`Microsoft.WindowsAppSDK.Base.targets:19-20` hard-errors with *"WindowsAppSDKSelfContained
should not be applied to a class library"*, and global properties flow into every
`ProjectReference` — so passing this flag to `AkariTool.sln` would fail all five of its
libraries. This is the only project in the repository where the flag is safe.

The spike was **never added to `AkariTool.sln`** and **has zero `ProjectReference`s**,
which is what makes D-05's deletion trivially clean: a solution build cannot reach it,
and `AkariTool.App.csproj`'s package list stays byte-for-byte identical.

### `DataGrid 7.1.2` provenance — an accepted unmaintained dependency

`CommunityToolkit.WinUI.UI.Controls.DataGrid 7.1.2` was **last published 2021-11-18**.
Six versions have ever existed, and the `CommunityToolkit.WinUI` **metapackage id itself
also stops at 7.1.2** — so this is the only DataGrid available at any version. It is
therefore an **accepted, understood risk**, recorded here so a future reader (or a future
`NU1901`/`NU1902`-style advisory against it) does not mistake it for an oversight.
Its single nuspec dependency is `Microsoft.WindowsAppSDK 1.0.0` — a *minimum*, cleared by
2.3.1 — and it carries no `CommunityToolkit.*` coupling at all.

### Two measured facts routed here from SPIKE-01 (plan 01-02)

These are **not** findings of this spike; they are the build context the spike's clean log has
to be read against, and they are reproduced because a later reader comparing the two build
logs will otherwise read a difference as a regression.

| Fact (measured in 01-02) | What it means for this spike |
|---|---|
| A solution `/t:Rebuild` never regenerates `vendor\WinUI.Framework\bin\x64\`, so a **fresh clone hits `CS0234` on its first solution build** | Did **not** occur here. The spike builds its own graph, has zero `ProjectReference`s and touches nothing under `vendor/`. It remains true of `AkariTool.sln` and must not be "fixed" by widening the error allowlist. |
| `PRI175`/`PRI252` is **not reproducible run-to-run** — present on a cold build, absent once `WinUI.Framework.pri` exists on disk | The spike's build log contained **no** `PRI175`/`PRI252` at all, and that is consistent with a build that never reached that PRI step. It is not evidence of anything about the toolkit packages. |

## 3. Configuration findings

**This section is not empty.** Three namespace-resolution failures occurred before the
project built. Each is reproduced verbatim below and each is explicitly **NOT a rendering
verdict** — they are statements about *where a type is declared*, and they say nothing
about whether any control renders.

The plan, and `01-RESEARCH.md`, predicted per-package XAML namespaces for the 8.2 train
(`CommunityToolkit.WinUI.Controls.SettingsControls`, `…Controls.Primitives`) and the
legacy `Microsoft.Toolkit.Uwp.UI.Controls.DataGrid` for the frozen DataGrid. **None of the
three predictions was right.** Every XAML prefix actually used was read out of the pinned
package's own XML doc file (which lists `T:` members), never guessed, and each superseded
prefix was allowed to fail once so the compiler would name the type it looked for.

**Finding C-1 — `DataGrid` is not in the legacy `Microsoft.Toolkit.Uwp.*` namespace.**

```
MainWindow.g.i.cs(20,35): error CS0234: The type or namespace name 'Toolkit' does not exist in the namespace 'Microsoft' (are you missing an assembly reference?) [C:\Users\isleap\Documents\GitHub\Akari-Tool\tools\spike\WinUiControlCompat\WinUiControlCompat.csproj]
```

**Finding C-2 — there is no `…UI.Controls.DataGrid` sub-namespace to fall back to.**

```
MainWindow.g.i.cs(20,69): error CS0426: The type name 'DataGrid' does not exist in the type 'DataGrid' [C:\Users\isleap\Documents\GitHub\Akari-Tool\tools\spike\WinUiControlCompat\WinUiControlCompat.csproj]
```

**Finding C-3 — the 8.2 train uses one flattened namespace, not per-package ones.**

```
MainWindow.xaml(65,14): XamlCompiler error WMC0001: Unknown type 'WrapPanel' in XML namespace 'using:CommunityToolkit.WinUI.Controls.Primitives' [C:\Users\isleap\Documents\GitHub\Akari-Tool\tools\spike\WinUiControlCompat\WinUiControlCompat.csproj]
MainWindow.xaml(52,14): XamlCompiler error WMC0001: Unknown type 'SettingsCard' in XML namespace 'using:CommunityToolkit.WinUI.Controls.SettingsControls' [C:\Users\isleap\Documents\GitHub\Akari-Tool\tools\spike\WinUiControlCompat\WinUiControlCompat.csproj]
```

### The namespace mapping that actually built

| Control | XAML prefix used | Package |
|---|---|---|
| `SettingsCard` | `using:CommunityToolkit.WinUI.Controls` | SettingsControls 8.2.251219 |
| `WrapPanel` | `using:CommunityToolkit.WinUI.Controls` | Primitives 8.2.251219 |
| `DataGrid`, `DataGridTextColumn` | `using:CommunityToolkit.WinUI.UI.Controls` | DataGrid 7.1.2 |

The 8.2 assemblies declare **both** `SettingsCard` and `WrapPanel` directly in the
flattened namespace `CommunityToolkit.WinUI.Controls`. The per-package names
(`…Controls.SettingsControls`, `…Controls.Primitives`) occur in those assemblies as
**assembly** names, which is the trap: an `xmlns:using:` prefix must name a *namespace*,
and those are not namespaces. Frozen DataGrid 7.1.2 sits in a different namespace again.

**No `NU1102`, `NU1605`, `NU1608` or `MSB4011` occurred.** Restore was clean on the first
attempt and no dependency-family conflict arose — `Microsoft.WindowsAppSDK 1.6.250108002`
in the WCT 8.2 nuspecs proved to be a *minimum* that 2.3.1 satisfies, so there was no
downgrade to record.

## 4. Automated gates

> **Both of these are necessary but NOT sufficient inputs to the verdict.**
> **A green compile and a green launch do not mean the controls render.** D-06 sets the
> bar at compile **plus** launch **plus** visual render, and rejects compile-only
> explicitly: a version-skewed toolkit typically resolves at compile time and fails at
> XAML-load or theming time — precisely the case a compile gate cannot see. No command
> distinguishes "renders correctly" from "instantiated but invisible or mis-themed", and
> none was invented for this spike.

### Gate 1 — compile

Restore and Build were run as **two separate MSBuild invocations** (a combined
`/t:Restore,Rebuild` breaks WinUI XAML codegen), against the toolchain discovered through
`vswhere.exe` — the same discovery `tools/run-tests.ps1`'s `Resolve-VSToolchain` performs,
reused rather than re-implemented:

```
& $msb 'tools\spike\WinUiControlCompat\WinUiControlCompat.csproj' /t:Restore  /p:Configuration=Debug /p:Platform=x64 /v:minimal /nologo
& $msb 'tools\spike\WinUiControlCompat\WinUiControlCompat.csproj' /t:Rebuild  /p:Configuration=Debug /p:Platform=x64 /p:WindowsAppSDKSelfContained=true /v:minimal /nologo
```

| | Result |
|---|---|
| Restore | **PASS** — `Restored …\WinUiControlCompat.csproj`, exit 0 |
| Rebuild | **PASS** — exit 0, **no `error ` line at all**, and no `error XLSQ` / `error WMC` / `error MC` |
| Emitted | `WinUiControlCompat -> …\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\WinUiControlCompat.dll` |

> **Path note:** the emitted binary lands under `bin\`**`x64`**`\Debug\…`, because the
> project declares `<Platforms>x64</Platforms>` and was built with `/p:Platform=x64`. The
> plan's verify block names `bin\Debug\…` without the `x64` segment; the segment is
> present, not missing. This is the same output-path shape STATE.md already records for
> `AkariTool.App` (`bin\x64\Debug\<tfm>\win-x64\`).

### Gate 2 — launch survival

The produced `.exe` was launched **directly** with `Start-Process -PassThru` — not via
`dotnet run`, which does not surface WinUI XAML-load failures the way a direct launch does.
**The window was left open** for the human render check.

```
$p = Start-Process -FilePath '…\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\WinUiControlCompat.exe' -PassThru
Start-Sleep -Seconds 5
if ($p.HasExited) { "FAIL: spike exited with code $($p.ExitCode)"; exit 1 } else { "PASS: spike still running after 5s" }
```

| | Result |
|---|---|
| Launch-survival assertion | **`PASS: spike still running after 5s`** |
| Process | PID `10320`, `HasExited = False`, `Responding = True` |
| Window | `MainWindowTitle = "SPIKE-02 - WinUI control compatibility probe"`, `IsWindowVisible = True`, `IsIconic = False`, rect `286,286 – 1006,926` (720×640, on a 3640×1920 virtual screen) |

**No `XamlParseException`, no `TypeLoadException`, no theming failure at load.** The
process is alive, its window is created, visible, un-minimized and responding to message
pumps — which means the XAML for the page, including all three controls, **loaded**. That
is a real and non-trivial signal, and it is still not the verdict: a loaded control can be
invisible, mis-themed, collapsed to zero size, or painted with no content.

## 5. Render check (manual) — PERFORMED, human visual confirmation

**This check was performed by a human looking at the spike window, and it is what closed the
verdict.** The automated gates in section 4 were green, but D-06 requires compile **plus**
launch **plus** visual render and states in bold that the first two are necessary and **not
sufficient** — so the plan could not be closed on them, and was not.

The human's answer was given at the **whole-page level**: *"all three render"*. No
finer-grained per-control observation was reported, and none is invented here. The table
below therefore names each control, states what that control was **required** to show, and
records that the required outcome is covered by the human's single page-level confirmation.

| Control | What it was required to show | Required outcome | How it is established |
|---|---|---|---|
| `SettingsCard` | A visible bordered card carrying the header *"SPIKE-02 SettingsCard"*, its description line and the body text *"SettingsCard body content"*, styled with WinUI theme resources rather than raw/default appearance | visible-and-themed | **Human visual confirmation** — covered by the page-level verdict `all three render`. Not established by any agent inspection; no agent had screen access. |
| `WrapPanel` | **Eight** coloured boxes inside a **420px-wide** strip, each 88px wide, so they **cannot** all fit on one line — a single-line result would mean the control did not lay out | laid out, wrapped across lines | **Human visual confirmation** — covered by the page-level verdict `all three render`. |
| `DataGrid` | Column headers *Name* and *Value* **plus two painted rows** (`row-one`/`alpha`, `row-two`/`beta`); a headers-only render is a *different* symptom from nothing at all, so the rows are what make this observable | both rows painted | **Human visual confirmation** — covered by the page-level verdict `all three render`. |

**No control was recorded as failing**, so no observed symptom, no exception name and no
render gap appears anywhere in this document.

Why the automated half could not stand in for this: no command distinguishes "renders
correctly" from "instantiated but invisible or mis-themed". An assertion that the control
type is present in the visual tree would pass on an invisible, mis-themed or zero-sized
control — precisely the failure D-06 exists to catch. No such proxy was invented for this
spike, and none should be added later for the same reason.

**Operational note (not a finding about the controls).** This gate was held open across
**two sessions**. The spike window did not survive the session boundary both times, and the
window the human finally answered about was launched afresh for that answer. Recorded because
it is a real cost of holding a human render gate open across a session rollover, and because
the PID recorded in section 4 is not the PID the human answered about.

## 6. Substitutes

**None were needed, and none were built.** No control failed, so the substitute branch of
D-07 — name a concrete substitute **and prove that substitute renders on this same page**
before the verdict is final — was never entered. Nothing was added to `MainWindow.xaml` after
the first successful build, and no substitute package, control or namespace was introduced.

The heading is kept rather than deleted so a later reader can tell "no substitute was
required" apart from "substitutes were never considered".

## 7. Scope note

**Root-cause diagnosis was deliberately not performed, and none appears in this document —
in either direction.** D-07 puts *why* a control is incompatible out of scope: it risks
turning a bounded spike into an open-ended compatibility project, and Phases 4, 5 and 9 plan
against what was observed, not against a theory. That exclusion applies symmetrically here:
**no mechanism is offered for why the controls did render either.** A passing result is not
an invitation to explain the passing, and a theory written after the fact would be the same
unbounded work in the opposite direction.

This document records the configuration used, the compiler's verbatim output, the two
automated gate results, the human render confirmation with its per-control criteria, and the
substitute outcome (none required) — and nothing more. The absence of a diagnosis is
deliberate, not an oversight.

## 8. Downstream

Three phases consume this verdict. **The gate is now resolved in their favour.**

| Phase | Consumes this for | Status |
|---|---|---|
| **Phase 4** | The domain file moves. Phase 4 was never formally gated on SPIKE-02, but it should not have assumed any of the three controls was available. | **Unblocked either way — and now explicitly unblocked.** All three controls are confirmed available under WindowsAppSDK 2.3.1. |
| **Phase 5** | **Shared row primitives.** `SettingsCard` is the row container these primitives target. | **Hard gate CLEARED — plan against the real `SettingsCard`, not a substitute row.** `SettingsCard` and its badge/details/banner targets all render. |
| **Phase 9** | **Software & Apps table view.** `DataGrid` is the table; `WrapPanel` also bears on any filter-chip or tag row. | **Hard gate CLEARED — plan against the real `DataGrid` and the real `WrapPanel`, not substitutes.** Both rows render, not just headers. |

**Phases 5 and 9 may now plan against the real controls.** No substitute path is needed, no
substitute package has to be designed, and nothing about their row-primitive or table work
changes shape because of this spike. A later phase that still believes the gate is open is
reading a stale planning document: the gate was open only from 2026-10-05 until the human
render confirmation recorded in section 5.

What the verdict does **not** settle: whether these controls are a good *fit* for Akari's
UX, or whether Phase 5 and Phase 9 prefer a different primitive on design grounds. This
spike answered compatibility, not design (D-06/D-07 are compatibility gates), and nothing
here should be read as endorsing a control choice beyond "it renders".

Per D-15 this verdict lives in `.planning/phases/01-baseline-spikes-test-harness/` rather
than in `tools/`, because phase directories are archived at milestone close while `tools/`
code is durable. The instrument it measured — `tools/spike/WinUiControlCompat/` — was
deleted per D-05 and leaves no trace, no canary page and no placeholder behind.

---

*SPIKE-02 · phase 01-baseline-spikes-test-harness · plan 01-03 · measured 2026-10-05 · render gate discharged 2026-10-06*
