using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// One Debloat row — MVVM port of a net8 <c>(Title, Desc, RunScript, UndoScript)</c>
/// tuple from <c>DebloatTab.BuildGroup</c>. Identity is the run-script filename; there is
/// no Id, no <c>TweakDefinition</c>, no <c>AppDefinition</c>, no TweakRegistry.
///
/// ⚠ UNGUARDED BY DESIGN. net8 fires Run and Undo with NO confirmation dialog (Phase 27),
/// and isleap chose (Phase 28) to port that verbatim — including the entries that also run
/// destructive removals (Debloat.ps1, RemoveEdge.ps1, RemoveOneDrive.ps1, WindowsAI.ps1).
/// The embedded .ps1 payloads are run byte-for-byte unchanged.
/// </summary>
public sealed partial class DebloatRowViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly List<string> _appliedTweaks;

    public string Title { get; }
    public string Description { get; }
    public string RunScript { get; }
    public string UndoScript { get; }

    /// <summary>net8 rendered the Undo button only when the 4th tuple field was non-empty.</summary>
    public bool HasUndo => !string.IsNullOrEmpty(UndoScript);

    public DebloatRowViewModel(
        string title, string description, string runScript, string undoScript,
        ToolService tool, List<string> appliedTweaks)
    {
        Title = title;
        Description = description;
        RunScript = runScript;
        UndoScript = undoScript;
        _tool = tool;
        _appliedTweaks = appliedTweaks;
    }

    /// <summary>net8: <c>Service.RunWithTracking(new ScriptAction(run), title, AppliedTweaks)</c>.</summary>
    [RelayCommand]
    private Task Run() =>
        _tool.RunWithTracking(new ScriptAction(RunScript), Title, _appliedTweaks);

    /// <summary>net8: <c>Service.RunAction(new ScriptAction(undo))</c>.</summary>
    [RelayCommand]
    private Task Undo() =>
        HasUndo ? _tool.RunAction(new ScriptAction(UndoScript)) : Task.CompletedTask;
}
