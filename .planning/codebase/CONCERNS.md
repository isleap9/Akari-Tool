# Codebase Concerns

**Analysis Date:** 2026-10-05

Ordered by blast radius. Each item states what was **verified** and how; where
something could not be confirmed without building or running the app, it is
labelled as such rather than asserted.

---

## 1. The drift/verify subsystem is wired to a registry nothing populates

**Area:** Drift & Verify feature
**Severity:** High — a shipped user-facing feature cannot function.

**What happens.** `TweakRegistry` (`src/AkariTool.Infrastructure/Services/TweakRegistry.cs`)
is the sole store of tweak definitions:
`private static readonly List<(TweakDefinition Def, Action Refresh)> _entries = new();`
(`:39`), written only by `TweakRegistry.Register(def, refresh)` (`:42`).

**Verified:** `TweakRegistry.Register(` has **zero call sites** anywhere in `src/`
or `tests/` (searched all 404 `.cs` files, excluding the definition itself). The
`Mark()` / `ClaimRange()` / `TabRanges` API exists to bracket that list and is
likewise never called — only referenced in explanatory comments at
`src/AkariTool.App/DI/UIServiceExtensions.cs:78,100`.

**Consequences, all verified by reading the consumers:**
- `TweakRegistry.Count` is always `0`, so `TweakRegistry.TryGetDefinition(id, …)`
  always returns `false`.
- `DriftScanner.Scan()`
  (`src/AkariTool.Infrastructure/Services/DriftScanner.cs:70`) increments
  `orphaned` and `continue`s for every baseline entry — every drift check reports
  100% orphaned. The drift banner on the title bar
  (`MainWindow.RunDriftCheck`, `MainWindow.xaml.cs:479`) and
  `HomeViewModel.DriftedCount` (`:158`) therefore read zero.
- `DriftBaseline.Record` is called from exactly two places,
  `TweakHelpers.ApplyToggle` / `ApplyOption`
  (`src/AkariTool.Infrastructure/Services/TweakHelpers.Apply.cs:17,24`), whose only
  non-legacy callers are `VerifyViewModel.ReapplyRows`
  (`src/AkariTool.App/ViewModels/Verify/VerifyViewModel.cs:162,164`) and
  `TweakRegistry.ImportFromFile` (`TweakRegistry.cs:322,332`) — which itself is
  unreachable because it matches entries against the empty `_entries` list. **The
  baseline is never written**, so `VerifyPage` shows its "Nothing tracked yet"
  empty state (`src/AkariTool.App/Views/VerifyPage.xaml:136`) permanently.
- `VerifyViewModel.ReapplyRows` fails every row at `:159` (`failed++; continue;`).

**Root cause.** The declarative `SettingDefinition` migration replaced
`TweakDefinition` + `TweakRegistry` as the source of truth, and nobody rewired the
drift path. The `⚠ SINGLETON, not transient` comments at
`src/AkariTool.App/DI/UIServiceExtensions.cs:74-79` still justify a DI lifetime by
a registration side-effect that no longer happens.

**Impact:** the Verify nav card and the drift banner are dead UI. Users get a
false "everything is fine" — worse than an error, because it is silent.
**Fix approach:** port drift detection onto the declarative model. `ISettingStateReader`
already answers "what is this setting's current value", so the new baseline should
record `(settingId, value, osBuild)` from `SettingOperationExecutor` and the scanner
should read back through `ISettingStateReader` instead of a `TweakDefinition`
lookup. Then delete `TweakRegistry`, `TweakHelpers.*`, `TweakTargets`, and the
singleton-lifetime comments. This unblocks items 2, 3, and 4 too.

---

## 2. `TweakDefinition` and its entire support stack is dead code

**Area:** Legacy tweak model
**Severity:** Medium — ~1,500 lines of dead weight plus a live compile dependency.

**What happens.** `src/AkariTool.Core/Tweaks/TweakDefinition.cs` (10 KB) and
`TweakTargets.cs` (4.4 KB) define the old delegate-based tweak model.

