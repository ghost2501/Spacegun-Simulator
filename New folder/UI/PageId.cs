namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Canonical page identifiers used for navigation, page art overrides, buffering, and autosave hooks.
    /// Keep these stable once playtesters depend on them.
    /// </summary>
    public static class PageId
    {
        // Title / Meta
        public const string Title = "Title";
        public const string MainMenu = "MainMenu";
        public const string DifficultySelection = "DifficultySelection";
        public const string GameOver = "GameOver";

        // Detection / Resource Phase
        public const string Detection = "Detection";
        public const string ResourceHub = "ResourceHub"; // "RESOURCES & RESEARCH" hub/wrapper
        public const string ResourceAllocation = "ResourceAllocation"; // "RESOURCE ALLOCATION"
        public const string ResourceOptions = "ResourceOptions";
        public const string PreparationSummary = "PreparationSummary";
        public const string ResearchMenu = "ResearchMenu";
        public const string PreparationStatus = "PreparationStatus";

        // Development
        public const string WeaponDevelopment = "WeaponDevelopment";
        public const string ProjectileDevelopment = "ProjectileDevelopment";
        public const string ProjectileConfigSummary = "ProjectileConfigSummary";
        public const string GunDevelopment = "GunDevelopment";

        // Firing Flow / Fire Control Tools
        public const string Firing = "Firing";
        public const string MotionComputer = "MotionComputer";
        public const string TrajectoryPlotter = "TrajectoryPlotter";
        public const string FireSimulator = "FireSimulator";
        public const string EnterFiringParameters = "EnterFiringParameters";
        public const string CommitFiringSolution = "CommitFiringSolution"; // if you separate from EnterFiringParameters
        public const string FiringVisualization = "FiringVisualization";
        public const string DetailedWeaponStatus = "DetailedWeaponStatus";

        // Diagnostics / Debug
        public const string TestModeMenu = "TestModeMenu";
        public const string FiringChallenge = "FiringChallenge";
        public const string SimulationTestMode = "SimulationTestMode";
    }
}
