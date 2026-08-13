# Akari Tool — Architecture Migration Plan
## WinUI 3 Build (#3) → Clean Architecture + Tests

---

## Reality Check Before Starting

One critical finding from source analysis: **almost every service in Akari Tool is a `public static class`**.

```
ElevationService     static    (15 call sites)
DefenderService      static
UpdateService        static
ShaderCacheService   static
NvidiaProfileService static
CompetitiveService   static
GameDetection        static
SystemInfoService    static
ProcessSuspender     static
ProcessTuning        static
AccountService       static
SteamLibrary         static
ToolFetchService     static
SystemUtilities      static
DefenderPhase2Scheduler static
```

Only `ToolService` is a proper instance class registered with DI.

This is the opposite of Winhance, where everything is behind interfaces.
The plan below accounts for this reality with a pragmatic strategy:
**don't convert everything at once — prioritize what actually enables tests.**

---

## Migration status check (do this first)

Before running any session, verify what's actually done in your working copy.
The PageMap in MainWindow.xaml.cs already shows:
- Verify → VerifyPage ✓ (wired)
- Update → UpdatePage ✓ (wired)  
- Taskbar/Explorer/ContextMenu/Appearance/StartMenu/Desktop ✓ (all wired)

**Run a quick audit prompt before assuming anything is missing.**

---

## Session 0 — Fresh audit (30 min, Claude Code)

**Goal:** Know exactly what's shipped vs. what's pending before any restructuring.

**Prompt to Claude Code:**
```
Read MainWindow.xaml, MainWindow.xaml.cs, and App.xaml.cs. 
List every PageMap entry and every NavigationViewItem Tag.
Cross-reference: are any Tags in the nav that have no PageMap entry?
Are any PageMap entries registered in DI (App.xaml.cs ConfigureServices)?
Report missing wiring only — do not modify any files.
```

**Expected output:** A list like:
- "Tag X has no PageMap entry" → missing wave
- "PageMap has Y but no DI registration" → broken navigation
- "All wired" → migration is complete, proceed to Session 1

**Sign-off:** Confirm audit results match expectations before moving on.

---

## Session 1 — Remaining migration waves (if any)

**Goal:** Complete any waves the audit found missing.

**Scope:** Only the specific tabs the audit flagged as incomplete.
Each missing tab gets its own sub-prompt following the existing pattern:
read source → propose → sign-off → extract.

**Do not start Session 2 until the build compiles clean.**

---

## Session 2 — Solution file + folder restructure (no code changes)

**Goal:** Create the .sln + three empty csproj shells. Zero code moves yet.

**Why before code moves:** VS MSBuild needs the solution structure to exist
before you move files or it produces confusing errors mid-refactor.

**Prompt to Claude Code:**
```
Create a Visual Studio solution file at the repo root: AkariTool.sln
Create three new project directories:
  - src/AkariTool.Core/         (class library, net10.0-windows10.0.26100.0)
  - src/AkariTool.Infrastructure/ (class library, net10.0-windows10.0.26100.0)
  - src/AkariTool.App/          (move existing AkariTool.csproj content here later)

For each new csproj, model after Winhance:
  - Core: no PackageReferences except CommunityToolkit.Mvvm
  - Infrastructure: references AkariTool.Core; adds System.ServiceProcess, 
    System.Management, Microsoft.Win32 etc (OS-touching packages)
  - App: references both Core and Infrastructure; keeps WinUI 3 SDK refs

Add InternalsVisibleTo entries to Core and Infrastructure pointing at their
future test projects.

Do NOT move any source files yet. Do NOT modify any existing .cs files.
Report the new file paths created.
```

**Sign-off:** Build must still succeed on the existing (unmodified) AkariTool.csproj.

---

## Session 3 — Core layer extraction

**Goal:** Move pure models and enums to AkariTool.Core. No behavior changes.

### What moves to Core (zero OS dependencies, pure C#):

