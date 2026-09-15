# ── Admin elevation ──────────────────────────────────────────────────────────
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")) {
    Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
    Exit
}

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
