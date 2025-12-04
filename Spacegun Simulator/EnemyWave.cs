using System;
using System.Collections.Generic;
using System.Linq;

namespace Spacegun_Simulator
{
    // ============================================================================ 
    // ENEMY WAVE - Single target per wave
    // ============================================================================

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

        // ====== NEW: Archetype data for ballistics ======
        public EnemyArchetype Archetype { get; set; } = null!;

        // ====== NEW: 3D approach vector in sky coordinates ======
        /// <summary>
        /// Elevation angle in degrees (30° = low approach, 150° = steep overhead).
        /// Represents the direction from which enemy approaches.
        /// </summary>
        public float ApproachElevation { get; set; }

        /// <summary>
        /// Azimuth bearing in degrees (0° = North, 90° = East, 180° = South, 270° = West).
        /// Represents the compass direction from which enemy approaches.
        /// </summary>
        public float ApproachAzimuth { get; set; }

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

            // Fallback: Generate with random archetype (for backward compatibility)
            var archetype = EnemyArchetype.SelectRandom(rng);
            return GenerateWaveFromArchetype(waveNumber, archetype, rng);
        }

        /// <summary>
        /// Generate a procedural enemy within the bounds of a given archetype.
        /// VALIDATES that the generated wave satisfies all playability constraints.
        /// Regenerates until a valid, beatable wave is produced.
        /// </summary>
        public static EnemyWave GenerateWaveFromArchetype(int waveNumber, EnemyArchetype archetype, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            Console.WriteLine($"\n[WAVE GEN] Generating Wave {waveNumber} (Tier {tierIndex}) with archetype: {archetype.Name}");

            // Intercept time constraints for this tier
            float minInterceptTime = 2f;
            float maxInterceptTime = tierIndex switch
            {
                0 => 15f,    // Early game: 2-15 seconds
                1 => 30f,    // Mid game: 2-30 seconds
                2 => 60f,    // Late game: 2-60 seconds
                _ => 60f     // Default to late game
            };

            // Attempt to generate valid wave (max 100 attempts)
            const int maxAttempts = 100;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var wave = new EnemyWave(waveNumber);
                wave.Archetype = archetype;

                // Generate detection distance within tier's range
                wave.InitialDistance = tier.DetectionRangeMin + 
                    rng.NextDouble() * (tier.DetectionRangeMax - tier.DetectionRangeMin);
                wave.CurrentDistance = wave.InitialDistance;

                // Generate velocity: base tier velocity × archetype multiplier
                double baseTierVelocity = tier.VelocityMin + 
                    rng.NextDouble() * (tier.VelocityMax - tier.VelocityMin);
                wave.AverageVelocity = baseTierVelocity * archetype.VelocityMultiplier;

                // Generate approach angles
                float minElevation = 30f + (tierIndex * 10f);
                float maxElevation = 60f + (tierIndex * 30f);
                minElevation = Math.Max(30f, Math.Min(minElevation, 150f));
                maxElevation = Math.Max(minElevation, Math.Min(maxElevation, 150f));
                
                wave.ApproachElevation = (float)(minElevation + rng.NextDouble() * (maxElevation - minElevation));
                wave.ApproachAzimuth = (float)(rng.NextDouble() * 360.0);

                // Generate target
                var target = GenerateTargetFromArchetype(waveNumber, tierIndex, archetype, rng);
                wave.Targets.Add(target);

                wave.AverageRadarCrossSection = target.CrossSection;
                wave.AverageEvasiveness = target.Evasiveness;
                wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;

                // ===== VALIDATION: Check if this wave is playable =====
                if (!IsWavePlayable(wave, tier, minInterceptTime, maxInterceptTime))
                {
                    Console.WriteLine($"  [Attempt {attempt + 1}/{maxAttempts}] ✗ Wave rejected: unplayable ballistic constraints");
                    continue;
                }

                // Wave passed validation
                double timeToImpactSeconds = wave.InitialDistance / wave.AverageVelocity;
                Console.WriteLine($"  [Attempt {attempt + 1}/{maxAttempts}] ✓ VALID WAVE");
                Console.WriteLine($"    Elev: {wave.ApproachElevation:F1}°, Azim: {wave.ApproachAzimuth:F1}°, Vel: {wave.AverageVelocity:F0} m/s");
                Console.WriteLine($"    Time to impact: {GameConstants.FormatTime(timeToImpactSeconds)}");
                Console.WriteLine($"    Target mass: {target.Mass:F0} tons, Fracture energy: {target.FractureEnergy:F0} MJ");

                return wave;
            }

            // Fallback: After 100 attempts, throw error indicating balance problem
            throw new InvalidOperationException(
                $"[WAVE GEN] Failed to generate valid wave {waveNumber} after {maxAttempts} attempts. " +
                $"Archetype velocity/energy parameters may be incompatible with tier intercept constraints. " +
                $"Consider adjusting archetype multipliers or tier time windows.");
        }

        /// <summary>
        /// Validate that a generated wave satisfies all playability constraints.
        /// Checks that the enemy horizon crossing time allows for valid intercepts.
        /// </summary>
        private static bool IsWavePlayable(EnemyWave wave, GameConstants.WaveTier tier, float minInterceptTime, float maxInterceptTime)
        {
            // Calculate 3D position and velocity from approach angles
            Vector3 enemyPosition = FiringSolution.AnglesToCartesian(
                wave.ApproachElevation, 
                wave.ApproachAzimuth, 
                (float)wave.CurrentDistance);

            // Decompose velocity along approach vector
            float approachElRad = wave.ApproachElevation * (float)Math.PI / 180f;
            float approachAzRad = wave.ApproachAzimuth * (float)Math.PI / 180f;

            float horizontalComponent = -(float)wave.AverageVelocity * (float)Math.Cos(approachElRad);
            float verticalComponent = -(float)wave.AverageVelocity * (float)Math.Sin(approachElRad);

            float vx = horizontalComponent * (float)Math.Sin(approachAzRad);
            float vy = horizontalComponent * (float)Math.Cos(approachAzRad);
            float vz = verticalComponent;

            Vector3 enemyVelocity = new Vector3(vx, vy, vz);

            // CRITICAL: Calculate when enemy reaches horizon (Z=0)
            float horizonTime = float.MaxValue;
            if (vz < -0.1f)  // Enemy descending
            {
                horizonTime = -enemyPosition.Z / vz;
                
                // Enemy must stay above horizon long enough for intercepts
                float safeHorizonTime = horizonTime * 0.95f;  // 95% safety margin
                
                if (safeHorizonTime < minInterceptTime * 1.1f)  // 1.1x safety on intercept time
                {
                    return false;  // Enemy descends too quickly
                }
            }
            else if (vz >= -0.1f)
            {
                // Enemy not descending significantly - always playable
                return true;
            }

            // Additional check: Minimum velocity must be achievable
            var target = wave.Targets[0];
            double minVelocityNeeded = Math.Sqrt(2 * target.FractureEnergy / 100.0);  // Assume 100kg projectile
            if (minVelocityNeeded > tier.MaxEffectiveGunRange)
            {
                return false;  // Fracture energy too high for tier
            }

            return true;
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

            // Get cross-section range for this type
            double crossSection = 50.0;
            if (GameConstants.CrossSectionRanges.TryGetValue(type, out var cr))
            {
                crossSection = cr.Item1 + rng.NextDouble() * (cr.Item2 - cr.Item1);
            }

            // Get evasiveness range for this type
            double evasiveness = 0.35;
            if (GameConstants.EvasivenessRanges.TryGetValue(type, out var er))
            {
                evasiveness = er.Item1 + rng.NextDouble() * (er.Item2 - er.Item1);
            }

            // Generate mass and fracture energy WITHIN ARCHETYPE BOUNDS
            // Add slight variation per wave for progression
            double waveProgression = Math.Min(1.0, waveNumber / 25.0); // 0-1 over campaign
            
            double mass = archetype.MassRange.Min + 
                (rng.NextDouble() * (archetype.MassRange.Max - archetype.MassRange.Min)) +
                (waveProgression * (archetype.MassRange.Max - archetype.MassRange.Min) * 0.1); // Slight increase

            double fractureEnergy = archetype.FractureEnergyRange.Min + 
                (rng.NextDouble() * (archetype.FractureEnergyRange.Max - archetype.FractureEnergyRange.Min)) +
                (waveProgression * (archetype.FractureEnergyRange.Max - archetype.FractureEnergyRange.Min) * 0.1); // Slight increase

            return new EnemyTarget
            {
                Name = $"{archetype.Name} ({type}) #{rng.Next(100, 999)}",
                Altitude = 0,
                Velocity = 0,
                CrossSection = crossSection,
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