| File | From | Namespace change |
|------|------|-----------------|
| `Tabs/Shared/TweakDefinition.cs` | AkariTool.Tabs | AkariTool.Core.Models |
| `Tabs/Shared/TweakTargets.cs` | AkariTool.Tabs | AkariTool.Core.Models |
| `Services/CompetitiveOptions.cs` | AkariTool.Services | AkariTool.Core.Models |
| `Services/CompetitivePrefs.cs` | AkariTool.Services | AkariTool.Core.Models |
| `Services/CompetitiveSession.cs` | AkariTool.Services | AkariTool.Core.Models |
| `ViewModels/Tweaks/TweakItemViewModel.cs` | AkariTool.ViewModels | AkariTool.Core.ViewModels |
| `ViewModels/Tweaks/TweakSectionViewModel.cs` | AkariTool.ViewModels | AkariTool.Core.ViewModels |
| `ViewModels/Tweaks/TweakPageViewModel.cs` | AkariTool.ViewModels | AkariTool.Core.ViewModels |

### What stays (has OS deps or WinUI deps — do NOT move yet):

- `Tabs/Shared/TweakHelpers.*` — calls registry directly
- `Tabs/Shared/SystemStateReader.*` — reads OS state
- `Tabs/Shared/TweakRegistry.cs` — runtime state, App-layer concern
- `ViewModels/Tweaks/ToggleTweakViewModel.cs` — calls TweakHelpers

**Prompt to Claude Code:**
```
Move the following files to src/AkariTool.Core/, updating namespaces only.
Do not change any logic. Add a global using or namespace alias in AkariTool.App
so all existing call sites compile without changes:

[list files from table above]

After moving, verify the solution builds. Report any compilation errors.
```

**Sign-off:** Clean build. No behavior changes. Git diff shows only file moves + namespace.

---

## Session 4 — Interface extraction (selective, not exhaustive)

**Goal:** Add interfaces to the services that are actually testable.
Do NOT attempt to convert static classes yet — that's a separate decision.

### Strategy: thin interface wrapper, not full conversion

For static classes, the pattern is:
```csharp
// Core/Interfaces/IUpdateService.cs
public interface IUpdateService {
    Task<VersionInfo?> CheckAsync();
}

// Infrastructure/UpdateServiceWrapper.cs  
public sealed class UpdateServiceWrapper : IUpdateService {
    public Task<VersionInfo?> CheckAsync() => UpdateService.CheckAsync(); // delegates to static
}
```

This gives you DI + mockability without the 15-call-site refactor.

### Priority order (highest test value first):

**Tier 1 — Extract interface + wrapper (enables meaningful tests):**
- `IUpdateService` → wraps `UpdateService.CheckAsync()`
- `IToolFetchService` → wraps `ToolFetchService`
- `ISystemInfoService` → wraps `SystemInfoService` (Home tab banner)
- `IShaderCacheService` → wraps `ShaderCacheService`
- `IDefenderService` → wraps `DefenderService` (needs mock for safe testing)

**Tier 2 — Already instance classes, just add interface:**
- `IToolService` → `ToolService` already non-static ✓
- `IAkariUiLogService` → already implements `ILogService` ✓

**Tier 3 — Defer (pure OS P/Invoke, no test value without Windows):**
- `ElevationService` — 15 call sites, needs real Windows to test
- `CompetitiveService` — tightly coupled to process lifecycle
- `GameDetection` — reads real Win32 process list
- `ProcessSuspender`, `ProcessTuning` — kernel operations

**Prompt to Claude Code:**
```
For each service in Tier 1, create:
1. An interface file in src/AkariTool.Core/Features/Common/Interfaces/
2. A wrapper class in src/AkariTool.Infrastructure/Features/Common/Services/
   that implements the interface by delegating to the existing static class.
3. Register the wrapper in App.xaml.cs ConfigureServices() under the interface.

Do not modify the existing static classes. Do not change any ViewModel 
call sites yet — they still call the static directly. This session only
adds the interface + wrapper for future use.

Files: IUpdateService, IToolFetchService, ISystemInfoService, 
       IShaderCacheService, IDefenderService
```

**Sign-off:** Clean build. Static classes untouched. Wrappers registered in DI.

---

## Session 5 — IHost adoption + CompositionRoot

