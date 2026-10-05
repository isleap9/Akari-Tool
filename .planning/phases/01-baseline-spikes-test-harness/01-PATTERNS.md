# Phase 01: Baseline, Spikes & Test Harness - Pattern Map

**Mapped:** 2026-10-05
**Files analyzed:** 13 new / 1 modified
**Analogs found:** 11 / 14 (3 files have no analog - see `## No Analog Found`)

**Tracked-source gate:** every analog path below was verified with `git ls-files -- <path>`
returning a non-empty result. `tools/` currently has **0 tracked files** and does **not exist on
disk** - it is genuinely new, and every `tools/*` path in this map is a *create*, not an *edit*.

**Licensing gate:** no excerpt in this document comes from the Winhance checkout. Where a fact about
Winhance is needed (package versions, ID formats), the fact is stated and the path is pointed at;
no Winhance source is reproduced.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj` | config | build-time | `tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` | exact (24 lines, byte-identical shape) |
| `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` | test | unit / pure-compute | `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs` | exact (same factory-helper + `{Method}_{Condition}_{Expected}` idiom) |
| `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` (fixtures) | test | unit | `tests/AkariTool.Infrastructure.Tests/Features/SettingOperationExecutorTests.cs` | exact (`MakeXxxSetting(string id)` fixture idiom) |
| `AkariTool.sln` (modified) | config | build-time | `AkariTool.sln` itself (lines 12-15, 110-117) | exact (in-place edit) |
| `tools/run-tests.ps1` | utility / build tool | batch-invoke → structured parse | `build-deelevated.ps1` | role-match (near-exact: same discover-MSBuild-then-invoke shape) |
| `tools/run-tests.ps1` (toolchain discovery) | utility / build tool | batch-invoke | `build-installer.ps1` L46-55 | exact (same vswhere idiom, verbatim lineage) |
| `tools/run-tests.ps1` (exit-code idiom) | utility / build tool | batch-invoke | `build-deelevated.ps1` L54-65 | exact (`exit $LASTEXITCODE` not `Write-Error`) |
| `tools/baseline.json` | config / data | file I/O | **none** | no analog (no committed machine-readable baseline exists) |
| `tools/gen-winhance-id-snapshot.ps1` | utility | transform (parse external text → emit sorted list) | `build-installer.ps1` header/comment style only | role-match (no parsing analog) |
| `tools/gen-setting-id-diff.ps1` | utility | transform (reflect → set-difference → emit markdown + ID sets) | `build-deelevated.ps1` (script discipline) + `SettingCatalogValidatorTests.cs` L434-463 (**anti**-analog, see warning) | role-match |
| `tools/data/winhance-setting-ids.txt` (+ `*.ids.txt` per domain) | config / data | file I/O | **none** | no analog |
| `spike/WinUiControlCompat/*.csproj` + `App.xaml(.cs)` + `MainWindow.xaml(.cs)` (throwaway) | config / view | compile + launch | `src/AkariTool.App/AkariTool.App.csproj` | role-match (nearest WinUI project shape) |
| `.planning/phases/01-…/01-BASELINE.md` | doc | n/a | **none** | no analog (no committed baseline doc exists) |
| `.planning/phases/01-…/01-SPIKE-02-VERDICT.md` | doc | n/a | **none** | no analog |
| `.planning/phases/01-…/01-SETTING-ID-DIFF.md` | doc | n/a (generated) | **none** | no analog |

---

## Pattern Assignments

### `tests/AkariTool.App.Tests/AkariTool.App.Tests.csproj` (config, build-time)

**Analog:** `tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` (git-tracked)

**Whole-file shape** (lines 1-24) - this is the entire analog; the new file differs only in the
`PropertyGroup` addition and the `ProjectReference`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

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
    <ProjectReference Include="../../src/AkariTool.Infrastructure/AkariTool.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

`AkariTool.Core.Tests.csproj` is byte-identical except it has **one** `ProjectReference` and is
23 lines. All five package versions are already in the local NuGet cache - no restore risk.

**The three deltas the planner must write:**

1. `UseWinUI=true` (D-01) - **decide between RESEARCH.md's options (a)/(b)/(c) before writing.**
   RESEARCH recommends (a) drop `UseWinUI` entirely, documented as a deviation from D-01's letter
   that preserves its stated rationale. RESEARCH's own recommendation should be surfaced to the
   user as a deviation, not silently taken.
2. `<ProjectReference Include="../../src/AkariTool.App/AkariTool.App.csproj" />` - **one** reference
   only. Do **not** also add Core or Infrastructure: `AkariTool.App` already references both, and
   `SettingBadgeCalculator` reaches `NumericConversionHelper` transitively (see the target section).
3. The 5-property `PropertyGroup` block above is verbatim; add only what option (a)/(b)/(c) requires
   (`<EnableMsixTooling>false</EnableMsixTooling>` + `<WindowsPackageType>None</WindowsPackageType>`).

**Do NOT add `InternalsVisibleTo`.** `SettingBadgeCalculator` is `public`. The precedent if ever needed
is `src/AkariTool.Core/AkariTool.Core.csproj:14-16`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="AkariTool.Core.Tests" />
  </ItemGroup>
```

RESEARCH's conclusion stands: an unused `InternalsVisibleTo` is a hole in the App assembly's surface
for zero benefit. RESEARCH also verified `AkariTool.App.csproj` has **no** `InternalsVisibleTo` block.

**Namespace/folder layout:** mirror the source path under `tests/`
(`src/AkariTool.App/ViewModels/Tweaks/X.cs` → `tests/AkariTool.App.Tests/ViewModels/Tweaks/XTests.cs`)
with namespace `AkariTool.App.Tests.ViewModels.Tweaks`. Compare
`tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs:7` (`AkariTool.Core.Tests.Features`)
against source `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs` - the tests
folder **flattens** `Common/`, and so should the new one.

---

### `tests/AkariTool.App.Tests/ViewModels/Tweaks/SettingBadgeCalculatorTests.cs` (test, unit)

**Analog:** `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs` (git-tracked)

**Using block + namespace ordering** (lines 1-11) - `System.*` first, then `AkariTool.*`, then
third-party alphabetical with `FluentAssertions` / `Microsoft.Win32` / `Xunit` last:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Validation;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class SettingCatalogValidatorTests
```

Note `public class` - **not** `sealed`. Compare `tests/AkariTool.Core.Tests/Features/BadgePillStateTests.cs:9`.

**Helpers block at the top, behind a section banner** (lines 15-34). This is the house fixture idiom
- a private static factory per shape, no `[SetUp]`, no fixture class:

```csharp
    // ── helpers ──────────────────────────────────────────────────────────────

    private static SettingDefinition Toggle(string id) => new()
    {
        Id = id,
        Name = id,
        Description = "d",
        InputType = InputType.Toggle,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                ValueName = id,
                RecommendedValue = null,
                DefaultValue = null,
                ValueType = RegistryValueKind.DWord,
            },
        },
    };
```

The badge test needs three such factories, not one: a **Toggle**, a **Selection** (with
`ComboBoxMetadata.Options`), and a **NumericRange** (with `PowerCfgSettings[].PowerModeSupport`).
`Selection(...)` at lines 36-98 is the template for the middle one - note how it takes *optional named
parameters with defaults* rather than an enum switch:

```csharp
    private static SettingDefinition Selection(
        string id,
        IEnumerable<ComboBoxOption>? options = null,
        bool subjective = false,
        bool powerCfgBacked = false,
        bool dynamic = false,
        bool emptyRegistry = false)
```

**Second fixture idiom** - `tests/AkariTool.Infrastructure.Tests/Features/SettingOperationExecutorTests.cs:42-62`
(`MakeToggleSetting`). Same purpose, block-body form instead of expression-bodied; either is house style:

```csharp
    private static SettingDefinition MakeToggleSetting(string id) =>
        new SettingDefinition
        {
            Id = id,
            Name = "Test",
            Description = "Desc",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\AkariTest",
                    ValueName = "TestVal",
                    RecommendedValue = 1,
                    DefaultValue = 0,
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = new object?[] { 1 },
                    DisabledValue = new object?[] { 0 },
                },
            },
        };
```

**NSubstitute is available but not needed here.** `SettingOperationExecutorTests.cs:16-40`
(`MakeExecutor(...)`) shows the substitution idiom if the plan ever needs it - but
`SettingBadgeCalculator.Compute` takes no services, so the badge test needs no substitutes. Do not add
an unused `MakeExecutor`.

**Test-method naming + assertion style** (lines 112-117, 119-130) - `{Shape}_{Condition}_{Expected}`,
expression-bodied body, FluentAssertions terminal:

```csharp
    [Fact]
    public void StandardSelection_OneRecommendedOneDefault_Passes()
    {
        var group = Group("g", Selection("s"));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    [Fact]
    public void StandardSelection_ZeroRecommended_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A"),
            Opt("B", def: true),
        }));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("exactly one IsRecommended");
    }
```

**Assertion-shape helper** (lines 107-108) - a private helper that narrows a list to a single message
so the assertion reads cleanly:

```csharp
    private static string? SingleMessage(IReadOnlyList<CatalogViolation> violations)
        => violations.Count == 1 ? violations[0].Message : null;
```

**`[Theory]` + `[InlineData]` for a matrix** (lines 304-310) - the idiom for enumerating input types:

```csharp
    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Test")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft")]
    [InlineData(@"HKEY_CLASSES_ROOT\.txt")]
    [InlineData(@"HKEY_USERS\S-1-5-18\Software")]
    [InlineData(@"HKEY_CURRENT_CONFIG\Display")]
    public void ValidRegistryPaths_Pass(string keyPath)
```

**`because:` on the gate assertion** (lines 465-472) - the repo's way of stating *why* an assertion is
non-negotiable. The badge characterization test should use this, because its whole job is to freeze
behaviour for Phase 5:

```csharp
    [Theory]
    [MemberData(nameof(AllFeatureCatalogs))]
    public void ShippedCatalog_ValidatesClean(string catalogName, IReadOnlyList<SettingGroup> groups)
    {
        var violations = SettingCatalogValidator.Validate(groups);
        violations.Should().BeEmpty(
            because: $"catalog {catalogName} ships to users — every authored row must satisfy the catalog invariants");
    }
```

#### Target-under-test (read this before writing a single test)

`src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs` (git-tracked). Three facts change the
test's shape; RESEARCH.md flags all three and they are confirmed here:

**(a) The class and its namespace** (lines 8-27) - `AkariTool.ViewModels.Tweaks`, **not**
`AkariTool.App.*`, so the `using` in the test is `using AkariTool.ViewModels.Tweaks;`:

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

**(b) The guard reads `definition.InputType`, not the `inputType` parameter** (lines 30-31) - this is
the behaviour the characterization test must **pin**, per RESEARCH's explicit recommendation:

```csharp
        if (definition.InputType == InputType.Action)
            return result;
```

So `Compute(def /* InputType.Action */, inputType: InputType.Toggle, …)` returns an empty list
regardless of the parameter. Every subsequent branch switches on `inputType`, not `definition.InputType`.
Give this one named test (e.g. `ActionDefinition_NonActionInputType_ReturnsNoPills`) so Phase 5's
CORE-04 extraction cannot silently "fix" it.

**(c) The second gate** (lines 33-41) - `hasBadgeData` must be satisfied or the result is empty
**before** any branch runs. Every fixture therefore needs at least one of
`RegistrySettings` / `ScheduledTaskSettings` / a flagged `ComboBoxOption` / a valued `PowerCfgSetting`:

```csharp
        bool hasBadgeData =
            definition.RegistrySettings.Count > 0
            || definition.ScheduledTaskSettings.Count > 0
            || definition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true
            || (definition.PowerCfgSettings?.Any(p =>
                p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue
                || p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true);
        if (!hasBadgeData)
            return result;
```

**(d) It reaches into Infrastructure** (lines 381-383) - the only external call, and it is the reason
the test project cannot be Core-only:

```csharp
    private static int ConvertFromSystemUnits(SettingDefinition definition, int systemValue) =>
        AkariTool.Infrastructure.Features.Common.Utilities.NumericConversionHelper
            .ConvertFromSystemUnits(systemValue, definition.NumericRange?.Units);
```

`src/AkariTool.Infrastructure/Features/Common/Utilities/NumericConversionHelper.cs:14-26` is a **pure
static switch** on a lower-cased unit string - no OS access, no I/O:

```csharp
    public static int ConvertFromSystemUnits(int systemValue, string? displayUnits)
    {
        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }
```

Consequence: the numeric-range fixtures can use `"Minutes"` as `NumericRange.Units` and reason about
the `/60` in the test - deterministic, no machine dependence. **Do not add a Core or Infrastructure
`ProjectReference` to the test csproj**; it arrives transitively through `AkariTool.App`.

Return element `BadgePillState` is Core, so the test needs no App type beyond the calculator.
Construct it exactly as `tests/AkariTool.Core.Tests/Features/BadgePillStateTests.cs:14` does:

```csharp
        var a = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
```

**Existing tests do not cover this class at all** - the 18 test classes in the TRX `className`
attributes are all Core/Infrastructure model and service tests; there is no App-layer test anywhere.
`SettingBadgeCalculatorTests` is the file's first test, so its `[Fact]` set is unconstrained by
precedent except by the conventions above.

---

### `AkariTool.sln` (config, build-time - in-place edit)

**Analog:** itself (git-tracked). Four edits, all copyable.

**1. A project entry** (lines 12-15) - SDK-style projects use project-type GUID
`{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`. (`WinGet.Interop` at line 22 carries the legacy
`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`; do **not** copy that one.)

```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "AkariTool.Infrastructure.Tests", "tests\AkariTool.Infrastructure.Tests\AkariTool.Infrastructure.Tests.csproj", "{1A474F6C-6041-44E5-A102-C4A2ADC74CA3}"
EndProject
```

**2. Nest it under the `tests` solution folder** (`{1AEE90C7-ED83-4FA3-82A8-FDDEB492B52F}`) in
`NestedProjects` (lines 110-117):

```
	GlobalSection(NestedProjects) = preSolution
		{C514611D-FAF3-4E61-9469-629CD3F35931} = {C59EA786-C9B0-4C45-A826-7BE879F788FA}
		{510D67F4-23AB-4721-9984-167AB7F1480C} = {C59EA786-C9B0-4C45-A826-7BE879F788FA}
		{EF2C04C9-2649-4CBD-8DDF-C9D1B6D3D14E} = {C59EA786-C9B0-4C45-A826-7BE879F788FA}
		{50A5C729-E7C6-48D1-BF65-F600EF79D233} = {1AEE90C7-ED83-4FA3-82A8-FDDEB492B52F}
		{1A474F6C-6041-44E5-A102-C4A2ADC74CA3} = {1AEE90C7-ED83-4FA3-82A8-FDDEB492B52F}
		{87D205B2-071A-43FE-811D-5ADA6165E132} = {3EFB08DA-2EA9-FE77-18DF-C4D4293AFD9C}
	EndGlobalSection
```

**3. `ProjectConfigurationPlatforms`** (lines 82-93) - copy the whole 12-line block for
`AkariTool.Infrastructure.Tests` and substitute the new GUID. Note the shape: `.Debug|x64.ActiveCfg = Debug|Any CPU`
plus `.Debug|x64.Build.0 = Debug|Any CPU`. `Debug|Any CPU`, `Debug|x86` and both `Release` rows get
entries too.

**4. A GUID for the new project** - generate a fresh one; do not reuse.

**Corrections to CONTEXT.md that the planner must not propagate:** the solution has **6 real projects
+ 3 solution folders** (`src`, `tests`, `vendor`), not "5 real projects + 2 solution folders".
`vendor/WinUI.Framework` is **absent** from the solution even though the `vendor` folder exists -
confirmed by `git ls-files` and by the solution text. Its assembly is still emitted because
`AkariTool.App.csproj:75` ProjectReferences it.

**Do NOT add the SPIKE-02 throwaway project to the solution** (RESEARCH's explicit recommendation);
it trivially satisfies the discretion item's "must not be left in the solution file after deletion".

---

### `tools/run-tests.ps1` (utility / build tool - NEW)

**Primary analog:** `build-deelevated.ps1` (git-tracked). This is the closest thing in the repo to
what the phase needs: param block → resolve MSBuild → restore → rebuild → `exit $LASTEXITCODE`.

**Header comment block** (`build-installer.ps1:1-29`). The house style is a substantial `#`-comment
header stating *what the script does*, then a `-- Section --` sub-header for the reasoning, then
`-- Requires: --` with concrete install instructions. `build-deelevated.ps1:1-17` does the same in
17 lines. Copy this shape; include the toolchain-discovery rationale, because nine later phases'
Build Gates cite this path.

**Param block** (`build-deelevated.ps1:18-23`) - note PowerShell **lowercase** built-in variables and
aligned `=`:

```powershell
param(
    [string]$Configuration = 'Debug',
    [string]$Platform      = 'x64'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
```

Add `[ValidateSet('Build','Baseline')] [string]$Mode = 'Build'` for D-12. `$root = $PSScriptRoot` is
the repo-root anchor and is how both existing scripts address `src\...`.

**vswhere discovery** (`build-deelevated.ps1:25-34`) - *the* idiom, and RESEARCH's most consequential
finding depends on it (the `AGENTS.md` path does not exist on this machine):

```powershell
# Locate VS MSBuild (never dotnet build — WinUI PRI/packaging needs VS MSBuild).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null
if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
}
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw "VS MSBuild.exe not found via vswhere."
}
```

`build-installer.ps1:46-55` is the same logic with `Write-Error` instead of `throw` and
`-prerelease` added. `throw` is the right choice for a gate.

**`vstest.console.exe` discovery is NOT in either analog** - it must be derived from the same
`installationPath`:

```powershell
$vsroot = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$vst = Join-Path $vsroot 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
```

**Separate restore pass, then build** (`build-deelevated.ps1:52-65`) - the comment states *why*, and
the reason matters here too (a combined `/t:Restore,Rebuild` breaks WinUI XAML codegen):

```powershell
# Restore first (shared obj\ assets), as a SEPARATE pass — a combined /t:Restore,Rebuild
# breaks WinUI XAML codegen (see Phase 6).
& $msbuild 'src\AkariTool.App\AkariTool.App.csproj' /t:Restore `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $msbuild 'src\AkariTool.App\AkariTool.App.csproj' /t:Rebuild `
    /p:Configuration=$Configuration /p:Platform=$Platform `
    /p:DeElevatedTest=true `
    /p:ApplicationManifest="$manifestPath" `
    /p:OutputPath="$binDir" `
    /v:minimal /nologo
exit $LASTEXITCODE
```

**Exit-code idiom - two divergent house styles, and the gate needs the right one.**
`build-deelevated.ps1` uses `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }` per step and a bare
`exit $LASTEXITCODE` at the end. `build-installer.ps1:61,79,105` uses
`if ($LASTEXITCODE -ne 0) { Write-Error "..." }` - which works only because
`$ErrorActionPreference = 'Stop'` turns it into a terminating error. **For `-Mode Baseline` use
`exit`, never `Write-Error`**, because the gate's pass condition is a *computed* verdict, not
"MSBuild returned non-zero".

**Never pass `/p:SelfContained` or `/p:WindowsAppSDKSelfContained`.** This is written down as a
first-class comment in `build-installer.ps1:63-68` and the script demonstrates it by passing a
custom flag instead:

```powershell
# NOTE: /p:SelfContained + /p:WindowsAppSDKSelfContained must NOT be passed here.
# They are GLOBAL properties and would flow into AkariTool.Core/Infrastructure
# (class libraries), where the Windows App SDK targets hard-error. The App csproj
# maps the custom /p:AkariPublish flag to both switches instead (project-file
# properties do NOT flow through ProjectReferences).
```

**Console-output idiom** (`build-installer.ps1:44,55,92,108-110`) - `Write-Host` with
`-ForegroundColor`, `Cyan` for the headline, `DarkGray` for progress, `Green` for success:

```powershell
Write-Host "Building Akari Tool v$version (unpackaged WinUI 3, fully self-contained)" -ForegroundColor Cyan
Write-Host "Using MSBuild: $msbuild" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Done -> $out" -ForegroundColor Green
```

The gate's `PASS (...)` / `FAIL (...)` lines should use this idiom, since RESEARCH's Validation
Architecture specifies their exact text (`PASS (warnings 118<=118, errors 2 allowlisted, tests 232>=230)`).

**Existing-artifact assertion idiom** (`build-installer.ps1:86-91`) - `Test-Path` on the exact expected
path, then `Write-Error` with the path interpolated into the message. This is the pattern for the
assembly inventory check; RESEARCH Pitfall 5/6 warn that a glob is meaningless here.

**What `run-tests.ps1` must NOT do**, restated from the two analogs and RESEARCH:

- Never `dotnet build` / `dotnet test` (`build-deelevated.ps1:25` comment).
- Never assert `$LASTEXITCODE -eq 0` after MSBuild - the build is *expected* to exit non-zero on the
  tolerated PRI175/PRI252 errors. Match the **error list** against the allowlist instead (D-16).
- Never glob `**\*.Tests.dll` - `bin\DeElevated\` holds an `asInvoker` duplicate of every assembly.
  Address the three exact paths below.

**Test-assembly invocation** (the verified working form, from RESEARCH; no repo analog exists):

```powershell
& $vst `
  "tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll" `
  "tests\AkariTool.Infrastructure.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.Tests.dll" `
  /Platform:x64 /logger:"trx;LogFileName=baseline-probe.trx" /ResultsDirectory:"$tmp"
```

Parse `<Counters total=".." executed=".." passed=".." failed=".." notExecuted=".."/>` from
`TestRun/ResultSummary/Counters` - never scrape `Total tests:`.

---

### `tools/baseline.json` (config / data - NEW)

**No analog.** No committed machine-readable data file of any kind exists in this repo
(`git ls-files` finds no `*.json` outside `obj/`, which is gitignored). Format and field set are
agent discretion per D-15. Two constraints bind the *content* regardless of format:

**Constraint 1 - the assembly list must match what the solution build actually emits.** Verified by
direct filesystem read of the current `bin/` tree. There are **9 expected files after Phase 1**, not
8 (RESEARCH.md's "6 project assemblies + 2 test assemblies = 8 after Phase 1" is off by one - Phase 1
adds a third test assembly):

| # | Exact path (relative to repo root) |
|---|---|
| 1 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll` |
| 2 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.exe` |
| 3 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.Core.dll` |
| 4 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.Infrastructure.dll` |
| 5 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\WinGet.Interop.dll` |
| 6 | `src\AkariTool.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\WinUI.Framework.dll` |
| 7 | `src\AkariTool.Core\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.dll` |
| 8 | `src\AkariTool.Infrastructure\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.dll` |
| 9 | `tests\AkariTool.Core.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Core.Tests.dll` |
| 10 | `tests\AkariTool.Infrastructure.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.Infrastructure.Tests.dll` |
| 11 | `tests\AkariTool.App.Tests\bin\Debug\net10.0-windows10.0.26100.0\AkariTool.App.Tests.dll` *(NEW)* |

Note the path asymmetry, which is why RESEARCH forbids globbing: the **App** output has a `win-x64`
segment (its csproj sets `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`), the **libraries** do not,
and a stale `src\AkariTool.App\bin\x64\Debug\...` tree also exists from direct-csproj builds.
`vendor\WinGet.Interop\bin\Debug\...` and `vendor\WinUI.Framework\bin\Debug\...` hold *further*
copies - `WinUI.Framework` is not in the solution yet still builds transitively, which is exactly
CONCERNS #7 and RESEARCH's note that assembly *presence* matters, not solution membership.
`bin\DeElevated\` at repo root is a fourth duplicate set and must be excluded.

Also assertable: `src\AkariTool.App\bin\Debug\...\win-x64\AkariTool.pri` exists and is 2,305,600
bytes today. Its existence is the *evidence* that the PRI errors are tolerated rather than fatal -
`build-installer.ps1:81-91` already treats a missing `AkariTool.pri` as a hard failure ("app launches
to NO WINDOW (process alive, blank)"), so the gate can borrow that framing.

**Constraint 2 - record only measured numbers.** RESEARCH Pitfall 11: `AGENTS.md` (53 + 136) and
`.planning/codebase/TESTING.md` (53 + 136) are both stale. The measured value is **230 total,
229 passed, 1 `NotExecuted`**. The baseline must be captured from the gate's own `/t:Rebuild` +
vstest run, **after** `AkariTool.App.Tests` lands (RESEARCH's ordering constraint / Pitfall 10).

---

### `tools/gen-setting-id-diff.ps1` (utility / transform - NEW)

**Analog for script discipline:** `build-deelevated.ps1` (param block, `$ErrorActionPreference`,
`$root`, `throw` on a failed precondition). That is all the repo offers; there is no text-parsing or
report-generating script anywhere.

**The Akari-side seam — 15 static factories, two model shapes.** All 11 `Build()` methods have the
identical signature, which is what makes reflection trivial and DI-free.
`src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs:6-24`:

```csharp
namespace AkariTool.Tabs.Gaming;

public static class GamingOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildGameMode(),
        .. BuildProcessor(),
        .. BuildGraphics(),
        .. BuildStorage(),
        .. BuildNetwork(),
        .. BuildXbox(),
        .. BuildSecurity(),
        .. BuildSystemServices(),
        .. BuildScheduledTasks(),
        .. BuildSystemRestore(),
        .. BuildAccessibility(),
        .. BuildVisualEffects(),
    ];
```

The other 10 (`PrivacyOptimizations`, `PowerOptimizations`, `NotificationsOptimizations`,
`SoundOptimizations`, `UpdateOptimizations`, `DesktopOptimizations`, `AppearanceOptimizations`,
`TaskbarOptimizations`, `StartMenuOptimizations`, `ExplorerOptimizations`) are identical in shape.
**Every one declares `AkariTool.Tabs.*`, not `AkariTool.Core.Features.*`** - the generator's type
names use `AkariTool.Tabs`, and Phase 3's ARCH-09 namespace alignment will break them (say so in a
comment).

The **four** SoftwareApps factories that D-09's "11 `Build()` methods" omits -
`src/AkariTool.Core/Features/Software/Catalogs/ExternalAppCatalog.cs:6-36`:

```csharp
public static partial class ExternalAppCatalog
{
    public static AppGroup GetExternalApps()
    {
        var allItems = new List<AppDefinition>();

        // Add all category items
        allItems.AddRange(Browsers.GetBrowsers().Items);
        // ... 15 more category calls ...

        return new AppGroup
        {
            Name = "External Apps",
            FeatureId = "ExternalApps",
            Items = allItems
        };
    }
}
```

The siblings are `WindowsAppCatalog.GetWindowsApps()`, `CapabilityCatalog.GetWindowsCapabilities()`,
`OptionalFeatureCatalog.GetWindowsOptionalFeatures()` - each also `public static`, zero-parameter,
returning `AppGroup`.

**The two shapes to walk.** `src/AkariTool.Core/Features/Common/Models/SettingGroup.cs` (whole file,
10 lines):

```csharp
using System.Collections.Generic;

namespace AkariTool.Core.Features.Common.Models;

public sealed record SettingGroup
{
    public required string Name { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<SettingDefinition> Settings { get; init; }
}
```

`src/AkariTool.Core/Features/Software/Catalogs/AppModels.cs:8,26-29,87-93`:

```csharp
namespace AkariTool.Tabs;
```

```csharp
public class AppDefinition : INotifyPropertyChanged
{
    // ── Immutable definition (matches Winhance ItemDefinition property names) ──
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
```

```csharp
/// <summary>A named group of app definitions (Winhance ItemGroup).</summary>
public record AppGroup
{
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public required string FeatureId { get; init; }
    public required IReadOnlyList<AppDefinition> Items { get; init; }
}
```

So the walk is `Build()` → `group.Settings.Select(s => s.Id)` and
`Get*()` → `group.Items.Select(i => i.Id)`, dispatched on the returned type.
`AppGroup` has no `Id` of its own; identity is `FeatureId` + per-item `Id`.

#### ⚠ Anti-analog: do NOT copy `SettingCatalogValidatorTests.cs`'s catalog switch

`tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs:434-463` looks like exactly the
seam SPIKE-03 needs and is **wrong** - it is the "second place to update" failure mode D-09 exists to
prevent, already written and live in the repo:

```csharp
    public static IEnumerable<object[]> AllFeatureCatalogs()
    {
        yield return new object[] { "Taskbar", Tabs("Customize.Taskbar") };
        yield return new object[] { "StartMenu", Tabs("Customize.StartMenu") };
        // ... 9 more
    }

    private static IReadOnlyList<SettingGroup> Tabs(string key) => key switch
    {
        "Customize.Taskbar" => AkariTool.Tabs.Customize.TaskbarOptimizations.Build(),
        // ... 10 more, then:
        _ => throw new InvalidOperationException($"Unknown catalog key '{key}'."),
    };
```

A hand-maintained string→factory switch, covering **11 of 15** entry points. Read it to understand the
shape; **do not reuse it.** Reflect instead: enumerate `public static` zero-parameter methods on
`AkariTool.Core` types and dispatch on return type. This is also the fact Phase 2's BUG-02 test will
need, so build the seam to be reusable rather than script-private.

**Second warning, same source:** Akari catalogs put the *same string* in both `FeatureId` and `Id`
(`GamingOptimizations.cs:31,36` - both `"gaming-game-mode"`). A text parse for `Id = "…"` therefore
**over-counts** the Akari side. The Akari side must be reflective; only the Winhance side (plain C#
literals, no factory seam) is text-parsed.

**Third:** RESEARCH Pitfall 8 - the report must emit both a raw and a normalised diff, because Akari
prefixes the domain (`customize-explorer-show-file-extensions`) where Winhance suffixes the page name
(`explorer-customization-shortcut-suffix`). Optimize and SoftwareApps share a scheme, so the bug is
invisible there. Classification: `raw-equal / normalised-equal / Akari-only / Winhance-only /
both-but-normalised-conflict`.

---

### `tools/gen-winhance-id-snapshot.ps1` (utility / transform - NEW)

**Analog:** only `build-installer.ps1`'s comment style and `$root`/`throw` discipline. No parsing
analog exists in the repo.

Two house conventions worth copying verbatim:

**Provenance comments on derived data.** `src/AkariTool.Core/Features/Software/Catalogs/ExternalAppCatalog.cs:1-2`
and `AppModels.cs:1-3` both open with a one-line provenance note before any `using`. The snapshot
header must record `git -C <winhance> rev-parse HEAD` in the same spirit - attributability, not
content copying.

**`-replace` over files rather than a parser** - `build-deelevated.ps1:44-48`:

```powershell
$manifest = (Get-Content (Join-Path $root 'src\AkariTool.App\app.manifest') -Raw) `
    -replace 'level="requireAdministrator"', 'level="asInvoker"'
$manifestPath = Join-Path $objDir 'app.asinvoker.manifest'
Set-Content -Path $manifestPath -Value $manifest -Encoding utf8
```

Note `(Get-Content … -Raw)`, the line-continuation backtick, and `-Encoding utf8` on `Set-Content`.
The repo already reads and rewrites a WinUI-3-C#-adjacent text file this way.

**Security constraints (RESEARCH §Security, ASVS V5):** treat every parsed token as untrusted - never
`Invoke-Expression`, never interpolate a parsed string into a command or a path. Open the Winhance
checkout read-only and never write to it. Emit only sorted, escaped `key=value` / plain text. The
snapshot is *derived data Akari produced by parsing*, never a copied file.

---

### SPIKE-02 throwaway project (`spike/WinUiControlCompat/*`)

**Analog:** `src/AkariTool.App/AkariTool.App.csproj` (git-tracked) - the nearest WinUI project shape.

**The property block** (`src/AkariTool.App/AkariTool.App.csproj:2-25`):

```xml
  <PropertyGroup>
    <Version>2.0.3</Version>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>AkariTool</RootNamespace>
    <AssemblyName>AkariTool</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\AkariLogo.ico</ApplicationIcon>
    <LangVersion>latest</LangVersion>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <WinUISDKReferences>false</WinUISDKReferences>
    <EnableMsixTooling>true</EnableMsixTooling>
    <!--
      Unpackaged + self-contained: this machine has no installed Windows App SDK
      runtime, so the runtime ships alongside the EXE (mirrors WinUI.Framework.App).
    -->
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
```

**What the spike may keep and what it must drop:**

| Property | Spike | Why |
|---|---|---|
| `OutputType`, `TargetFramework`, `TargetPlatformMinVersion`, `Platforms`, `RuntimeIdentifier`, `UseWinUI` | **keep** | a `WinExe` is the only thing that can render (D-06); `Platforms=x64` + `RuntimeIdentifier=win-x64` mirror the solution |
| `WindowsAppSDKSelfContained` | **omit** | on a `Library` it is a hard error from the SDK targets. On a `WinExe` it is *safe* - RESEARCH Pitfall 3 explicitly allows `/p:WindowsAppSDKSelfContained=true` for the spike |
| `EnableMsixTooling` | **`false`** | `AkariTool.App.csproj:16` sets it `true`; the AppX targets warn about multiple executables. RESEARCH: set it `false` so the spike's own log stays clean |
| `WindowsPackageType` | **`None`** | matches App (line 21) and keeps the spike unpackaged |
| `ApplicationManifest`, `ApplicationIcon`, `Version`, `RootNamespace`, `AssemblyName` | **omit / change** | the spike is not the app; it needs no `requireAdministrator` manifest |
| `ProjectReference` (any) | **omit** | RESEARCH: a reference to `AkariTool.App` drags the self-contained `win-x64` graph, `Material.Icons.WinUI3`'s CsWinRT floor and `vendor/WinUI.Framework` - three variables the spike does not need |
| `CommunityToolkit.Mvvm`, `Material.Icons.WinUI3`, `FluentIcons.WinUI`, `Microsoft.Extensions.DependencyInjection`, `System.Management`, `System.ServiceProcess.ServiceController` | **omit** | none are needed to render three controls |

**Package-reference comment style** (`src/AkariTool.App/AkariTool.App.csproj:54-67`) - every
`PackageReference` carries a `<!-- -->` comment stating *why it is there*. Copy that discipline for the
spike's package set, since each pin is a deliberate decision that must survive the project's deletion:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.3.1" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <!-- Row header icons (Winhance parity): Material SVG paths + Fluent glyphs,
         resolved by IconConverter from SettingDefinition.Icon / .IconPack. -->
    <PackageReference Include="Material.Icons.WinUI3" Version="3.0.2" />
```

RESEARCH supplies the verified candidate package set and its version provenance (NuGet
`v3-flatcontainer` + `registration5`, all Microsoft-owned, all approved for the spike). Copy those
versions and their inline justifications from RESEARCH.md §SPIKE-02 verbatim into the spike csproj -
do not re-derive them, and do not let a restore failure be recorded as a render verdict (Pitfall 12).

**Where the spike lives:** any path outside `src/` and `tests/`. `tools/spike/WinUiControlCompat/`
is the natural home under D-15 (durable code lives in `tools/`), and it guarantees `AkariTool.sln`
builds never touch it. The plan must state the chosen path so D-05's deletion is unambiguous.

**XAML/code-behind layout:** the repo pairs `<Name>Page.xaml` + `<Name>Page.xaml.cs`. For the spike's
single window, `App.xaml(.cs)` + `MainWindow.xaml(.cs)` mirrors `src/AkariTool.App/`. The existing
`MainWindow.xaml.cs` is the structural analog for the code-behind, but note the spike's
`MainWindow.xaml.cs` should contain **no** DI, no `App.xaml.cs` service registration, and no
`ServiceLocator` call - the whole point is a clean-room render.

**How this repo thinks about invoking PowerShell** (relevant only if the verdict capture shells out) -
`src/AkariTool.Infrastructure/Features/Common/Services/PowerShellRunner.cs:13-14,25-33`:

```csharp
    private const string PowerShellPath =
        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
```

```csharp
        var psi = new ProcessStartInfo(PowerShellPath,
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
```

and lines 42-52, the stdout/stderr-separate-and-log-then-check-exit-code shape:

```csharp
            if (process.ExitCode != 0)
                _log.Log(LogLevel.Warning, $"[PowerShell] Exited with code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[PowerShell] Failed: {ex.Message}");
        }
```

The flag set (`-NoProfile -NonInteractive -ExecutionPolicy Bypass`) and the
"stdout, stderr, exit code are three separate signals" discipline are the transferable parts. Note
this analog runs in the *opposite direction* (C# → PowerShell), so it is a partial match only.

---

## Shared Patterns

### Toolchain discovery - never hardcode a VS path

**Source:** `build-deelevated.ps1:25-34` (primary), `build-installer.ps1:46-55` (variant)
**Apply to:** every `tools/*.ps1` that touches MSBuild or vstest

RESEARCH verified `Test-Path` on the `AGENTS.md` path returns **False** on this machine; the install is
Visual Studio Build Tools 2026 under `Program Files (x86)`. Both existing scripts already do the right
thing. Derive `$vsroot` from vswhere on every run; a hardcoded path is a roadmap-wide fragility
because nine later phases' Build Gates cite these scripts by path.

### Fail-fast preconditions

**Source:** `build-installer.ps1:40-43, 48-50, 54, 100-102` and `build-deelevated.ps1:32-34`
**Apply to:** all new scripts

Every external dependency is `Test-Path`-checked, then `throw`/`Write-Error`-ed with the *fix*
embedded in the message. Copy the message style:

```powershell
    if (-not $msbuild) { Write-Error "MSBuild.exe not found via vswhere. Is the .NET desktop / WinUI workload installed?" }
```

```powershell
if (-not $iscc) {
    Write-Error "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup"
}
```

RESEARCH adds one: an elevation precondition check (`WindowsPrincipal.IsInRole(Administrator)`),
because the suite reads registry state and `tools/` is a privileged surface.

### Comments as design rationale, not narration

**Source:** `build-deelevated.ps1:6-16`, `build-installer.ps1:10-28`,
`src/AkariTool.App/AkariTool.App.csproj:17-20,27-34,40-52`,
`ExternalAppCatalog.cs:1-2`, `NumericConversionHelper.cs:9-13,28-34`
**Apply to:** every new file in this phase

The house style is a *why*-comment carrying the trap that motivated the code, usually in the form
"`-- Label ----`" for scripts, XML comments in csproj, `// ── Label ──` banners in C#, and inline
`<!-- ... -->` in catalogs. Phase 1 has at least four traps that belong in comments, not in a phase
document a reader will never open: the `/p:`-global-property leak, the `$LASTEXITCODE`-is-not-the-gate
truth, the `UseWinUI`-on-a-library PRI risk, and the 15-vs-11 entry-point count.

### Derived data carries provenance

**Source:** `ExternalAppCatalog.cs:1-2`, `AppModels.cs:1-3`, `SettingCatalogValidatorTests.cs:434-463`
**Apply to:** `tools/baseline.json`, `tools/data/winhance-setting-ids.txt`, the generated report

Each derived artefact states its source and its capture time/commit in its own header. The Winhance
snapshot records `git rev-parse HEAD`; `baseline.json` records the toolchain versions; the report
records both sides' normalisation rule. This is also the licensing control - attribution by reference
rather than by reproduction.

### Never let an instrument perturb what it measures

**Source:** `build-deelevated.ps1:6-16` (the isolation write-up) - the closest existing precedent
**Apply to:** the SPIKE-02 spike, and the SPIKE-01 baseline ordering

`build-deelevated.ps1` exists because an earlier de-elevated build poisoned `obj\` and later normal
builds picked up the wrong manifest. Its lesson - *structural isolation over discipline* - is the
pattern for Phase 1: the spike is a separate project outside `src/`, not a scratch page inside
`AkariTool.App` (D-05), and the baseline capture runs *after* the test project lands.

---

## No Analog Found

Do not manufacture an analog for these. The planner should use `01-RESEARCH.md` as the source of truth.

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `tools/baseline.json` | config / data | file I/O | `tools/` does not exist (0 tracked files). No committed machine-readable data file exists anywhere in the repo - `git ls-files` finds no `*.json` outside gitignored `obj/`. Field set and format are agent discretion (D-15); RESEARCH pins the *contents* (assembly list, warning/error counts, test count). |
| `tools/data/winhance-setting-ids.txt` (+ per-domain `*.ids.txt`) | config / data | file I/O | D-08's committed snapshot has no precedent. It must be **derived by parsing** the read-only Winhance checkout and must **never** be a copied file (PolyForm noncompete + required notice). Header must carry the Winhance commit SHA for attribution. |
| `.planning/phases/01-…/01-BASELINE.md`, `01-SPIKE-02-VERDICT.md`, `01-SETTING-ID-DIFF.md` | docs | n/a | `.planning/` is git-tracked (22 files) so they are committable, but no comparable artefact has ever been committed in this repo. Doc style should follow `AGENTS.md`'s own conventions section rather than an artifact analog. |

---

## Corrections the Planner Must Carry Forward

Facts in upstream documents that this pass disproved or sharpened. Propagating any of them into
`PLAN.md` produces a wrong action.

| Claim | Source | Correction |
|---|---|---|
| "AkariTool.sln has 5 real projects + 2 solution folders" | CONTEXT.md `<canonical_refs>` | **6 real projects + 3 solution folders** (`src`, `tests`, `vendor`). Verified in the solution text. |
| "`AkariTool.sln` — 5 real projects" / "2 test assemblies = 8 after Phase 1" | CONTEXT.md, RESEARCH §Toolchain | Assembly inventory is **9 expected files after Phase 1** (6 project + 3 test). `src\AkariTool.App` contributes *two* (`AkariTool.dll` + `AkariTool.exe`). Table in the `tools/baseline.json` section above. |
| `MSBuild.exe` at `C:\Program Files\Microsoft Visual Studio\18\Community\...` | `AGENTS.md`, CONTEXT.md | Path does not exist on this machine. Discover via `vswhere` - which both existing scripts already do correctly, so the *scripts* are not wrong, only the *documentation*. |
| "AkariTool.Core.Tests: 53 passing; Infrastructure: 136 passing + 1 skipped" | `AGENTS.md`, `.planning/codebase/TESTING.md` | Stale. Measured on the current `bin/`: **Core 93, Infrastructure 137, total 230 (229 passed, 1 `NotExecuted`)**. Baseline records the measured value only. |
| TESTING.md's `dotnet test` run command | `.planning/codebase/TESTING.md` | Violates the VS-MSBuild-only constraint. D-03 resolves it in favour of MSBuild + vstest. Do not reintroduce it in code, comments, or docs. |

---

## Metadata

**Analog search scope:** repo root (`*.ps1`, `AkariTool.sln`), `src/AkariTool.App/` (csproj,
`ViewModels/Tweaks/`, `Services/`, `MainWindow*`), `src/AkariTool.Core/` (csproj,
`Features/*/Catalogs/`, `Features/Common/Models/`, `Features/Common/Validation/`),
`src/AkariTool.Infrastructure/` (`Features/Common/Utilities/`, `Features/Common/Services/`),
`tests/` (all 18 `*Tests.cs` files surveyed; 3 read in full), `.gitignore`, `vendor/`.

**Files read in full:** `AGENTS.md`, `build-installer.ps1` (111), `build-deelevated.ps1` (65),
`AkariTool.sln` (121), `src/AkariTool.App/AkariTool.App.csproj` (97), `src/AkariTool.Core/AkariTool.Core.csproj` (18),
`tests/AkariTool.Core.Tests/AkariTool.Core.Tests.csproj` (23),
`tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` (24),
`tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs` (473),
`tests/AkariTool.Core.Tests/Features/BadgePillStateTests.cs` (32),
`src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs` (384),
`src/AkariTool.Infrastructure/Features/Common/Utilities/NumericConversionHelper.cs` (59),
`src/AkariTool.Core/Features/Common/Models/SettingGroup.cs` (10),
`src/AkariTool.Core/Features/Software/Catalogs/ExternalAppCatalog.cs` (37),
`src/AkariTool.Core/Features/Software/Catalogs/AppModels.cs` (100, lines 1-100),
`src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs` (50, lines 1-50),
`src/AkariTool.Infrastructure/Features/Common/Services/PowerShellRunner.cs` (54),
`tests/AkariTool.Infrastructure.Tests/Features/SettingOperationExecutorTests.cs` (80, lines 1-80).

**Filesystem verifications performed:** `Test-Path tools` → False; `git ls-files -- tools` → 0 files;
`git ls-files -- .planning` → 22 files; current assembly inventory under `src/**/bin` and
`tests/**/bin` (11 relevant DLLs/EXEs enumerated by exact path); `bin/DeElevated` exists → True;
3 solution-folder entries in `AkariTool.sln`.

**Tracked-source verification:** all 10 analog paths confirmed via `git ls-files -- <path>` returning
non-empty. No path under `plugins/` or any untracked mirror is named anywhere in this document.

**Pattern extraction date:** 2026-10-05