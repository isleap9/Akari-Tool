namespace AkariTool.Services
{
    /// <summary>
    /// Priority classes offered for the game process. Realtime is deliberately
    /// absent and must never be added: it outranks the audio and input threads,
    /// which produces stutter and unresponsive peripherals — the opposite of what
    /// this feature is for.
    /// </summary>
    public enum GamePriorityLevel { AboveNormal, High }

    public enum GameIoPriority { Normal, High }

    /// <summary>CPU set policy. Only AllCores in v1; P-core / CCD modes come later.</summary>
    public enum CpuSetMode { AllCores }

    /// <summary>The user's Competitive Mode option set, persisted between sessions.</summary>
    public sealed record CompetitiveOptions(
        bool BoostGamePriority,
        GamePriorityLevel PriorityLevel,
        GameIoPriority IoPriority,
        CpuSetMode CpuSets,
        bool GameFocus,                 // suspend background apps
        bool PauseNonEssentialServices,
        bool ConsistentPerformance,     // power plan + power throttling opt-out
        bool CloseAfterLaunch,
        bool ClearStandbyMemory,
        bool LaunchThroughSteam)   // steam://rungameid when the exe is a Steam game
    {
        /// <summary>First-run defaults, matching the checkbox defaults in the UI.</summary>
        public static CompetitiveOptions Default { get; } = new(
            BoostGamePriority:         true,
            PriorityLevel:             GamePriorityLevel.High,
            IoPriority:                GameIoPriority.High,
            CpuSets:                   CpuSetMode.AllCores,
            GameFocus:                 true,
            PauseNonEssentialServices: true,
            ConsistentPerformance:     true,
            CloseAfterLaunch:          false,
            ClearStandbyMemory:        false,
            LaunchThroughSteam:        true);
    }
}
