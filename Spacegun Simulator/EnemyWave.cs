namespace Spacegun_Simulator
{
    // ============================================================================ 
    // ENEMY WAVE - Detection Phase Enemy Generation
    // ============================================================================
    // SIMPLIFIED: Generates ONLY detection-phase statistics.
    // 
    // Detection Phase generates:
    // - Velocity (m/s) - CONSTRAINED BY TIER BOUNDS
    // - Detection Distance
    // - ETA (Estimated Time to reach gun)
    // - Mass (metric tons)
    // - Diameter (meters) - used as Radar Cross-Section
    // - Fracture Energy (MJ)
    //
    // NO trajectory data generated here. Elevation/Azimuth/Cartesian position 
    // are generated during FIRING PHASE when engagement distance (500-600km) is known.
    //
    // TUTORIAL MODE: Uses simplified beachball scenarios with human-scale physics.

    public class EnemyWave
    {
        public int WaveNumber { get; set; }
        public List<EnemyTarget> Targets { get; set; } = new List<EnemyTarget>();

        public double InitialDistance { get; set; }
        public double CurrentDistance { get; set; }
        public double AverageVelocity { get; set; }
        public double AverageRadarCrossSection { get; set; }
        public double AverageEvasiveness { get; set; }
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

        public int TargetCount => Targets.Count;
        public double TimeToImpact => AverageVelocity > 0 ? CurrentDistance / AverageVelocity : double.PositiveInfinity;

        public EnemyWave(int waveNumber)
        {
            WaveNumber = waveNumber;
        }

        // ====================================================================
        // TUTORIAL MODE CONSTANTS
        // ====================================================================

        /// <summary>
        /// Tutorial beachball specifications.
        /// 2m diameter, very light, minimal fracture energy.
        /// </summary>
        public static class TutorialBeachball
        {
            public const double DiameterMeters = 2.0;
            public const double RadiusMeters = 1.0;  // Hit tolerance
            public const double MassKg = 0.5;  // 500g inflatable ball
            public const double MassTons = 0.0005;  // For archetype compatibility (metric tons)
            public const double FractureEnergyJoules = 50.0;  // Pop the ball (~50 J)
            public const double FractureEnergyMJ = 0.00005;  // 50 J = 0.00005 MJ
            public const double CrossSectionM2 = 3.14;  // π × r² ≈ π × 1²
            public const double Evasiveness = 0.0;  // No evasion, just floating
        }

        /// <summary>
        /// Tutorial potato cannon specifications.
        /// </summary>
        public static class TutorialPotatoCannon
        {
            public const double MuzzleVelocityMs = 50.0;  // ~112 mph, realistic
            public const double ProjectileMassKg = 0.3;  // ~300g potato
            public const double EffectiveRangeMeters = 150.0;  // Max effective range
        }

        /// <summary>
        /// Tutorial trajectory scenarios - varying difficulty within tutorial.
        /// Each scenario has round numbers for easy mental math.
        /// </summary>
        public static class TutorialScenarios
        {
            /// <summary>
            /// Scenario 1: Stationary target directly ahead.
            /// Simplest possible - just calculate range and velocity.
            /// </summary>
            public static readonly TutorialScenarioData Stationary = new()
            {
                Name = "Stationary Beachball",
                Description = "A beachball is floating motionless at 50 meters.",
                StartDistanceMeters = 50.0,
                ApproachSpeedMs = 0.0,
                ArcHeightMeters = 0.0,
                Elevation = 0.0f,
                Azimuth = 0.0f
            };

            /// <summary>
            /// Scenario 2: Slow approach, head-on.
            /// Introduces timing calculation.
            /// </summary>
            public static readonly TutorialScenarioData SlowApproach = new()
            {
                Name = "Slow Approach",
                Description = "A beachball is drifting toward you at 5 m/s from 100 meters.",
                StartDistanceMeters = 100.0,
                ApproachSpeedMs = 5.0,
                ArcHeightMeters = 0.0,
                Elevation = 0.0f,
                Azimuth = 0.0f
            };

            /// <summary>
            /// Scenario 3: Arc trajectory - introduces gravity and elevation.
            /// Beachball thrown in an arc, player must lead and elevate.
            /// </summary>
            public static readonly TutorialScenarioData ArcTrajectory = new()
            {
                Name = "Arc Trajectory",
                Description = "A beachball is arcing toward you - 100m away, 20m high, descending.",
                StartDistanceMeters = 100.0,
                ApproachSpeedMs = 10.0,
                ArcHeightMeters = 20.0,
                Elevation = 15.0f,  // Starts elevated
                Azimuth = 0.0f
            };

            /// <summary>
            /// Scenario 4: Crossing target - introduces azimuth calculation.
            /// Beachball moving across the field of view.
            /// </summary>
            public static readonly TutorialScenarioData CrossingTarget = new()
            {
                Name = "Crossing Target",
                Description = "A beachball is floating across your field of view from East to West.",
                StartDistanceMeters = 80.0,
                ApproachSpeedMs = 8.0,
                ArcHeightMeters = 10.0,
                Elevation = 10.0f,
                Azimuth = 90.0f  // Coming from East
            };

            /// <summary>
            /// Scenario 5: Full 3D intercept - combines all elements.
            /// Complete tutorial challenge before "graduating" to real game.
            /// </summary>
            public static readonly TutorialScenarioData Full3DIntercept = new()
            {
                Name = "Full 3D Intercept",
                Description = "Final challenge: A beachball arcing from the Northeast. Calculate all parameters!",
                StartDistanceMeters = 100.0,
                ApproachSpeedMs = 10.0,
                ArcHeightMeters = 20.0,
                Elevation = 20.0f,
                Azimuth = 45.0f  // Coming from Northeast
            };

            /// <summary>
            /// All tutorial scenarios in order of difficulty.
            /// </summary>
            public static readonly TutorialScenarioData[] All =
            {
                Stationary,
                SlowApproach,
                ArcTrajectory,
                CrossingTarget,
                Full3DIntercept
            };
        }

        /// <summary>
        /// Data structure for tutorial scenarios.
        /// </summary>
        public class TutorialScenarioData
        {
            public string Name { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public double StartDistanceMeters { get; init; }
            public double ApproachSpeedMs { get; init; }
            public double ArcHeightMeters { get; init; }
            public float Elevation { get; init; }
            public float Azimuth { get; init; }
        }

        // ====================================================================
        // WAVE GENERATION
        // ====================================================================

        /// <summary>
        /// Generate a wave with a given archetype.
        /// If no campaign enemy type provided, uses a random archetype.
        /// </summary>
        public static EnemyWave GenerateWave(int waveNumber, Random rng, EnemyType? campaignEnemyType = null)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // If we have a campaign enemy type (ongoing game), use it
            if (campaignEnemyType != null)
            {
                return GenerateWaveFromArchetype(waveNumber, campaignEnemyType.Archetype, rng);
            }

            // Fallback: Generate with random archetype
            var archetype = EnemyArchetype.SelectRandom(rng);
            return GenerateWaveFromArchetype(waveNumber, archetype, rng);
        }

        /// <summary>
        /// Generate a tutorial wave for the "Potato Cannons and Beachballs" difficulty.
        /// Uses simplified physics with human-scale distances and velocities.
        /// 
        /// TUTORIAL DESIGN:
        /// - All numbers are small, round, and easy to work with
        /// - 5 progressive scenarios teach different aspects of ballistic calculation
        /// - Fixed weapon (potato cannon) eliminates resource management
        /// - Generous hit tolerance (1m = beachball radius)
        /// </summary>
        public static EnemyWave GenerateTutorialWave(int waveNumber, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Select scenario based on wave number (cycles through 5 scenarios)
            int scenarioIndex = (waveNumber - 1) % TutorialScenarios.All.Length;
            var scenario = TutorialScenarios.All[scenarioIndex];

            var wave = new EnemyWave(waveNumber)
            {
                IsTutorialWave = true,
                Archetype = EnemyArchetype.Beachball,  // Use beachball archetype
                InitialDistance = scenario.StartDistanceMeters,
                CurrentDistance = scenario.StartDistanceMeters,
                AverageVelocity = scenario.ApproachSpeedMs,
                AverageRadarCrossSection = TutorialBeachball.CrossSectionM2,
                AverageEvasiveness = TutorialBeachball.Evasiveness,
                HasStealthCoating = false,
                ApproachElevation = scenario.Elevation,
                ApproachAzimuth = scenario.Azimuth
            };

            // Create the beachball target
            var target = new EnemyTarget
            {
                Name = $"Beachball ({scenario.Name}) #{waveNumber}",
                Altitude = scenario.ArcHeightMeters,
                Velocity = scenario.ApproachSpeedMs,
                CrossSection = TutorialBeachball.CrossSectionM2,
                Evasiveness = TutorialBeachball.Evasiveness,
                Mass = TutorialBeachball.MassTons,
                FractureEnergy = TutorialBeachball.FractureEnergyMJ
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
        private static void CalculateTutorialVectors(EnemyWave wave, TutorialScenarioData scenario)
        {
            // Convert angles to radians
            double elevationRad = scenario.Elevation * Math.PI / 180.0;
            double azimuthRad = scenario.Azimuth * Math.PI / 180.0;

            // Calculate initial position from spherical coordinates
            // X = distance * cos(elevation) * sin(azimuth)  [East-West]
            // Y = distance * cos(elevation) * cos(azimuth)  [North-South]
            // Z = distance * sin(elevation) + arcHeight     [Up-Down]
            double horizontalDist = scenario.StartDistanceMeters * Math.Cos(elevationRad);
            double x = horizontalDist * Math.Sin(azimuthRad);
            double y = horizontalDist * Math.Cos(azimuthRad);
            double z = scenario.StartDistanceMeters * Math.Sin(elevationRad) + scenario.ArcHeightMeters;

            wave.CachedEnemyPosition = new Vector3(x, y, z);

            // Calculate velocity vector (approaching the origin)
            // The beachball moves toward the gun, so velocity is opposite to position direction
            if (scenario.ApproachSpeedMs > 0)
            {
                // Normalize position vector and multiply by negative speed
                double magnitude = Math.Sqrt(x * x + y * y + z * z);
                if (magnitude > 0)
                {
                    double vx = -scenario.ApproachSpeedMs * (x / magnitude);
                    double vy = -scenario.ApproachSpeedMs * (y / magnitude);
                    // Z velocity includes arc descent (simplified: linear descent over flight time)
                    double flightTime = scenario.StartDistanceMeters / Math.Max(1.0, scenario.ApproachSpeedMs);
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
                // Stationary target
                wave.CachedEnemyVelocity = Vector3.Zero;
            }
        }

        /// <summary>
        /// Generate a procedural enemy wave for detection phase.
        /// 
        /// KEY CHANGE: Velocity is now constrained by tier bounds, not archetype multiplier.
        /// This ensures enemies stay within solvable ranges for their tier.
        /// 
        /// Generates:
        /// - Velocity (within tier bounds only)
        /// - Detection Distance (within tier bounds)
        /// - ETA (distance / velocity)
        /// - Mass (within archetype bounds)
        /// - Diameter (calculated from mass)
        /// - Radar Cross-Section = Diameter
        /// - Fracture Energy (within archetype bounds)
        /// - Evasiveness (within type bounds)
        /// 
        /// Trajectory data (elevation, azimuth, position) is generated during
        /// FIRING PHASE when engagement distance (1000-2000km) is known.
        /// </summary>
        public static EnemyWave GenerateWaveFromArchetype(int waveNumber, EnemyArchetype archetype, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            var wave = new EnemyWave(waveNumber);
            wave.Archetype = archetype;

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
            var target = GenerateTargetFromArchetype(waveNumber, tierIndex, archetype, rng);
            wave.Targets.Add(target);

            // FIX: Use consolidated BallisticsCalculator method instead of local duplicate
            double diameterMeters = BallisticsCalculator.CalculateDiameterFromMass(target.Mass);

            // Set BOTH wave average AND target's CrossSection
            wave.AverageRadarCrossSection = diameterMeters;  // RCS = Diameter
            target.CrossSection = diameterMeters;             // Set target's cross-section too
            wave.AverageEvasiveness = target.Evasiveness;
            wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;

            // Calculate display information
            double timeToImpactSeconds = wave.InitialDistance / wave.AverageVelocity;

            return wave;
        }

        /// <summary>
        /// Generate a target procedurally within archetype bounds.
        /// </summary>
        private static EnemyTarget GenerateTargetFromArchetype(int waveNumber, int tierIndex, EnemyArchetype archetype, Random rng)
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

            // Get evasiveness range for this type
            double evasiveness = 0.35;
            if (GameConstants.EvasivenessRanges.TryGetValue(type, out var er))
            {
                evasiveness = er.Item1 + rng.NextDouble() * (er.Item2 - er.Item1);
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
                Name = $"{archetype.Name} ({type}) #{rng.Next(100, 999)}",
                Altitude = 0,
                Velocity = 0,
                CrossSection = 0.0,  // Will be overwritten with diameter in GenerateWaveFromArchetype
                Evasiveness = evasiveness,
                Mass = mass,
                FractureEnergy = fractureEnergy
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
