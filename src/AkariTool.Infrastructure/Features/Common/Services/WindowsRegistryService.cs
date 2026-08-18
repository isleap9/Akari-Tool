using System;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class WindowsRegistryService : IWindowsRegistryService
{
    public bool ApplySetting(RegistrySetting setting, bool enable) => throw new NotImplementedException();
    public bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue) => throw new NotImplementedException();
    public bool ApplySetting(RegistrySetting setting, bool enable, bool useDefaultValue) => throw new NotImplementedException();
}
