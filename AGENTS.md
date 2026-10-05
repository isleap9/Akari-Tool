<!-- GSD:project-start source:PROJECT.md -->

## Project

**Akari Tool**

Akari Tool is a Windows 11 optimization utility built on WinUI 3 + MVVM. Its tuning catalog is
already at content parity with the Winhance reference implementation — ~430 settings across
Optimize (301), Customize (121), and SoftwareApps (254+ items), with the 16 external-app
categories matching Winhance's counts exactly. What is *not* at parity is the structure: Akari
started a vertical-slice migration and never finished it, so it has 11 domains in Core but only 3
in Infrastructure and 3 in App, plus legacy top-level `Views/`, `ViewModels/`, `Services/`,
`Defender/`, `Nvidia/`, and `Scripts/` directories. This project finishes that migration onto
Winhance's clean per-domain slices and then builds the machinery that is genuinely missing.

**Core Value:** Every new capability must be addable as one self-contained vertical slice — a Core contract, an
Infrastructure service, and a UI page — without touching code outside its own feature folder. If a
change requires edits across layer boundaries, the architecture has failed, and that is a defect to
fix rather than a cost to accept.

### Constraints

- **Licensing — clean-room design parity (hard constraint).** Winhance is PolyForm Shield 1.0.0,
  which carries a **noncompete** clause, a **required notice**
  (`Required Notice: Copyright (c) 2025 Marco du Plessis (https://github.com/Jeyloh/Winhance)`), and
  patent-defense and cure provisions. Akari is a Windows optimization utility in the same category
  and already contains ~1:1 copies of Winhance's model layer. Parity therefore means matching
  Winhance's **architecture, conventions, feature set, and UX patterns while writing Akari's own
  implementation**. **No new file may be copied from Winhance.** Already-adopted 1:1 model types
  stay as they are and are out of scope for this project.
- **Licensing — outstanding compliance debt (not this project's scope, but must not be made
  worse).** Akari's README shows an MIT badge while no `LICENSE` file exists in the repository, and
  the PolyForm required notice is not shipped. Both predate this project. Getting a real legal
  opinion before the next release is recommended and is tracked outside this roadmap.
- **Tech stack**: WinUI 3 (WindowsAppSDK 2.3.1), .NET 10 (SDK 10.0.401 / global.json pin
  10.0.102), CommunityToolkit.Mvvm 8.4.2, C# 12. No new frameworks.
- **Build**: VS MSBuild only (`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`).
  `dotnet build` fails on WinUI 3 PRI/resource targets.
- **Unverified compatibility (gates the UX-parity work)**: Akari is on WindowsAppSDK 2.3.1 while
  Winhance is on 1.8.x, and Akari has never used `CommunityToolkit.WinUI`. Whether Winhance's
  `SettingsCard`, `DataGrid`, and `WrapPanel` resolve under 2.3.1 needs a spike before the
  UI-parity phases are planned.
- **Elevation**: the app is always-elevated by manifest. Operations needing SYSTEM or
  TrustedInstaller context use the existing impersonation path, not raw process launches.
- **Safety**: the pre-existing Core Value holds — every optimization must be safe, reversible, and
  legible to the user before and after. The restructure must not regress this.
- **Scope**: every Winhance capability is in scope. There is no anti-parity list; edge cases are
  decided at phase-planning time.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

## Languages

- C# 12 (latest) - All production code, split across Core (models/interfaces), Infrastructure (OS-touching services), and App (WinUI 3 UI layer)
- PowerShell 5.1 - Embedded scripts for system tweaks, feature toggles, and uninstall operations (executed via `PowerShellRunner` in `AkariTool.Infrastructure`)
- Batch (.bat) - Network configuration scripts in `src/AkariTool.App/Scripts/Network/`
- XML - XAML UI definitions, WinUI 3 control templates, and autounattend.xml generation

## Runtime

