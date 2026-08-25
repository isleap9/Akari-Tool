using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);

    /// <summary>
    /// Suppresses all process/service restarts until the returned scope is disposed.
    /// Used by the dependency resolver when auto-enabling multiple children,
    /// so that a single restart from the parent covers all of them.
    /// </summary>
    IDisposable SuppressRestarts();

    /// <summary>
    /// Winhance parity: after a suppressed bulk apply, restarts once per distinct
    /// process/service that ANY applied setting required. Callers dispose their
    /// SuppressRestarts scope first, then flush with the applied settings.
    /// </summary>
    Task FlushCoalescedRestartsAsync(System.Collections.Generic.IReadOnlyCollection<SettingDefinition> appliedSettings);
}
