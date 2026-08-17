using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // ENVIRONMENT VARIABLES (telemetry opt-outs)
        // ═════════════════════════════════════════════════════════════════════

        private static readonly (string Name, string Value)[] EnvVars =
        {
            ("DOTNET_CLI_TELEMETRY_OPTOUT",     "1"),
            ("DOTNET_TRY_CLI_TELEMETRY_OPTOUT", "1"),
            ("DOCKER_CLI_TELEMETRY_OPTOUT",     "1"),
            ("VS_TELEMETRY_OPT_OUT",            "1"),
        };

        private static async Task ApplyEnvironmentVarsAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Setting telemetry opt-out environment variables...");
            foreach (var (name, value) in EnvVars)
            {
                await RunCommandAsync("setx", $"{name} {value}", log);
            }
            log.Log("[PLAYBOOK] Environment variables set.");
        }

        private static async Task UndoEnvironmentVarsAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Removing telemetry environment variables...");
            foreach (var (name, _) in EnvVars)
            {
                // setx with empty string removes the variable
                await RunCommandAsync("reg", $@"delete ""HKCU\Environment"" /v {name} /f", log);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // FSUTIL TWEAKS
        // ═════════════════════════════════════════════════════════════════════

        private static async Task ApplyFsutilAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Applying filesystem tweaks...");
            await RunCommandAsync("fsutil", "behavior set disablelastaccess 1", log);
            await RunCommandAsync("fsutil", "behavior set disable8dot3 1", log);
            log.Log("[PLAYBOOK] Filesystem tweaks applied.");
        }

        private static async Task UndoFsutilAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Reverting filesystem tweaks...");
            await RunCommandAsync("fsutil", "behavior set disablelastaccess 0", log);
            await RunCommandAsync("fsutil", "behavior set disable8dot3 0", log);
        }

    }
}
