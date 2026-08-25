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
                KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                ValueName = id,
                RecommendedValue = null,
                DefaultValue = null,
                ValueType = RegistryValueKind.DWord,
            },
        },
    };

    private static ComboBoxOption Opt(string name, bool rec = false, bool def = false, string? mapKey = null) => new()
    {
        DisplayName = name,
        IsRecommended = rec,
        IsDefault = def,
        ValueMappings = mapKey is null
            ? null
            : new Dictionary<string, object?> { [mapKey] = 1 },
    };

    private static SettingDefinition Selection(
        string id,
        IEnumerable<ComboBoxOption>? options = null,
        bool subjective = false,
        bool powerCfgBacked = false,
        bool dynamic = false,
        bool emptyRegistry = false)
    {
        var opts = options?.ToList() ?? new List<ComboBoxOption>();
        if (options is null && !subjective && !powerCfgBacked && !dynamic && opts.Count == 0)
        {
            // Standard default shape used by most tests.
            opts.Add(Opt("A", rec: true, def: false));
            opts.Add(Opt("B", rec: false, def: true));
        }

        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "d",
            InputType = InputType.Selection,
            IsSubjectivePreference = subjective,
            Recommendation = dynamic ? new PowerRecommendation { LoadDynamicOptions = true } : null,
            PowerCfgSettings = powerCfgBacked
                ? new[]
                {
                    new PowerCfgSetting
                    {
                        SettingGuid = "set",
                        RecommendedValueAC = null,
                        RecommendedValueDC = null,
                        DefaultValueAC = null,
                        DefaultValueDC = null,
                    },
                }
                : null,
            RegistrySettings = emptyRegistry
                ? Array.Empty<RegistrySetting>()
                : new[]
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
            ComboBox = new ComboBoxMetadata { Options = opts },
        };
    }

    private static SettingGroup Group(string name, params SettingDefinition[] settings) => new()
    {
        Name = name,
        FeatureId = "test",
        Settings = settings,
    };

    private static string? SingleMessage(IReadOnlyList<CatalogViolation> violations)
        => violations.Count == 1 ? violations[0].Message : null;

    // ── Selection shape: Standard ────────────────────────────────────────────

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

    [Fact]
    public void StandardSelection_TwoRecommended_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true),
            Opt("B", rec: true, def: true),
        }));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("exactly one IsRecommended option (found 2)");
    }

    [Fact]
    public void StandardSelection_ZeroDefault_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true),
            Opt("B"),
        }));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("exactly one IsDefault");
    }

    [Fact]
    public void StandardSelection_TwoDefault_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true, def: true),
            Opt("B", def: true),
        }));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("exactly one IsDefault option (found 2)");
    }

    [Fact]
    public void StandardSelection_NoOptions_IsFlagged()
    {
        var group = Group("g", Selection("s", Array.Empty<ComboBoxOption>()));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("no ComboBox options");
    }

    // ── Selection shape: Subjective ──────────────────────────────────────────

    [Fact]
    public void SubjectiveSelection_ZeroFlags_Passes()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A"),
            Opt("B"),
        }, subjective: true));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    [Fact]
    public void SubjectiveSelection_OneOfEach_Passes()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true),
            Opt("B", def: true),
        }, subjective: true));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    [Fact]
    public void SubjectiveSelection_TwoRecommended_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true),
            Opt("B", rec: true),
        }, subjective: true));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("Subjective Selection has 2 IsRecommended");
    }

    [Fact]
    public void SubjectiveSelection_TwoDefault_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", def: true),
            Opt("B", def: true),
        }, subjective: true));
        SingleMessage(SettingCatalogValidator.Validate(group))
            .Should().Contain("Subjective Selection has 2 IsDefault");
    }

    // ── Selection shape: PowerCfg / Dynamic ──────────────────────────────────

    [Fact]
    public void PowerCfgSelection_WithOptionFlags_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true, def: true),
            Opt("B"),
        }, powerCfgBacked: true));

        var messages = SettingCatalogValidator.Validate(group).Select(v => v.Message).ToList();
        messages.Should().Contain(m => m.Contains("must not set ComboBoxOption.IsRecommended"));
        messages.Should().Contain(m => m.Contains("must not set ComboBoxOption.IsDefault"));
    }

    [Fact]
    public void PowerCfgSelection_WithoutOptionFlags_Passes()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A"),
            Opt("B"),
        }, powerCfgBacked: true));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    [Fact]
    public void DynamicSelection_IsSkipped()
    {
        var group = Group("g", Selection("s", Array.Empty<ComboBoxOption>(), dynamic: true));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    // ── Non-selection inputs are ignored by shape checks ─────────────────────

    [Fact]
    public void ToggleSettings_NeverShapeChecked()
    {
        var group = Group("g", Toggle("t1"), Toggle("t2"));
        SettingCatalogValidator.Validate(group).Should().BeEmpty();
    }

    // ── Id uniqueness ────────────────────────────────────────────────────────

    [Fact]
    public void DuplicateId_AcrossGroups_IsFlagged()
    {
        var groups = new[]
        {
            Group("g1", Toggle("dup")),
            Group("g2", Toggle("other")),
            Group("g3", Toggle("dup")),
        };
        var violations = SettingCatalogValidator.Validate((IEnumerable<SettingGroup>)groups);
        violations.Should().ContainSingle(v => v.SettingId == "dup")
            .Which.Message.Should().Contain("already defined in group 'g1'");
    }

    [Fact]
    public void DuplicateId_WithinSameGroup_IsFlagged()
    {
        var group = Group("g", Toggle("dup"), Toggle("dup"));
        var violations = SettingCatalogValidator.Validate(new[] { group });
        violations.Should().ContainSingle(v => v.SettingId == "dup")
            .Which.Message.Should().Contain("Duplicate setting id");
    }

    [Fact]
    public void UniqueIds_AcrossManyGroups_Pass()
    {
        var groups = new[]
        {
            Group("g1", Toggle("a"), Toggle("b")),
            Group("g2", Toggle("c")),
        };
        SettingCatalogValidator.Validate((IEnumerable<SettingGroup>)groups).Should().BeEmpty();
    }

    // ── Registry path well-formedness ────────────────────────────────────────

    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Test")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft")]
    [InlineData(@"HKEY_CLASSES_ROOT\.txt")]
    [InlineData(@"HKEY_USERS\S-1-5-18\Software")]
    [InlineData(@"HKEY_CURRENT_CONFIG\Display")]
    public void ValidRegistryPaths_Pass(string keyPath)
    {
        var s = new SettingDefinition
        {
            Id = "s",
            Name = "s",
            Description = "d",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = keyPath,
                    ValueName = "v",
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };
        SettingCatalogValidator.Validate(new[] { Group("g", s) }).Should().BeEmpty();
    }

    [Fact]
    public void EmptyRegistryKeyPath_IsFlagged()
    {
        var s = new SettingDefinition
        {
            Id = "s",
            Name = "s",
            Description = "d",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = "",
                    ValueName = "v",
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };
        SettingCatalogValidator.Validate(new[] { Group("g", s) })
            .Should().ContainSingle(v => v.Message.Contains("empty KeyPath"));
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\Test")]           // abbreviated hive not honoured by RegistrySetting
    [InlineData(@"SOFTWARE\Test")]                 // no hive at all
    [InlineData(@"HKEY_LOCAL_MACHINE\")]          // bare hive, empty subkey
    public void MalformedRegistryKeyPath_IsFlagged(string keyPath)
    {
        var s = new SettingDefinition
        {
            Id = "s",
            Name = "s",
            Description = "d",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = keyPath,
                    ValueName = "v",
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };
        SettingCatalogValidator.Validate(new[] { Group("g", s) })
            .Should().ContainSingle(v => v.Message.Contains("known hive"));
    }

    // ── Combo mapping ↔ declared value-name consistency ──────────────────────

    [Fact]
    public void ComboMapping_KeyMatchingDeclaredValueName_Passes()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true, mapKey: "s"),
            Opt("B", def: true, mapKey: "s"),
        }));
        SettingCatalogValidator.Validate(new[] { group }).Should().BeEmpty();
    }

    [Fact]
    public void ComboMapping_UnmatchedKey_IsFlagged()
    {
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true, mapKey: "WrongValueName"),
            Opt("B", def: true, mapKey: "s"),
        }));
        SettingCatalogValidator.Validate(new[] { group })
            .Should().ContainSingle(v => v.Message.Contains("'WrongValueName'"))
            .Which.Message.Should().Contain("no matching RegistrySetting.ValueName");
    }

    [Fact]
    public void ComboMapping_HandlerRoutedEmptyRegistryList_IsExempt()
    {
        // updates-policy-mode pattern: state lives behind ISpecialSettingHandler,
        // RegistrySettings deliberately empty — mapping keys cannot be cross-checked.
        var group = Group("g", Selection("s", new[]
        {
            Opt("A", rec: true, mapKey: "Anything"),
            Opt("B", def: true, mapKey: "Anything"),
        }, emptyRegistry: true));
        SettingCatalogValidator.Validate(new[] { group }).Should().BeEmpty();
    }

    [Fact]
    public void Violations_CarrySettingIdAndGroupName()
    {
        var group = Group("my-group", Selection("my-setting", Array.Empty<ComboBoxOption>()));
        var v = SettingCatalogValidator.Validate(new[] { group });
        v.Should().ContainSingle();
        v[0].SettingId.Should().Be("my-setting");
        v[0].GroupName.Should().Be("my-group");
    }

    // ── Live gate: every shipped catalog must validate clean ─────────────────

    public static IEnumerable<object[]> AllFeatureCatalogs()
    {
        yield return new object[] { "Taskbar", Tabs("Customize.Taskbar") };
        yield return new object[] { "StartMenu", Tabs("Customize.StartMenu") };
        yield return new object[] { "Explorer", Tabs("Customize.Explorer") };
        yield return new object[] { "Desktop", Tabs("Customize.Desktop") };
        yield return new object[] { "Appearance", Tabs("Customize.Appearance") };
        yield return new object[] { "Gaming", Tabs("Gaming") };
        yield return new object[] { "Power", Tabs("Power") };
        yield return new object[] { "Notifications", Tabs("Notifications") };
        yield return new object[] { "Privacy", Tabs("Privacy") };
        yield return new object[] { "Sound", Tabs("Sound") };
        yield return new object[] { "Update", Tabs("Update") };
    }

    private static IReadOnlyList<SettingGroup> Tabs(string key) => key switch
    {
        "Customize.Taskbar" => AkariTool.Tabs.Customize.TaskbarOptimizations.Build(),
        "Customize.StartMenu" => AkariTool.Tabs.Customize.StartMenuOptimizations.Build(),
        "Customize.Explorer" => AkariTool.Tabs.Customize.ExplorerOptimizations.Build(),
        "Customize.Desktop" => AkariTool.Tabs.Customize.DesktopOptimizations.Build(),
        "Customize.Appearance" => AkariTool.Tabs.Customize.AppearanceOptimizations.Build(),
        "Gaming" => AkariTool.Tabs.Gaming.GamingOptimizations.Build(),
        "Power" => AkariTool.Tabs.Power.PowerOptimizations.Build(),
        "Notifications" => AkariTool.Tabs.Notifications.NotificationsOptimizations.Build(),
        "Privacy" => AkariTool.Tabs.Privacy.PrivacyOptimizations.Build(),
        "Sound" => AkariTool.Tabs.Sound.SoundOptimizations.Build(),
        "Update" => AkariTool.Tabs.Update.UpdateOptimizations.Build(),
        _ => throw new InvalidOperationException($"Unknown catalog key '{key}'."),
    };

    [Theory]
    [MemberData(nameof(AllFeatureCatalogs))]
    public void ShippedCatalog_ValidatesClean(string catalogName, IReadOnlyList<SettingGroup> groups)
    {
        var violations = SettingCatalogValidator.Validate(groups);
        violations.Should().BeEmpty(
            because: $"catalog {catalogName} ships to users — every authored row must satisfy the catalog invariants");
    }
}
