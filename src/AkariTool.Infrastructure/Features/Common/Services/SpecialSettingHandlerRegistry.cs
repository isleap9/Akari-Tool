using System.Collections.Generic;
using AkariTool.Core.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class SpecialSettingHandlerRegistry(IReadOnlyDictionary<string, ISpecialSettingHandler> handlers)
    : ISpecialSettingHandlerRegistry
{
    public ISpecialSettingHandler? TryGet(string settingId)
        => handlers.TryGetValue(settingId, out var h) ? h : null;
}
