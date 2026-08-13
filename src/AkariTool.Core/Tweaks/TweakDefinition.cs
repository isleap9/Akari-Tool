namespace AkariTool.Core.Tweaks
{
    /// <summary>
    /// Badge kinds — mirrors Winhance's SettingBadgeKind.
    /// </summary>
    public enum TweakBadgeKind
    {
        Recommended,   // Akari's suggestion     → dark red
        Default,       // Windows factory value  → gray
        Custom,        // Something else         → muted
        Preference,    // No objectively correct answer
    }

    /// <summary>
    /// A single computed badge pill state.
    /// </summary>
    public sealed record TweakBadgePill(
        TweakBadgeKind Kind,
        bool IsActive,        // true = current state matches this badge
        string Label,
        string Tooltip);

    /// <summary>
    /// Input kind for a tweak — toggle or dropdown.
    /// </summary>
    public enum TweakInputKind { Toggle, Dropdown }

    /// <summary>
    /// One option in a Dropdown tweak.
    /// </summary>
    public sealed record TweakDropdownOption(
        string Label,
        object Value,               // the raw value written to registry
        bool IsRecommended = false,
        bool IsDefault = false,
        string? Warning = null);    // shown as a confirmation before this option is applied

    /// <summary>
    /// Lightweight definition of a single tweak setting.
    /// Inspired by Winhance's SettingDefinition — trimmed to what Akari Tool needs.
    ///
    /// For Toggle tweaks:
    ///   RecommendedValue / DefaultValue are bool? (true = ON recommended/default)
    ///   ReadState() returns bool? (true = currently ON, null = unknown)
    ///   Apply(bool) sets the toggle
    ///
    /// For Dropdown tweaks:
    ///   Options carries the choices; RecommendedValue/DefaultValue are the raw Values
    ///   ReadCurrentIndex() returns the current selected index (null = unknown)
    ///   Apply is unused — dropdown changes call ApplyIndex(int) instead
    /// </summary>
    public sealed class TweakDefinition
    {
        // ── Identity ─────────────────────────────────────────────────────────
        public required string Id          { get; init; }
        public required string Name        { get; init; }
        public required string Description { get; init; }
        public string? Group               { get; init; }   // section header label

        // ── Input kind ───────────────────────────────────────────────────────
        public TweakInputKind InputKind { get; init; } = TweakInputKind.Toggle;

        // ── Badge data (Toggle) ──────────────────────────────────────────────
        // true = ON is recommended/default, false = OFF is, null = no badge
        public bool? RecommendedState { get; init; }   // Winhance: RecommendedToggleState
        public bool? DefaultState     { get; init; }   // Winhance: DefaultToggleState
        public bool IsPreference      { get; init; }   // no objectively correct answer

        // When true, the Default/Recommended pill TOOLTIPS describe the underlying FEATURE
        // state rather than the toggle position. Used by inverted rows (e.g. "Disable X"
        // where toggle ON = feature OFF) so "Windows default: On" reads correctly. Affects
        // ONLY the tooltip wording — the pill's active-highlight math is unchanged.
        public bool InvertBadgeLabelWording { get; init; }

        // ── State reader (Toggle) ────────────────────────────────────────────
        public Func<bool?>? ReadState { get; init; }

        // ── Apply / Undo (Toggle) ────────────────────────────────────────────
        public Action<bool>? Apply { get; init; }

        // ── Dropdown options ─────────────────────────────────────────────────
        public TweakDropdownOption[]? Options      { get; init; }
        public Func<int?>? ReadCurrentIndex        { get; init; }
        public Action<int>? ApplyIndex             { get; init; }

        // ── Misc ─────────────────────────────────────────────────────────────
        public bool RequiresRestart { get; init; }

        // ── Warnings (Winhance-style) ────────────────────────────────────────
        // Toggle tweaks: Warning is shown as an OK/Cancel confirmation before
        // Apply() runs. WarningState narrows it to one direction:
        //   WarningState = false → warn only when switching OFF
        //   WarningState = true  → warn only when switching ON
        //   WarningState = null  → warn on any change
        // Dropdown tweaks use per-option warnings (TweakDropdownOption.Warning).
        public string? Warning      { get; init; }
        public bool?   WarningState { get; init; }

        /// <summary>Warning to confirm before applying <paramref name="target"/> to a Toggle, or null.</summary>
        public string? GetToggleWarning(bool target)
        {
            if (Warning == null) return null;
            if (WarningState.HasValue && target != WarningState.Value) return null;
            return Warning;
        }

        /// <summary>Warning to confirm before applying dropdown option <paramref name="index"/>, or null.</summary>
        public string? GetOptionWarning(int index)
        {
            if (Options == null || index < 0 || index >= Options.Length) return null;
            return Options[index].Warning;
        }

        // ── Badge computation ────────────────────────────────────────────────

        /// <summary>
        /// Computes the badge pill row for a Toggle tweak given the current UI state.
        /// Mirrors Winhance's ComputeBadgeState() logic, simplified for toggle-only.
        /// </summary>
        public TweakBadgePill[] ComputeToggleBadges(bool currentState)
        {
            var pills = new System.Collections.Generic.List<TweakBadgePill>();

            // Preference pill always shown first when IsPreference—same as Winhance
            if (IsPreference)
                pills.Add(new TweakBadgePill(TweakBadgeKind.Preference, true, "Preference", "Personal preference — no objectively correct answer"));

            if (RecommendedState.HasValue)
                pills.Add(new TweakBadgePill(
                    TweakBadgeKind.Recommended,
                    currentState == RecommendedState.Value,
                    "Recommended",
                    InvertBadgeLabelWording
                        ? (RecommendedState.Value ? "Akari recommends: feature OFF" : "Akari recommends: feature ON")
                        : (RecommendedState.Value ? "Akari recommends: ON" : "Akari recommends: OFF")));

            if (DefaultState.HasValue)
                pills.Add(new TweakBadgePill(
                    TweakBadgeKind.Default,
                    currentState == DefaultState.Value,
                    "Windows Default",
                    InvertBadgeLabelWording
                        ? (DefaultState.Value ? "Windows default: Off" : "Windows default: On")
                        : (DefaultState.Value ? "Windows default: ON" : "Windows default: OFF")));

            return pills.ToArray();
        }

        /// <summary>
        /// Computes badge pills for a Dropdown tweak given the current selected index.
        /// </summary>
        public TweakBadgePill[] ComputeDropdownBadges(int currentIndex)
        {
            if (Options == null || Options.Length == 0) return Array.Empty<TweakBadgePill>();

            var pills = new System.Collections.Generic.List<TweakBadgePill>();

            // Preference pill always shown first when IsPreference—same as Winhance
            if (IsPreference)
                pills.Add(new TweakBadgePill(TweakBadgeKind.Preference, true, "Preference", "Personal preference"));

            bool currentIsRecommended = currentIndex >= 0 && currentIndex < Options.Length && Options[currentIndex].IsRecommended;
            bool currentIsDefault     = currentIndex >= 0 && currentIndex < Options.Length && Options[currentIndex].IsDefault;
            bool hasRecommended       = Options.Any(o => o.IsRecommended);
            bool hasDefault           = Options.Any(o => o.IsDefault);

            if (hasRecommended)
                pills.Add(new TweakBadgePill(TweakBadgeKind.Recommended, currentIsRecommended, "Recommended", "Akari's recommended option"));
            if (hasDefault)
                pills.Add(new TweakBadgePill(TweakBadgeKind.Default, currentIsDefault, "Windows Default", "Windows factory default option"));

            // currentIndex < 0 = the row is unselected because the machine's value
            // matches no option (custom locale, vendor-specific index). That is
            // exactly the "Custom" case — and it's the only way a preference row
            // with no Recommended/Default option can light this pill.
            bool isCustom = currentIndex < 0
                         || ((hasRecommended || hasDefault) && !currentIsRecommended && !currentIsDefault);
            pills.Add(new TweakBadgePill(TweakBadgeKind.Custom, isCustom, "Custom",
                currentIndex < 0
                    ? "Current value matches none of the listed options"
                    : "Current value differs from Recommended and Default"));

            return pills.ToArray();
        }
    }
}
