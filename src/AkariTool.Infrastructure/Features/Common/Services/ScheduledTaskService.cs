using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class ScheduledTaskService : IScheduledTaskService
{
    private readonly IAkariLogService _log;

    public ScheduledTaskService(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<OperationResult> EnableTaskAsync(string taskPath)
        => await Task.Run(() => SetTaskEnabled(taskPath, true)).ConfigureAwait(false);

    public async Task<OperationResult> DisableTaskAsync(string taskPath)
        => await Task.Run(() => SetTaskEnabled(taskPath, false)).ConfigureAwait(false);

    private OperationResult SetTaskEnabled(string taskPath, bool enabled)
    {
        dynamic? taskService = null;
        dynamic? folder = null;
        dynamic? task = null;
        try
        {
            taskService = CreateTaskService();
            var (folderPath, taskName) = SplitTaskPath(taskPath);
            folder = taskService.GetFolder(folderPath);
            task = folder.GetTask(taskName);
            task.Enabled = enabled;
            _log.Log(LogLevel.Info, $"{(enabled ? "Enabled" : "Disabled")} scheduled task: {taskPath}");
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warning, $"Failed to {(enabled ? "enable" : "disable")} task '{taskPath}': {ex.Message}");
            return OperationResult.Failed(ex.Message, ex);
        }
        finally
        {
            ReleaseComObject(task);
            ReleaseComObject(folder);
            ReleaseComObject(taskService);
        }
    }

    private static dynamic CreateTaskService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")!;
        dynamic svc = Activator.CreateInstance(type)!;
        svc.Connect();
        return svc;
    }

    private static (string FolderPath, string TaskName) SplitTaskPath(string taskPath)
    {
        var last = taskPath.LastIndexOf('\\');
        if (last <= 0)
            return ("\\", taskPath.TrimStart('\\'));
        return (taskPath[..last], taskPath[(last + 1)..]);
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject != null)
            try { Marshal.ReleaseComObject(comObject); } catch { }
    }
}
