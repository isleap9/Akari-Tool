using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IAkariLogService
{
    void Log(LogLevel level, string message);
}
