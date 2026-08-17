using System.Threading.Tasks;
using AkariTool.Infrastructure.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IProcessExecutor
{
    Task<ProcessResult> ExecuteAsync(string executable, string arguments);
}
