using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IPowerCfgApplier
{
    Task ApplyPowerCfgSettingsAsync(SettingDefinition setting, bool enable, object? value);
}
