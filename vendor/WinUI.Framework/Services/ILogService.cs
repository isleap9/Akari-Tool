namespace WinUI.Framework.Services;

/// <summary>Log severity levels, ordered from least to most severe.</summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// Minimal logging abstraction. Swap the implementation with your favorite
/// provider (Serilog, Microsoft.Extensions.Logging, ...) behind this interface.
/// </summary>
public interface ILogService
{
    /// <summary>Messages below this level are dropped.</summary>
    LogLevel MinimumLevel { get; set; }

    /// <summary>Folder where log files are written.</summary>
    string LogDirectory { get; }

    /// <summary>Path of the active log file (rotates daily).</summary>
    string LogFilePath { get; }

    /// <summary>Writes a message with an explicit level and optional exception.</summary>
    void Log(LogLevel level, string message, Exception? exception = null);

    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}
