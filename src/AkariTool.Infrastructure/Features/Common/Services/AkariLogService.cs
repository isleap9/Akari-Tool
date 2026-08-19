using System;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Log sink for the declarative SettingDefinition apply path.
/// Delegates to the Action<string> provided at construction — wired in
/// UIServiceExtensions to ToolService.Log so messages reach the UI log panel.
/// </summary>
public sealed class AkariLogService : IAkariLogService
{
    private readonly Action<string> _log;

    public AkariLogService(Action<string> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Log(LogLevel level, string message)
    {
        var prefix = level switch
        {
            LogLevel.Warning => "[WARN] ",
            LogLevel.Error   => "[ERROR] ",
            _                => ""
        };
        _log(prefix + message);
    }
}
