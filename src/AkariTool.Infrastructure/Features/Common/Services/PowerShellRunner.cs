using System;
using System.Threading.Tasks;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class PowerShellRunner : IPowerShellRunner
{
    public Task RunScriptInMemoryAsync(string script) => throw new NotImplementedException();
}
