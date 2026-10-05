using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.ViewModels.Tweaks;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.App.Tests.ViewModels.Tweaks;

public class SettingBadgeCalculatorTests
{
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
                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Test",
                ValueName = id,
                RecommendedValue = null,
                DefaultValue = null,
                ValueType = RegistryValueKind.DWord,
            },
        },
    };

    // ── Smoke: discovery + reachability ─────────────────────────────────────

    [Fact]
    public void Compute_ToggleWithNoBadgeData_ReturnsNoPills()
    {
        // Smoke test proving the App assembly is reachable from this project and that
        // vstest discovers tests here. A Toggle whose single registry setting carries
        // neither a RecommendedValue nor a DefaultValue and which declares no combo
        // option, scheduled task or valued PowerCfg entry satisfies none of the
        // hasBadgeData sources, so Compute returns early with an empty list
        // (SettingBadgeCalculator.cs:33-41).
        var pills = SettingBadgeCalculator.Compute(
            Toggle("toggle-no-badge-data"),
            InputType.Toggle,
            isOn: true,
            selectedIndex: -1,
            numericValue: 0,
            acNumericValue: 0,
            dcNumericValue: 0,
            hasBattery: false,
            supportsSeparateACDC: false);

        pills.Should().BeEmpty();
    }
}