# Testing Patterns

**Analysis Date:** 2026-08-27

## Test Framework

**Runner:**
- xunit 2.9.3
- Config: No explicit config file; uses default xunit discovery and execution
- Visual Studio runner: xunit.runner.visualstudio 3.1.5

**Assertion Library:**
- FluentAssertions 8.7.1
- Syntax: `.Should().Be()`, `.Should().BeTrue()`, `.Should().BeNull()`, etc.

**Run Commands:**
```bash
dotnet test                          # Run all tests in solution
dotnet test --filter "Category=SomeTest"  # Run filtered tests
```

## Test File Organization

**Location:**
- Separate from source code
- Mirrors source structure: `tests/AkariTool.Core.Tests/Features/` mirrors `src/AkariTool.Core/Features/`
- Namespace matches source with `.Tests` suffix: `AkariTool.Infrastructure.Tests.Features` mirrors `AkariTool.Infrastructure.Features.Common.Services`

**Naming:**
- File: `{ClassUnderTest}Tests.cs` (e.g., `SettingDefinitionToggleStateTests.cs`, `SettingStateReaderTests.cs`)
- Test method: `{MethodName}_{Condition}_{ExpectedResult}` (e.g., `ApplyAsync_ToggleSetting_RegistrySucceeds_ReturnsSuccess`)
- Alternative simpler form: `{MethodName}_{ConditionDescription}` (e.g., `GetPrimaryRegistrySetting_ReturnsPrimaryFlaggedEntry`)

**Directory structure:**
```
tests/
├── AkariTool.Core.Tests/
│   ├── Features/
│   │   ├── SettingDefinitionToggleStateTests.cs
│   │   ├── SettingDefinitionModelTests.cs
│   │   ├── BadgePillStateTests.cs
│   │   └── ...
│   ├── Helpers/
│   │   └── BuildVersionGateTests.cs
│   ├── Models/
│   │   └── UpdateModelsTests.cs
│   └── AkariTool.Core.Tests.csproj
└── AkariTool.Infrastructure.Tests/
    ├── Features/
    │   ├── SettingStateReaderTests.cs
    │   ├── SettingOperationExecutorTests.cs
    │   ├── Optimize/
    │   │   └── WindowsUpdatePolicyHandlerTests.cs
    │   └── ...
    ├── Services/
    │   └── UpdateServiceTests.cs
    └── AkariTool.Infrastructure.Tests.csproj
```

## Test Structure

**Suite Organization:**
```csharp
using System;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class SettingDefinitionToggleStateTests
{
    // Factory methods for creating test objects
    private static SettingDefinition Setting(params RegistrySetting[] registry) =>
        new SettingDefinition
        {
            Id = "id",
            Name = "name",
            Description = "desc",
            RegistrySettings = registry,
        };

    // Individual test methods
    [Fact]
    public void GetPrimaryRegistrySetting_ReturnsPrimaryFlaggedEntry()
    {
        // Arrange
        var primary = new RegistrySetting { /* ... */ };
        var setting = Setting(primary);

        // Act
        var result = SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting);

        // Assert
        result.Should().Be(primary);
    }

    [Theory]
    [InlineData(26100, 7171, true)]
    [InlineData(26101, 0, false)]
    public void BuildVersionGate_WithVersionData_EvaluatesCorrectly(int build, int revision, bool expected)
    {
        // Arrange, Act, Assert in one line for simple theories
        BuildVersionGate.IsCompatible(build, revision, 26100, null, null, null).Should().Be(expected);
    }
}
```

**Patterns:**
- **Setup (Arrange):** Factory methods for creating test objects (`Setting()`, `MakeExecutor()`)
- **Teardown:** Not used; xunit creates new instance per test; no cleanup needed for managed dependencies
- **Assertion:** FluentAssertions fluent syntax: `.Should().Be(expected)`, `.Should().BeTrue()`, `.Should().HaveBeenCalled()`

## Mocking

**Framework:** NSubstitute 5.3.0

