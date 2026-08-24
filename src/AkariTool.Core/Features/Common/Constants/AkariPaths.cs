namespace AkariTool.Tabs;

/// <summary>
/// Well-known Akari file-system paths (ProgramData roots). Core-side constant
/// twin of the App-layer AkariPaths so catalogs/script generators can reference
/// literal paths without an App dependency. (Winhance keeps equivalents in
/// ConfigFileConstants; Akari centralizes them here.)
/// </summary>
public static class AkariPaths
{
    public static readonly string ScriptsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AkariTool", "Scripts");

    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\AkariTool\Scripts";
    public const string LogsDirectoryLiteral = @"C:\ProgramData\AkariTool\Logs";
    public const string PowerShellExePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
}
