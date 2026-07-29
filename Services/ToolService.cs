using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// Shared service that handles script execution, process management, logging,
    /// and progress bar state. Each tab gets a reference to the single instance
    /// created in MainWindow, so they all share the same log and progress bar.
    /// </summary>
    public class ToolService
    {
        private readonly TextBox _log;
        private readonly ProgressBar _progress;
        private readonly TextBlock _progressStatus;

        private int _activeProcessCount;

        private static readonly Assembly AppAssembly = typeof(ToolService).Assembly;

        /// <summary>
        /// The single ToolService instance created in MainWindow. Lets static tweak
        /// catalogs (which only receive an Action&lt;string&gt; Log) reach the full service
        /// when a row needs it — e.g. the Defender row calling DefenderService.SetAsync.
        /// </summary>
        public static ToolService? Current { get; private set; }

        public ToolService(TextBox log, ProgressBar progress, TextBlock progressStatus)
        {
            _log = log;
            _progress = progress;
            _progressStatus = progressStatus;
            Current = this;
        }

        // ── Logging ────────────────────────────────────────────────────────────

        public void Log(string message) =>
            _log.Dispatcher.Invoke(() =>
            {
                _log.AppendText(message + Environment.NewLine);
                _log.ScrollToEnd();
            });

        // ── Progress bar ───────────────────────────────────────────────────────

        public void StartProgress(string processName)
        {
            _progress.Dispatcher.Invoke(() =>
            {
                _activeProcessCount++;
                _progressStatus.Text = $"Running {processName}...";
                _progressStatus.Visibility = Visibility.Visible;
                _progress.Value = 0;
                _progress.IsIndeterminate = false;
                _progress.Visibility = Visibility.Visible;
            });
        }

        public void StopProgress()
        {
            _progress.Dispatcher.Invoke(() =>
            {
                _activeProcessCount = Math.Max(0, _activeProcessCount - 1);
                if (_activeProcessCount > 0) return;

                _progress.Value = 0;
                _progress.Visibility = Visibility.Collapsed;
                _progressStatus.Visibility = Visibility.Collapsed;
            });
        }

        // ── Action routing ─────────────────────────────────────────────────────

        public async Task RunAction(RunAction action)
        {
            switch (action)
            {
                case ScriptAction script:
                    await RunScript(script.FileName);
                    break;
                case CommandAction command:
                    await RunPackageCommand(command.Command, command.AppName);
                    break;
                case UrlAction url:
                    OpenUrl(url.Url);
                    break;
            }
        }

        public async Task RunWithTracking(RunAction action, string tweakTitle, List<string> appliedTweaks)
        {
            if (action is ScriptAction)
            {
                appliedTweaks.Add(tweakTitle);
                Log($"[APPLIED] {tweakTitle}");
            }
            await RunAction(action);
        }

        // ── Script execution ───────────────────────────────────────────────────

        public async Task RunScript(string scriptName)
        {
            var resourceName = FindEmbeddedScriptResource(scriptName);
            if (resourceName is null)
            {
                Log($"[ERROR] Embedded script not found: {scriptName}");
                return;
            }

            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"AkariTool-{Guid.NewGuid():N}-{scriptName}");

            try
            {
                await ExtractEmbeddedScript(resourceName, tempScriptPath);
                Log($"[RUN] {scriptName}");
                await RunProcess("powershell", $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"", timeoutMilliseconds: null);
                Log("[DONE]");
            }
            finally
            {
                TryDeleteTempScript(tempScriptPath);
            }
        }

        // ── Package manager install (WinGet or Chocolatey) ────────────────────

        public async Task RunPackageCommand(string command, string? appName)
        {
            Log($"[INSTALLING] {appName ?? "Application"}...");
            var exitCode = await RunProcess("powershell", $"-NoProfile -Command \"{command}\"", timeoutMilliseconds: 600_000);

            if (exitCode == 0 && appName is not null)
                CreateDesktopShortcut(appName);

            Log(exitCode == 0 ? "[DONE]" : $"[ERROR] Process exited with code {exitCode}");
        }

        // ── Process runner ─────────────────────────────────────────────────────

        public async Task<int> RunProcess(string fileName, string arguments, int? timeoutMilliseconds)
        {
            StartProgress(fileName);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) Log(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[ERROR] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var waitTask = process.WaitForExitAsync();

                if (timeoutMilliseconds is null)
                {
                    await waitTask;
                }
                else if (await Task.WhenAny(waitTask, Task.Delay(timeoutMilliseconds.Value)) != waitTask)
                {
                    process.Kill(entireProcessTree: true);
                    Log("[TIMEOUT] Installation took too long");
                    return -1;
                }

                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Log($"[EXCEPTION] {ex.Message}");
                return -1;
            }
            finally
            {
                StopProgress();
            }
        }

        // ── URL opener ─────────────────────────────────────────────────────────

        public void OpenUrl(string url)
        {
            try
            {
                Log($"[OPENING] {url}");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log($"[EXCEPTION] {ex.Message}");
            }
        }

        // ── Desktop shortcut ───────────────────────────────────────────────────

        public void CreateDesktopShortcut(string appName)
        {
            var exePaths = new Dictionary<string, string>
            {
                ["Chrome"]             = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ["Firefox"]            = @"C:\Program Files\Mozilla Firefox\firefox.exe",
                ["LibreWolf"]          = @"C:\Program Files\LibreWolf\librewolf.exe",
                ["Ungoogled Chromium"] = @"C:\Program Files\Chromium\Application\chrome.exe",
                ["Notepad++"]          = @"C:\Program Files\Notepad++\notepad++.exe",
                ["VSCode"]             = @"C:\Program Files\Microsoft VS Code\Code.exe"
            };

            if (!exePaths.TryGetValue(appName, out var exePath))
            {
                Log($"[WARNING] Unknown app name: {appName}");
                return;
            }
            if (!File.Exists(exePath))
            {
                Log($"[WARNING] Executable not found: {exePath}");
                return;
            }

            var desktopPath      = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath     = Path.Combine(desktopPath, $"{appName}.lnk");
            var workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;

            var script = "$WshShell = New-Object -ComObject WScript.Shell; " +
                         $"$Shortcut = $WshShell.CreateShortcut('{EscapePsString(shortcutPath)}'); " +
                         $"$Shortcut.TargetPath = '{EscapePsString(exePath)}'; " +
                         $"$Shortcut.WorkingDirectory = '{EscapePsString(workingDirectory)}'; " +
                         "$Shortcut.Save()";

            Log($"[SHORTCUT] Creating desktop shortcut for {appName}...");
            _ = Task.Run(async () =>
            {
                var exitCode = await RunProcess("powershell", $"-NoProfile -Command \"{script}\"", timeoutMilliseconds: null);
                Log(exitCode == 0
                    ? $"[SUCCESS] Desktop shortcut created: {shortcutPath}"
                    : "[ERROR] Failed to create shortcut");
            });
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        public static Brush BrushFrom(string color) =>
            (Brush)new BrushConverter().ConvertFromString(color)!;

        private static string? FindEmbeddedScriptResource(string scriptName)
        {
            var expectedEnding = $".Scripts.{scriptName}";
            return AppAssembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(expectedEnding, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task ExtractEmbeddedScript(string resourceName, string destinationPath)
        {
            await using var resourceStream = AppAssembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");

            await using var fileStream = File.Create(destinationPath);
            await resourceStream.CopyToAsync(fileStream);
        }

        private void TryDeleteTempScript(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Log($"[WARNING] Could not delete temporary script: {ex.Message}");
            }
        }

        private static string EscapePsString(string value) =>
            value.Replace("'", "''");
    }
}
