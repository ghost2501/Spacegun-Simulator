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
        /// Archetype for this wave (Scout, Balanced, Titan, Sniper).
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

        public int TargetCount => Targets.Count;
        public double TimeToImpact => AverageVelocity > 0 ? CurrentDistance / AverageVelocity : double.PositiveInfinity;

        public EnemyWave(int waveNumber)
        {
            WaveNumber = waveNumber;
        }

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

            // Calculate diameter from mass - this IS the radar cross-section
            double diameterMeters = CalculateDiameterFromMass(target.Mass);

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
        /// Calculate ship diameter from mass assuming standard density.
        /// Assumes spherical vessel with density ~500 kg/m³ (similar to space-grade alloys)
        /// Formula: Volume = Mass / Density, then Diameter = 2 * ∛(3V/4π)
        /// 
        /// This diameter is used as the Radar Cross-Section in detection phase
        /// and as the basis for hit tolerance calculation in firing phase.
        /// </summary>
        private static double CalculateDiameterFromMass(double massTons)
        {
            const double STANDARD_DENSITY = 500.0;  // kg/m³
            const double TONS_TO_KG = 1000.0;

            double massKg = massTons * TONS_TO_KG;
            double volumeM3 = massKg / STANDARD_DENSITY;
            double radiusM = Math.Pow(3.0 * volumeM3 / (4.0 * Math.PI), 1.0 / 3.0);
            double diameterM = radiusM * 2.0;

            return diameterM;
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
