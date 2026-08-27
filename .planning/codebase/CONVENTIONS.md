# Coding Conventions

**Analysis Date:** 2026-08-27

## Naming Patterns

**Files:**
- PascalCase for all C# files: `SettingDefinition.cs`, `WindowsRegistryService.cs`, `SettingItemViewModel.cs`
- Test files: `{SubjectUnderTest}Tests.cs` (e.g., `SettingDefinitionToggleStateTests.cs`, `BuildVersionGateTests.cs`)

**Functions/Methods:**
- PascalCase for all public and private methods: `ApplySetting`, `CreateKey`, `GetValue`, `RefreshFromSystem`
- Async methods end with `Async`: `ApplySettingOperationsAsync`, `HandleUnlockAsync`, `UpdateTechnicalDetailsAsync`
- Commands use `{VerbName}Command` naming: `DeletePlanCommand`, `UnlockCommand`, `OpenRegeditCommand`
- Relay commands defined with `[RelayCommand]` attribute pattern (see MVVM Community Toolkit)

**Variables:**
- camelCase for all local variables and parameters: `isEnabled`, `registryService`, `specificValue`, `allSucceeded`
- Private fields use camelCase with leading underscore: `_log`, `_stateReader`, `_executor`, `_dialogs`
- Constants in PascalCase at method or class scope: `MinDeleteDepth`, `AdvancedPowerWarningText`, `AdvancedPowerSettingsUnlocked`

**Types:**
- PascalCase for all classes, records, interfaces, and enums: `SettingDefinition`, `WindowsRegistryService`, `ISettingOperationExecutor`
- Records use `sealed record` and init-only properties: `public sealed record SettingDefinition : BaseDefinition, ISettingItem`
- Enum members use PascalCase: `Toggle`, `Selection`, `NumericRange` (in `InputType` enum)

## Code Style

**Formatting:**
- No explicit formatter configured (.editorconfig not present)
- Implicit usings enabled (`ImplicitUsings>enable` in .csproj)
- Nullable reference types enabled (`Nullable>enable` in .csproj)
- Standard C# 12+ syntax with records, init accessors, and pattern matching

**Linting:**
- No explicit linting rules configured (no .eslintrc or StyleCop config)
- Code quality enforced through test coverage and manual review

## Import Organization

**Order:**
1. System namespaces: `using System;`, `using System.Collections.Generic;`
2. External packages: `using CommunityToolkit.Mvvm.ComponentModel;`, `using Microsoft.Win32;`
3. Internal project namespaces: `using AkariTool.Core.Features.Common.Models;`, `using AkariTool.Infrastructure.Features.Common.Services;`
4. Framework-specific: `using WinUI.Framework.Services;`

**Path Aliases:**
- No widespread use of aliases; full namespaces preferred for clarity
- Exception: `using LogLevel = AkariTool.Core.Features.Common.Enums.LogLevel;` used when namespace collision occurs (e.g., when importing both `WinUI.Framework.Services` and Core enums)

## Error Handling

**Patterns:**
- Try-catch with logging: catch exceptions, log via `_log.Log(LogLevel.Error, $"[Category] {ex.Message}")`, return false/null
- Early returns for validation: `if (setting == null) return false;` pattern
- Boolean return type for operations: service methods return `bool` indicating success/failure
- No exception re-throwing in service layers; failures logged and handled gracefully
- Async safe operations: try-catch blocks with comment `/* diagnostics must never block the UI */` for UI-layer operations

**Example pattern from `WindowsRegistryService.cs`:**
```csharp
try
{
    // operation
    if (!CreateKey(setting.KeyPath)) return false;
    return SetValue(setting.KeyPath, setting.ValueName, valueToSet, setting.ValueType);
}
catch (Exception ex)
{
    _log.Log(LogLevel.Error, $"[Registry] Error applying '{setting.KeyPath}\\{setting.ValueName}': {ex.Message}");
    return false;
}
```

## Logging

**Framework:** `IAkariLogService` abstraction (injected via DI)