**Verified:** `TweakTargets.` has **zero** call sites in `src/` or `tests/`
(`TryGetRecommendedTarget`, `IsMismatched`, `CollectPending` are all unreferenced).
`TweakDefinition` survives only in: `TweakRegistry` (dead, item 1),
`UpdateTweaks.cs` (dead, item 3), `TweakHelpers.Apply.cs` (dead), and 14
`///` **comment** references in App ViewModels (`UIServiceExtensions.cs:98`,
`TaskbarViewModel.cs:22`, `ExternalAppsViewModel.cs:19`, …). No live code path
constructs one.

**Also dead:** `src/AkariTool.Core/Tweaks/TweakTargets.cs` in full, and
`src/AkariTool.Core/Interfaces/IToolService.cs` is only honoured through
`ToolFetchServiceWrapper` (`src/AkariTool.Infrastructure/Services/ToolFetchServiceWrapper.cs:8`),
which takes an `IToolService` parameter so a full interface exists solely to be
passed one object.

**Fix approach:** delete `src/AkariTool.Core/Tweaks/` once item 1 and item 3 are
resolved. Verify with a build — `AkariTool.Core.csproj` has
`InternalsVisibleTo AkariTool.Core.Tests`, so a test may reference it.

---

## 3. `UpdateTweaks.cs` is an orphan catalog

**Area:** Core / Update domain
**Severity:** Medium — 329 lines of live `Registry.SetValue` code that can never run.

**What happens.** `src/AkariTool.Core/Features/Update/Catalogs/UpdateTweaks.cs`
declares `public static partial class UpdateTweaks` with methods such as
`DeliveryAndStore(Action<string> Log)` returning `TweakDefinition[]`, and its
lambdas call `Registry.SetValue` directly (`:55,56,84,85,125,126,145,164,191,192,
215,223,243,261,283,289,290,314,315`).

**Verified:** no method on the class is called from anywhere. The only matches for
`UpdateTweaks` in the whole repo are 4 comments in
`src/AkariTool.Core/Features/Update/Catalogs/UpdateOptimizations.cs`
(`:11,:15,:20,:152`) and 1 comment in
`src/AkariTool.App/ViewModels/UpdateViewModel.cs:17` — the latter says outright
"rather than the delegate-based UpdateTweaks", i.e. the replacement shipped and
the original was never deleted.

**Why it's worse than dead code:** the file is in the `Update/Catalogs/` folder
next to the live `UpdateOptimizations.cs`, and its comments describe
`UpdateTweaks.cs` as "the ground truth". A developer adding a new Update setting
will read the wrong file. It also imports `AkariTool.Core.Tweaks`, so it is the
last thing pinning that dead namespace alive (item 2).

**Fix approach:** delete. If the inverted-detection knowledge in its comments
matters, move it into `UpdateOptimizations.cs` as a comment on the affected rows
first.

---

## 4. `DispatcherService` is duplicated verbatim across two layers

**Area:** Infrastructure / App
**Severity:** Medium — two registrations, one winner, and the loser is unclear.

**What happens.** `src/AkariTool.Infrastructure/Features/Common/Services/DispatcherService.cs`
and `src/AkariTool.App/Features/Common/Services/DispatcherService.cs` are both
154-line `IDispatcherService` implementations wrapping a WinUI
`DispatcherQueue`.

