using System;
using System.Collections.Generic;
using System.Linq;

namespace Spacegun_Simulator
{
    // ============================================================================ 
    // ENEMY WAVE
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

            int tier = (waveNumber - 1) / 5;
            tier = Math.Min(tier, GameConstants.InitialDistanceBaseByTier.Length - 1);

            double baseDistance = GameConstants.InitialDistanceBaseByTier[tier];
            double variance = GameConstants.InitialDistanceVarianceByTier[Math.Min(tier, GameConstants.InitialDistanceVarianceByTier.Length - 1)];
            wave.InitialDistance = baseDistance + rng.Next((int)variance);

            wave.CurrentDistance = wave.InitialDistance;

            double baseVel = (tier < GameConstants.VelocityBaseByTier.Length) ? GameConstants.VelocityBaseByTier[tier] : GameConstants.VelocityBaseByTier[^1];
            double velVar = (tier < GameConstants.VelocityVarianceByTier.Length) ? GameConstants.VelocityVarianceByTier[tier] : GameConstants.VelocityVarianceByTier[^1];
            wave.AverageVelocity = baseVel + rng.Next((int)velVar);

            int targetCount = GameConstants.TargetCountBase + Math.Min(GameConstants.TargetCountTierBonus * tier, 1000) + rng.Next(GameConstants.TargetCountRandomMaxExclusive);

            // build the list in one expression
            wave.Targets = Enumerable.Range(0, targetCount)
                .Select(_ => GenerateTarget(waveNumber, tier, rng))
                .ToList();

            wave.AverageRadarCrossSection = wave.Targets.Average(t => t.CrossSection);
            wave.AverageEvasiveness = wave.Targets.Average(t => t.Evasiveness);
            wave.HasStealthCoating = tier >= 2 && rng.NextDouble() < GameConstants.StealthChanceForLateTiers;

            return wave;
        }

        private static EnemyTarget GenerateTarget(int waveNumber, int tier, Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            string[] typePool = tier switch
            {
                0 => GameConstants.EarlyTypes,
                1 => ConcatArrays(GameConstants.EarlyTypes, GameConstants.MidTypes),
                2 => ConcatArrays(GameConstants.MidTypes, GameConstants.LateTypes),
                _ => GameConstants.LateTypes
            };

            string type = typePool[rng.Next(typePool.Length)];

            // Cross-section
            // Avoid accessing tuple element names that can be lost by the compiler when types differ across branches.
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

                int initialHp = GameConstants.HpBase + tier * GameConstants.HpPerTier + rng.Next(GameConstants.HpRandomVariance);

                var targetFromCr = new EnemyTarget
                {
                    Name = $"{type} #{rng.Next(100, 999)}",
                    Altitude = 0,
                    Velocity = 0,
                    CrossSection = crossSection,
                    Evasiveness = evasiveness,
                    ArmorThickness = GameConstants.ArmorThicknessBase + tier * GameConstants.ArmorThicknessPerTier + rng.Next(GameConstants.ArmorThicknessRandomVariance),
                    ArmorQuality = GameConstants.ArmorQualityBase + tier * GameConstants.ArmorQualityPerTier + rng.NextDouble() * GameConstants.ArmorQualityRandomVariance,
                    MaxHitPoints = initialHp,
                    HitPoints = initialHp // set to same initial value as MaxHitPoints
                };

                return targetFromCr;
            }
            else
            {
                // Default cross-section range when no entry exists
                double csMin = 50.0;
                double csMax = 100.0;
                double crossSection = csMin + rng.NextDouble() * (csMax - csMin);

                double evMin, evMax;
                if (GameConstants.EvasivenessRanges.TryGetValue(type, out var er2))
                {
                    evMin = er2.Item1;
                    evMax = er2.Item2;
                }
                else
                {
                    evMin = 0.2;
                    evMax = 0.5;
                }
                double evasiveness = evMin + rng.NextDouble() * (evMax - evMin);

                int initialHp = GameConstants.HpBase + tier * GameConstants.HpPerTier + rng.Next(GameConstants.HpRandomVariance);

                var defaultTarget = new EnemyTarget
                {
                    Name = $"{type} #{rng.Next(100, 999)}",
                    Altitude = 0,
                    Velocity = 0,
                    CrossSection = crossSection,
                    Evasiveness = evasiveness,
                    ArmorThickness = GameConstants.ArmorThicknessBase + tier * GameConstants.ArmorThicknessPerTier + rng.Next(GameConstants.ArmorThicknessRandomVariance),
                    ArmorQuality = GameConstants.ArmorQualityBase + tier * GameConstants.ArmorQualityPerTier + rng.NextDouble() * GameConstants.ArmorQualityRandomVariance,
                    MaxHitPoints = initialHp,
                    HitPoints = initialHp
                };

                return defaultTarget;
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
