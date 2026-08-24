using System.Threading.Tasks;
using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

public interface IHardwareCompatibilityFilter
{
    Task<IEnumerable<SettingDefinition>> FilterSettingsByHardwareAsync(IEnumerable<SettingDefinition> settings);
}
