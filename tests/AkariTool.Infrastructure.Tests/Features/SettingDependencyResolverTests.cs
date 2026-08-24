using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class SettingDependencyResolverTests
{
    private readonly ISettingOperationExecutor _executor = Substitute.For<ISettingOperationExecutor>();
    private readonly ISettingStateReader _stateReader = Substitute.For<ISettingStateReader>();
    private readonly IProcessRestartManager _restartManager = Substitute.For<IProcessRestartManager>();
    private readonly IAkariLogService _log = Substitute.For<IAkariLogService>();

    private SettingDependencyResolver MakeResolver() =>
        new(_executor, _stateReader, _restartManager, _log);

    private static SettingDefinition Toggle(string id, params SettingDependency[] deps) => new()
    {
        Id = id,
        Name = id,
        Description = id,
        InputType = InputType.Toggle,
        Dependencies = deps,
    };

    private static SettingDependency RequiresEnabled(string dependentId, string requiredId) => new()
    {
        DependencyType = SettingDependencyType.RequiresEnabled,
        DependentSettingId = dependentId,
        RequiredSettingId = requiredId,
    };

    private void StateIs(string settingId, bool enabled)
    {
        _stateReader.ReadToggleState(Arg.Any<SettingDefinition>())
            .Returns(callInfo => callInfo.ArgAt<SettingDefinition>(0).Id == settingId ? enabled : false);
    }

    private void StatesAre(params (string Id, bool Enabled)[] states)
    {
        var map = states.ToDictionary(s => s.Id, s => s.Enabled);
        _stateReader.ReadToggleState(Arg.Any<SettingDefinition>())
            .Returns(callInfo => map.TryGetValue(callInfo.ArgAt<SettingDefinition>(0).Id, out var enabled) && enabled);
    }

    private void ExecutorSucceeds()
    {
        _executor.ApplySettingOperationsAsync(Arg.Any<SettingDefinition>(), Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>())
            .Returns(OperationResult.Succeeded());
    }

    [Fact]
    public async Task Enable_WithUnsatisfiedParent_AppliesParentEnabled_AndFiresEvent()
    {
        var child = Toggle("child", RequiresEnabled("child", "parent"));
        var parent = Toggle("parent");
        StateIs("parent", enabled: false);
        ExecutorSucceeds();
        var resolver = MakeResolver();
        string? appliedId = null;
        resolver.SettingApplied += id => appliedId = id;

        await resolver.HandleDependenciesAsync("child", new[] { child, parent }, enable: true, value: null);

        await _executor.Received(1).ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "parent"),
            Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>());
        appliedId.Should().Be("parent");
    }

    [Fact]
    public async Task Enable_WithSatisfiedParent_DoesNotReapplyParent()
    {
        var child = Toggle("child", RequiresEnabled("child", "parent"));
        var parent = Toggle("parent");
        StateIs("parent", enabled: true);
        ExecutorSucceeds();
        var resolver = MakeResolver();

        await resolver.HandleDependenciesAsync("child", new[] { child, parent }, enable: true, value: null);

        await _executor.DidNotReceive().ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "parent"), Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Enable_WithMissingRequiredSetting_ThrowsInvalidOperation()
    {
        var child = Toggle("child", RequiresEnabled("child", "ghost"));
        StateIs("child", enabled: false);
        var resolver = MakeResolver();

        var act = async () => await resolver.HandleDependenciesAsync("child", new[] { child }, enable: true, value: null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unsatisfied dependencies*");
    }

    [Fact]
    public async Task Enable_AutoEnableChild_AlwaysApplies_UnderSuppressedRestarts()
    {
        var parent = new SettingDefinition
        {
            Id = "parent",
            Name = "parent",
            Description = "parent",
            InputType = InputType.Toggle,
            AutoEnableSettingIds = new[] { "child" },
        };
        var child = Toggle("child");
        ExecutorSucceeds();
        _restartManager.SuppressRestarts().Returns(Substitute.For<IDisposable>());
        var resolver = MakeResolver();

        await resolver.HandleDependenciesAsync("parent", new[] { parent, child }, enable: true, value: null);

        // Winhance parity: the auto-enable applies even when the child is already on.
        await _executor.Received(1).ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "child"),
            Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>());
        _restartManager.Received(1).SuppressRestarts();
    }

    [Fact]
    public async Task Disable_CascadesEnabledDependent_ToDefault_AndRecurses()
    {
        var parent = Toggle("hibernation");
        var mid = Toggle("fast-startup", RequiresEnabled("fast-startup", "hibernation"));
        var leaf = Toggle("leaf", RequiresEnabled("leaf", "fast-startup"));
        StatesAre(("fast-startup", true), ("leaf", true));
        ExecutorSucceeds();
        var applied = new List<(string Id, bool Reset)>();
        _executor.ApplySettingOperationsAsync(Arg.Any<SettingDefinition>(), Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>())
            .Returns(callInfo =>
            {
                applied.Add((callInfo.ArgAt<SettingDefinition>(0).Id, callInfo.ArgAt<bool>(3)));
                return OperationResult.Succeeded();
            });
        var resolver = MakeResolver();

        await resolver.HandleDependenciesAsync("hibernation", new[] { parent, mid, leaf }, enable: false, value: null);

        applied.Select(a => a.Id).Should().Equal("fast-startup", "leaf");
        applied.Should().OnlyContain(a => a.Reset);
    }

    [Fact]
    public async Task Disable_SkipsAlreadyDisabledDependent()
    {
        var parent = Toggle("parent");
        var dependent = Toggle("dependent", RequiresEnabled("dependent", "parent"));
        StateIs("dependent", enabled: false);
        var resolver = MakeResolver();

        await resolver.HandleDependenciesAsync("parent", new[] { parent, dependent }, enable: false, value: null);

        await _executor.DidNotReceive().ApplySettingOperationsAsync(
            Arg.Any<SettingDefinition>(), Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Disable_PowerCfgSelectionCascade_NseIsLogged_NotThrown()
    {
        // Winhance divergence: its PowerCfgApplier throws NotSupportedException for a
        // Selection reset with no value; Akari logs and continues so the parent lands.
        var selection = new SettingDefinition
        {
            Id = "hybrid-sleep",
            Name = "hybrid-sleep",
            Description = "hybrid-sleep",
            InputType = InputType.Selection,
            Dependencies = new[] { RequiresEnabled("hybrid-sleep", "hibernation") },
        };
        var parent = Toggle("hibernation");
        StateIs("hybrid-sleep", enabled: true);
        _executor.ApplySettingOperationsAsync(Arg.Any<SettingDefinition>(), Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>())
            .Returns(_ => Task.FromException<OperationResult>(new NotSupportedException("Selection not supported for PowerCfg operations")));
        var resolver = MakeResolver();

        var act = async () => await resolver.HandleDependenciesAsync("hibernation", new[] { parent, selection }, enable: false, value: null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValueChange_UnsatisfiedRequiresSpecificValue_ResetsDependent()
    {
        var dropdown = new SettingDefinition
        {
            Id = "dropdown",
            Name = "dropdown",
            Description = "dropdown",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata
            {
                Options = new List<ComboBoxOption>
                {
                    new() { DisplayName = "Off" },
                    new() { DisplayName = "On" },
                },
            },
        };
        var dependent = Toggle("dependent", new SettingDependency
        {
            DependencyType = SettingDependencyType.RequiresSpecificValue,
            DependentSettingId = "dependent",
            RequiredSettingId = "dropdown",
            RequiredValue = "On",
        });
        StateIs("dependent", enabled: true);
        _stateReader.ReadSelectionIndex(Arg.Any<SettingDefinition>()).Returns(0); // now "Off"
        ExecutorSucceeds();
        var resolver = MakeResolver();

        await resolver.HandleDependenciesAsync("dropdown", new[] { dropdown, dependent }, enable: true, value: 0);

        await _executor.Received(1).ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "dependent"),
            Arg.Is<bool>(v => !v), Arg.Any<object?>(), Arg.Is<bool>(v => v));
    }

    [Fact]
    public async Task ValuePrerequisites_UnsatisfiedToggleRequirement_AutoFixesBeforeApply()
    {
        var required = Toggle("required");
        var setting = Toggle("setting", new SettingDependency
        {
            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
            DependentSettingId = "setting",
            RequiredSettingId = "required",
            RequiredValue = "true",
        });
        StateIs("required", enabled: false);
        ExecutorSucceeds();
        var resolver = MakeResolver();

        await resolver.HandleValuePrerequisitesAsync(setting, "setting", new[] { required, setting });

        await _executor.Received(1).ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "required"),
            Arg.Any<bool>(), Arg.Any<object?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SyncParentToMatchingPreset_AllChildrenMatch_MovesParentToPresetIndex()
    {
        var parent = new SettingDefinition
        {
            Id = "parent",
            Name = "parent",
            Description = "parent",
            InputType = InputType.Selection,
            SettingPresets = new Dictionary<int, Dictionary<string, bool>>
            {
                [1] = new Dictionary<string, bool> { ["child-a"] = true, ["child-b"] = true },
            },
        };
        var childA = Toggle("child-a");
        var childB = Toggle("child-b");
        var changedChild = Toggle("changed-child", new SettingDependency
        {
            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
            DependentSettingId = "changed-child",
            RequiredSettingId = "parent",
        });
        StatesAre(("child-a", true), ("child-b", true));
        ExecutorSucceeds();
        var resolver = MakeResolver();

        await resolver.SyncParentToMatchingPresetAsync(changedChild, "changed-child", new[] { parent, childA, childB, changedChild });

        await _executor.Received(1).ApplySettingOperationsAsync(
            Arg.Is<SettingDefinition>(d => d.Id == "parent"),
            Arg.Any<bool>(), Arg.Is<object?>(v => v is int && (int)v! == 1), Arg.Any<bool>());
    }
}
