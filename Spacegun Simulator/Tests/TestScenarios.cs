using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

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
    public static partial class TestScenarios
    {
        // Standard projectile specs
        private const float StandardProjectileMass = 100.0f;

        // Use canonical value from GameConstants (tier 3 as the "standard" test velocity).
        // Falls back to previous hard-coded value if the array is missing.
        private static readonly float StandardMuzzleVelocity =
            (float)(GameConstants.WeaponsTechBaseVelocity.Length > 2
                ? GameConstants.WeaponsTechBaseVelocity[2]
                : 320_000.0);

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

        // ====================================================================
        // WEAPONS TECH AUDIT SCENARIOS
        // ====================================================================

        /// <summary>
        /// Generate a set of scenarios to audit weapon tech levels and
        /// projectile/propulsion upgrades. Uses a single fixed target so
        /// results are directly comparable between runs.
        /// 
        /// Scenarios vary:
        ///  - Weapons Tech (base muzzle velocity): L1/L2/L3
        ///  - Delta-V "upgrades" applied on top of the gun base
        ///  - Projectile core masses (light/standard/heavy/ultra)
        /// 
        /// Unlimited resources are assumed for the audit; the harness will only
        /// evaluate ballistic results and write them to disk.
        /// </summary>
        public static List<TestScenario> GetTechAuditScenarios()
        {
            var scenarios = new List<TestScenario>();

            // Fixed target (easy to compare across runs)
            var fixedTargetPosition = new Vector3(0, 500_000, 0);   // 500 km out on Y axis
            var fixedTargetVelocity = new Vector3(0, 0, 0);         // stationary
            const double fixedTargetMass = 1000.0;                  // tons
            const float fixedRcs = 10.0f;                           // m^2

            // Compute fracture energy from mass so value is derived not hard-coded.
            // Uses a reference velocity and scale factor so returned MJ are in a sensible gameplay range.
            float computedFractureEnergy = (float)ComputeFractureEnergyMJ(fixedTargetMass);

            // Define weapons tech base muzzle velocities (m/s) - use canonical values.
            // Build tuples (TechLevel, BaseMs) dynamically from GameConstants to avoid duplication.
            var weaponBases = GameConstants.WeaponsTechBaseVelocity
                .Select((v, idx) => (TechLevel: idx + 1, BaseMs: v))
                .ToArray();

            // Delta-V "upgrades" to simulate propulsion options (m/s)
            double[] deltaVs = { 0, 20_000, 40_000, 80_000 };

            // Core masses (kg) representing projectile cores
            var cores = new (string Id, double MassKg)[]
            {
                ("light", 10.0),
                ("standard", 15.0),
                ("heavy", 25.0),
                ("ultra", 50.0)
            };

            int idx = 1;
            foreach (var wb in weaponBases)
            {
                foreach (var dv in deltaVs)
                {
                    foreach (var core in cores)
                    {
                        double finalMuzzle = wb.BaseMs + dv;

                        var scenario = new TestScenario
                        {
                            Name = $"TechAudit #{idx++} - W{wb.TechLevel} DV+{(int)(dv/1000)}km/s Core:{core.Id}",
                            Difficulty = TestDifficulty.Moderate,
                            Description = $"WeaponsTech L{wb.TechLevel}, +{dv:N0} m/s delta-V, core {core.Id} ({core.MassKg} kg).",

                            // Fixed target
                            TargetPosition = fixedTargetPosition,
                            TargetVelocity = fixedTargetVelocity,

                            // Test parameters: use the final muzzle as the launch velocity for consistency
                            CorrectLaunchDelay = 0.0f,
                            CorrectElevation = 45.0f, // fire at 45°
                            CorrectAzimuth = 0.0f,    // fire at 0°
                            CorrectVelocity = (float)finalMuzzle,

                            // Weapon configuration
                            ProjectileMass = (float)core.MassKg,
                            MuzzleVelocity = (float)finalMuzzle,

                            // Target configuration (computed from mass so it isn't always the same constant)
                            TargetFractureEnergy = computedFractureEnergy,
                            TargetMass = fixedTargetMass,
                            TargetRadarCrossSection = fixedRcs,

                            // Audit metadata for CSV output
                            TechLevel = wb.TechLevel,
                            BaseMuzzleVelocityMs = wb.BaseMs,
                            DeltaVMs = dv,
                            CoreType = core.Id,

                            // Expectations are intentionally permissive; this audit is for comparison
                            MaxDeviation = 1_000_000f,
                            RequiresSufficientEnergy = false,
                            MinExpectedIterations = 0,
                            MaxExpectedIterations = 0
                        };

                        scenarios.Add(scenario);
                    }
                }
            }

            return scenarios;
        }

        /// <summary>
        /// Compute a target fracture energy (MJ) from mass (tons).
        /// Method:
        ///  - Convert tons -> kg
        ///  - Compute kinetic energy at a sensible reference velocity (1000 m/s)
        ///  - Scale that energy by a small factor so fracture values are in a gameplay-friendly range
        /// The constants (refVelocity, scale) are chosen so 1000 tons -> ~10,000 MJ (matches previous constant),
        /// but the value will vary if mass changes.
        /// </summary>
        private static double ComputeFractureEnergyMJ(double massTons)
        {
            const double referenceVelocityMs = 1000.0; // reference speed for mapping mass -> energy
            const double scale = 0.02;                 // factor to convert KE@refVel -> fracture energy MJ

            double massKg = massTons * 1000.0;
            double keMJ = BallisticsCalculator.CalculateKineticEnergyMJ(massKg, referenceVelocityMs);

            double fractureMJ = keMJ * scale;
            // Floor the value to a sensible minimum
            return Math.Max(100.0, fractureMJ);
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

        // ===== NEW: Audit metadata =====
        public int TechLevel { get; set; } = 1;
        public double BaseMuzzleVelocityMs { get; set; } = 0.0;
        public double DeltaVMs { get; set; } = 0.0;
        public string CoreType { get; set; } = string.Empty;

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