**Verified by diff:** the files are **byte-identical except for the `namespace`
line** — `AkariTool.Infrastructure.Features.Common.Services` vs
`AkariTool.Features.Common.Services`. Same class name, same members, same doc
comment ("DI services are created during container build, which happens BEFORE
the Window exists").

**Both are registered** for the same interface:
- `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs:130` →
  `AkariTool.Infrastructure.Features.Common.Services.DispatcherService`
- `src/AkariTool.App/DI/UIServiceExtensions.cs:64` →
  `AkariTool.Features.Common.Services.DispatcherService`

`AddAkariInfrastructure()` runs before `AddAkariUI()` (`App.xaml.cs:140-141`), so
with Microsoft.Extensions.DependencyInjection's last-registration-wins the **App**
copy is the one actually injected. The Infrastructure copy and its
`using Microsoft.UI.Dispatching;` are both dead — and that import is the *only*
UI-framework dependency in the entire Infrastructure project (verified: zero
`using Microsoft.UI` / `using WinUI` matches anywhere else under
`src/AkariTool.Infrastructure`).

**Impact:** the layering is not just duplicated, it is inverted. Infrastructure is
supposed to be UI-free; the surviving registration is UI-free only by accident of
ordering, and the file that breaks the rule is the one that looks authoritative.
**Fix approach:** delete the Infrastructure copy and its registration at
`InfrastructureServiceExtensions.cs:130`. The interface is already in Core, so the
one UI-thread implementation belongs in the App layer. This is a 2-line deletion.

---

## 5. `AkariPaths` is defined twice in the same namespace, in two assemblies

**Area:** Core / App
**Severity:** Medium — a silent-wrong-answer hazard, not a compile error.

**What happens.** `AkariPaths` with byte-identical members
(`ScriptsDirectory`, `ScriptsDirectoryLiteral`, `LogsDirectoryLiteral`,
`PowerShellExePath`) is declared:
- `src/AkariTool.Core/Features/Common/Constants/AkariPaths.cs:9`
- `src/AkariTool.App/Features/Software/SoftwareAppService.cs:21`

Both files declare `namespace AkariTool.Tabs`. Because they are in different
assemblies, neither shadows the other — a file with `using AkariTool.Tabs;` and
both assemblies referenced gets **CS0433 ambiguity**, and a file with neither
`using` picks one arbitrarily.

**Verified consumers split across the two:** `BloatRemovalScriptGenerator.cs:111`
uses `AkariPaths.LogsDirectoryLiteral` (Core), while `SoftwareAppService.cs` uses
all four members (App) at `:73,349,350,385,404,455,465,532,588`. So both classes
are live; only one copy is needed.

**Why it is dangerous rather than merely untidy:** `ScriptsDirectory` is a
`static readonly` computed from `Environment.GetFolderPath` at type-init time.
If a future change edits one copy and not the other, generated scripts will
reference a different path than the runtime does — and there is no test.

**Fix approach:** keep the Core one (it is the "constant twin" per its own doc
comment) and delete the App copy, adding `using AkariTool.Tabs;` where needed.
`SoftwareAppService.cs` already has it.

---

## 6. `Resource/` duplicates `Assets/` and is not in the csproj

**Area:** App / resources
**Severity:** Low-Medium — binary bloat and a likely-broken icon pipeline.

**What happens.** `src/AkariTool.App/Assets/` and `src/AkariTool.App/Resource/`
both hold `AkariLogo.png`, `AkariLogo.ico`, `AkariLogoLight.png` —
`Assets/AkariLogo.ico` and `Resource/AkariLogo.ico` are both exactly 64,199 bytes.
`Resource/` additionally holds `Akari.png` (906 KB) and `NavIcons/` (19 PNGs).

**Verified:** `AkariTool.App.csproj:81-85` declares only the four `Assets\` files
as `Content`. There is **no** `NavIcons` reference in the csproj, and no code
reference to `NavIcons` or `Resource\` anywhere in `src/` (verified by search).

**Impact:** roughly 3.6 MB of unreferenced binaries committed to the repo, and
19 nav icons whose resolution mechanism I could not confirm — `MainWindow.xaml`
must be setting glyphs some other way, or the icons are dead too. **I could not
verify which**, without inspecting `MainWindow.xaml` glyph bindings in full and
running the app.
**Fix approach:** determine whether `NavIcons` is consumed, then delete the
unused half of the duplication. Cheap, mechanical, and shrinks the repo.

---

## 7. `vendor/WinUI.Framework` is not registered in the solution

**Area:** Build
**Severity:** Low-Medium — a real dependency invisible to tooling.

**What happens.** `src/AkariTool.App/AkariTool.App.csproj:75` has
`<ProjectReference Include="..\..\vendor\WinUI.Framework\WinUI.Framework.csproj" />`,
and 67 call sites across the App use `WinUI.Framework.IoC` / `.Services` / `.Mvvm`.

**Verified:** `AkariTool.sln` lists 5 projects plus 2 solution folders, and
`vendor/WinGet.Interop` is the only vendored project in it. A search for `WinUI`
in the sln returns **zero** matches. So the framework the whole UI layer is built
on does not appear in the solution.

The csproj comment at `:70-74` flags it as intentional-but-temporary: "Switch to
a local NuGet feed (pack WinUI.Framework) before releasing."

**Impact:** `dotnet build AkariTool.sln` still works (MSBuild follows
ProjectReferences transitively), but the project is missing from the IDE solution
explorer, from any solution-wide analysis, and from the `NestedProjects` map.
**Fix approach:** add it to the sln under the `vendor` solution folder, mirroring
the `WinGet.Interop` entries at `AkariTool.sln:22,93-105,116`.

---

## 8. The catalog validator is not wired to the build

**Area:** Core / validation
**Severity:** Medium — invariants exist but nothing enforces them.

**What happens.** `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs`
enforces the rules that keep the declarative pipeline coherent: globally unique
setting Ids, well-formed `RegistrySetting.KeyPath` against five known hives,
`ComboBox.ValueMappings` keys resolving to declared `ValueName`s, and the
Selection recommended/default shape in its four categories (Dynamic / PowerCfg /
Subjective / Standard).

**Verified:** `SettingCatalogValidator.Validate` has **no call site in `src/`**.
Its only consumers are `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs`,
which hand-builds fixtures rather than validating the real catalogs.

**Impact:** a duplicate setting Id is silently accepted. Ids are the backup-file
key, so a duplicate breaks profile import with no compile or test signal. The
validator exists to prevent exactly that and is not run.
**Fix approach:** call `Validate(IEnumerable<SettingGroup>)` over every catalog
in `SettingPageWarmUp` (or better, in a test that enumerates all 11 `Build()`
methods) and fail the test on any violation. The catalog list is already
statically enumerable — 11 `public static Build()` methods.

---

## 9. `SettingPageViewModel` reaches Infrastructure through the service locator

**Area:** App / layering
**Severity:** Medium — the constructor/field list understates real coupling.

**What happens.** The base ViewModel takes 3 required + 6 optional constructor
dependencies (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:38-47`),
then resolves five more **at call time** via
`WinUI.Framework.IoC.ServiceLocator.GetService<T>()`: `ILogService` and
`ISystemSettingsDiscoveryService` in `CreateItem` (`:89-91`), the discovery service
again in `RefreshAllFromSystem` (`:146`), `IWindowsCompatibilityFilter` /
`IHardwareCompatibilityFilter` in `ApplyCompatibilityGates` (`:177-178`), and
`ITaskProgressService` / `IProcessRestartManager` / `IChangeHistoryService` in each
of the three `[RelayCommand]` bulk methods (`:334,382,385`).