- .NET 10.0 (LTS)
- Target: Windows 10 (OS Version 10.0.26100.0)
- Minimum: Windows 10 (10.0.17763.0 for main App; WinGet.Interop requires 10.0.22621.0)
- Platform: x64 only (`win-x64` RuntimeIdentifier)
- NuGet
- Lockfile: `obj/project.assets.json` (MSBuild-generated, not checked in)

## Frameworks

- Windows App SDK 2.3.1 - WinUI 3 desktop application framework; self-contained deployment (runtime ships with executable)
- WinUI.Framework (local vendored) - Custom MVVM framework (`vendor/WinUI.Framework/`), providing `IThemeService`, `ISettingsService`, `IDispatcherService`, `ITaskProgressService`, base VM/View patterns
- CommunityToolkit.Mvvm 8.4.2 - MVVM source generators (`RelayCommand`, `ObservableProperty`), used across App and Core ViewModels
- Microsoft.Extensions.DependencyInjection 10.0.10 - Service registration and resolution in `App.xaml.cs` and extension methods (`InfrastructureServiceExtensions.cs`, `UIServiceExtensions.cs`)
- xunit 2.9.3 - Test runner and assertions
- FluentAssertions 8.7.1 - Fluent assertion syntax
- NSubstitute 5.3.0 - Mock/substitution framework for service isolation
- Microsoft.NET.Test.Sdk 17.14.1 - Test discovery and execution
- MSBuild (Visual Studio 2022 Community via hardcoded path `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`)
- CsWinRT 2.0.4 - C# projection generator for WinRT APIs (WinGet.Interop COM projection)
- CsWin32 0.3.49-beta - P/Invoke stub generator for Win32 APIs

## Key Dependencies

- Microsoft.WindowsAppSDK 2.3.1 - All WinUI 3 controls, Window management, dispatcher, theming; enables self-contained deployment
- CommunityToolkit.Mvvm 8.4.2 - Eliminates boilerplate in property/command definitions across entire MVVM codebase
- Microsoft.Extensions.DependencyInjection 10.0.10 - Service resolution at startup and runtime (SettingOperationExecutor, SettingStateReader, power/update services)
- System.Management 10.0.0 - WMI queries for hardware detection (RAM, disk, GPU, battery status, account info)
- System.ServiceProcess.ServiceController 10.0.0 - Windows service enumeration and control (for service presets, Competitive Mode, system resource tuning)
- Microsoft.WindowsPackageManager.ComInterop 1.9.25180 - COM interop for Windows Package Manager (WinGet) detection and installation
- Material.Icons.WinUI3 3.0.2 - Material Design icon set, resolved by `IconConverter` from row definitions
- FluentIcons.WinUI 2.1.326 - Fluent Design icon set (same versions as Winhance upstream for parity)
- Microsoft.Windows.CsWinRT 2.0.4 - XAML compilation targets, resource PRI generation for self-contained deployment

## Configuration

