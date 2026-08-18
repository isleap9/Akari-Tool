using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

public class ComboBoxResolver : IComboBoxResolver
{
    private static Dictionary<int, Dictionary<string, object?>>? ValueMappingsView(ComboBoxMetadata? meta)
    {
        if (meta?.Options is null) return null;
        var dict = new Dictionary<int, Dictionary<string, object?>>();
        for (int i = 0; i < meta.Options.Count; i++)
        {
            if (meta.Options[i].ValueMappings is { } vm)
                dict[i] = vm;
        }
        return dict.Count == 0 ? null : dict;
    }

    private static string[]? DisplayNamesView(ComboBoxMetadata? meta)
        => meta?.Options?.Select(o => o.DisplayName).ToArray();

    public int GetValueFromIndex(SettingDefinition setting, int index)
    {
        if (index == AkariTool.Core.Features.Common.Constants.ComboBoxConstants.CustomStateIndex)
        {
            return 0;
        }

        if (setting.ComboBox?.Options == null)
        {
            return index;
        }

        var mappings = ValueMappingsView(setting.ComboBox);
        if (mappings != null && mappings.TryGetValue(index, out var valueDict))
        {
            var firstValue = valueDict.Values.FirstOrDefault();
            return firstValue is int intVal ? intVal : (firstValue != null ? Convert.ToInt32(firstValue) : index);
        }

        return index;
    }

    public Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index)
    {
        var result = new Dictionary<string, object?>();

        if (setting.ComboBox?.Options == null)
        {
            return result;
        }

        var mappings = ValueMappingsView(setting.ComboBox);
        if (mappings != null && mappings.TryGetValue(index, out var expectedValues))
        {
            foreach (var expectedValue in expectedValues)
            {
                result[expectedValue.Key] = expectedValue.Value;
            }
        }

        return result;
    }

    public int GetIndexFromDisplayName(SettingDefinition setting, string displayName)
    {
        if (DisplayNamesView(setting.ComboBox) is { } displayNames)
        {
            for (int i = 0; i < displayNames.Length; i++)
            {
                if (string.Equals(displayNames[i], displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return 0;
    }
}
