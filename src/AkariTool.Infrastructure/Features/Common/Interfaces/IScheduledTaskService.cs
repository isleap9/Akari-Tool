using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IScheduledTaskService
{
    Task<OperationResult> EnableTaskAsync(string taskPath);
    Task<OperationResult> DisableTaskAsync(string taskPath);
}
