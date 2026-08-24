using System.Collections.Generic;
using AkariTool.Core.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Winhance SpecialDiscoveryRegistry 1:1: simple iteration surface over the
/// discovery-capable special handlers, populated at DI registration time.
/// </summary>
public sealed class SpecialDiscoveryRegistry(IReadOnlyList<ISpecialSettingHandler> handlers)
    : ISpecialDiscoveryRegistry
{
    public IEnumerable<ISpecialSettingHandler> All => handlers;
}