**Patterns:**
```csharp
// Create substitutes (mocks)
var registrySub = Substitute.For<IWindowsRegistryService>();

// Set up return values
registrySub.ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>()).Returns(true);

// Set up async return values
var queryService = Substitute.For<IPowerSettingsQueryService>();
queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
    .Returns(Task.FromResult(((int?)1, (int?)1)));

// Verify method was called with specific args
registrySub.DidNotReceive().ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>());

// Create executor with mocked dependencies
var executor = new SettingOperationExecutor(
    registrySub,
    Substitute.For<IComboBoxResolver>(),
    Substitute.For<IProcessRestartManager>(),
    // ... other mocked dependencies
);
```

**What to Mock:**
- External dependencies: `IWindowsRegistryService`, `IPowerSettingsQueryService`, `IScheduledTaskService`, `IPowerShellRunner`
- Services that interact with OS or file system
- Database/data access layers
- Network or timing-dependent services
- Dependencies that cannot be easily controlled in test environment

**What NOT to Mock:**
- Objects under test (the class being tested)
- Simple data models (`SettingDefinition`, `RegistrySetting` — use real instances)
- Core business logic that doesn't require IO (create real instances when possible)
- Pure functions and static methods

## Fixtures and Factories

**Test Data:**
Common pattern in tests: local factory methods create test objects inline

```csharp
// Simple factory for Settings
private static SettingDefinition Setting(params RegistrySetting[] registry) =>
    new SettingDefinition
    {
        Id = "id",
        Name = "name",
        Description = "desc",
        RegistrySettings = registry,
    };

// Complex factory for Executor with all dependencies
private static SettingOperationExecutor MakeExecutor(IWindowsRegistryService registryService)
{
    var comboBoxResolver = Substitute.For<IComboBoxResolver>();
    var processRestartManager = Substitute.For<IProcessRestartManager>();
    // ... create and configure all dependencies
    return new SettingOperationExecutor(registryService, comboBoxResolver, /* ... */);
}

// Power Selection setting factory with fully configured options
private static SettingDefinition MakePowerSelectionSetting()
{
    return new SettingDefinition
    {
        Id = "power-test",
        Name = "Test",
        Description = "Desc",
        InputType = InputType.Selection,
        ComboBox = new ComboBoxMetadata
        {
            Options = new[]
            {
                new ComboBoxOption { DisplayName = "Balanced", ValueMappings = /* ... */ },
                new ComboBoxOption { DisplayName = "High performance", ValueMappings = /* ... */ },
            },
        },
    };
}
```

**Location:**
- Private static methods at the top of test class
- Inline creation when fixture is trivial (one-line `new` expressions)
- No separate fixture classes; factories embedded in test class

## Coverage

**Requirements:** None enforced (no coverage gates in CI/CD)

## Test Types

**Unit Tests:**
- Scope: Individual methods or small functions in isolation
- Approach: Test pure logic, mocking OS/IO dependencies
- Example: `SettingDefinitionToggleStateTests` tests static toggle-state derivation logic
- Example: `BuildVersionGateTests` tests version compatibility predicates
- Example: `SettingStateReaderTests` tests registry value detection and ComboBox option matching

**Integration Tests:**
- Scope: Multiple components working together (but not with real OS/registry)
- Approach: Mock external resources (registry, file system, processes); test interaction between layers
- Example: `SettingOperationExecutorTests` tests executor coordinating with registry, task, and PowerShell services (all mocked)
- Example: `SettingDependencyResolverTests` tests resolver evaluating dependency chains
- Example: `SystemBackupServiceParsingTests` tests vssadmin shadow-storage parsing without actual system calls