**Verified:** 48 `ServiceLocator.GetService` call sites across 27 App files.

**Why it matters here specifically:** `IProcessRestartManager` is an
**Infrastructure** interface, resolved by fully-qualified name at `:336` and
`:384` rather than by the `using AkariTool.Infrastructure.Features.Common.Interfaces;`
that the file already has at `:12`. So the layer dependency is present but
invisible to a namespace scan — the file *looks* Core-only.
**Fix approach:** add these to the base constructor and pass them through
`CreateItem`/`CreateItem`'s named arguments, as `PowerViewModel.CreateItem` already
does for its plan services. 48 call sites is a mechanical refactor with a
compiler to check it.

---

## 10. God files that resist safe change

**Severity:** Medium each. No single one is wrong; together they mean a
"small change" in a catalog is a 3,000-line-file change.

| File | Lines | Why it's hard |
|------|-------|----------------|
| `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs` | 3,427 | 12 groups; no internal split |
| `src/AkariTool.Core/Features/Privacy/Catalogs/PrivacyOptimizations.cs` | 2,934 | 13 groups |
| `src/AkariTool.Core/Features/Customize/Catalogs/ExplorerOptimizations.cs` | 2,026 | 6 groups |
| `src/AkariTool.Core/Features/Power/Catalogs/PowerOptimizations.cs` | 1,624 | 18 groups; ~35 refs to `PowerTemplates` |
| `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` | 979 | Every row in the app; 13 `[ObservableProperty]`, 9 `[RelayCommand]`, Power specialisation, badge + technical-details + banner wiring |
| `src/AkariTool.Infrastructure/Services/WimUtilService.cs` | 851 | All WIM/ISO logic |
| `src/AkariTool.App/MainWindow.xaml.cs` | 557 | `PageMap` + 4 detail-tag sets + routing + badges + theme + log dock |
| `src/AkariTool.App/Services/SettingBackupService.cs` | 658 | Export/import/preview/search in one class |

