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
        /// Number of threats (projectiles/objects) in this wave.
        /// Full mode only; Pure mode always uses 1.
        /// Note: Current gameplay is still single-target; this models future multi-threat waves.
        /// </summary>
        public int ThreatCount { get; set; } = 1;

        public double InitialDistance { get; set; }
        public double CurrentDistance { get; set; }
        public double AverageVelocity { get; set; }
        public double AverageRadarCrossSection { get; set; }
        public bool HasStealthCoating { get; set; }

        /// <summary>
        /// Archetype for this wave (Needle, Slug, Boulder, RKV, or Beachball for tutorial).
        /// </summary>
        public EnemyArchetype Archetype { get; set; } = null!;

        /// <summary>
        /// Doctrine for this wave (soft modifiers layered on top of archetype + tier).
        /// Typically matches the campaign's primary doctrine.
        /// </summary>
        public EnemyDoctrine Doctrine { get; set; } = EnemyDoctrine.None;

        public EnemyDoctrineSource DoctrineSource { get; set; } = EnemyDoctrineSource.None;

        public string DoctrineName => EnemyDoctrineProfile.Get(Doctrine).Name;
        public string DoctrineDescription => EnemyDoctrineProfile.Get(Doctrine).Description;
        public bool IsGuestDoctrine => DoctrineSource == EnemyDoctrineSource.Guest;

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

        // ThreatCount is the gameplay-relevant count (Full mode can spawn multi-target waves).
        // Targets currently stores a single representative target used by firing-phase mechanics.
        public int TargetCount => Math.Max(1, ThreatCount);
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
                Archetype = EnemyArchetypeCatalog.GetById("beachball"),
                Doctrine = EnemyDoctrine.None,
                DoctrineSource = EnemyDoctrineSource.None,
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
                var tier = GameConstants.GetTierForWave(waveNumber);
                int tierIndex = tier.TierIndex;

                EnemyDoctrine doctrine = campaignEnemyType.PrimaryDoctrine;
                EnemyDoctrineSource source = EnemyDoctrineSource.Campaign;

                // Rare "guest doctrine" injections for variety and soft-counter spikes.
                // Keep probability low to avoid strategy whiplash.
                if (ruleset != EnemyGenerationRuleset.Pure
                    && doctrine != EnemyDoctrine.None
                    && tierIndex >= 1)
                {
                    double guestChance = tierIndex switch
                    {
                        1 => 0.05,
                        2 => 0.08,
                        _ => 0.10
                    };

                    if (rng.NextDouble() < guestChance)
                    {
                        doctrine = EnemyDoctrineSelector.SelectGuestDoctrine(campaignEnemyType.PrimaryDoctrine, campaignEnemyType.Archetype, rng);
                        if (doctrine != EnemyDoctrine.None && doctrine != campaignEnemyType.PrimaryDoctrine)
                            source = EnemyDoctrineSource.Guest;
                        else
                            doctrine = campaignEnemyType.PrimaryDoctrine;
                    }
                }

                return GenerateWaveFromArchetype(waveNumber, campaignEnemyType.Archetype, rng, ruleset, doctrine, source);
            }

            // Fallback: Generate with random archetype
            var archetype = EnemyArchetypeCatalog.SelectRandom(rng);
            return GenerateWaveFromArchetype(waveNumber, archetype, rng, ruleset, EnemyDoctrine.None, EnemyDoctrineSource.None);
        }

        /// <summary>
        /// Generate a procedural enemy wave for detection phase from an archetype.
        /// </summary>
        public static EnemyWave GenerateWaveFromArchetype(
            int waveNumber,
            EnemyArchetype archetype,
            Random rng,
            EnemyGenerationRuleset ruleset,
            EnemyDoctrine doctrine,
            EnemyDoctrineSource doctrineSource)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            var wave = new EnemyWave(waveNumber);
            wave.Archetype = archetype;
            wave.Doctrine = doctrine;
            wave.DoctrineSource = doctrine == EnemyDoctrine.None ? EnemyDoctrineSource.None : doctrineSource;

            var doctrineProfile = EnemyDoctrineProfile.Get(doctrine);

            wave.ThreatCount = ruleset == EnemyGenerationRuleset.Pure
                ? 1
                : GenerateThreatCountForWave(waveNumber, rng);

            // Doctrine: Swarm increases threat-count pressure.
            // Note: still capped by existing wave bucket logic.
            if (ruleset != EnemyGenerationRuleset.Pure && doctrine == EnemyDoctrine.Swarm)
                wave.ThreatCount = Math.Clamp(wave.ThreatCount + 1, 1, 6);

            // ===== DETECTION PHASE GENERATION =====

            // Generate detection distance within tier's range
            wave.InitialDistance = tier.DetectionRangeMin +
                rng.NextDouble() * (tier.DetectionRangeMax - tier.DetectionRangeMin);
            wave.CurrentDistance = wave.InitialDistance;

            // ===== TIER-CONSTRAINED VELOCITY (NEW) =====
            // Get tier-specific velocity bounds - enemies must stay within these
            var (enemyMinVel, enemyMaxVel) = GameConstants.GetTierEnemyVelocityConstraints(tierIndex);

            // Generate velocity uniformly within tier constraints
            // NO archetype multiplier - velocity is determined by tier alone
            double rawVel = enemyMinVel + rng.NextDouble() * (enemyMaxVel - enemyMinVel);
            wave.AverageVelocity = rawVel * doctrineProfile.VelocityMultiplier;

            // Generate target with stats
            var target = GenerateTargetFromArchetype(waveNumber, tierIndex, archetype, rng, ruleset, doctrineProfile);
            wave.Targets.Add(target);

            // Canonical meaning: CrossSection is radar cross-sectional AREA in m^2.
            // This is derived from tier-sampled mass + density inside GenerateTargetFromArchetype.
            wave.AverageRadarCrossSection = target.CrossSection * doctrineProfile.RadarCrossSectionMultiplier;
            // Pure mode: keep enemy stats as simple physical variables only.
            // No stealth / maneuver modifiers.
            if (ruleset == EnemyGenerationRuleset.Pure)
            {
                wave.HasStealthCoating = false;
                target.Maneuverability = 0.0;
            }
            else
            {
                double stealthChance = GameConstants.StealthChanceForLateTiers * doctrineProfile.StealthChanceMultiplier;
                wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < Math.Clamp(stealthChance, 0.0, 1.0);
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
            EnemyGenerationRuleset ruleset,
            EnemyDoctrineProfile doctrine)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Select threat designation from tier-appropriate pool
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

                // Apply doctrine multipliers (soft counters).
                acceleration *= doctrine.AccelerationMultiplier;
                maneuverability *= doctrine.ManeuverabilityMultiplier;
                defense *= doctrine.DefenseMultiplier;
                offense *= doctrine.OffenseMultiplier;

                // Keep factors in reasonable bounds.
                maneuverability = Math.Clamp(maneuverability, 0.0, 1.0);

                // Late tiers should never have "free" zero-evasion targets.
                // This supports the intended curve where unmodded shots cannot reliably win at Tier 3+.
                double maneuverabilityFloor = tierIndex switch
                {
                    3 => 0.18,
                    >= 4 => 0.22,
                    _ => 0.0
                };
                if (maneuverabilityFloor > 0.0)
                    maneuverability = Math.Max(maneuverability, maneuverabilityFloor);

                defense = Math.Clamp(defense, 0.0, 1.0);
                offense = Math.Clamp(offense, 0.0, 1.0);
            }

            // Tier-derived base properties (single-source physics).
            // Archetype does not constrain mass/density/strength; it remains a behavioral/flavor label.
            static double SampleTierRange(double[] mins, double[] maxs, int index, Random r)
            {
                if (mins is null || maxs is null || mins.Length == 0 || maxs.Length == 0) return 0.0;
                int safe = Math.Clamp(index, 0, Math.Min(mins.Length, maxs.Length) - 1);
                double min = mins[safe];
                double max = maxs[safe];
                if (max < min) (min, max) = (max, min);
                return min + r.NextDouble() * (max - min);
            }

            var mat = DevelopmentTuning.TierTargetMaterial;
            double massTons = SampleTierRange(mat.TierEnemyMassTonsMin, mat.TierEnemyMassTonsMax, tierIndex, rng);
            double densityKgM3 = SampleTierRange(mat.TierEnemyDensityKgM3Min, mat.TierEnemyDensityKgM3Max, tierIndex, rng);
            double bulkModulusGpa = SampleTierRange(mat.TierEnemyBulkModulusGpaMin, mat.TierEnemyBulkModulusGpaMax, tierIndex, rng);

            // Derived quantities.
            double crossSectionM2 = BallisticsCalculator.CalculateCrossSectionAreaM2FromMassAndDensity(massTons, densityKgM3);
            double fractureEnergyMJ = BallisticsCalculator.CalculateFractureEnergyMJFromMassDensityAndBulkModulus(
                massTons,
                densityKgM3,
                bulkModulusGpa,
                mat.FractureStrain);

            return new EnemyTarget
            {
                Name = $"{archetype.Name} #{rng.Next(100, 999)}",
                Altitude = 0,
                Velocity = 0,
                CrossSection = crossSectionM2,
                Acceleration = acceleration,
                Maneuverability = maneuverability,
                Defense = defense,
                Offense = offense,
                Mass = massTons,
                DensityKgM3 = densityKgM3,
                BulkModulusGpa = bulkModulusGpa,
                FractureEnergy = fractureEnergyMJ
            };
        }

        private static int GenerateThreatCountForWave(int waveNumber, Random rng)
        {
            // Threat-count pattern by wave bucket (Tier 1..5):
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
                3 => (AccMin: 0.20, AccMax: 1.00, ManMax: 0.35, DefMax: 0.35, OffMax: 0.35),
                _ => (AccMin: 0.35, AccMax: 1.75, ManMax: 0.45, DefMax: 0.50, OffMax: 0.50),
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
