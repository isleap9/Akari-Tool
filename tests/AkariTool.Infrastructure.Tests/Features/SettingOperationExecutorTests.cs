using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using Microsoft.Win32;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class SettingOperationExecutorTests
{
    private static SettingOperationExecutor MakeExecutor(IWindowsRegistryService registryService)
    {
        var comboBoxResolver = Substitute.For<IComboBoxResolver>();
        var processRestartManager = Substitute.For<IProcessRestartManager>();
        var powerCfgApplier = Substitute.For<IPowerCfgApplier>();
        var scheduledTaskService = Substitute.For<IScheduledTaskService>();
        var processExecutor = Substitute.For<IProcessExecutor>();
        var powerShellRunner = Substitute.For<IPowerShellRunner>();
        var fileSystemService = Substitute.For<IFileSystemService>();
        var logService = Substitute.For<IAkariLogService>();
        var specialHandlerRegistry = Substitute.For<ISpecialSettingHandlerRegistry>();
        specialHandlerRegistry.TryGet(Arg.Any<string>()).Returns((ISpecialSettingHandler?)null);

        return new SettingOperationExecutor(
            registryService,
            comboBoxResolver,
            processRestartManager,
            powerCfgApplier,
            scheduledTaskService,
            processExecutor,
            powerShellRunner,
            fileSystemService,
            logService,
            specialHandlerRegistry);
    }

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

    [Fact]
    public async Task ApplyAsync_ToggleSetting_RegistrySucceeds_ReturnsSuccess()
    {
        var registrySub = Substitute.For<IWindowsRegistryService>();
        registrySub.ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>()).Returns(true);
        var executor = MakeExecutor(registrySub);

        var result = await executor.ApplySettingOperationsAsync(MakeToggleSetting("t1"), enable: true, value: null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_NoRegistrySettings_ReturnsSuccess_WithoutCallingRegistry()
    {
        var registrySub = Substitute.For<IWindowsRegistryService>();
        var executor = MakeExecutor(registrySub);

        var setting = new SettingDefinition
        {
            Id = "t2",
            Name = "Test",
            Description = "Desc",
        };

        var result = await executor.ApplySettingOperationsAsync(setting, enable: true, value: null);

        result.Success.Should().BeTrue();
        registrySub.DidNotReceive().ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ApplyAsync_RegistryReturnsFalse_ReturnsFailure()
    {
        var registrySub = Substitute.For<IWindowsRegistryService>();
        registrySub.ApplySetting(Arg.Any<RegistrySetting>(), Arg.Any<bool>()).Returns(false);
        var executor = MakeExecutor(registrySub);

        var result = await executor.ApplySettingOperationsAsync(MakeToggleSetting("t3"), enable: true, value: null);

        result.Success.Should().BeFalse();
    }
}