**Patterns:**
- Format: `_log.Log(LogLevel.{Level}, $"[{Category}] {Message}")`
- Levels: `LogLevel.Info`, `LogLevel.Warning`, `LogLevel.Error`, `LogLevel.Debug`
- Category prefix in square brackets identifies the component: `[Registry]`, `[PowerShell]`, `[ProcessRestart]`, `[RegeditLauncher]`
- String interpolation with `$"..."` for dynamic values
- Always log exceptions with `ex.Message`: `$"[Registry] Failed to create key '{keyPath}': {ex.Message}"`

**Common categories:**
- `[Registry]` — registry operations (in `WindowsRegistryService`)
- `[PowerShell]` — script execution
- `[ProcessRestart]` — process/service restart operations
- `[RegeditLauncher]` — Registry Editor launch attempts

## Comments

**When to Comment:**
- Section headers using `// ──` decoration for major code sections: `// ── Status Banner ──────────────────────────────────────`
- Explain complex logic, non-obvious patterns, or workarounds
- Note Winhance parity or design decisions with prefix like `// Winhance parity:` or `// 4h —` (session markers)
- Link to dependent code or constraints

**XMLDoc/TSDoc/C# Documentation:**
- Use `/// <summary>` for public types and methods: describes purpose, not implementation
- Use `/// <summary>` for public properties: brief description of what the property represents
- Use `/// <remarks>` for complex behavior requiring elaboration
- Use `/// <see cref="Type"/>` for cross-references to related types
- Long descriptions (20+ lines) are acceptable on complex properties like `SettingDefinition.LocalizationId`

**Example from `SettingDefinition.cs`:**
```csharp
/// <summary>
/// True when this Selection setting has no objectively-better choice —
/// the correct answer is user-, region-, or preference-driven.
/// </summary>
public bool IsSubjectivePreference { get; init; } = false;
```

## Function Design

**Size:** 
- Short methods preferred (most service methods 10–50 lines)
- Complex methods like `ApplySettingCore` in `WindowsRegistryService` can exceed 100 lines when branching on operation type (registry, task, PowerShell, etc.)
- Use helper methods to extract logic: `GetWriteValue()`, `ParseKeyPath()`, `CreateKey()` are private helpers

**Parameters:**
- Nullable reference types enabled; use `?` for optional parameters
- Prefer dependency injection through constructor over method parameters
- Method parameters use camelCase: `enable`, `specificValue`, `keyPath`

**Return Values:**
- Boolean for success/failure: `bool ApplySetting(...)`
- Nullable values for optional results: `object? GetValue(...)`
- Records for composite results: `PowerCfgSetting`, `OperationResult`, `ProcessResult`
- Collections as `IReadOnlyList<T>` in public APIs
- Tasks for async: `async Task MethodAsync()` or `async Task<T> MethodAsync()`

## Module Design

**Exports:**
- Public types and interfaces exported via project's main namespace
- Core types in `AkariTool.Core.Features.Common.*` namespaces
- Infrastructure services in `AkariTool.Infrastructure.Features.Common.*` namespaces
- App ViewModels in `AkariTool.ViewModels.*` namespace (with `AkariTool.Services` alias for backward compatibility)

**Barrel Files:**
- No barrel files (no `index.ts` or `__init__.cs` re-exports)
- Direct namespaced imports: `using AkariTool.Core.Features.Common.Models;`

**Sealed Records for Immutability:**
- All data models use `sealed record` with init-only properties: prevents accidental mutation
- Example: `public sealed record SettingDefinition : BaseDefinition, ISettingItem`

## MVVM & UI Patterns

**ViewModels:**
- Inherit from `ObservableObject` (CommunityToolkit.Mvvm)
- Use `[ObservableProperty]` attribute for properties with change notifications: `[ObservableProperty] public partial bool IsLocked { get; set; }`
- Implement partial void change handlers: `partial void OnIsLockedChanged(bool value)`
- Commands declared with `[RelayCommand]` attribute for simple commands: `[RelayCommand] private async Task ToggleTechnicalDetails()`
- Async commands stored as properties: `public IAsyncRelayCommand UnlockCommand { get; }`

**Dependency Injection:**
- Constructor injection pattern exclusively
- Null checks for optional dependencies: `if (Definition.RequiresAdvancedUnlock && _settingsService != null)`
- ServiceLocator used only as fallback in parameterless constructors (e.g., XAML-instantiated controls)

---

*Convention analysis: 2026-08-27*
