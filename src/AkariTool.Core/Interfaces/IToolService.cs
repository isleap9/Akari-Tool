namespace AkariTool.Core.Interfaces
{
    /// <summary>
    /// Minimal Core-visible surface of the concrete ToolService (which lives in the
    /// main project). Only the members other Core abstractions actually need are
    /// exposed here — currently just logging, used by IDefenderService /
    /// IToolFetchService so their signatures don't depend on the concrete class.
    /// </summary>
    public interface IToolService
    {
        void Log(string message);
    }
}
