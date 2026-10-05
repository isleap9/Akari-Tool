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

> **Status: OPEN.** The compile and launch gates are green. The render check — the
> bar D-06 actually sets — has not been performed. The Verdict section below is
> unfilled, and stays that way until a human has looked at the spike window.

## 1. Verdict

`PENDING`

Permitted values, exactly one of:

- `all three render`
- `<control> fails: <observed symptom>` (one line per failing control)
- `all three render via <named substitute>`

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

## 5. Render check (manual)

**Not yet performed.** This is the obligation D-06 exists for, and it is why the Verdict
section above is still unfilled.

With the spike window open on screen, confirm **each control individually** and record its
outcome:

| Control | What to look for | Outcome |
|---|---|---|
| `SettingsCard` | A visible bordered card with the header *"SPIKE-02 SettingsCard"*, its description line, and the body text *"SettingsCard body content"*, styled with WinUI theme resources rather than raw/default appearance | _awaiting human_ |
| `WrapPanel` | **Eight** coloured boxes in a **420px-wide** strip. Each box is 88px wide, so they **cannot** all fit on one line: if they appear on a single line, the control did not lay out | _awaiting human_ |
| `DataGrid` | Column headers *Name* and *Value* **plus two painted rows** (`row-one`/`alpha`, `row-two`/`beta`). Headers-only is a *different* symptom from nothing at all, so the rows are what make this observable | _awaiting human_ |

Then record each control as **visible-and-themed**, or as a **named failure** with its
observed symptom (`XamlParseException: <type> could not be found`, `TypeLoadException`,
blank content, unstyled fallback, collapsed to zero size, …).

No automated proxy for "renders correctly" exists and none was invented. An assertion that
the control type is instantiated in the visual tree would pass on an invisible or
mis-themed control — the exact failure D-06 exists to catch.

## 6. Substitutes

**None yet.** Empty until a control actually fails. Per D-07, a failing control's
substitute must be named **and proved to render on this same page** — added to
`MainWindow.xaml` alongside the failed control, rebuilt with the same two-pass invocation,
re-launched, and looked at again. A substitute that has not been *seen* rendering is a
guess, and a guess is what D-07 forbids.

## 7. Scope note

**Root-cause diagnosis was deliberately not performed, and none appears in this document.**
Whether any failure here stems from the WindowsAppSDK version, a CsWinRT version gap, or
the WCT 8.x namespace reorganisation is out of scope per D-07: it risks turning a bounded
spike into an open-ended compatibility project, and Phases 4, 5 and 9 plan against the
observed substitute, not against a theory. This document records the configuration used,
the compiler's verbatim output, the two automated gate results, the observed per-control
render outcomes, and the named substitutes — and nothing more. The absence of a diagnosis
is deliberate, not an oversight.

## 8. Downstream

Three phases plan against this verdict:

| Phase | Consumes this for |
|---|---|
| **Phase 4** | The domain file moves — nothing here blocks them, but any control that fails changes what the moved code can reference |
| **Phase 5** | **Shared row primitives.** `SettingsCard` is the row container these primitives target; a failed `SettingsCard` means Phase 5 designs a substitute row instead |
| **Phase 9** | **Software & Apps table view.** `DataGrid` is the table; a failed `DataGrid` means Phase 9 designs a substitute grid. `WrapPanel` also bears on any filter-chip or tag row |

Per D-15 this verdict lives in `.planning/phases/01-baseline-spikes-test-harness/` rather
than in `tools/`, because phase directories are archived at milestone close while `tools/`
code is durable.

---

*SPIKE-02 · phase 01-baseline-spikes-test-harness · plan 01-03 · measured 2026-10-05*
