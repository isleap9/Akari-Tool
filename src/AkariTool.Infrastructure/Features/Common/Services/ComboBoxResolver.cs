using System;
using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class ComboBoxResolver : IComboBoxResolver
{
    public int GetIndexFromDisplayName(SettingDefinition setting, string displayName) => throw new NotImplementedException();
    public Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index) => throw new NotImplementedException();
    public int GetValueFromIndex(SettingDefinition setting, int index) => throw new NotImplementedException();
}
