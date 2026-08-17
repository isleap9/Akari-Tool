using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IWindowsRegistryService
{
    bool ApplySetting(RegistrySetting setting, bool enable);
    bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue);
    bool ApplySetting(RegistrySetting setting, bool enable, bool useDefaultValue);
}
