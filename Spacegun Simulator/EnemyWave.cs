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
        /// Generates basic enemy stats: velocity, mass, fracture energy, approach angles.
        /// No trajectory validation - that happens in Firing phase when gun stats are known.
        /// </summary>
        public static EnemyWave GenerateWaveFromArchetype(int waveNumber, EnemyArchetype archetype, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            if (archetype is null) throw new ArgumentNullException(nameof(archetype));

            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            Console.WriteLine($"\n[WAVE GEN] Generating Wave {waveNumber} (Tier {tierIndex}) with archetype: {archetype.Name}");

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

            Console.WriteLine($"  Elev: {wave.ApproachElevation:F1}°, Azim: {wave.ApproachAzimuth:F1}°, Vel: {wave.AverageVelocity:F0} m/s");

            // Generate target
            var target = GenerateTargetFromArchetype(waveNumber, tierIndex, archetype, rng);
            wave.Targets.Add(target);

            wave.AverageRadarCrossSection = target.CrossSection;
            wave.AverageEvasiveness = target.Evasiveness;
            wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;

            // Calculate time to impact from detection range and velocity
            double timeToImpactSeconds = wave.InitialDistance / wave.AverageVelocity;
            Console.WriteLine($"  Time to impact: {GameConstants.FormatTime(timeToImpactSeconds)}");
            Console.WriteLine($"  Target mass: {target.Mass:F0} tons, Fracture energy: {target.FractureEnergy:F0} MJ");

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
