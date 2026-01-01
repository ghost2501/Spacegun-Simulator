using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Enemies
{
    // ============================================================================ 
    // ENEMY WAVE - Detection Phase Enemy Generation
    // ============================================================================

    public class EnemyWave
    {
        public int WaveNumber { get; set; }
        public List<EnemyTarget> Targets { get; set; } = new List<EnemyTarget>();

        /// <summary>
        /// Number of ships in this wave.
        /// Full mode only; Pure mode always uses 1.
        /// Note: Current gameplay is still single-target; this models future multi-ship waves.
        /// </summary>
        public int ShipCount { get; set; } = 1;

        public double InitialDistance { get; set; }
        public double CurrentDistance { get; set; }
        public double AverageVelocity { get; set; }
        public double AverageRadarCrossSection { get; set; }
        public bool HasStealthCoating { get; set; }

        /// <summary>
        /// Archetype for this wave (Scout, Balanced, Titan, Sniper, or Beachball for tutorial).
        /// </summary>
        public EnemyArchetype Archetype { get; set; } = null!;

        /// <summary>
        /// Approach elevation angle (generated during firing phase, not detection).
        /// Elevation: 0° = horizon, 90° = zenith, -90° = nadir
        /// </summary>
        public float ApproachElevation { get; set; }

        /// <summary>
        /// Approach azimuth bearing (generated during firing phase, not detection).
        /// Azimuth: 0° = North, 90° = East, 180° = South, 270° = West
        /// </summary>
        public float ApproachAzimuth { get; set; }

        /// <summary>
        /// Cached Cartesian position vector (X, Y, Z in meters).
        /// Computed from ApproachElevation, ApproachAzimuth, and engagement distance.
        /// MUST be persisted to ensure consistency across save/restore cycles.
        /// Uses the custom Vector3 struct from FiringSolution for precision.
        /// </summary>
        public Vector3? CachedEnemyPosition { get; set; }

        /// <summary>
        /// Cached enemy velocity vector (Vx, Vy, Vz in m/s).
        /// Derived from approach angles and AverageVelocity magnitude.
        /// MUST be persisted to ensure consistency across save/restore cycles.
        /// Uses the custom Vector3 struct from FiringSolution for precision.
        /// </summary>
        public Vector3? CachedEnemyVelocity { get; set; }

        /// <summary>
        /// Cached correct firing solution for save/restore.
        /// </summary>
        public float CachedCorrectLaunchDelayTime { get; set; }
        public float CachedCorrectElevation { get; set; }
        public float CachedCorrectAzimuth { get; set; }
        public float CachedCorrectVelocity { get; set; }

        /// <summary>
        /// Flag indicating this wave was restored from a save file (not freshly generated).
        /// Used to distinguish between new wave generation and restoration of saved waves.
        /// </summary>
        public bool IsRestoredFromSave { get; set; } = false;

        /// <summary>
        /// Flag indicating this is a tutorial wave with simplified physics.
        /// </summary>
        public bool IsTutorialWave { get; set; } = false;

        // ShipCount is the gameplay-relevant count (Full mode can spawn multi-ship waves).
        // Targets currently stores a single representative target used by firing-phase mechanics.
        public int TargetCount => Math.Max(1, ShipCount);
        public double TimeToImpact => AverageVelocity > 0 ? CurrentDistance / AverageVelocity : double.PositiveInfinity;

        public EnemyWave(int waveNumber)
        {
            WaveNumber = waveNumber;
        }

        // ====================================================================
        // TUTORIAL MODE SUPPORT
        // (tutorial constants removed from here; use DifficultyConfig as single source)
        // ====================================================================

        /// <summary>
        /// Generate a tutorial wave for the "Potato Cannons and Beachballs" difficulty.
        /// Uses simplified physics with human-scale distances and velocities.
        /// </summary>
        public static EnemyWave GenerateTutorialWave(int waveNumber, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Use canonical tutorial scenarios from DifficultyConfig
            int scenarioIndex = (waveNumber - 1) % DifficultyConfig.TutorialScenarios.All.Length;
            var scenario = DifficultyConfig.TutorialScenarios.All[scenarioIndex];

            var wave = new EnemyWave(waveNumber)
            {
                IsTutorialWave = true,
                Archetype = EnemyArchetype.Beachball,  // Use beachball archetype
                InitialDistance = scenario.StartDistanceMeters,
                CurrentDistance = scenario.StartDistanceMeters,
                AverageVelocity = scenario.ApproachSpeedMs,
                AverageRadarCrossSection = DifficultyConfig.TutorialBeachball.CrossSectionM2,
                HasStealthCoating = false,
                ApproachElevation = scenario.Elevation,
                ApproachAzimuth = scenario.Azimuth
            };

            // Create the beachball target using canonical values
            var target = new EnemyTarget
            {
                Name = $"Beachball ({scenario.Name}) #{waveNumber}",
                Altitude = scenario.ArcHeightMeters,
                Velocity = scenario.ApproachSpeedMs,
                CrossSection = DifficultyConfig.TutorialBeachball.CrossSectionM2,
                Maneuverability = DifficultyConfig.TutorialBeachball.Evasiveness,
                Mass = DifficultyConfig.TutorialBeachball.MassTons,
                FractureEnergy = DifficultyConfig.TutorialBeachball.FractureEnergyMJ
            };

            wave.Targets.Add(target);

            // Pre-calculate position and velocity vectors for this scenario
            CalculateTutorialVectors(wave, scenario);

            return wave;
        }

        /// <summary>
        /// Calculate the position and velocity vectors for a tutorial scenario.
        /// Uses simple geometry suitable for the tutorial's educational purpose.
        /// </summary>
        private static void CalculateTutorialVectors(EnemyWave wave, DifficultyConfig.TutorialScenarioData scenario)
        {
            // Convert angles to radians
            double elevationRad = scenario.Elevation * Math.PI / 180.0;
            double azimuthRad = scenario.Azimuth * Math.PI / 180.0;

            // Calculate initial position from spherical coordinates
            double horizontalDist = scenario.StartDistanceMeters * Math.Cos(elevationRad);
            double x = horizontalDist * Math.Sin(azimuthRad);
            double y = horizontalDist * Math.Cos(azimuthRad);
            double z = scenario.StartDistanceMeters * Math.Sin(elevationRad) + scenario.ArcHeightMeters;

            wave.CachedEnemyPosition = new Vector3(x, y, z);

            // Calculate velocity vector (approaching the origin)
            if (scenario.ApproachSpeedMs > 0)
            {
                double magnitude = Math.Sqrt(x * x + y * y + z * z);
                if (magnitude > 0)
                {
                    double vx = -scenario.ApproachSpeedMs * (x / magnitude);
                    double vy = -scenario.ApproachSpeedMs * (y / magnitude);
                    double vz = -scenario.ApproachSpeedMs * (z / magnitude);

                    wave.CachedEnemyVelocity = new Vector3(vx, vy, vz);
                }
                else
                {
                    wave.CachedEnemyVelocity = Vector3.Zero;
                }
            }
            else
            {
                wave.CachedEnemyVelocity = Vector3.Zero;
            }
        }

        // ====================================================================
        // WAVE GENERATION (non-tutorial)
        // ====================================================================

        /// <summary>
        /// Generate a wave with a given archetype.
        /// If no campaign enemy type provided, uses a random archetype.
        /// This method kept as the public entry used by GameState and pre-generation.
        /// </summary>
        public static EnemyWave GenerateWave(
            int waveNumber,
            Random rng,
            EnemyGenerationRuleset ruleset,
            EnemyType? campaignEnemyType = null)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // If we have a campaign enemy type (ongoing game), use it
            if (campaignEnemyType != null)
            {
                return GenerateWaveFromArchetype(waveNumber, campaignEnemyType.Archetype, rng, ruleset);
            }

            // Fallback: Generate with random archetype
            var archetype = EnemyArchetype.SelectRandom(rng);
            return GenerateWaveFromArchetype(waveNumber, archetype, rng, ruleset);
        }

        /// <summary>
        /// Generate a procedural enemy wave for detection phase from an archetype.
        /// </summary>
        public static EnemyWave GenerateWaveFromArchetype(
            int waveNumber,
            EnemyArchetype archetype,
            Random rng,
            EnemyGenerationRuleset ruleset)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            var wave = new EnemyWave(waveNumber);
            wave.Archetype = archetype;

            wave.ShipCount = ruleset == EnemyGenerationRuleset.Pure
                ? 1
                : GenerateShipCountForWave(waveNumber, rng);

            // ===== DETECTION PHASE GENERATION =====

            // Generate detection distance within tier's range
            wave.InitialDistance = tier.DetectionRangeMin +
                rng.NextDouble() * (tier.DetectionRangeMax - tier.DetectionRangeMin);
            wave.CurrentDistance = wave.InitialDistance;

            // ===== TIER-CONSTRAINED VELOCITY (NEW) =====
            // Get tier-specific velocity bounds - enemies must stay within these
            var (enemyMinVel, enemyMaxVel, _, _) = GameConstants.GetTierVelocityConstraints(tierIndex);

            // Generate velocity uniformly within tier constraints
            // NO archetype multiplier - velocity is determined by tier alone
            wave.AverageVelocity = enemyMinVel + rng.NextDouble() * (enemyMaxVel - enemyMinVel);

            // Generate target with stats
            var target = GenerateTargetFromArchetype(waveNumber, tierIndex, archetype, rng, ruleset);
            wave.Targets.Add(target);

            // FIX: Use consolidated BallisticsCalculator method instead of local duplicate
            double diameterMeters = BallisticsCalculator.CalculateDiameterFromMass(target.Mass);

            // Set BOTH wave average AND target's CrossSection
            wave.AverageRadarCrossSection = diameterMeters;  // RCS = Diameter
            target.CrossSection = diameterMeters;             // Set target's cross-section too
            // Pure mode: keep enemy stats as simple physical variables only.
            // No stealth / maneuver modifiers.
            if (ruleset == EnemyGenerationRuleset.Pure)
            {
                wave.HasStealthCoating = false;
                target.Maneuverability = 0.0;
            }
            else
            {
                wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;
            }

            // Calculate display information (timeToImpact maybe used elsewhere)
            double timeToImpactSeconds = wave.InitialDistance / wave.AverageVelocity;

            return wave;
        }

        /// <summary>
        /// Generate a target procedurally within archetype bounds.
        /// </summary>
        private static EnemyTarget GenerateTargetFromArchetype(
            int waveNumber,
            int tierIndex,
            EnemyArchetype archetype,
            Random rng,
            EnemyGenerationRuleset ruleset)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Select ship type from tier-appropriate pool
            string[] typePool = tierIndex switch
            {
                0 => GameConstants.EarlyTypes,
                1 => ConcatArrays(GameConstants.EarlyTypes, GameConstants.MidTypes),
                2 => ConcatArrays(GameConstants.MidTypes, GameConstants.LateTypes),
                _ => GameConstants.LateTypes
            };

            string type = typePool[rng.Next(typePool.Length)];

            // Full mode: additional enemy capability variables.
            // Pure mode: keep physical-only (all set to 0).
            double acceleration = 0.0;
            double maneuverability = 0.0;
            double defense = 0.0;
            double offense = 0.0;

            if (ruleset != EnemyGenerationRuleset.Pure)
            {
                var (accMin, accMax, manMax, defMax, offMax) = GetCapabilityRangesForTier(tierIndex);
                acceleration = accMin + rng.NextDouble() * (accMax - accMin);
                maneuverability = rng.NextDouble() * manMax;
                defense = rng.NextDouble() * defMax;
                offense = rng.NextDouble() * offMax;
            }

            // Generate mass and fracture energy WITHIN ARCHETYPE BOUNDS
            double waveProgression = Math.Min(1.0, waveNumber / 25.0); // 0-1 over campaign

            double mass = archetype.MassRange.Min +
                (rng.NextDouble() * (archetype.MassRange.Max - archetype.MassRange.Min)) +
                (waveProgression * (archetype.MassRange.Max - archetype.MassRange.Min) * 0.1);

            double fractureEnergy = archetype.FractureEnergyRange.Min +
                (rng.NextDouble() * (archetype.FractureEnergyRange.Max - archetype.FractureEnergyRange.Min)) +
                (waveProgression * (archetype.FractureEnergyRange.Max - archetype.FractureEnergyRange.Min) * 0.1);

            return new EnemyTarget
            {
                Name = $"{archetype.Name} #{rng.Next(100, 999)}",
                Altitude = 0,
                Velocity = 0,
                CrossSection = 0.0,  // Will be overwritten with diameter in GenerateWaveFromArchetype
                Acceleration = acceleration,
                Maneuverability = maneuverability,
                Defense = defense,
                Offense = offense,
                Mass = mass,
                FractureEnergy = fractureEnergy
            };
        }

        private static int GenerateShipCountForWave(int waveNumber, Random rng)
        {
            // Ship-count pattern by wave bucket (Tier 1..5):
            // 1-5: 1
            // 6-10: 1-2
            // 11-15: 1-3
            // 16-20: 1-4
            // 21-25+: 1-5
            // (Uses non-overlapping ranges; if waveNumber is out of bounds, clamp to the nearest bucket.)

            if (waveNumber <= 5) return 1;

            int max = waveNumber switch
            {
                <= 10 => 2,
                <= 15 => 3,
                <= 20 => 4,
                _ => 5
            };

            // rng.Next upper bound is exclusive
            return rng.Next(1, max + 1);
        }

        private static (double AccMin, double AccMax, double ManMax, double DefMax, double OffMax) GetCapabilityRangesForTier(int tierIndex)
        {
            // Full-mode capability ranges by tier.
            // Acceleration in m/s^2; Maneuverability/Defense are 0..1 factors.
            return tierIndex switch
            {
                0 => (AccMin: 0.00, AccMax: 0.00, ManMax: 0.00, DefMax: 0.00, OffMax: 0.00),
                1 => (AccMin: 0.05, AccMax: 0.25, ManMax: 0.15, DefMax: 0.10, OffMax: 0.10),
                2 => (AccMin: 0.10, AccMax: 0.50, ManMax: 0.30, DefMax: 0.20, OffMax: 0.20),
                3 => (AccMin: 0.20, AccMax: 1.00, ManMax: 0.45, DefMax: 0.35, OffMax: 0.35),
                _ => (AccMin: 0.35, AccMax: 1.75, ManMax: 0.60, DefMax: 0.50, OffMax: 0.50),
            };
        }

        /// <summary>
        /// Concatenate two arrays into one.
        /// </summary>
        private static T[] ConcatArrays<T>(T[] a, T[] b)
        {
            var result = new T[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
