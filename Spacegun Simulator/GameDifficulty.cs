namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME DIFFICULTY SYSTEM
    // ============================================================================
    // Three difficulty levels with narrative flavor and mechanical differences.
    // Each level modifies how hit tolerance is calculated based on the scenario.

    /// <summary>
    /// Difficulty levels representing different strategic scenarios.
    /// Each has unique narrative context and mechanical impact on hit tolerance.
    /// </summary>
    public enum GameDifficulty
    {
        /// <summary>
        /// THE NUCLEAR OPTION (easy)
        /// 
        /// You have nuclear warheads and are not afraid to use them.
        ///
        /// - Launch Delay: Accept 0.1s increments → "3.2s", "3.3s", "3.4s"
        /// - Elevation: Accept 1° increments → "15°", "16°", "17°"
        /// - Azimuth: Accept 5° increments → "90°", "95°", "100°"
        /// - Velocity: Accept 1,000 m/s increments → "200000", "201000", "202000"
        /// 
        /// </summary>
        NuclearOption = 0,

        /// <summary>
        /// COMETS AND ASTEROIDS (difficult)
        /// 
        /// They are slinging comets and asteroids, all we we have are big bullets. 
        /// 
        /// - Launch Delay: Accept 0.01s increments → "3.22s", "3.23s", "3.24s"
        /// - Elevation: Accept 0.1° increments → "15.2°", "15.3°", "15.4°"
        /// - Azimuth: Accept 0.5° increments → "90.0°", "90.5°", "91.0°"
        /// - Velocity: Accept 100 m/s increments → "200000", "200100", "200200"
        ///
        /// </summary>
        CometsAndAsteroids = 1,

        /// <summary>
        /// THE REAL SPACEGUN SIMULATOR (god tier)
        /// 
        /// Space bullet vs Space bullet.
        /// 
        /// - Launch Delay: Accept 0.001s increments → "3.221s", "3.222s", "3.223s"
        /// - Elevation: Accept 0.01° increments → "15.22°", "15.23°", "15.24°"
        /// - Azimuth: Accept 0.05° increments → "90.00°", "90.05°", "90.10°"
        /// - Velocity: Accept 10 m/s increments → "200000", "200010", "200020"
        /// 
        /// </summary>
        RealSpacegunSimulator = 2
    }

    /// <summary>
    /// Configuration for a specific difficulty level.
    /// Defines how the difficulty affects hit tolerance calculations.
    /// </summary>
    public class DifficultyConfig
    {
        /// <summary>
        /// The difficulty level this config represents.
        /// </summary>
        public GameDifficulty Difficulty { get; set; }

        /// <summary>
        /// Display name for this difficulty level in the UI.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Short flavor text describing the narrative scenario.
        /// </summary>
        public string NarrativeDescription { get; set; } = string.Empty;

        /// <summary>
        /// Multiplier applied to hit tolerance.
        /// Used for NuclearOption mode (100x).
        /// For other modes, this remains 1.0.
        /// </summary>
        public double HitToleranceMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Multiplier applied to target RCS (Radar Cross-Section).
        /// Used for CometsAndAsteroids mode (10x).
        /// For other modes, this remains 1.0.
        /// Increasing RCS effectively increases the hitbox.
        /// </summary>
        public double TargetRcsMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Gets the configuration for a specific difficulty level.
        /// </summary>
        public static DifficultyConfig GetConfig(GameDifficulty difficulty) => difficulty switch
        {
            GameDifficulty.NuclearOption => new DifficultyConfig
            {
                Difficulty = GameDifficulty.NuclearOption,
                DisplayName = "The Nuclear Option (easy)",
                NarrativeDescription =
                    "You have nuclear warheads and are not afraid to use them.\n" +
                    "\n" +
                    " - Launch Delay: Accept 0.1s increments → \"3.2s\", \"3.3s\", \"3.4s\"\n" +
                    " - Elevation: Accept 1° increments → \"15°\", \"16°\", \"17°\"\n" +
                    " - Azimuth: Accept 5° increments → \"90°\", \"95°\", \"100°\"\n" +
                    " - Velocity: Accept 1,000 m/s increments → \"200000\", \"201000\", \"202000\"",
                HitToleranceMultiplier = 100.0,
                TargetRcsMultiplier = 1.0
            },

            GameDifficulty.CometsAndAsteroids => new DifficultyConfig
            {
                Difficulty = GameDifficulty.CometsAndAsteroids,
                DisplayName = "Comets and Asteroids (hard)",
                NarrativeDescription =
                    "They are slinging comets and asteroids, all we have are big bullets.\n" +
                    "\n" +
                    " - Launch Delay: Accept 0.01s increments → \"3.22s\", \"3.23s\", \"3.24s\"\n" +
                    " - Elevation: Accept 0.1° increments → \"15.2°\", \"15.3°\", \"15.4°\"\n" +
                    " - Azimuth: Accept 0.5° increments → \"90.0°\", \"90.5°\", \"91.0°\"\n" +
                    " - Velocity: Accept 100 m/s increments → \"200000\", \"200100\", \"200200\"",
                HitToleranceMultiplier = 1.0,
                TargetRcsMultiplier = 10.0
            },

            GameDifficulty.RealSpacegunSimulator => new DifficultyConfig
            {
                Difficulty = GameDifficulty.RealSpacegunSimulator,
                DisplayName = "The Real Spacegun Simulator",
                NarrativeDescription =
                    "Space bullet vs Space bullet.\n" +
                    "\n" +
                    " - Launch Delay: Accept 0.001s increments → \"3.221s\", \"3.222s\", \"3.223s\"\n" +
                    " - Elevation: Accept 0.01° increments → \"15.22°\", \"15.23°\", \"15.24°\"\n" +
                    " - Azimuth: Accept 0.05° increments → \"90.00°\", \"90.05°\", \"90.10°\"\n" +
                    " - Velocity: Accept 10 m/s increments → \"200000\", \"200010\", \"200020\"",
                HitToleranceMultiplier = 1.0,
                TargetRcsMultiplier = 1.0
            },

            _ => throw new ArgumentException($"Unknown difficulty: {difficulty}")
        };

        /// <summary>
        /// Get a list of all available difficulty configurations for UI display.
        /// </summary>
        public static List<DifficultyConfig> GetAllConfigs()
        {
            return new List<DifficultyConfig>
            {
                GetConfig(GameDifficulty.NuclearOption),
                GetConfig(GameDifficulty.CometsAndAsteroids),
                GetConfig(GameDifficulty.RealSpacegunSimulator)
            };
        }
    }
}