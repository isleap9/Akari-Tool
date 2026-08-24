namespace AkariTool.Core.Models.Actions
{
    /// <summary>
    /// Base type for anything a row button can execute.
    /// </summary>
    public abstract record RunAction;

    /// <summary>Runs an embedded PowerShell script by file name.</summary>
    public sealed record ScriptAction(string FileName) : RunAction;

    /// <summary>Runs a PowerShell command via WinGet or Chocolatey.</summary>
    public sealed record CommandAction(string Command, string? AppName = null) : RunAction;

    /// <summary>Opens a URL in the user's default browser.</summary>
    public sealed record UrlAction(string Url) : RunAction;
}
