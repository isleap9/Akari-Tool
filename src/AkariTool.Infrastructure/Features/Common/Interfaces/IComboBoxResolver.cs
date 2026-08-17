using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IComboBoxResolver
{
    int GetIndexFromDisplayName(SettingDefinition setting, string displayName);
    Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index);
    int GetValueFromIndex(SettingDefinition setting, int index);
}
