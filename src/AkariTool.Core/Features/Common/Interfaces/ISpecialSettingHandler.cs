using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

public interface ISpecialSettingHandler
{
    Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object? value);

    Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(
        IEnumerable<SettingDefinition> settings)
    {
        return Task.FromResult(new Dictionary<string, Dictionary<string, object?>>());
    }
}
