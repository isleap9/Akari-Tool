namespace AkariTool.Core.Interfaces
{
    public interface IToolFetchService
    {
        Task LaunchAsync(string key, IToolService log);

        void ClearCache(Action<string> log);
    }
}