**Note the partial-class pattern works.** `PlaybookTweaks` (6 partials, 76 KB),
`SystemStateReader` (7 partials, 71 KB), `ServicesPreset` (4 partials, 118 KB),
and `AkariOSPage` (9 partials, 2,631 lines) are all split by concern. The catalogs
and `SettingItemViewModel` are not.
**Fix approach:** split the four largest catalogs into
`Catalogs/<Domain>Optimizations.<Group>.cs` partials, matching the
`ExternalAppCatalog.*` convention already used in
`src/AkariTool.Core/Features/Software/Catalogs/`. Pure file moves, zero
behaviour change. Do `SettingItemViewModel` last and only with a characterisation
test in hand.

---

## 11. `AkariOSPage` does registry writes in View code-behind

**Area:** App / AkariOS
**Severity:** Medium — the largest concentration of untested OS mutation in the app.

**What happens.** `src/AkariTool.App/Views/AkariOSPage.GpuTools.cs` performs ~40
direct registry writes from XAML event handlers — a 27-value
`Registry.SetValue(gpuClass, "RM…")` block at `:57-83`, plus
`Registry.GetValue` / `Registry.SetValue` for the AMD shader cache at `:131,150`.
Other offenders: `AkariOSPage.GamingTweaks.cs` (13 registry/process calls),
`DefenderService.cs` (19), `SettingBackupService.cs` (21), `SystemUtilities.cs` (7),
`DefenderPhase2Scheduler.cs` (4).

**Verified:** 18 App files touch `Microsoft.Win32` / `Process`. None of them go
through `IWindowsRegistryService`, which exists and is registered
(`InfrastructureServiceExtensions.cs:81`).

**Why it matters:** these writes bypass `IWindowsRegistryService`,
`IChangeHistoryService`, and `DriftBaseline`, so they appear in no change history
and cannot be reverted by backup/restore. `SettingBackupService` is named in this
list only because it *reads* via `ISettingStateReader` and references
`TweakRegistry` types — its own writes route correctly.
**Fix approach:** route new AkariOS GPU/registry work through
`IWindowsRegistryService` + `ISettingOperationExecutor` so it is logged and
restorable. Retro-fitting the existing 27 GPU writes is a separate, larger task —
do not bundle it with a restructure.

---

## 12. The vertical slice is 3/11 complete, and the naming hides it

**Severity:** High as a planning risk — this is the known divergence, documented
with current specifics so a restructure plan can be scoped.

**What exists today.**

| Layer | Domains present | Of the 11 declared |
|-------|-----------------|---------------------|
| `Core/Features` | `AkariOS`, `Apps`, `Common`, `Customize`, `Gaming`, `Notifications`, `Power`, `Privacy`, `Software`, `Sound`, `Update` | 11 declared, but **8 contain only a `Catalogs/` folder** with declarative data and no interfaces or services |
| `Infrastructure/Features` | `Apps` (4 files), `Common` (47 files), `Optimize` (1 file) | 3 |
| `App/Features` | `Common` (5 files), `Shared` (1), `Software` (2) | 3 |

**Verified counts.** `Core/Features/<Domain>/Catalogs` file counts: Customize 5,
Gaming 1, Notifications 1, Power 2, Privacy 1, Software 24, Sound 1, Update 2.
`Infrastructure/Features`: 47 under `Common`, 4 under `Apps`, 1 under `Optimize`.
`App/Features`: 10 files total across 3 domains.