- No `.env` files or secrets management — all configuration is in-code (CLAUDE.md defines registry root `HKLM\SOFTWARE\AkariTool`, theme/settings persist via Windows Registry and `%LOCALAPPDATA%`)
- Required elevation: App manifest declares `requireAdministrator` (Windows 10+ built-in UAC prompt)
- Storage root: `%ProgramData%\AkariTool\` (Scripts, Logs, IconCache directories created on first use)
- `AkariTool.sln` - Solution file with 7 projects (3 main, 2 test, 2 vendor)
- `src/AkariTool.App/AkariTool.App.csproj` - WinUI 3 executable; embeds PowerShell scripts, DefenderService cab/ps1, NVIDIA NIP profiles
- `src/AkariTool.Core/AkariTool.Core.csproj` - Pure C# models (zero OS dependencies, zero UI dependencies)
- `src/AkariTool.Infrastructure/AkariTool.Infrastructure.csproj` - OS-touching services (registry, WMI, PowerShell, tasks, elevation)
- `tests/AkariTool.Core.Tests/AkariTool.Core.Tests.csproj` - 53 passing tests
- `tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` - 136 passing + 1 skipped tests
- `vendor/WinUI.Framework/WinUI.Framework.csproj` - Local framework (ProjectReference, not NuGet)
- `vendor/WinGet.Interop/WinGet.Interop.csproj` - WinGet COM projection (ProjectReference)
- LangVersion: `latest` (C# 12.0)
- Nullable: `enable` (strict null-reference safety)
- ImplicitUsings: `enable` (top-level `using` directives auto-included)
- RuntimeIdentifier: `win-x64` (x64-only builds)
- TargetFramework: `net10.0-windows10.0.26100.0` (tied to Windows SDK version 26100)

## Platform Requirements

- Windows 10 or later (10.0.17763.0+ for app, 10.0.22621.0+ for WinGet features)
- Visual Studio 2022 Community or later (for MSBuild, XAML designer, WinUI workload)
- .NET 10 SDK (installed automatically with VS workload)
- Administrator privileges (to run tests and debug app)
- Windows 10 (1909 or later minimum, though optimized for Windows 11)
- No additional runtime installation required (Windows App SDK runtime bundled in executable)
- Administrator privileges required at launch (UAC prompt on startup)
- At least 100 MB free disk space (for extracted runtime and cache)
- PowerShell 5.1 (built-in since Windows 10; PowerShell 7+ not required)
- WinGet (Windows Package Manager, bundled in Windows 11+; backported in GitHub repo for Windows 10)
- Steam (for shader cache cleaning, auto-detected at runtime)

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

## Naming Patterns

- One public type per file; **filename == type name**. No exceptions found.
- Partial classes are split by concern with a `.` separator, and the concern is a
- UI pairing: `<Name>Page.xaml` + `<Name>Page.xaml.cs`. For XAML pages with multiple
- Service/behaviour suffixes: `Service`, `Wrapper`, `Registry`, `Manager`,
- Catalog naming is `*Optimizations` for declarative tuning data
- Tests: `<TypeUnderTest>Tests.cs`, one class per file, xUnit + FluentAssertions.
- `PascalCase`, verbs first: `Build()`, `Apply()`, `Read()`, `Filter()`, `Get()`.
- Async methods carry the `Async` suffix consistently —
- Predicate functions read as assertions: `IsNew`, `HasRecommendedQuickSet`,
- **Never** name a boolean parameter `b` except in a tiny local scope. In catalogs,
- `camelCase` for locals and parameters; `_camelCase` for private fields.
- Readonly dependencies are `private readonly ISettingStateReader _stateReader;`.
- **A leading underscore means private — but the codebase is inconsistent about
- Boolean locals are prefixed `is`/`has`/`should` where it aids reading
- `CTS` is always `cts`; `CancellationTokenSource` fields are `private CancellationTokenSource? _cts;`.
- Interfaces: `I` + PascalCase noun (`ISettingStateReader`, `IPowerCfgApplier`,
- The one `I`-less service is deliberate and legacy: `ToolService` (see
- Models are `sealed record` with `init` accessors — this is the dominant idiom
- `required` is used for genuinely mandatory members
- `BaseDefinition` is the abstract base for anything renderable as a setting row.
- ViewModels are `sealed partial class X : ViewModelBase` (framework base) or
- Primary-constructor classes appear in the newest Infrastructure code:

## Code Style

- No formatter config. Observed: 4-space indent, Allman braces, `(a, b)` tuple
- Region-free. Large classes are delimited by `// ── Section name ─────` comment
- `var` is used for locals whose type is obvious from the right-hand side
- Early-return guard clauses are preferred over nesting.
- None configured. `Nullable` is `enable` in all four project files, and
- Nullable annotations are used honestly on optional dependencies
- `catch { }` with no body is **not** used; every swallow has an inline reason

## Import Organization

- Infrastructure utilities reached from App ViewModels:
- Services reached through the service locator inside a method body:

| Assembly | Namespace | Files | Folder |
|----------|-----------|-------|--------|
| Core | `AkariTool.Core.Features.*` | ~110 | `Features/` ✅ matches |
| Core | `AkariTool.Tabs` / `.Customize` / `.Gaming` / `.Notifications` / `.Power` / `.Privacy` / `.Sound` / `.Update` | 37 | `Features/*/Catalogs/` ❌ |
| Core | `AkariTool.Tabs` | 1 | `Features/Common/Constants/AkariPaths.cs` ❌ |
| Infrastructure | `AkariTool.Infrastructure.Features.*` | 52 | `Features/` ✅ |
| Infrastructure | `AkariTool.Tabs` | 18 | `Services/` ❌ |
| Infrastructure | `AkariTool.Services` | 19 | `Services/` ❌ |
| App | `AkariTool.Views` / `.ViewModels` / `.Services` | 69 | matches ✅ |
| App | `AkariTool.Tabs` | 2 | `Features/Software/`, `Features/Shared/` ❌ |
| App | `AkariTool.Features.Common.*` | 5 | `Features/Common/` ✅ |

## File Organization

- One concern per file, except data-heavy catalogs which are intentionally one
- A feature folder groups by *role* (`Models/`, `Interfaces/`, `Services/`),
- Large legacy classes are split into `partial` files rather than extracted into
- New Infrastructure code goes under `Features/<Domain>/Services/`; the flat

## How to Add a New Setting

## How to Add a New Page

## ViewModel Patterns

- `NavTag` must exactly match the `PageMap` key in `MainWindow.xaml.cs` and the
- `Title` / `Subtitle` are set in the ctor, not as `[ObservableProperty]`.
- `newBadgeService: null` is passed explicitly at every call site even though the
- **Power is the only page that overrides anything structural**: it overrides
- `GamingViewModel` is the only page that injects a bespoke row

## Async Patterns