**Goal:** Replace App.xaml.cs's 200-line `ConfigureServices()` with Winhance's
cleaner `IHost` + `CompositionRoot` pattern.

**Current Akari:** Manual `ServiceCollection` + `ServiceLocator.Initialize(provider)`

**Target pattern (from Winhance):**
```csharp
// App layer: CompositionRoot.cs
public static class CompositionRoot {
    public static IServiceCollection ConfigureAkariServices(this IServiceCollection services) {
        services
            .AddCoreServices()
            .AddInfrastructureServices()
            .AddUIServices();
        return services;
    }
}

// Split registration into three extension methods:
// AddCoreServices() — interfaces from Core layer
// AddInfrastructureServices() — wrappers + OS-touching services  
// AddUIServices() — ViewModels, MainWindow, TweakDialogs
```

**Important:** Winhance uses `IHost` with full host lifecycle. Akari can adopt
`IHost` but must be careful: `WindowsAppSDKSelfContained=true` + unpackaged
means no hosted services lifecycle. Keep it simple — use `IHost` only for
the DI container, not for `IHostedService` background workers yet.

**Prompt to Claude Code:**
```
Refactor App.xaml.cs ConfigureServices() into a CompositionRoot pattern.
Create src/AkariTool.App/DI/CompositionRoot.cs with:
  - ConfigureAkariServices() extension method on IServiceCollection
  - Calls AddInfrastructureServices() and AddUIServices() 

Create src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs:
  - AddInfrastructureServices() registers all Service wrappers + ToolService

Create src/AkariTool.App/DI/UIServiceExtensions.cs:
  - AddUIServices() registers ViewModels, MainWindow, TweakDialogs

App.xaml.cs ConfigureServices() becomes a single line:
  services.AddWinUIFrameworkCore().ConfigureAkariServices();

Do not change any registration semantics — same singletons/transients as now.
Do not use IHostedService. Just reorganize registration into these three files.
```

**Sign-off:** Clean build. Startup behavior unchanged. DI container identical.

---

## Session 6 — File moves to src/ layout

**Goal:** Move all remaining files into `src/AkariTool.App/` to match the
clean folder structure, and reorganize `Tabs/` into `Features/`.

**Target structure (mirroring Winhance):**
```
src/
├── AkariTool.Core/
│   └── Features/
│       ├── Common/
│       │   ├── Models/     ← TweakDefinition, TweakTargets, etc.
│       │   └── Interfaces/ ← IUpdateService, etc.
│       └── Competitive/
│           └── Models/     ← CompetitiveOptions, CompetitivePrefs, CompetitiveSession
├── AkariTool.Infrastructure/
│   └── Features/
│       ├── Common/
│       │   └── Services/   ← ToolService, wrappers
│       ├── Tweaks/
│       │   └── Services/   ← TweakHelpers.*, SystemStateReader.*
│       ├── Gaming/
│       │   └── Services/   ← GameDetection, GpuTweaks, NvidiaProfileService
│       ├── Defender/
│       │   └── Services/   ← DefenderService, DefenderPhase2Scheduler
│       └── Software/
│           └── Services/   ← SoftwareAppService, AppIconService, removal scripts
└── AkariTool.App/
    ├── Features/
    │   ├── Gaming/
    │   │   └── Catalog/    ← gaming tweak definitions
    │   ├── Software/
    │   │   └── Catalog/    ← app catalogs
    │   └── [one dir per tab]
    ├── Views/
    ├── ViewModels/
    ├── DI/
    ├── App.xaml
    └── MainWindow.xaml
```

**Prompt to Claude Code:**
```
Move files from the flat root layout into the src/ project structure.
Namespace updates only — no logic changes. Update all using directives.

Do this in batches, verifying the build compiles after each batch:
Batch 1: Infrastructure/Features/Tweaks (TweakHelpers.*, SystemStateReader.*, PlaybookTweaks.*)
Batch 2: Infrastructure/Features/Gaming (GameDetection, GpuTweaks, NvidiaProfileService)
Batch 3: Infrastructure/Features/Defender (DefenderService, DefenderPhase2Scheduler)
Batch 4: App/Features/[tabs] (Catalog files, Tabs/Gaming, Tabs/Customize, etc.)
Batch 5: App/Views, App/ViewModels (already correct structure, just move to new root)

Report compilation errors after each batch before proceeding to next.
```

