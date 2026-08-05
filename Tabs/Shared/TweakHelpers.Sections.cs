using Microsoft.UI.Xaml;           // Visibility
using Microsoft.UI.Xaml.Controls;  // TextBlock, StackPanel

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        // ── Collapsible section state ─────────────────────────────────────────
        // Extracted from TweakHelpers.Controls.cs during the WinUI migration so
        // BaseTab.FilterTweaks can resolve section collapse state without the full
        // (deferred) control-building partial. The section CARD builders
        // (BuildSection etc.) are ported with the first tweak-tab batch.

        /// <summary>
        /// Tracks one section's collapse state. Collapse drives the INNER content
        /// panel's Visibility — never the card's — because search owns the card's
        /// Visibility and the two must not fight.
        /// </summary>
        internal sealed class SectionCollapse
        {
            public required TextBlock  Chevron;
            public required StackPanel Body;
            public required string     Title;
            public bool UserCollapsed;          // the user's persisted choice
            public bool ForcedOpenBySearch;     // temporarily expanded for a query

            public void Render()
            {
                bool show = !UserCollapsed || ForcedOpenBySearch;
                Body.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                Chevron.Text    = show ? "▾" : "▸"; // ▾ / ▸
            }
        }

        internal static readonly Dictionary<StackPanel, SectionCollapse> SectionCollapseStates = new();
    }
}