- **`ConfigureAwait(false)` is the default in Infrastructure** (192 occurrences in
- **`GetAwaiter().GetResult()` is confined to the synchronous warm-up path**, and
- **Cancellation** flows as an explicit `CancellationToken` parameter, never
- **Fire-and-forget is expressed as `_ = ...`** to satisfy CS4014
- **Startup phases are sequential, never parallel.** The comment at
- **Bulk applies suppress side effects then flush once**:
- **Bulk applies are wrapped in one change-history batch:**

## Logging

- **One chain, three hops.** Infrastructure and App code log through
- **The dominant call is `logService.Log(LogLevel.Info, $"[Component] message")`.**
- **`ToolService.Current?.Log(...)`** is the legacy static escape hatch, 35
- **Views also log** through `ToolService` — e.g.
- **Never log a secret.** No such pattern was found, and none should be added —

## Error Handling

- **Prefer a result record over an exception** for anything the user can trigger
- **Catch-all is the norm** (229 `catch (Exception …)` sites) and is used
- **A swallowed exception must carry an inline reason.** 35 sites use
- **Never swallow silently in a loop.** Per-item failures are counted or
- **Unreadable ≠ absent** is an explicit rule in this codebase; see
- Log the exception (`log.Error(msg, ex)`) whenever a user-visible action fails.

## Comments and Docs

- **XML doc comments on public Core contracts and every non-obvious service.** Most
- **Long architectural comments are the house style.** The dominant pattern is a
- **Cross-references use `<see cref="…"/>`** extensively, including to types in
- **Winhance parity is annotated inline.** The codebase was ported from a
- **⚠ and 🔍 markers** are used to flag sharp edges to future readers:
- **Provenance comments on moved code.** Ported files state their origin:
- Section banners use `// ── Name ─────` (box-drawing U+2500), not `#region`.
- **No `TODO` / `FIXME` / `HACK` markers anywhere in `src/`** — verified zero

## Module Design

- **One public type per file; no barrel/`_` index files.** Namespaces are
- **Registries are explicit dictionaries, not reflection.** See
- **Feature ids are `const string` in a single file**
- **Utility classes are `internal static`** when Infrastructure-local
- **Interfaces are declared in Core, implemented in Infrastructure, except where a

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

## System Overview

```text

```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Shell / router | Rail tags → `Page` types, hub drill-down, nav badges, log dock, theme | `src/AkariTool.App/MainWindow.xaml.cs` |
| App bootstrap | DI container build, `ServiceLocator.Initialize`, startup orchestration, `--competitive` argv | `src/AkariTool.App/App.xaml.cs` |
| UI DI | ViewModel + App-side service registrations, `SettingPageViewModel` marker enumeration | `src/AkariTool.App/DI/UIServiceExtensions.cs` |
| Infra DI | All `Infrastructure/Features/**` service registrations | `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs` |
| Declarative setting model | `SettingDefinition` / `SettingGroup` / `BaseDefinition` — the tuning data model | `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs` |
| Catalog validation | Cross-group id uniqueness, registry-path shape, ComboBox mapping integrity | `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs` |
| Tuning catalogs | 7 declarative `*Optimizations` catalogs + 2 helper catalogs + 24 Software catalogs | `src/AkariTool.Core/Features/<Domain>/Catalogs/` |
| Page ViewModel base | Section build, compatibility gates, quick actions, restore point, search | `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` |
| Row ViewModel | Per-setting read/apply/badges/technical details/power-plan specialisation | `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` |
| Apply engine | Registry write, PowerCfg, scheduled task, PowerShell, restart coalescing, change history | `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs` |
| Read engines | `SettingStateReader` (per-row) and `SystemSettingsDiscoveryService` (batched) | `.../Features/Common/Services/SettingStateReader.cs`, `.../SystemSettingsDiscoveryService.cs` |
| Backup / profile | Export, preview, import, global search over all `SettingPageViewModel`s | `src/AkariTool.App/Services/SettingBackupService.cs` |
| Legacy static OS services | 40+ `public static` classes (elevation, Defender, WIM, presets, GPU, BCD) | `src/AkariTool.Infrastructure/Services/` |

## Pattern Overview

- **Declarative tuning pipeline (Track A).** A setting is a `SettingDefinition`
- **Bespoke legacy path.** AkariOS, Software, Advanced Tools, Backup, Verify, Home,
- **Service locator as a second DI channel.** DI is a real container
- **Interface wrapping over statics.** `Infrastructure/Services/*Wrapper.cs` are

## Layers

### AkariTool.Core — "pure models, interfaces, compiler-enforced"

- Purpose: models, enums, interfaces, event contracts, catalog data, P/Invoke
- Location: `src/AkariTool.Core/`
- Contains: `Features/Common/{Models,Interfaces,Enums,Constants,Events,Helpers,Native,Services,Validation}`,
- Depends on: nothing but the BCL. **No** `Infrastructure`, **no** `App`.
- Used by: `AkariTool.Infrastructure` and `AkariTool.App`, plus both test projects.

| Deviation | Evidence |
|-----------|----------|
| P/Invoke in Core | `Features/Common/Native/PowerProf.cs` (23 `DllImport`s to `PowrProf.dll`/`Kernel32.dll`), `Features/Common/Native/SrClientApi.cs` (`SrClient.dll`) |
| `Microsoft.Win32` in Core models | `RegistryValueKind` in `Models/RegistrySetting.cs`, `Models/SettingDefinition.cs`, `Models/SettingDefinitionToggleState.cs` and all 11 tuning catalogs |
| File I/O in Core | `Competitive/CompetitiveSession.cs` reads/writes `%AppData%` JSON via `File.*` / `Directory.CreateDirectory` |
| Environment paths in Core | `Features/Common/Constants/AkariPaths.cs` calls `Environment.GetFolderPath` in a `static readonly` field |
| Concrete service impl in Core | `Features/Common/Services/GlobalSettingsRegistry.cs` — a real `ConcurrentDictionary` implementation, not a model |
| WindowsAppSDK package ref | `AkariTool.Core.csproj` references `Microsoft.WindowsAppSDK 2.3.1`; **no Core file uses `Microsoft.UI.*`** — verified zero matches. The reference is unnecessary weight. |

### AkariTool.Infrastructure — OS services and wrappers

- Purpose: every `IWindowsRegistryService`, power, filesystem, process, WinGet,
- Location: `src/AkariTool.Infrastructure/`
- Contains: `Features/Common/{Services,Interfaces,Utilities,Events,Models}` (the
- Depends on: `AkariTool.Core`, `vendor/WinGet.Interop`, `System.Management`,
- Used by: `AkariTool.App` only.

### AkariTool.App — WinUI 3 shell

- Purpose: pages (XAML + code-behind), ViewModels, dialogs, backup/profile,
- Location: `src/AkariTool.App/`
- Contains: `MainWindow.xaml(.cs)`, `App.xaml(.cs)`, `Views/`, `ViewModels/`,
- Depends on: `AkariTool.Core`, `AkariTool.Infrastructure`,

## Data Flow

### Primary Request Path — a declarative tuning setting

### Secondary Flow — a bespoke AkariOS action

- ViewModels are `CommunityToolkit.Mvvm` source-generated (`[ObservableProperty]`,
- Page ViewModels are DI **singletons** (see CONCERNS — the lifetime is load-bearing
- OS state is never cached in a store; every read goes to the registry via
- Cross-feature notification is a hand-rolled event bus:
- `DriftBaseline` (`src/AkariTool.Infrastructure/Services/DriftBaseline.cs`) is a

## Key Abstractions

- Purpose: the declarative description of every tuning row and its section.
- Examples: `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs`,
- Pattern: immutable `sealed record` with `init` properties; `SettingGroup` requires
- Purpose: the read/write seam between a row and Windows.
- Examples: `src/AkariTool.Core/Features/Common/Interfaces/ISettingStateReader.cs`,
- Pattern: one interface per direction; `ISettingOperationExecutor` takes 11
- Purpose: escape hatch for settings the generic executor cannot express
- Examples: `src/AkariTool.Core/Features/Common/Interfaces/ISpecialSettingHandler.cs`,
- Pattern: registry keyed by setting `Id`, consulted first in both the apply
- Purpose: make legacy statics injectable.
- Examples: `src/AkariTool.Infrastructure/Services/ToolFetchServiceWrapper.cs`
- Pattern: 4 wrappers exist. The other ~40 static services are *not* wrapped and

## Entry Points

- Location: `src/AkariTool.App/App.xaml.cs`
- Triggers: process launch.
- Responsibilities: `ConfigureServices()` builds the container and calls
- Location: `src/AkariTool.App/MainWindow.xaml.cs`
- Triggers: resolved from DI by `App`.
- Responsibilities: owns `PageMap` (tag → `Page` type), the four detail-tag sets,
- `--competitive <exe>` handled in `App.ParseCompetitiveArgument`
- `build-installer.ps1` passes `/p:AkariPublish=true`.
- `build-deelevated.ps1` passes `/p:DeElevatedTest=true`.
- Both are consumed by conditional `PropertyGroup`s in

## Architectural Constraints

- **Threading:** WinUI 3 single-threaded UI apartment. All page construction is
- **Global state:** module-level mutable statics reachable from any layer —
- **Circular imports:** no circular *project* references (Core ← Infra ← App is
- **Self-contained publishing:** `WindowsAppSDKSelfContained` must never reach the
- **No analyzer gate:** nothing prevents a new Core file from taking a WinUI or

## Anti-Patterns

### Layer discipline leaks: WinUI and registry in the "pure" layers

### `ServiceLocator` used as a second DI channel

### Statics wrapped in interfaces, then injected as if they were real

## Error Handling

- **Operation results over exceptions on the write path.**
- **Try/catch-all at process boundaries.** 229 `catch (Exception ...)` sites in
- **Best-effort with empty catch and a comment.** 35 sites match
- **Unobserved-task pattern.** Several fire-and-forget calls use `_ = ...` to

## Cross-Cutting Concerns

<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-fast` for a trivial task inline, with no subagents and no PLAN.md
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
