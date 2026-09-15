# ── Admin elevation ──────────────────────────────────────────────────────────
# Web launch URL (used to re-elevate when started via `irm <url> | iex`, where
# there is no script file on disk to re-run).
$AkariUrl = "https://raw.githubusercontent.com/isleap9/Akari-Tool/main/akari.ps1"
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")) {
    if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) {
        # normal case: relaunch the local file elevated
        Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
    } else {
        # launched via `irm <url> | iex` (no file on disk): re-fetch and run elevated
        Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -Command `"irm {0} | iex`"" -f $AkariUrl) -Verb RunAs
    }
    Exit
}

# ── Taskbar identity + console handling (custom taskbar icon, no PS console) ──
Add-Type -Namespace Akari -Name Native -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError=true)]
public static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern System.IntPtr GetConsoleWindow();
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
"@
# Give the process its own taskbar identity so it shows as "Akari Tool" with the
# Akari icon (pinnable) instead of grouping under powershell.exe.
try { [Akari.Native]::SetCurrentProcessExplicitAppUserModelID("Akari.Tool") | Out-Null } catch {}
# Hide the PowerShell console window so only the app window appears in the taskbar.
try {
    $consoleWnd = [Akari.Native]::GetConsoleWindow()
    if ($consoleWnd -ne [IntPtr]::Zero) { [Akari.Native]::ShowWindow($consoleWnd, 0) | Out-Null }  # 0 = SW_HIDE
} catch {}

# ── WPF assemblies ───────────────────────────────────────────────────────────
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

# ── DWM P/Invoke  (Mica backdrop + dark title bar on Win11) ─────────────────
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class DwmApi {
    // Mica / Acrylic backdrop
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Extend frame into client area (required for Mica)
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int Left, Right, Top, Bottom; }
}
"@

# ── Shared state across runspaces ────────────────────────────────────────────
$sync             = [Hashtable]::Synchronized(@{})
$sync.configs     = @{}
$sync.runspaces   = [System.Collections.Generic.List[hashtable]]::new()
