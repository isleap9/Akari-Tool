using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    public partial class AutounattendService
    {
        // ── User pass sections ─────────────────────────────────────────────

        private static void AppendUserDetectionBridge(StringBuilder sb)
        {
            sb.AppendLine(@"    $runningAsSystem = ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -eq 'S-1-5-18')

    if ($runningAsSystem) {
        # ================================================================
        # SYSTEM path: detect user, check marker, launch child as user
        # ================================================================
        Write-Log ""UserCustomizations running as SYSTEM, detecting logged-in user..."" ""INFO""

        if (-not (Test-Path ""HKU:\"")) {
            New-PSDrive -PSProvider Registry -Name HKU -Root HKEY_USERS -ErrorAction SilentlyContinue | Out-Null
        }

        $targetUser = $null
        for ($attempt = 1; $attempt -le 12; $attempt++) {
            $targetUser = Get-TargetUser
            if ($targetUser) { break }
            Write-Log ""Waiting for user login (attempt $attempt/12)..."" ""INFO""
            Start-Sleep -Seconds 10
        }
        if (-not $targetUser) {
            Write-Log ""No logged-in user detected after 2 minutes, will retry at next logon"" ""WARNING""
            exit 1
        }

        $targetUserSID = Get-UserSID -Username $targetUser
        if (-not $targetUserSID) {
            Write-Log ""Failed to get SID for user: $targetUser"" ""ERROR""
            exit 1
        }

        Write-Log ""Target user: $targetUser (SID: $targetUserSID)"" ""INFO""

        $markerPath = ""HKU:\$targetUserSID\" + MarkerKey + @"""
        $markerName = ""UserCustomizationsApplied""
        $alreadyApplied = $false

        try {
            if (Test-Path $markerPath) {
                $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue
                if ($value.$markerName -eq 1) { $alreadyApplied = $true }
            }
        } catch { }

        if ($alreadyApplied) {
            Write-Log ""User customizations have already been applied for this user"" ""INFO""
        } else {
            Write-Log ""Launching child process as interactive user to apply customizations..."" ""INFO""
            icacls $LogPath /grant ""${targetUser}:(M)"" 2>&1 | Out-Null
            $scriptPath = $MyInvocation.MyCommand.Path
            $cmdLine = ""powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File `""$scriptPath`"" -UserCustomizations""
            $success = Start-ProcessAsUser -CommandLine $cmdLine

            if ($success) {
                Write-Log ""Child process completed successfully"" ""SUCCESS""
                Write-Log ""Rebooting system to apply user customizations..."" ""INFO""
                shutdown.exe /r /t 20
            } else {
                Write-Log ""Child process failed or timed out - will retry at next logon"" ""ERROR""
                exit 1
            }
        }
    } else {
        # ================================================================
        # User path: apply per-user tweaks (natural HKCU resolution)
        # ================================================================
        Write-Log ""UserCustomizations running as user"" ""INFO""

        $markerPath = ""HKCU:\" + MarkerKey + @"""
        $markerName = ""UserCustomizationsApplied""
        $alreadyApplied = $false

        try {
            if (Test-Path $markerPath) {
                $value = Get-ItemProperty -Path $markerPath -Name $markerName -ErrorAction SilentlyContinue
                if ($value.$markerName -eq 1) { $alreadyApplied = $true }
            }
        } catch { }

        if ($alreadyApplied) {
            Write-Log ""User customizations have already been applied for this user"" ""INFO""
        } else {
            Write-Log ""Applying user customizations for the first time..."" ""INFO""
");
        }

        private static void AppendUserTweaksSection(StringBuilder sb, IReadOnlyList<UnattendTweakOption> userTweaks)
        {
            var scripted = userTweaks.Where(t => t.ScriptName != null).ToList();
            if (scripted.Count == 0) return;

            sb.AppendLine("            $scriptsDir = \"" + ScriptsDir + "\"");
            foreach (var t in scripted)
                sb.AppendLine($"            Invoke-TweakScript -Path \"$scriptsDir\\{t.ScriptName}\" -Name \"{t.Name}\"");
        }

        private static void AppendUserDetectionBridgeClosing(StringBuilder sb)
        {
            sb.AppendLine(@"
            try {
                if (-not (Test-Path $markerPath)) {
                    New-Item -Path $markerPath -Force | Out-Null
                }
                Set-ItemProperty -Path $markerPath -Name $markerName -Value 1 -Type DWord -Force
                Write-Log ""User customizations completed and marked as applied"" ""SUCCESS""
            } catch {
                Write-Log ""Failed to create completion marker: $($_.Exception.Message)"" ""WARNING""
            }
        }
    }
}");
        }

        private static void AppendCustomScriptPlaceholder(StringBuilder sb, string indent, string scopeLabel)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine($"{indent}# ADD YOUR {scopeLabel} POWERSHELL SCRIPT CONTENTS BELOW");
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine();
            sb.AppendLine($"{indent}# Start here");
            sb.AppendLine();
            sb.AppendLine($"{indent}# End here");
            sb.AppendLine();
        }

        private static void AppendCompletionBlock(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
            sb.AppendLine("Write-Log \"Akari Tool Windows Optimization & Customization Script Completed\" \"SUCCESS\"");
            sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
        }

        // ── Embedding helpers ──────────────────────────────────────────────

        /// <summary>Writes a child script into $scriptsDir via Base64 (content-safe).</summary>
        private static void AppendBase64Script(StringBuilder sb, string baseName, string content)
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
            sb.AppendLine($"    # Create {baseName}.ps1 script");
            // Chunk the base64 into a joined array so no single line is megabytes long
            sb.AppendLine($"    ${SafeVar(baseName)}B64 = @(");
            for (int i = 0; i < b64.Length; i += 200)
                sb.AppendLine($"        '{b64.Substring(i, Math.Min(200, b64.Length - i))}',");
            sb.AppendLine("        ''");
            sb.AppendLine("    ) -join ''");
            sb.AppendLine($"    Write-EmbeddedScript -Path \"$scriptsDir\\{baseName}.ps1\" -Base64 ${SafeVar(baseName)}B64");
            sb.AppendLine();
        }

        private static string SafeVar(string baseName) =>
            new string(baseName.Where(char.IsLetterOrDigit).ToArray());

        private static string ScriptFileBase(string scriptName) =>
            Path.GetFileNameWithoutExtension(scriptName);

        private string LoadScriptContent(string scriptName)
        {
            string content;
            if (ScriptContentOverride != null) content = ScriptContentOverride(scriptName);
            else
            {
                var expectedEnding = $".Scripts.{scriptName}";
                var resourceName = AppAssembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(expectedEnding, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileNotFoundException($"Embedded script not found: {scriptName}");

                using var stream = AppAssembly.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream);
                content = reader.ReadToEnd();
            }
            return TweakScriptPrelude + content;
        }

        /// <summary>
        /// Prepended to every embedded tweak script. Prevents the class of hang
        /// that froze Windows Setup: DISM cmdlets without -NoRestart prompt
        /// "restart now? [Y/N]" on a hidden console and block forever, and
        /// $ConfirmPreference suppresses any other confirmation prompts.
        /// </summary>
        private const string TweakScriptPrelude = @"# — Akari unattend safety prelude (injected by the generator) —
$ProgressPreference = 'SilentlyContinue'
$ConfirmPreference  = 'None'
$PSDefaultParameterValues['Disable-WindowsOptionalFeature:NoRestart'] = $true
$PSDefaultParameterValues['Enable-WindowsOptionalFeature:NoRestart']  = $true
$PSDefaultParameterValues['Remove-WindowsCapability:ErrorAction']     = 'SilentlyContinue'

";
    }
}
