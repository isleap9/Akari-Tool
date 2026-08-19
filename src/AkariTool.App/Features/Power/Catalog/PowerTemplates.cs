using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Tabs.Power;

// Flags intentionally omitted on all templates below — every PowerTemplates consumer in
// PowerOptimizations.cs is a PowerCfg-backed Selection whose Recommended/Default state lives on
// PowerRecommendation (per-mode AC/DC) + PowerCfgSetting.RecommendedValueAC/DC / DefaultValueAC/DC,
// not on ComboBoxOption.IsRecommended / IsDefault. Single-flag options can't encode distinct
// AC/DC recommendations.
// Ported 1:1 from Winhance PowerTemplates.cs (DisplayName literals resolved from en.json).
public static class PowerTemplates
{
    public static readonly ComboBoxMetadata TimeIntervals = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Never",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "1 minute",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 60 },
            },
            new ComboBoxOption
            {
                DisplayName = "2 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 120 },
            },
            new ComboBoxOption
            {
                DisplayName = "3 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 180 },
            },
            new ComboBoxOption
            {
                DisplayName = "5 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 300 },
            },
            new ComboBoxOption
            {
                DisplayName = "10 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 600 },
            },
            new ComboBoxOption
            {
                DisplayName = "15 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 900 },
            },
            new ComboBoxOption
            {
                DisplayName = "20 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1200 },
            },
            new ComboBoxOption
            {
                DisplayName = "25 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1500 },
            },
            new ComboBoxOption
            {
                DisplayName = "30 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1800 },
            },
            new ComboBoxOption
            {
                DisplayName = "45 minutes",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2700 },
            },
            new ComboBoxOption
            {
                DisplayName = "1 hour",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3600 },
            },
            new ComboBoxOption
            {
                DisplayName = "2 hours",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 7200 },
            },
            new ComboBoxOption
            {
                DisplayName = "3 hours",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 10800 },
            },
            new ComboBoxOption
            {
                DisplayName = "4 hours",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 14400 },
            },
            new ComboBoxOption
            {
                DisplayName = "5 hours",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 18000 },
            },
        },
    };

    public static readonly ComboBoxMetadata OnOff = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Off",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "On",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata EnabledDisabled = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Disabled",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Enabled",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata WakeTimers = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Disable",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Enable",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Important Wake Timers Only",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata PowerButtonActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Do nothing",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Sleep",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Hibernate",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Shut down",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
            new ComboBoxOption
            {
                DisplayName = "Turn off the display",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 },
            },
        },
    };

    public static readonly ComboBoxMetadata LidActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Do nothing",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Sleep",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Hibernate",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Shut down",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata CoolingPolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Passive",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Active",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata BatteryActions = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Do nothing",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Sleep",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Hibernate",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Shut down",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata WirelessPower = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Maximum Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Low Power Saving",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Medium Power Saving",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum Power Saving",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata Slideshow = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Available",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Paused",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata PciExpress = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Off",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Moderate power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata Usb3LinkPower = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Off",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Minimum power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Moderate power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata MediaSharing = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Allow the computer to sleep",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Prevent idling to sleep",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata VideoQualityBias = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Video playback power-saving bias",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Video playback performance bias",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata VideoPlayback = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Optimize video quality",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Balanced",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Optimize power savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata AmdPowerSlider = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Battery Saver",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Better Battery",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Better Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Best Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata JavaScriptTimers = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Maximum Power Savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
        },
    };

    public static readonly ComboBoxMetadata IntelGraphics = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Maximum Battery Life",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Balanced",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata AtiPowerPlay = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Maximum Battery Life",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Balanced",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximum Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata SwitchableGraphics = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Maximize Battery Life",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Optimize Power Savings",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Maximize Performance",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static readonly ComboBoxMetadata ProcessorBoostMode = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Disabled",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Enabled",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Aggressive",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "Efficient Enabled",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
            new ComboBoxOption
            {
                DisplayName = "Efficient Aggressive",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 4 },
            },
            new ComboBoxOption
            {
                DisplayName = "Aggressive At Guaranteed",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 5 },
            },
            new ComboBoxOption
            {
                DisplayName = "Efficient Aggressive At Guaranteed",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 6 },
            },
        },
    };

    public static readonly ComboBoxMetadata PerformanceIncreasePolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Ideal",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Single",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Rocket",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
            new ComboBoxOption
            {
                DisplayName = "IdealAggressive",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 3 },
            },
        },
    };

    public static readonly ComboBoxMetadata PerformanceDecreasePolicy = new()
    {
        Options = new[]
        {
            new ComboBoxOption
            {
                DisplayName = "Ideal",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 },
            },
            new ComboBoxOption
            {
                DisplayName = "Single",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 },
            },
            new ComboBoxOption
            {
                DisplayName = "Rocket",
                ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 },
            },
        },
    };

    public static NumericRangeMetadata CreateNumericRange(int minValue, int maxValue, string units)
    {
        return new NumericRangeMetadata
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Increment = 1,
            Units = units
        };
    }
}