**E2E Tests:**
- Not used; functional validation happens on real Windows via manual testing or VM gates

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task ApplyAsync_ToggleSetting_RegistrySucceeds_ReturnsSuccess()
{
    // Arrange
    var registrySub = Substitute.For<IWindowsRegistryService>();
    registrySub.ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>()).Returns(true);
    var executor = MakeExecutor(registrySub);

    // Act
    var result = await executor.ApplySettingOperationsAsync(
        MakeToggleSetting("t1"), 
        enable: true, 
        value: null);

    // Assert
    result.Success.Should().BeTrue();
}
```

**Parameterized Testing (Theory with InlineData):**
```csharp
[Theory]
[InlineData(1, true)]      // acValue=1, expected=true (enabled)
[InlineData(0, false)]     // acValue=0, expected=false (disabled)
public void ReadToggleState_PowerCfgOnly_NonZeroAcValue_IsEnabled(int acValue, bool expected)
{
    var queryService = Substitute.For<IPowerSettingsQueryService>();
    queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
        .Returns(Task.FromResult(((int?)acValue, (int?)acValue)));
    
    var reader = MakeReader(queryService);
    
    reader.ReadToggleState(MakePowerSelectionSetting()).Should().Be(expected);
}
```

**Mocking Async Dependencies:**
```csharp
[Fact]
public void ReadSelectionIndex_PowerCfgQueryFails_FallsBackToIsDefault()
{
    // Setup async service to return Task with null values
    var queryService = Substitute.For<IPowerSettingsQueryService>();
    queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
        .Returns(Task.FromResult(((int?)null, (int?)null)));

    var reader = MakeReader(queryService);

    reader.ReadSelectionIndex(MakePowerSelectionSetting()).Should().Be(0);
}
```

**Testing Verification (Did method get called?):**
```csharp
[Fact]
public async Task ApplyAsync_NoRegistrySettings_ReturnsSuccess_WithoutCallingRegistry()
{
    var registrySub = Substitute.For<IWindowsRegistryService>();
    var executor = MakeExecutor(registrySub);

    var setting = new SettingDefinition { /* ... */ };
    var result = await executor.ApplySettingOperationsAsync(setting, enable: true, value: null);

    result.Success.Should().BeTrue();
    
    // Verify the substitute was never called
    registrySub.DidNotReceive().ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>());
}
```

## Test Statistics

**Current coverage** (as of 2026-08-27):

| Project | Suite | Test Count | Status |
|---------|-------|-----------|--------|
| Core.Tests | SettingDefinitionToggleStateTests | 10 | ✓ Pass |
| Core.Tests | SettingDefinitionModelTests | 4 | ✓ Pass |
| Core.Tests | BadgePillStateTests | 4 | ✓ Pass |
| Core.Tests | BackupResultTests | 2 | ✓ Pass |
| Core.Tests | BuildVersionGateTests | 22 | ✓ Pass |
| Core.Tests | UpdateModelsTests | 11 | ✓ Pass |
| Core.Tests | SettingCatalogValidatorTests | + tests | ✓ Pass |
| Infra.Tests | SettingStateReaderTests | 32 | ✓ Pass |
| Infra.Tests | SettingOperationExecutorTests | 3 | ✓ Pass |
| Infra.Tests | SettingDependencyResolverTests | 10 | ✓ Pass |
| Infra.Tests | PowerPlanHelperTests | 13 | ✓ Pass |
| Infra.Tests | PowerPlanComboBoxServiceTests | 6 | ✓ Pass |
| Infra.Tests | ComboBoxResolverTests | 5 | ✓ Pass |
| Infra.Tests | WindowsUpdatePolicyHandlerTests | 4 | ✓ Pass |
| Infra.Tests | SystemBackupServiceParsingTests | 14 | ✓ Pass |
| Infra.Tests | WindowsCompatibilityFilterTests | 15 | ✓ Pass |
| Infra.Tests | HardwareCompatibilityFilterTests | 9 | ✓ Pass |
| Infra.Tests | UpdateServiceTests | 25 + 1 skip | ✓ Pass (1 network-dependent test skipped) |

**Totals:** Core 53 passing; Infrastructure 136 passing + 1 skipped (network)

---

*Testing analysis: 2026-08-27*
