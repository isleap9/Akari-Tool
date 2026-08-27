<!-- refreshed: 2026-08-27 -->
# Architecture

**Analysis Date:** 2026-08-27

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────┐
│                       App Layer (WinUI 3 Shell)                      │
│                     `src/AkariTool.App`                              │
├──────────────────────────────────────────────────────────────────────┤
│ MainWindow (XAML) → Frame Navigation → Pages (XAML)                  │
│ ViewModels (MVVM) → SettingPageViewModel hierarchy                   │
│ DI: App.xaml.cs → UIServiceExtensions.cs + InfrastructureExtensions  │
└────────────────────────┬─────────────────────────────────────────────┘
                         │
         ┌───────────────┴────────────────┐
         │                                │
┌────────▼──────────────────────────────────────────────────────────────┐
│          Infrastructure Layer (OS Interactions)                       │
│              `src/AkariTool.Infrastructure`                           │
├────────────────────────────────────────────────────────────────────────┤
│ SettingOperationExecutor → Apply settings (Registry/PS/Tasks/PowerCfg)│
│ SettingStateReader → Read current state from OS                       │
│ Service implementations: Windows Registry, PowerShell, Tasks, PowerCfg│
│ Power, Backup, WinGet, Compatibility Filters                         │
└────────────────────────┬──────────────────────────────────────────────┘
                         │
