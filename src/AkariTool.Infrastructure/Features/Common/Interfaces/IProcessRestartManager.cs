using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);
}
