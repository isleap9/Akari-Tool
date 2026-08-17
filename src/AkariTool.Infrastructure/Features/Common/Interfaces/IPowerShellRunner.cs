using System.Threading.Tasks;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IPowerShellRunner
{
    Task RunScriptInMemoryAsync(string script);
}
