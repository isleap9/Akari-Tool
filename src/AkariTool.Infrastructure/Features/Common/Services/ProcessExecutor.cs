using System;
using System.Threading.Tasks;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class ProcessExecutor : IProcessExecutor
{
    public Task<ProcessResult> ExecuteAsync(string executable, string arguments) => throw new NotImplementedException();
}