┌────────────────────────▼──────────────────────────────────────────────┐
│              Core Layer (Pure C# Models & Interfaces)                 │
│                   `src/AkariTool.Core`                                │
├────────────────────────────────────────────────────────────────────────┤
│ SettingDefinition record + supporting models (SettingGroup,           │
│ RegistrySetting, BadgePillState, etc.)                                │
│ Catalogs: GamingOptimizations, PrivacyOptimizations, etc.             │
│ Enums: InputType, DetectionType, SettingBadgeKind, RunContext        │
│ Interfaces: ISettingOperationExecutor, ISettingStateReader            │
│ Zero OS dependencies — compiler enforced                              │
└────────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| **App Entry Point** | WinUI 3 application class; DI container setup; lifecycle management | `src/AkariTool.App/App.xaml.cs` |
| **MainWindow** | Shell UI container; NavigationView rail; Frame-based page navigation | `src/AkariTool.App/MainWindow.xaml(.cs)` |
| **SettingPageViewModel** | Base VM for all tweak pages; builds sections from SettingDefinition catalogs; manages state refresh and badge counts | `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` |
| **SettingItemViewModel** | Per-row VM; handles toggle/dropdown state, badge computation, apply/restore commands | `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` |
| **SettingSectionViewModel** | Titled section containing rows; search filtering | `src/AkariTool.App/ViewModels/Tweaks/SettingSectionViewModel.cs` |
| **Concrete VMs** | GamingViewModel, SoundViewModel, PrivacyViewModel, etc. — each implements BuildSettingGroups() | `src/AkariTool.App/ViewModels/Gaming(Sound/Notifications/etc)/` |
| **Catalogs** | Declarative SettingDefinition + SettingGroup definitions per feature | `src/AkariTool.Core/Features/*/Catalogs/*.cs` |
| **SettingOperationExecutor** | Applies operations to OS: registry writes, PowerShell scripts, tasks, power settings | `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs` |
| **SettingStateReader** | Reads current OS state for badge computation and validation | `src/AkariTool.Infrastructure/Features/Common/Services/SettingStateReader.cs` |
| **Service Implementations** | WindowsRegistryService, PowerShellRunner, ProcessExecutor, PowerCfgApplier, etc. | `src/AkariTool.Infrastructure/Features/Common/Services/` |
| **Compatibility Filters** | WindowsCompatibilityFilter, HardwareCompatibilityFilter — gates rows at Build time | `src/AkariTool.Infrastructure/Features/` |
| **SettingBackupService** | Exports/imports settings; drives global search | `src/AkariTool.App/Services/SettingBackupService.cs` |

## Pattern Overview

**Overall:** Three-layer MVVM with declarative catalog-driven configuration and a full-stack write path (UI → ViewModel → Executor → OS Services)

**Key Characteristics:**
- **Declarative-over-imperative**: Settings defined as SettingDefinition records in catalogs, not hardcoded ViewModel logic
- **Single-page singleton VMs**: All SettingPageViewModel pages are DI singletons; rows register themselves on construction
- **Compatibility-gated catalogs**: Windows version + hardware filters applied at Build() time; incompatible rows removed
- **Batch discovery**: SystemSettingsDiscoveryService reads all rows in one pass; SettingStateReader interprets results
- **Layered dependency injection**: Core interfaces → Infrastructure implementations → App UI registration

## Layers

**Core (`src/AkariTool.Core/`)**
- Purpose: Immutable catalog definitions + model contracts; zero OS dependencies
- Location: `src/AkariTool.Core/`
- Contains: SettingDefinition records, SettingGroup, catalogs (GamingOptimizations, etc.), enums, interfaces
- Depends on: Nothing (pure C#, no OS calls)
- Used by: Infrastructure (implements interfaces), App (reads catalogs)

**Infrastructure (`src/AkariTool.Infrastructure/`)**
- Purpose: OS-touching implementations; reads system state and applies changes
- Location: `src/AkariTool.Infrastructure/`
- Contains: SettingOperationExecutor, SettingStateReader, WindowsRegistryService, PowerShellRunner, all OS service wrappers, compatibility filters, WinGet COM stack
- Depends on: Core (reads SettingDefinition, implements ISettingOperationExecutor)
- Used by: App (injected into ViewModels)

**App (`src/AkariTool.App/`)**
- Purpose: WinUI 3 shell, user interface, and presentation logic
- Location: `src/AkariTool.App/`
- Contains: MainWindow, Pages (XAML), ViewModels (SettingPageViewModel + concrete impls), DI registration, TweakDialogs
- Depends on: Core (reads models + interfaces), Infrastructure (uses ISettingOperationExecutor, ISettingStateReader)
- Used by: Entry point; hosts the running application

## Data Flow

### Primary Setting Apply Path

1. **Catalog Definition** — `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs` defines a SettingDefinition record with registry/task/script operations
2. **ViewModel Build** — `GamingViewModel.BuildSettingGroups()` → calls `GamingOptimizations.Build()` → returns `IReadOnlyList<SettingGroup>`
3. **Compatibility Gate** — `SettingPageViewModel.Build()` applies Windows/hardware filters; empty groups drop
4. **Row Creation** — `CreateItem(SettingDefinition)` materializes a `SettingItemViewModel` per setting
5. **Badge Computation** — `SettingStateReader.ReadSelectionIndex()` / `GetCurrentValue()` → reads OS state → computes badge (Recommended/Default/Custom/Preference)
6. **UI Binding** — XAML templates bind to row VM properties (Name, Description, Badge, CurrentValue)
7. **User Interaction** — User toggles/selects → `SettingItemViewModel.ApplyRecommendedCommand.Execute()` or direct apply
8. **Executor** — `SettingOperationExecutor.ApplySettingOperationsAsync()` dispatches to OS service (registry, PowerShell, tasks, etc.)
9. **OS Update** — Registry key written, task enabled, PowerShell script runs, etc.
10. **Refresh** — `SettingItemViewModel.RefreshFromSystem()` re-reads OS state → updates badge → propagates `AppliedStateChanged` event
11. **Badge Update** — `SettingPageViewModel.RefreshQuickActionCounts()` re-tally pending counts → nav badge updates

### State Management

- **Per-row state**: Held in `SettingItemViewModel.Badges`, `Definition`, `CurrentValue`, `SelectedIndex`
- **Page-level aggregates**: `SettingPageViewModel.RecommendedPendingCount`, `DefaultPendingCount`
- **Global search**: `SettingBackupService` maintains a snapshot of all rows across all VMs
- **Cross-row effects**: `ISettingDependencyResolver` handles cascading applies (auto-enable child rows, restore presets)
- **Event subscription**: Rows notify their page via `AppliedStateChanged`, `PowerPlanChanged`, `AdvancedUnlockPersisted`

## Key Abstractions

**SettingDefinition Record**
- Purpose: Immutable specification of a single OS setting (toggle, dropdown, numeric range, or action)
- Examples: `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs`
- Pattern: Record-based with init-only properties; supports registry, scheduled task, PowerShell, power config, BCD, dependency metadata
- Contains: Id, Name, Description, InputType, RecommendedValue, DefaultValue, RegistrySettings[], PowerShellScripts[], etc.

**SettingGroup Record**
- Purpose: Titled container grouping related SettingDefinitions (e.g., "Game Mode", "Processor", "Graphics")
- Pattern: Part of catalog return types (e.g., `GamingOptimizations.Build()` returns `List<SettingGroup>`)
- Depends on: SettingDefinition collection

**ISettingOperationExecutor**
- Purpose: Apply a SettingDefinition's operations to the OS
- Implementation: `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs`
- Pattern: Single async method `ApplySettingOperationsAsync(SettingDefinition, enable, value)` → delegates to registry/PS/tasks/power based on setting type

**ISettingStateReader**
- Purpose: Read current OS state for a SettingDefinition; compute badge
- Implementation: `src/AkariTool.Infrastructure/Features/Common/Services/SettingStateReader.cs`
- Pattern: `ReadSelectionIndex(SettingDefinition)` → registry/task/power query → matches against declared value mappings → returns index or DefaultValue

**Catalog Methods**
- Purpose: Static methods returning `IReadOnlyList<SettingGroup>` per feature
- Examples: `GamingOptimizations.Build()`, `PrivacyOptimizations.Build()`, `PowerOptimizations.Build()`
- Pattern: Declarative SettingDefinition records grouped by UI section; pure construction (no side effects, zero I/O)

## Entry Points

**App.OnLaunched**
- Location: `src/AkariTool.App/App.xaml.cs` (line 42–93)
- Triggers: Windows application startup
- Responsibilities:
  - Store DispatcherQueue globally
  - Register unhandled exception logger
  - Resolve MainWindow from DI
  - Check for `--competitive` command-line argument (Competitive mode recovery)
  - Activate MainWindow (or keep hidden for competitive sessions)
  - Launch background StartupOrchestrator task (warm-up all SettingPageVMs, drift check)

**MainWindow (Shell)**
- Location: `src/AkariTool.App/MainWindow.xaml(.cs)`
- Triggers: After App.OnLaunched → MainWindow.Activate()
- Responsibilities:
  - Present Mica-backdrop WinUI 3 shell
  - NavigationView rail (left sidebar)
  - Frame for page content
  - Log console (docked)
  - Status bar (theme toggle, build stamp)
  - Subscribe to drift check callback (title-bar banner flip)

**Page Navigation**
- Location: `MainWindow.xaml.cs` PageMap + `INavigationService`
- Triggers: NavigationView rail selection + global search + home cards
- Destinations:
  - HomePage (landing)
  - Optimize hub + detail pages (Gaming, Sound, Notifications, Privacy, Update, Power, Taskbar, Explorer, Appearance, StartMenu, Desktop)
  - Software hub + detail pages
  - Advanced tools hub + detail pages
  - Settings

**SettingPageWarmUp**
- Location: `src/AkariTool.App/Services/SettingPageWarmUp.cs`
- Triggers: Startup orchestrator after compatibility filtering
- Responsibilities:
  - Enumerate all registered SettingPageViewModel instances via DI
  - Call `Build()` on each one on a single background thread
  - Ensures SettingBackupService sees all rows even if user never navigates to a tab

## Architectural Constraints

- **Threading:** Single-threaded event loop (WinUI UI thread) with background thread for warm-up and drift scanning. `DispatcherQueue.TryEnqueue()` marshals back to UI.
- **Global state:** All SettingPageViewModel pages are DI singletons; once built, they persist for the application lifetime. `TweakRegistry` holds a live catalog of all declarative rows (legacy compatibility, not actively used for new code).
- **Circular imports:** Core has zero dependencies; Infrastructure references Core; App references both. No cycles.
- **DI Singleton Lifetime:** SettingPageViewModel and all concrete VMs (GamingViewModel, SoundViewModel, etc.) are singletons to preserve row registration and state across navigations.
- **Compatibility gating:** Applied synchronously at Build() time on the warm-up thread; filtering is idempotent and deterministic per Windows build + hardware.
- **Registry ACL:** `OpenSubKey(writable: true)` raises `SecurityException` (not `UnauthorizedAccessException`) for ACL-locked service keys; handled transparently by WindowsRegistryService.
- **Power scheme drift:** Detected and surfaced via persist indicator but **NEVER** reactivated from a read path; only on next write does active plan update.
- **Startup timing:** Warm-up runs after MainWindow is visible (Activate already called), drift check runs after warm-up so DriftScanner can resolve every baseline entry against TweakRegistry.

## Anti-Patterns

### Transient SettingPageViewModel Registration

**What happens:** A SettingPageViewModel is registered as `AddTransient` instead of `AddSingleton`
**Why it's wrong:** Every page navigation would call `Build()` again, re-registering all rows with TweakRegistry, inflating tweak counts, duplicating Backup entries, and breaking the contiguous `ClaimRange` index ranges
**Do this instead:** All SettingPageViewModel subclasses MUST be `AddSingleton` in `src/AkariTool.App/DI/UIServiceExtensions.cs` (already implemented correctly, lines 80–130)

### Calling Build() on the UI Thread

**What happens:** `SettingPageViewModel.Build()` is invoked from UI event handlers or synchronous property getters
**Why it's wrong:** Warm-up compatibility filters use `.GetAwaiter().GetResult()` which blocks; UI thread blocking causes janky interactions and can deadlock if filters await async work
**Do this instead:** Call `Build()` only from background threads or lazy-init seams; the warm-up orchestrator handles this. If a page needs lazy Build (never happens in current codebase), guard with a background Task and marshal UI updates back via DispatcherQueue

### Reading State in Write Paths

**What happens:** After applying a setting, the code calls a read operation again from the same execution path (e.g., reactivating power scheme after writing it)
**Why it's wrong:** Drift detection and user undo/revert depend on observing ACTUAL system state, not echo-backed cached reads
**Do this instead:** Write operations are fire-and-forget; state refresh is triggered by the next UI update or explicit `RefreshFromSystem()` call from user or dependency resolver

### Defender Toggle Refactoring

**What happens:** Code attempts to move `DefenderService` or `DefenderToggleViewModel` to Infrastructure or rewrite its implementation
**Why it's wrong:** `DefenderService` uses `GetExecutingAssembly()` to load embedded CAB + PowerShell files; moving to Infrastructure would make the assembly lookup fail at runtime
**Do this instead:** DefenderService MUST stay in App (`src/AkariTool.App/Services/DefenderService.cs`); its bespoke row is injected into Gaming's Security section via `AddDefenderRow()`; never modify without explicit sign-off

## Error Handling

**Strategy:** Layered error capture with silent fallback and detailed logging

**Patterns:**
- **Registry failures:** `WindowsRegistryService.ApplySetting()` returns bool (false = failed); caller decides to report or suppress
- **PowerShell timeouts:** `PowerShellRunner` catches all exceptions, logs, returns OperationResult.Failed()
- **Task enable/disable:** `ScheduledTaskService` handles missing tasks gracefully (service doesn't exist = no-op)
- **Process launch:** `ProcessExecutor` awaits process exit; caller checks exit code
- **Unhandled exceptions:** Caught by `App.OnLaunched` and routed to `ILogService.Error()` → file log (survives crashes)
- **Dialog confirmations:** `TweakDialogs` serializes ContentDialogs (no concurrent dialogs); user can cancel bulk applies mid-loop

## Cross-Cutting Concerns

**Logging:** 
- `ILogService` (WinUI.Framework) → routed to `AkariUiLogService` → fed to ToolService.LineLogged event → caught by MainWindow log console
- `ToolService.Log(line)` is the singleton app-wide logger
- All Infrastructure and SettingOperationExecutor operations log via `IAkariLogService`

**Validation:**
- `SettingCatalogValidator` (Core tests) ensures catalog correctness at compile-time (test-driven validation)
- Runtime row validation happens in `SettingStateReader` (unmatched value defaults to DefaultValue)
- `SettingDependencyResolver` validates dependency chains and applies cascades

**Authentication:**
- Not applicable — app runs with current user's privileges; elevation happens via `IProcessRestartManager` for elevation-required operations
- Defender toggle uses embedded DISM + reboot handoff for kernel-mode service removal

**Dependency Resolution:**
- `ISettingDependencyResolver` resolves dependencies between rows within the same catalog + cross-catalog (e.g., Power depends on Privacy's privacy-lock-screen)
- Rows get `SetDependencyContext(allSettings)` after all sections are built; resolver applies child settings after parent

---

*Architecture analysis: 2026-08-27*
