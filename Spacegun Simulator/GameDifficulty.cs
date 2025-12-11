namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME DIFFICULTY SYSTEM
    // ============================================================================
    // Four difficulty levels with narrative flavor and mechanical differences.
    // Each level modifies how hit tolerance is calculated based on the scenario.
    //
    // PRECISION SYSTEM: All input/output precision is controlled here.
    // Tools (TargetMotionComputer, TrajectoryPlotter, FiringPhaseFormatter, etc.)
    // should call DifficultyConfig.GetPrecision() for consistent formatting.
    //
    // PRECISION CALIBRATION:
    // Precision is set so that a single increment error results in approximately
    // 10-50% of the hit tolerance deviation. This makes the game challenging but fair.

    /// <summary>
    /// Difficulty levels representing different strategic scenarios.
    /// Each has unique narrative context and mechanical impact on hit tolerance.
    /// </summary>
    public enum GameDifficulty
    {
        /// <summary>
        /// POTATO CANNONS AND BEACHBALLS (tutorial)
        /// Learn the game mechanics with simple, human-scale physics.
        /// Hit tolerance: 1 meter (beachball radius)
        /// </summary>
        PotatoCannonsAndBeachballs = 0,

        /// <summary>
        /// THE NUCLEAR OPTION (easy)
        /// You have nuclear warheads and are not afraid to use them.
        /// Hit tolerance: ~1,680 meters (warhead blast radius)
        /// </summary>
        NuclearOption = 1,

        /// <summary>
        /// COMETS AND ASTEROIDS (hard)
        /// They are slinging comets and asteroids, all we have are big bullets.
        /// Hit tolerance: ~53 meters (large asteroid cross-section)
        /// </summary>
        CometsAndAsteroids = 2,

        /// <summary>
        /// THE REAL SPACEGUN SIMULATOR (god tier)
        /// Space bullet vs Space bullet.
        /// Hit tolerance: ~16.8 meters (direct kinetic impact required)
        /// </summary>
        RealSpacegunSimulator = 3
    }

    /// <summary>
    /// Precision settings for a specific parameter type.
    /// Controls both display formatting and input validation increments.
    /// </summary>
    public class PrecisionConfig
    {
        /// <summary>Number of decimal places for display formatting.</summary>
        public int DecimalPlaces { get; init; }

        /// <summary>Minimum increment the player can input (e.g., 0.1 means "3.1", "3.2", "3.3").</summary>
        public double Increment { get; init; }

        /// <summary>Format string for display (e.g., "F1" for 1 decimal place).</summary>
        public string FormatString => $"F{DecimalPlaces}";

        /// <summary>Format a value with this precision.</summary>
        public string Format(double value) => value.ToString(FormatString);

        /// <summary>Format a value with this precision.</summary>
        public string Format(float value) => value.ToString(FormatString);

        /// <summary>Round a value to this precision's increment.</summary>
        public double RoundToIncrement(double value) => Math.Round(value / Increment) * Increment;
    }

    /// <summary>
    /// Configuration for a specific difficulty level.
    /// Defines how the difficulty affects hit tolerance calculations AND precision requirements.
    /// This is the SINGLE SOURCE OF TRUTH for all precision in the game.
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
        /// Whether this difficulty skips resource allocation and development phases.
        /// Used for tutorial mode to streamline the experience.
        /// </summary>
        public bool SkipResourcePhases { get; set; } = false;

        /// <summary>
        /// Whether this difficulty uses simplified tutorial scenarios.
        /// When true, uses fixed "beachball" scenarios instead of generated waves.
        /// </summary>
        public bool IsTutorialMode { get; set; } = false;

        // ====================================================================
        // PRECISION CONFIGURATION - Single source of truth
        // ====================================================================

        /// <summary>Precision for launch delay time (seconds).</summary>
        public PrecisionConfig LaunchDelayPrecision { get; set; } = null!;

        /// <summary>Precision for elevation angle (degrees).</summary>
        public PrecisionConfig ElevationPrecision { get; set; } = null!;

        /// <summary>Precision for azimuth bearing (degrees).</summary>
        public PrecisionConfig AzimuthPrecision { get; set; } = null!;

        /// <summary>Precision for velocity (m/s).</summary>
        public PrecisionConfig VelocityPrecision { get; set; } = null!;

        /// <summary>Precision for distance/position coordinates (meters).</summary>
        public PrecisionConfig DistancePrecision { get; set; } = null!;

        /// <summary>Precision for energy values (MJ or J).</summary>
        public PrecisionConfig EnergyPrecision { get; set; } = null!;

        /// <summary>Precision for mass values (kg or tons).</summary>
        public PrecisionConfig MassPrecision { get; set; } = null!;

        // ====================================================================
        // FACTORY METHOD
        // ====================================================================

        /// <summary>
        /// Gets the configuration for a specific difficulty level.
        /// </summary>
        public static DifficultyConfig GetConfig(GameDifficulty difficulty) => difficulty switch
        {
            // ================================================================
            // POTATO CANNONS AND BEACHBALLS (Tutorial)
            // Hit tolerance: 1 meter (beachball radius, target is 2m diameter)
            // Strategy: Learn the basics with human-scale physics
            // 
            // SCENARIO:
            // - Target: 2m diameter beachball at 100m, moving in a gentle arc
            // - Weapon: Potato cannon (~50 m/s muzzle velocity)
            // - All numbers are small, round, and easy to work with
            // ================================================================
            GameDifficulty.PotatoCannonsAndBeachballs => new DifficultyConfig
            {
                Difficulty = GameDifficulty.PotatoCannonsAndBeachballs,
                DisplayName = "Potato Cannons and Beachballs (tutorial)",
                NarrativeDescription =
                    "Learn the basics of ballistic interception!\n" +
                    "A 2-meter beachball is floating toward you in a gentle arc.\n" +
                    "Your weapon: a trusty potato cannon.\n\n" +
                    "• Simple, round numbers\n" +
                    "• No resource management\n" +
                    "• Generous hit tolerance (1 meter)\n" +
                    "• Perfect for learning the workflow",
                HitToleranceMultiplier = 1.0,  // Base tolerance is target radius
                TargetRcsMultiplier = 1.0,
                SkipResourcePhases = true,
                IsTutorialMode = true,

                // TUTORIAL: Whole numbers only - easiest possible precision
                // At 100m range with 50 m/s projectile:
                // 1s error = 50m miss, 1° error = 1.7m miss
                // With 1m tolerance, need to be within ~0.02s and ~30° (very forgiving)
                LaunchDelayPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                ElevationPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                AzimuthPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                VelocityPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                DistancePrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                EnergyPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 1.0 },
                MassPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 0.1 }
            },

            // ================================================================
            // NUCLEAR OPTION (Easy)
            // Hit tolerance: ~1,680 meters
            // Strategy: Get reasonably close, warhead does the rest
            // ================================================================
            GameDifficulty.NuclearOption => new DifficultyConfig
            {
                Difficulty = GameDifficulty.NuclearOption,
                DisplayName = "The Nuclear Option (easy)",
                NarrativeDescription =
                    "You have nuclear warheads and are not afraid to use them.\n" +
                    "Precision requirements are relaxed - get close enough and the warhead does the rest.\n" +
                    "Hit tolerance: ~1,680 meters (warhead blast radius)",
                HitToleranceMultiplier = 100.0,
                TargetRcsMultiplier = 1.0,

                LaunchDelayPrecision = new PrecisionConfig { DecimalPlaces = 2, Increment = 0.01 },
                ElevationPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 0.1 },
                AzimuthPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 0.1 },
                VelocityPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 100.0 },
                DistancePrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 100.0 },
                EnergyPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 10.0 },
                MassPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 10.0 }
            },

            // ================================================================
            // COMETS AND ASTEROIDS (Hard)
            // Hit tolerance: ~53 meters (√10 × 16.8m from RCS multiplier)
            // Strategy: Larger targets but still need good calculations
            // ================================================================
            GameDifficulty.CometsAndAsteroids => new DifficultyConfig
            {
                Difficulty = GameDifficulty.CometsAndAsteroids,
                DisplayName = "Comets and Asteroids (hard)",
                NarrativeDescription =
                    "They are slinging comets and asteroids, all we have are big bullets.\n" +
                    "Targets are large but you need precise calculations to score a direct hit.\n" +
                    "Hit tolerance: ~53 meters (asteroid cross-section)",
                HitToleranceMultiplier = 1.0,
                TargetRcsMultiplier = 10.0,

                LaunchDelayPrecision = new PrecisionConfig { DecimalPlaces = 4, Increment = 0.0001 },
                ElevationPrecision = new PrecisionConfig { DecimalPlaces = 3, Increment = 0.001 },
                AzimuthPrecision = new PrecisionConfig { DecimalPlaces = 3, Increment = 0.001 },
                VelocityPrecision = new PrecisionConfig { DecimalPlaces = 0, Increment = 10.0 },
                DistancePrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 10.0 },
                EnergyPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 1.0 },
                MassPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 1.0 }
            },

            // ================================================================
            // THE REAL SPACEGUN SIMULATOR (Extreme / God Tier)
            // Hit tolerance: ~16.8 meters (pure ballistic impact)
            // Strategy: Surgical precision required - every decimal matters
            // ================================================================
            GameDifficulty.RealSpacegunSimulator => new DifficultyConfig
            {
                Difficulty = GameDifficulty.RealSpacegunSimulator,
                DisplayName = "The Real Spacegun Simulator (extreme)",
                NarrativeDescription =
                    "Space bullet vs Space bullet.\n" +
                    "Maximum precision required. You're threading the needle at 1000+ km.\n" +
                    "Hit tolerance: ~16.8 meters (direct kinetic impact)",
                HitToleranceMultiplier = 1.0,
                TargetRcsMultiplier = 1.0,

                LaunchDelayPrecision = new PrecisionConfig { DecimalPlaces = 5, Increment = 0.00001 },
                ElevationPrecision = new PrecisionConfig { DecimalPlaces = 4, Increment = 0.0001 },
                AzimuthPrecision = new PrecisionConfig { DecimalPlaces = 4, Increment = 0.0001 },
                VelocityPrecision = new PrecisionConfig { DecimalPlaces = 1, Increment = 1.0 },
                DistancePrecision = new PrecisionConfig { DecimalPlaces = 2, Increment = 1.0 },
                EnergyPrecision = new PrecisionConfig { DecimalPlaces = 2, Increment = 0.1 },
                MassPrecision = new PrecisionConfig { DecimalPlaces = 2, Increment = 0.1 }
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
                GetConfig(GameDifficulty.PotatoCannonsAndBeachballs),
                GetConfig(GameDifficulty.NuclearOption),
                GetConfig(GameDifficulty.CometsAndAsteroids),
                GetConfig(GameDifficulty.RealSpacegunSimulator)
            };
        }

        // ====================================================================
        // TUTORIAL MODE CONSTANTS
        // ====================================================================

        /// <summary>
        /// Tutorial scenario: Beachball target specifications.
        /// 2m diameter beachball, very light, minimal fracture energy needed.
        /// </summary>
        public static class TutorialBeachball
        {
            public const double DiameterMeters = 2.0;
            public const double RadiusMeters = 1.0;  // Hit tolerance
            public const double MassKg = 0.5;  // Light inflatable ball
            public const double FractureEnergyJoules = 50.0;  // Pop the ball (~50 J)
            public const double CrossSectionM2 = 3.14;  // π × r² ≈ π × 1² ≈ 3.14 m²
        }

        /// <summary>
        /// Tutorial scenario: Potato cannon specifications.
        /// Realistic potato cannon physics for educational purposes.
        /// </summary>
        public static class TutorialPotatoCannon
        {
            public const double MuzzleVelocityMs = 50.0;  // ~112 mph, realistic for potato cannon
            public const double ProjectileMassKg = 0.3;  // ~300g potato
            public const double EffectiveRangeMeters = 150.0;  // Max effective range
        }

        /// <summary>
        /// Tutorial scenario: Beachball trajectory.
        /// Gentle arc traveling ~100m, easy to intercept.
        /// </summary>
        public static class TutorialTrajectory
        {
            public const double StartDistanceMeters = 100.0;  // Start 100m away
            public const double ApproachSpeedMs = 10.0;  // Slow, gentle approach (~22 mph)
            public const double ArcHeightMeters = 20.0;  // Peak height of arc
            public const double FlightTimeSeconds = 10.0;  // ~10 seconds to reach gun
        }

        // ====================================================================
        // CONVENIENCE FORMATTING METHODS
        // ====================================================================

        /// <summary>Format a launch delay value.</summary>
        public string FormatLaunchDelay(double seconds) => $"{LaunchDelayPrecision.Format(seconds)}s";

        /// <summary>Format an elevation angle.</summary>
        public string FormatElevation(double degrees) => $"{ElevationPrecision.Format(degrees)}°";

        /// <summary>Format an azimuth bearing.</summary>
        public string FormatAzimuth(double degrees) => $"{AzimuthPrecision.Format(degrees)}°";

        /// <summary>Format a velocity value.</summary>
        public string FormatVelocity(double metersPerSecond) => $"{VelocityPrecision.Format(metersPerSecond)} m/s";

        /// <summary>
        /// Format a distance/coordinate value with automatic unit scaling.
        /// Preserves precision relative to the base meter accuracy.
        /// </summary>
        public string FormatDistance(double meters)
        {
            if (Math.Abs(meters) >= 1_000_000)
            {
                double mm = meters / 1_000_000.0;
                int scaledDecimals = Math.Min(DistancePrecision.DecimalPlaces + 6, 10);
                return $"{mm.ToString($"F{scaledDecimals}")} Mm";
            }
            else if (Math.Abs(meters) >= 1_000)
            {
                double km = meters / 1_000.0;
                int scaledDecimals = Math.Min(DistancePrecision.DecimalPlaces + 3, 8);
                return $"{km.ToString($"F{scaledDecimals}")} km";
            }
            else
            {
                return $"{DistancePrecision.Format(meters)} m";
            }
        }

        /// <summary>Format an energy value.</summary>
        public string FormatEnergy(double megajoules)
        {
            // For tutorial mode, show in Joules if very small
            if (IsTutorialMode && megajoules < 0.001)
            {
                double joules = megajoules * 1_000_000.0;
                return $"{EnergyPrecision.Format(joules)} J";
            }
            return $"{EnergyPrecision.Format(megajoules)} MJ";
        }

        /// <summary>Format a mass value.</summary>
        public string FormatMass(double kilograms) => $"{MassPrecision.Format(kilograms)} kg";

        /// <summary>Format a 3D position vector.</summary>
        public string FormatVector3(Vector3 v) =>
            $"({DistancePrecision.Format(v.X)}, {DistancePrecision.Format(v.Y)}, {DistancePrecision.Format(v.Z)})";

        /// <summary>Format a 3D velocity vector.</summary>
        public string FormatVelocityVector(Vector3 v) =>
            $"({VelocityPrecision.Format(v.X)}, {VelocityPrecision.Format(v.Y)}, {VelocityPrecision.Format(v.Z)}) m/s";

        /// <summary>
        /// Generate a summary of precision requirements for display.
        /// </summary>
        public string GetPrecisionSummary()
        {
            return $"  Launch Delay: {LaunchDelayPrecision.Increment}s increments ({LaunchDelayPrecision.DecimalPlaces} decimals)\n" +
                   $"  Elevation: {ElevationPrecision.Increment}° increments ({ElevationPrecision.DecimalPlaces} decimals)\n" +
                   $"  Azimuth: {AzimuthPrecision.Increment}° increments ({AzimuthPrecision.DecimalPlaces} decimals)\n" +
                   $"  Velocity: {VelocityPrecision.Increment} m/s increments";
        }
    }
}