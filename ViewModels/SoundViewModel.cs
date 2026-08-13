using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Sound;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Sound page. First-wave rollout tab — a single-catalog, standard tweak-row tab
/// with no bespoke sections, reusing the Gaming rendering layer wholesale.
///
/// Section order matches net8's SoundTab.Build() exactly (one section:
/// "System Sounds"). Includes the "Sound Ducking Preference" dropdown, so this tab
/// also exercises the dropdown row in a second tab.
/// </summary>
public sealed partial class SoundViewModel : TweakPageViewModel
{
    public SoundViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Sound";
        Subtitle = "Control system sounds, audio ducking, and voice activation.";
    }

    public override string NavTag => "Sound";
    public override string NavLabel => "Sound";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("System Sounds", SoundTweaks.SystemSounds(log));
    }
}
