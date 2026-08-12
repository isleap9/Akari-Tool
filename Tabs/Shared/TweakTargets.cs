namespace AkariTool.Tabs
{
    /// <summary>
    /// Target resolution + mismatch predicates shared by every bulk surface
    /// (section bulk bars, tab-level Quick Actions counts, and the bulk engine).
    ///
    /// MVVM PORT: carried over VERBATIM from the net8 factory partial
    /// <c>TweakHelpers.BulkActions.cs</c> (TryGetRecommendedTarget /
    /// TryGetDefaultTarget / IsMismatched / CollectPending). Those four methods were
    /// pure logic that merely happened to live in a rendering file — the section
    /// registration dictionaries and the button builders stayed behind. Keeping the
    /// predicates in ONE place is what stops the section pill, the Quick Actions
    /// count and the bulk run from ever disagreeing (the net8 comment on
    /// CollectPending says exactly this).
    /// </summary>
    public static class TweakTargets
    {
        /// <summary>One tweak that needs changing, with the resolved target value.</summary>
        public readonly record struct PendingWork(
            TweakDefinition Def,
            Action Refresh,
            bool ToggleTarget,
            int OptionTarget);

        public static bool TryGetRecommendedTarget(TweakDefinition def, out bool toggleTarget, out int optionTarget)
        {
            toggleTarget = false; optionTarget = -1;

            if (def.InputKind == TweakInputKind.Toggle)
            {
                if (!def.RecommendedState.HasValue || def.Apply == null) return false;
                toggleTarget = def.RecommendedState.Value;
                return true;
            }
            if (def.Options is { Length: > 0 } && def.ApplyIndex != null)
            {
                optionTarget = Array.FindIndex(def.Options, o => o.IsRecommended);
                return optionTarget >= 0;
            }
            return false;
        }

        public static bool TryGetDefaultTarget(TweakDefinition def, out bool toggleTarget, out int optionTarget)
        {
            toggleTarget = false; optionTarget = -1;

            if (def.InputKind == TweakInputKind.Toggle)
            {
                if (!def.DefaultState.HasValue || def.Apply == null) return false;
                toggleTarget = def.DefaultState.Value;
                return true;
            }
            if (def.Options is { Length: > 0 } && def.ApplyIndex != null)
            {
                optionTarget = Array.FindIndex(def.Options, o => o.IsDefault);
                return optionTarget >= 0;
            }
            return false;
        }

        /// <summary>True when the tweak's current state differs from the given target.</summary>
        public static bool IsMismatched(TweakDefinition def, bool toggleTarget, int optionTarget)
        {
            try
            {
                if (def.InputKind == TweakInputKind.Toggle)
                {
                    var cur = def.ReadState?.Invoke();
                    return cur.HasValue && cur.Value != toggleTarget;   // unknown state → don't count
                }
                var idx = def.ReadCurrentIndex?.Invoke();
                return idx.HasValue && idx.Value >= 0 && idx.Value != optionTarget;
            }
            catch { return false; }
        }

        /// <summary>
        /// Shared predicate for every bulk surface (section bars, tab-level Quick
        /// Actions counts AND the bulk engine).
        /// </summary>
        public static List<PendingWork> CollectPending(
            IEnumerable<(TweakDefinition Def, Action Refresh)> entries, bool useRecommended)
        {
            var work = new List<PendingWork>();
            foreach (var (def, refresh) in entries)
            {
                bool ok = useRecommended
                    ? TryGetRecommendedTarget(def, out var t, out var o)
                    : TryGetDefaultTarget(def, out t, out o);
                if (ok && IsMismatched(def, t, o))
                    work.Add(new PendingWork(def, refresh, t, o));
            }
            return work;
        }

        /// <summary>The warning text that applies to this pending change, or null.</summary>
        public static string? WarningFor(in PendingWork w) =>
            w.Def.InputKind == TweakInputKind.Toggle
                ? w.Def.GetToggleWarning(w.ToggleTarget)
                : w.Def.GetOptionWarning(w.OptionTarget);
    }
}
