using AkariTool.Core.Interfaces;
using AkariTool.Core.Models;
using AkariTool.Services;

namespace AkariTool.Infrastructure.Services
{
    public sealed class SystemInfoServiceWrapper : ISystemInfoService
    {
        public SystemInfo Gather() => SystemInfoService.Gather();

        public string GetEdition() => SystemInfoService.GetEdition();
        public string GetVersion() => SystemInfoService.GetVersion();
        public string GetCpu() => SystemInfoService.GetCpu();
        public string GetGpu() => SystemInfoService.GetGpu();
        public string GetMemory() => SystemInfoService.GetMemory();

        public string? GetRegValue(string subKey, string valueName)
            => SystemInfoService.GetRegValue(subKey, valueName);

        public string GetWmiValue(string wmiClass, string property, string? where = null)
            => SystemInfoService.GetWmiValue(wmiClass, property, where);

        public List<string> GetWmiValues(string wmiClass, string property, string? where = null)
            => SystemInfoService.GetWmiValues(wmiClass, property, where);
    }
}
