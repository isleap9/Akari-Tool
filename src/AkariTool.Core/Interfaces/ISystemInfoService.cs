using AkariTool.Core.Models;

namespace AkariTool.Core.Interfaces
{
    public interface ISystemInfoService
    {
        SystemInfo Gather();

        string GetEdition();
        string GetVersion();
        string GetCpu();
        string GetGpu();
        string GetMemory();

        string? GetRegValue(string subKey, string valueName);
        string GetWmiValue(string wmiClass, string property, string? where = null);
        List<string> GetWmiValues(string wmiClass, string property, string? where = null);
    }
}
