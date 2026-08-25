using System;
using System.Text;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Writes the user-facing change receipt to %ProgramData%\AkariTool\ChangeHistory.txt.
/// Append-only, UTF-8 with BOM, CRLF. Never throws — a failed receipt write must
/// never block the actual operation. Winhance ChangeHistoryService 1:1 (Akari is
/// English-only, so localization lookups are dropped in favor of literal strings).
/// </summary>
public class ChangeHistoryService(
    IFileSystemService fileSystemService,
    IAkariLogService logService) : IChangeHistoryService
{
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly object _lock = new();
    private int _batchDepth;
    private string? _pendingBatchHeader;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AkariTool",
        "ChangeHistory.txt");

    public void LogSettingChange(string displayName, string? groupName, string before, string after) =>
        WriteEntry(FormatSettingLabel(displayName, groupName) + $": {before} → {after}");

    public void LogSettingAction(string displayName, string? groupName) =>
        WriteEntry(FormatSettingLabel(displayName, groupName));

    public void LogAppChange(string appDisplayName, AppChangeKind kind) =>
        WriteEntry($"{(kind == AppChangeKind.Installed ? "App installed" : "App removed")}: {appDisplayName}");

    public IDisposable BeginBatch(string header)
    {
        lock (_lock)
        {
            _batchDepth++;
            if (_batchDepth == 1)
                _pendingBatchHeader = header;
        }
        return new BatchScope(this);
    }

    public string GetFilePath()
    {
        lock (_lock)
        {
            try
            {
                EnsureFileExistsNoLock();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ChangeHistory] Failed to create history file: {ex.Message}");
            }
        }
        return FilePath;
    }

    private static string FormatSettingLabel(string displayName, string? groupName) =>
        string.IsNullOrEmpty(groupName) ? displayName : $"{groupName} — {displayName}";

    private void WriteEntry(string line)
    {
        lock (_lock)
        {
            try
            {
                EnsureFileExistsNoLock();

                var sb = new StringBuilder();
                if (_pendingBatchHeader != null)
                {
                    sb.Append($"[{Timestamp()}] {_pendingBatchHeader}:\r\n");
                    _pendingBatchHeader = null;
                }
                var indent = _batchDepth > 0 ? "    " : string.Empty;
                sb.Append($"{indent}[{Timestamp()}] {line}\r\n");

                File.AppendAllText(FilePath, sb.ToString(), Utf8Bom);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ChangeHistory] Failed to write entry: {ex.Message}");
            }
        }
    }

    private void EnsureFileExistsNoLock()
    {
        if (File.Exists(FilePath))
            return;

        var directory = Path.GetDirectoryName(FilePath)!;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.AppendAllText(FilePath,
            "Changes made by Akari Tool are listed below (newest at the bottom).\r\n\r\n",
            Utf8Bom);
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    private sealed class BatchScope(ChangeHistoryService owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            lock (owner._lock)
            {
                owner._batchDepth--;
                if (owner._batchDepth <= 0)
                {
                    owner._batchDepth = 0;
                    owner._pendingBatchHeader = null;
                }
            }
        }
    }
}
