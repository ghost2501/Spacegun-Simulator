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

        public int TargetCount => Targets.Count;
        public double TimeToImpact => AverageVelocity > 0 ? CurrentDistance / AverageVelocity : double.PositiveInfinity;

        public EnemyWave(int waveNumber)
        {
            WaveNumber = waveNumber;
        }

        public static EnemyWave GenerateWave(int waveNumber, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            var wave = new EnemyWave(waveNumber);
            
            // Get tier data for this wave
            var tier = GameConstants.GetTierForWave(waveNumber);
            int tierIndex = tier.TierIndex;

            // Generate distance within tier's range (meters)
            wave.InitialDistance = tier.DetectionRangeMin + 
                rng.NextDouble() * (tier.DetectionRangeMax - tier.DetectionRangeMin);
            wave.CurrentDistance = wave.InitialDistance;

            // Generate velocity within tier's range (m/s)
            wave.AverageVelocity = tier.VelocityMin + 
                rng.NextDouble() * (tier.VelocityMax - tier.VelocityMin);

            // Generate single target
            var target = GenerateTarget(waveNumber, tierIndex, rng);
            wave.Targets.Add(target);

            wave.AverageRadarCrossSection = target.CrossSection;
            wave.AverageEvasiveness = target.Evasiveness;
            wave.HasStealthCoating = tierIndex >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;

            return wave;
        }

        private static EnemyTarget GenerateTarget(int waveNumber, int tierIndex, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            string[] typePool = tierIndex switch
            {
                0 => GameConstants.EarlyTypes,
                1 => ConcatArrays(GameConstants.EarlyTypes, GameConstants.MidTypes),
                2 => ConcatArrays(GameConstants.MidTypes, GameConstants.LateTypes),
                _ => GameConstants.LateTypes
            };

            string type = typePool[rng.Next(typePool.Length)];

            // Cross-section
            if (GameConstants.CrossSectionRanges.TryGetValue(type, out var cr))
            {
                double csMin = cr.Item1;
                double csMax = cr.Item2;
                double crossSection = csMin + rng.NextDouble() * (csMax - csMin);

                // Evasiveness
                double evMin, evMax;
                if (GameConstants.EvasivenessRanges.TryGetValue(type, out var er))
                {
                    evMin = er.Item1;
                    evMax = er.Item2;
                }
                else
                {
                    evMin = 0.2;
                    evMax = 0.5;
                }
                double evasiveness = evMin + rng.NextDouble() * (evMax - evMin);

                int initialHp = GameConstants.HpBase + tierIndex * GameConstants.HpPerTier + rng.Next(GameConstants.HpRandomVariance);

                return new EnemyTarget
                {
                    Name = $"{type} #{rng.Next(100, 999)}",
                    Altitude = 0,
                    Velocity = 0,
                    CrossSection = crossSection,
                    Evasiveness = evasiveness,
                    ArmorThickness = GameConstants.ArmorThicknessBase + tierIndex * GameConstants.ArmorThicknessPerTier + rng.Next(GameConstants.ArmorThicknessRandomVariance),
                    ArmorQuality = GameConstants.ArmorQualityBase + tierIndex * GameConstants.ArmorQualityPerTier + rng.NextDouble() * GameConstants.ArmorQualityRandomVariance,
                    MaxHitPoints = initialHp,
                    HitPoints = initialHp
                };
            }
            else
            {
                // Default fallback
                double crossSection = 50.0 + rng.NextDouble() * 50.0;
                double evasiveness = 0.2 + rng.NextDouble() * 0.3;
                int initialHp = GameConstants.HpBase + tierIndex * GameConstants.HpPerTier + rng.Next(GameConstants.HpRandomVariance);

                return new EnemyTarget
                {
                    Name = $"{type} #{rng.Next(100, 999)}",
                    Altitude = 0,
                    Velocity = 0,
                    CrossSection = crossSection,
                    Evasiveness = evasiveness,
                    ArmorThickness = GameConstants.ArmorThicknessBase + tierIndex * GameConstants.ArmorThicknessPerTier + rng.Next(GameConstants.ArmorThicknessRandomVariance),
                    ArmorQuality = GameConstants.ArmorQualityBase + tierIndex * GameConstants.ArmorQualityPerTier + rng.NextDouble() * GameConstants.ArmorQualityRandomVariance,
                    MaxHitPoints = initialHp,
                    HitPoints = initialHp
                };
            }
        }

        private static T[] ConcatArrays<T>(T[] a, T[] b)
        {
            var result = new T[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