**Sign-off:** Clean build. Git diff shows namespace changes + file moves only.

---

## Session 7 — Test project scaffolding

**Goal:** Create xUnit test projects and write the first meaningful tests.

**What's actually testable (be honest):**

| Target | Testable? | Why |
|--------|-----------|-----|
| `TweakDefinition` evaluation logic | ✅ Yes | Pure C# |
| `TweakSectionViewModel` | ✅ Yes | No OS deps |
| `TweakPageViewModel.Build()` | ⚠️ Partial | Depends on catalog static data |
| `UpdateService` via `IUpdateService` mock | ✅ Yes | With mock wrapper |
| `ElevationService` | ❌ No | Requires real Windows + real SYSTEM token |
| `DefenderService` | ❌ No | Requires real Windows + real CAB |
| WinUI 3 pages | ❌ No | No test host for WinAppSDK |

**Prompt to Claude Code:**
```
Create two test projects:

1. tests/AkariTool.Core.Tests/
   - xUnit + FluentAssertions
   - ProjectReference to AkariTool.Core
   - Write tests for:
     a. TweakDefinition: verify ReadState/Apply delegates fire correctly
     b. TweakBadgePill: verify badge state logic (Recommended/Default/Custom)
     c. Any pure model validation logic in CompetitiveOptions/CompetitiveSession

2. tests/AkariTool.Infrastructure.Tests/
   - xUnit + FluentAssertions + NSubstitute (for mocking IUpdateService)
   - ProjectReference to AkariTool.Core + AkariTool.Infrastructure
   - Write tests for:
     a. UpdateServiceWrapper: mock the HTTP call, verify version comparison logic
     b. IShaderCacheService: mock path resolution

Do not write tests for anything that requires real Windows registry or SYSTEM token.
Add a [Fact] with [Skip="Requires Windows registry"] for those as placeholders.
```

**Sign-off:** `dotnet test` passes all non-skipped tests. Skipped tests documented.

---

## What NOT to do in these sessions

1. **Don't convert static classes to instances all at once.** `ElevationService`
   alone has 15 call sites. Do it one class at a time if ever — and only when
   you need to test that specific class. The wrapper pattern handles it for now.

2. **Don't change tweak behavior during restructuring.** If a TweakHelpers method
   needs a fix, that's a separate PR. Restructuring sessions are namespace-only.

3. **Don't touch Defender code without explicit sign-off.** The two-phase mechanism
   requires byte-identical files. Not touching it during any of these sessions.

4. **Don't break TweakDefinition IDs.** Moving files doesn't change IDs, but if
   any refactor serializes type names, backup file compatibility breaks.

5. **Don't run Sessions 3-7 on the same day.** Each session needs a real build
   verification before the next starts.

---

## Effort estimate (realistic, with Claude Code)

| Session | Estimated time | Risk |
|---------|---------------|------|
| 0 — Audit | 30 min | Low |
| 1 — Remaining waves (if any) | 1–3 hrs | Low (known pattern) |
| 2 — Solution file | 1 hr | Low |
| 3 — Core extraction | 2 hrs | Medium (namespace chain) |
| 4 — Interface extraction | 3 hrs | Medium (15 new files) |
| 5 — IHost / CompositionRoot | 2 hrs | Medium (startup path) |
| 6 — File moves | 4 hrs | High (biggest session) |
| 7 — Test scaffolding | 3 hrs | Low |

**Total: ~16–18 hrs of Claude Code sessions across separate days.**

---

## Key difference from Winhance you should NOT try to copy

Winhance tests are in a separate `tests/` root (not visible in the 7z).
They appear to have full test coverage including UI tests via WinAppDriver.
**WinAppDriver for WinUI 3 is painful and not worth pursuing** for Akari Tool.
Stick to xUnit for Core + Infrastructure. Your regression safety comes from
those layers, not from simulating button clicks.
