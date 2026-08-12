using WinUI.Framework.Services;

namespace AkariTool.Services;

/// <summary>
/// <see cref="ILogService"/> decorator that forwards every log call to an inner
/// implementation and raises <see cref="LineLogged"/> with a console-ready line so
/// the shell's log dock can render live output (the framework's FileLogService
/// only writes to disk — it has no UI event).
/// </summary>
public sealed class AkariUiLogService : ILogService
{
    private readonly ILogService _inner;

    /// <summary>Raised (on whatever thread logged) with a formatted log line.</summary>
    public event Action<string>? LineLogged;

    public AkariUiLogService(ILogService inner)
    {
        _inner = inner;
    }

    public LogLevel MinimumLevel
    {
        get => _inner.MinimumLevel;
        set => _inner.MinimumLevel = value;
    }

    public string LogDirectory => _inner.LogDirectory;

    public string LogFilePath => _inner.LogFilePath;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _inner.Log(level, message, exception);
        var line = $"{DateTime.Now:HH:mm:ss} [{level.ToString().ToUpperInvariant(),-7}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        LineLogged?.Invoke(line);
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);

    public void Info(string message) => Log(LogLevel.Info, message);

    public void Warning(string message) => Log(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
}
