using System.Collections.Generic;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance ISpecialDiscoveryRegistry 1:1: lets SystemSettingsDiscoveryService iterate
/// every special handler that implements DiscoverSpecialSettingsAsync, so each can
/// declare which raw values it wants injected into the discovery results.
/// </summary>
public interface ISpecialDiscoveryRegistry
{
    /// <summary>Every registered discovery-capable handler, in registration order.</summary>
    IEnumerable<ISpecialSettingHandler> All { get; }
}
