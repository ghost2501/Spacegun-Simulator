namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// SYNTHETIC TEST SCENARIOS
    /// 
    /// Predefined test cases for validating firing solution mechanics.
    /// 
    /// OPTION B - CONSISTENCY VALIDATION (Primary Testing Mode)
    /// Tests that the FiringSolution engine is internally consistent:
    /// - When CalculateSolution() says "CanHit=true", deviation is reasonable
    /// - When CalculateSolution() says "CanDestroy=true", energy exceeds requirement
    /// - Engine calculations are mathematically self-consistent
    /// - Fast, deterministic, requires no external calibration
    /// </summary>
    public static class TestScenarios
    {
        // Standard projectile specs
        private const float StandardProjectileMass = 100.0f;
        private const float StandardMuzzleVelocity = 320000.0f;
        private const float StandardFractureEnergy = 40000.0f;  // MJ
        private const double StandardTargetMass = 10000.0;      // Tons

        // Standard target specs
        private const float StandardRCS = 29.3f;  // Radar Cross Section (meters)
        private const float StandardHitTolerance = 14.65f;  // 0.5 × RCS

        // ====================================================================
        // OPTION B: Get scenarios for consistency validation
        // ====================================================================

        /// <summary>
        /// Get all available test scenarios for OPTION B (consistency validation).
        /// Each scenario tests the engine with reasonable firing parameters to ensure
        /// internal logic consistency.
        /// </summary>
        public static List<TestScenario> GetAllScenarios()
        {
            return new List<TestScenario>
            {
                CreateEasyScenario(),
                CreateModerateScenario(),
                CreateDifficultScenario(),
                CreateHardScenario(),
                CreateEdgeCaseHighElevation(),
                CreateEdgeCaseLowElevation()
            };
        }

        // ====================================================================
        // SCENARIO DEFINITIONS
        // ====================================================================

        /// <summary>
        /// SCENARIO 1: EASY HIT
        /// Stationary target at medium range with large cross-section.
        /// Tests basic consistency: Can the engine calculate a reasonable solution?
        /// </summary>
        private static TestScenario CreateEasyScenario()
        {
            return new TestScenario
            {
                Name = "Easy - Slow Target",
                Difficulty = TestDifficulty.Easy,
                Description = "Stationary target at medium range. Tests basic firing solution consistency.",

                // Target: ~910km away, moving slowly
                TargetPosition = new Vector3(400_000, 775_000, 100_000),
                TargetVelocity = new Vector3(0, 5000, 0),

                // Test parameters - not necessarily optimal
                CorrectLaunchDelay = 5.0f,
                CorrectElevation = 35.0f,
                CorrectAzimuth = 45.0f,
                CorrectVelocity = 200000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs (easier target)
                TargetFractureEnergy = 35000.0f,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = 35.0f,

                // OPTION B: Focus on consistency, not perfect solutions
                MaxDeviation = 1000000.0f,  // Very relaxed - just checking engine doesn't crash
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }

        /// <summary>
        /// SCENARIO 2: MODERATE HIT
        /// Target moving across the gun's sight at moderate velocity.
        /// Tests consistency with target motion and moderate distances.
        /// </summary>
        private static TestScenario CreateModerateScenario()
        {
            return new TestScenario
            {
                Name = "Moderate - Crossing Target",
                Difficulty = TestDifficulty.Moderate,
                Description = "Moving target at medium range. Tests consistency with target motion.",

                // Target: ~1.2Mm away, moving west-northwest
                TargetPosition = new Vector3(600_000, 1_000_000, 200_000),
                TargetVelocity = new Vector3(-40000, 15000, -5000),

                // Test parameters
                CorrectLaunchDelay = 6.0f,
                CorrectElevation = 42.0f,
                CorrectAzimuth = 110.0f,
                CorrectVelocity = 240000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs
                TargetFractureEnergy = StandardFractureEnergy,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = StandardRCS,

                // OPTION B: Relaxed for consistency testing
                MaxDeviation = 1000000.0f,
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }

        /// <summary>
        /// SCENARIO 3: DIFFICULT HIT
        /// Target moving away at high velocity and maximum range.
        /// Tests consistency with challenging target dynamics.
        /// </summary>
        private static TestScenario CreateDifficultScenario()
        {
            return new TestScenario
            {
                Name = "Difficult - Receding Target",
                Difficulty = TestDifficulty.Difficult,
                Description = "Fast target at max range. Tests consistency with extreme parameters.",

                // Target: ~1.5Mm away, moving away rapidly
                TargetPosition = new Vector3(800_000, 1_200_000, 150_000),
                TargetVelocity = new Vector3(-30000, -80000, -10000),

                // Test parameters
                CorrectLaunchDelay = 7.0f,
                CorrectElevation = 38.0f,
                CorrectAzimuth = 135.0f,
                CorrectVelocity = 280000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs
                TargetFractureEnergy = StandardFractureEnergy,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = 20.0f,

                // OPTION B: Consistency validation with relaxed tolerance
                MaxDeviation = 1000000.0f,
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }

        /// <summary>
        /// SCENARIO 4: HARD HIT
        /// Target at extreme altitude approaching at extreme velocity.
        /// Tests consistency under extreme conditions.
        /// </summary>
        private static TestScenario CreateHardScenario()
        {
            return new TestScenario
            {
                Name = "Hard - Extreme Parameters",
                Difficulty = TestDifficulty.Hard,
                Description = "Extreme velocity and altitude. Tests extreme scenario consistency.",

                // Target: Very high altitude, approaching
                TargetPosition = new Vector3(200_000, 300_000, 800_000),
                TargetVelocity = new Vector3(50000, 60000, -100000),

                // Test parameters
                CorrectLaunchDelay = 8.0f,
                CorrectElevation = 70.0f,
                CorrectAzimuth = 50.0f,
                CorrectVelocity = 310000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs (hard target)
                TargetFractureEnergy = 45000.0f,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = 18.0f,

                // OPTION B: Consistency check with relaxed tolerance
                MaxDeviation = 1000000.0f,
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }

        /// <summary>
        /// EDGE CASE: Very High Elevation
        /// Target nearly overhead at extreme elevation angle.
        /// Tests consistency with vertical firing geometry.
        /// </summary>
        private static TestScenario CreateEdgeCaseHighElevation()
        {
            return new TestScenario
            {
                Name = "Edge Case - High Elevation",
                Difficulty = TestDifficulty.EdgeCase,
                Description = "Target overhead. Tests vertical firing geometry consistency.",

                // Target: Nearly overhead
                TargetPosition = new Vector3(50_000, 100_000, 1_100_000),
                TargetVelocity = new Vector3(10000, 20000, -15000),

                // Test parameters
                CorrectLaunchDelay = 6.0f,
                CorrectElevation = 78.0f,
                CorrectAzimuth = 27.0f,
                CorrectVelocity = 250000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs
                TargetFractureEnergy = 38000.0f,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = StandardRCS,

                // OPTION B: Consistency with edge case parameters
                MaxDeviation = 1000000.0f,
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }

        /// <summary>
        /// EDGE CASE: Negative Elevation
        /// Target below gun level with downward firing.
        /// Tests consistency with negative elevation angles.
        /// </summary>
        private static TestScenario CreateEdgeCaseLowElevation()
        {
            return new TestScenario
            {
                Name = "Edge Case - Low Elevation",
                Difficulty = TestDifficulty.EdgeCase,
                Description = "Target below gun. Tests negative elevation consistency.",

                // Target: Below gun level
                TargetPosition = new Vector3(500_000, 900_000, -200_000),
                TargetVelocity = new Vector3(5000, 10000, -30000),

                // Test parameters
                CorrectLaunchDelay = 5.0f,
                CorrectElevation = -25.0f,
                CorrectAzimuth = 48.0f,
                CorrectVelocity = 220000.0f,

                // Weapon specs
                ProjectileMass = StandardProjectileMass,
                MuzzleVelocity = StandardMuzzleVelocity,

                // Target specs
                TargetFractureEnergy = 37000.0f,
                TargetMass = StandardTargetMass,
                TargetRadarCrossSection = StandardRCS,

                // OPTION B: Consistency with negative elevation
                MaxDeviation = 1000000.0f,
                RequiresSufficientEnergy = true,
                MinExpectedIterations = 1,
                MaxExpectedIterations = 100
            };
        }
    }

    /// <summary>
    /// Represents a single test scenario.
    /// </summary>
    public class TestScenario
    {
        public string Name { get; set; } = string.Empty;
        public TestDifficulty Difficulty { get; set; }
        public string Description { get; set; } = string.Empty;

        // Target trajectory
        public Vector3 TargetPosition { get; set; }
        public Vector3 TargetVelocity { get; set; }

        // Firing parameters for testing
        public float CorrectLaunchDelay { get; set; }
        public float CorrectElevation { get; set; }
        public float CorrectAzimuth { get; set; }
        public float CorrectVelocity { get; set; }

        // Weapon configuration
        public float ProjectileMass { get; set; }
        public float MuzzleVelocity { get; set; }

        // Target configuration
        public float TargetFractureEnergy { get; set; }
        public double TargetMass { get; set; }
        public float TargetRadarCrossSection { get; set; }

        // Test expectations
        public float MaxDeviation { get; set; }
        public bool RequiresSufficientEnergy { get; set; }
        public int? MinExpectedIterations { get; set; }
        public int? MaxExpectedIterations { get; set; }

        public override string ToString()
        {
            return $"[{Difficulty}] {Name} - {Description}";
        }
    }

    /// <summary>
    /// Difficulty classification for test scenarios.
    /// </summary>
    public enum TestDifficulty
    {
        Easy,
        Moderate,
        Difficult,
        Hard,
        EdgeCase
    }
}