namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="ILogService"/> that appends timestamped lines to a file,
/// one file per day, under <c>%LOCALAPPDATA%\&lt;AppName&gt;\logs</c>.
/// Thread-safe via a lock; failures are swallowed so logging never crashes the app.
/// </summary>
public class FileLogService : ILogService
{
    private static readonly object SyncLock = new();

    private readonly string _logDirectory;

    public FileLogService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            GetAppName(),
            "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// The app name from the entry assembly; falls back to the output folder name
    /// (which can differ from the app name in standalone publishes).
    /// </summary>
    private static string GetAppName()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrEmpty(entry))
        {
            return entry;
        }

        return Path.GetFileNameWithoutExtension(
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
    }

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public string LogDirectory => _logDirectory;

    public string LogFilePath => Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");

    public void Debug(string message) => Log(LogLevel.Debug, message);

    public void Info(string message) => Log(LogLevel.Info, message);

    public void Warning(string message) => Log(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-7}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        lock (SyncLock)
        {
            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Never let logging take the app down.
            }
        }
    }
}
