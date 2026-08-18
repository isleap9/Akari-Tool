using System;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class ScheduledTaskService : IScheduledTaskService
{
    public Task<OperationResult> EnableTaskAsync(string taskPath) => throw new NotImplementedException();
    public Task<OperationResult> DisableTaskAsync(string taskPath) => throw new NotImplementedException();
}
