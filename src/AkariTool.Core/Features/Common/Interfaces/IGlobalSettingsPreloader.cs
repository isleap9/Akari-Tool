using System.Threading.Tasks;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance IGlobalSettingsPreloader 1:1: registers every feature's bypassed
/// (unfiltered) settings into the global registry at startup, before any page
/// opens — so cross-feature lookups and config export work without visiting tabs.
/// </summary>
public interface IGlobalSettingsPreloader
{
    Task PreloadAllSettingsAsync();
    bool IsPreloaded { get; }
}