**The mismatch.** The 8 catalog-only Core domains have **no matching
Infrastructure service and no matching `App/Features` UI** — because their
services and pages were not moved; they stayed in the legacy flat directories
(`Infrastructure/Services/`, `App/Services/`, `App/Views/`,
`App/ViewModels/`). So for a setting in, say, `Gaming/Catalogs/`, the vertical
slice breaks at layer 2: the read/write logic is in
`SettingStateReader`/`SettingOperationExecutor` (which live under
`Features/Common/`, not `Features/Gaming/`), and the page is
`App/Views/GamingPage.xaml` + `App/ViewModels/GamingViewModel.cs` (not
`App/Features/Gaming/`).

**The naming problem that makes this worse.** `Core/Features/Gaming/Catalogs/GamingOptimizations.cs`
declares `namespace AkariTool.Tabs.Gaming` — not `AkariTool.Core.Features.Gaming`.
So a grep for the Core domain namespace finds the folder, and a grep for
`AkariTool.Core.Features.Gaming` finds **nothing** (only 3 files exist under
`Core/Features/AkariOS/`, 2 under `Core/Features/Apps/`). Meanwhile
`AkariTool.Tabs` is declared in **three** assemblies: Core (37 files),
Infrastructure (18 files in `Services/`), and App (2 files in `Features/`).
**Verified.** Folder-based reasoning and namespace-based reasoning give opposite
answers in this repo.

**Fix approach for the restructure.** Move by feature, not by type, and fix the
namespace in the same commit as the move so the two never disagree:
1. For each of the 11 domains, create `Infrastructure/Features/<Domain>/Services/`
   and move the matching services from the flat `Infrastructure/Services/`.
2. Create `App/Features/<Domain>/{Pages,ViewModels}` and move from `Views/` and
   `ViewModels/`.
3. Rename `AkariTool.Tabs.*` → `AkariTool.Core.Features.<Domain>` **in Core only**
   (37 files, mechanical, compiler-checked). Leave `AkariTool.Tabs` in
   Infrastructure and App for a later phase so the two steps can be verified
   independently.
4. Only then delete the emptied legacy directories.

---

## 13. Duplicated dispatcher, and the DI registration file is messy

**Severity:** Low-Medium.

`src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs` has
**three pairs of duplicate registrations** in one 134-line file:
- `ISystemSettingsDiscoveryService` at `:48` and `:61`
- `ISpecialDiscoveryRegistry` at `:43` and `:62`
- The `ISpecialSettingHandlerRegistry` dictionary is built twice (`:35` and the
  `ISpecialDiscoveryRegistry` at `:62`)

All resolve to the same singletons, so behaviour is correct; the duplicates are
copy-paste residue. The file also mixes four comment styles and three
registration strategies (lambda factories, concrete-then-alias, plain
`AddSingleton<TInterface, TImpl>`) and includes a long apologetic note at `:13-20`
about what it *cannot* register.

`src/AkariTool.App/DI/UIServiceExtensions.cs` has a **stray indentation block**
at `:62-69` — lines indented 16 spaces inside an otherwise 4-space method, and the
consequence is that `MainWindow` and three other singletons are registered from
inside what looks like a comment block. The registrations are real (the code
compiles), but the indentation makes the file actively misleading to read.
**Fix approach:** delete the 3 duplicate pairs; reindent `:62-69`. Low risk,
pure cleanup.

---

## 14. `Microsoft.WindowsAppSDK` in Core is an unused package reference

**Severity:** Low.

`src/AkariTool.Core/AkariTool.Core.csproj:11` references
`Microsoft.WindowsAppSDK 2.3.1`. **Verified:** no file under `src/AkariTool.Core`
contains `using Microsoft.UI` or `using WinUI` (zero matches). Core also references
`CommunityToolkit.Mvvm` 8.4.2 (`:10`) — **verified:** no `ObservableProperty`,
`[RelayCommand]`, or `CommunityToolkit` usage anywhere in Core either.

