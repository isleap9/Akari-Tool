using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Privacy;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Privacy &amp; Security page. Wave-2 rollout tab — a single-catalog, standard
/// tweak-row tab with no bespoke sections, reusing the Gaming rendering layer.
///
/// Section order matches net8's PrivacyTab.Build() exactly — all 13 sections
/// across the 7 catalog partials, in the same order:
///   Security → Content Delivery &amp; Advertising → Lock Screen → General →
///   Speech → Inking &amp; Typing Personalization → Diagnostics &amp; Feedback →
///   Search Permissions → Activity History → App Permissions → Windows AI →
///   Microsoft Edge AI → Microsoft Office AI.
///
/// ⚠ The "Security" section was audited for this rollout: all 10 rows are pure
/// registry-key tweaks (UAC level, Workplace Join, BitLocker auto-encryption,
/// WiFi-Sense, Automatic Maintenance, Error Reporting, Remote Assistance, Smart
/// App Control [Code Integrity policy state], Developer Mode, PowerShell execution
/// policy). NONE references DefenderService or arms/disables Defender real-time,
/// tamper, or antivirus protection. Nothing protective is touched.
/// </summary>
public sealed partial class PrivacyViewModel : TweakPageViewModel
{
    public PrivacyViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Privacy & Security";
        Subtitle = "Telemetry, data collection, app permissions, and AI opt-outs.";
    }

    public override string NavTag => "Privacy";
    public override string NavLabel => "Privacy & Security";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Security", PrivacyTweaks.Security(log));
        yield return ("Content Delivery & Advertising", PrivacyTweaks.ContentDelivery(log));
        yield return ("Lock Screen", PrivacyTweaks.LockScreen(log));
        yield return ("General", PrivacyTweaks.General(log));
        yield return ("Speech", PrivacyTweaks.Speech(log));
        yield return ("Inking & Typing Personalization", PrivacyTweaks.InkingTyping(log));
        yield return ("Diagnostics & Feedback", PrivacyTweaks.Diagnostics(log));
        yield return ("Search Permissions", PrivacyTweaks.SearchPermissions(log));
        yield return ("Activity History", PrivacyTweaks.ActivityHistory(log));
        yield return ("App Permissions", PrivacyTweaks.AppPermissions(log));
        yield return ("Windows AI", PrivacyTweaks.WindowsAI(log));
        yield return ("Microsoft Edge AI", PrivacyTweaks.EdgeAI(log));
        yield return ("Microsoft Office AI", PrivacyTweaks.OfficeAI(log));
    }
}
