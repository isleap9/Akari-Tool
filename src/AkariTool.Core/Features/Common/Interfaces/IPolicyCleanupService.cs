namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance IPolicyCleanupService 1:1: deletes Group Policy registry keys that
/// override user tweaks (LGPO/AD leftovers silently beat HKCU settings).
/// Collects every RegistrySetting flagged IsGroupPolicy from the compatible
/// settings registry and deletes those keys (parent paths deduped over children).
/// </summary>
public interface IPolicyCleanupService
{
    /// <summary>Number of policy keys successfully deleted.</summary>
    int CleanupPolicyKeys();
}
