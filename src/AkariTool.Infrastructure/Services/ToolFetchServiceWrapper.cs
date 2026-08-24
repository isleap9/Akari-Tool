using AkariTool.Core.Interfaces;
using AkariTool.Services;

namespace AkariTool.Infrastructure.Services
{
    public sealed class ToolFetchServiceWrapper : IToolFetchService
    {
        public Task LaunchAsync(string key, IToolService log) => ToolFetchService.LaunchAsync(key, log);

        public void ClearCache(Action<string> log) => ToolFetchService.ClearCache(log);
    }
}
