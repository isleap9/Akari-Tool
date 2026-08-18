using System;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class ProcessRestartManager : IProcessRestartManager
{
    public Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting) => throw new NotImplementedException();
}