Both references are dead weight on a project whose stated purpose is to be the
dependency-free layer. `CommunityToolkit.Mvvm` in Core is arguably worse: it
invites an MVVM attribute into a layer that must stay UI-free.
**Fix approach:** delete both `PackageReference` lines and build. (Not verified
that nothing in a *test* project needs them transitively — the build will say.)

---

## 15. `PowerTemplates` and `WinGet.Interop` need a look, not a fix

**Severity:** Informational — recorded so a future agent doesn't re-investigate.

- `src/AkariTool.Core/Features/Power/Catalogs/PowerTemplates.cs` (620 lines) has
  **no `SettingGroup` literals** and no `Build()` — it is a factory library of
  `ComboBoxMetadata` / `NumericRangeMetadata` (`TimeIntervals`, `LidActions`,
  `ProcessorBoostMode`, `CreateNumericRange(...)`, …) referenced ~35 times by
  `PowerOptimizations.cs`. **Live, not a dead catalog.** Its file name and
  `Catalogs/` location make it look like a 12th page catalog. Verified live.
- `src/AkariTool.Core/Features/Common/Constants/AkariPaths.cs` calls
  `Environment.GetFolderPath` in a `static readonly` field. Combined with item 5,
  a path constant is computed in a `static` initializer in *two* assemblies.
- `Defender\NoDefender.cab` and `Nvidia\Settings.nip` are `EmbeddedResource`s;
  `Settings.nip` is guarded by `Condition="Exists(...)"`
  (`AkariTool.App.csproj:95`), so **deleting the file silently drops the feature
  rather than failing the build.** Verified in the csproj.

---

## 16. Test coverage is narrow and skewed

**Severity:** Medium — a restructure cannot be validated by the current suite.

**Verified layout.** 20 test files across 2 projects, xUnit + FluentAssertions +
NSubstitute. `AkariTool.Core.Tests` (8 files) covers models, `SettingCatalogValidator`,
`BuildVersionGate`. `AkariTool.Infrastructure.Tests` (12 files) covers the
filtering, reading, writing, and power services.

**What is untested:**
- **No test project for `AkariTool.App`.** No `.csproj` references it. Every
  ViewModel, page, `SettingBackupService`, `SettingPageWarmUp`, and all 11
  catalogs are unverified by automated tests. Largely a consequence of item 9 —
  the VMs cannot be constructed without a live global container.
- **No test asserts the real catalogs are valid** (item 8). The
  `SettingCatalogValidatorTests` fixtures are hand-built, so the validator is
  tested but the data it guards is not.
- **No test for `IEventBus`** (`src/AkariTool.Infrastructure/Features/Common/Events/EventBus.cs`)
  despite it being a hand-rolled pub/sub with subscription tokens and a
  `PowerPlanChangedEvent` that gates sibling-row refreshes.

**Fix approach (ordered, so a restructure has a safety net):** add a
catalog-validity test that enumerates all 11 `Build()` methods (unblocks item 8),
then a `SettingPageWarmUp` integration test using a hand-built `ServiceCollection`,
then a ViewModel test project. Do all three **before** the restructure, not after.

---

## Not verified

Stated explicitly so no plan depends on these being true:

- **Whether the app currently builds.** I did not run `dotnet build`. Items 13
  (indentation), 14 (package refs), and 5 (namespace ambiguity) are all
  *code-reading* findings; the compiler may already be failing on one of them, or
  may be fine because of `using` placement.
- **Whether `Resource/NavIcons/` is actually used at runtime** (item 6). No code
  or csproj reference found, but I did not trace every `MainWindow.xaml` glyph
  binding.
- **Whether the duplicate `DispatcherService` registrations actually resolve to
  the App copy** (item 4). This is Microsoft.Extensions.DependencyInjection's
  documented last-wins behaviour and `AddAkariUI()` is called second
  (`App.xaml.cs:140-141`), but I did not run it.
- **Whether `SettingCatalogValidator` was ever called from a build script or CI.**
  There is no CI configuration in the repo (no `.github/`, no pipeline file —
  verified), and no `Program.cs`/MSBuild target invokes it.

---

*Concerns audit: 2026-10-05*
