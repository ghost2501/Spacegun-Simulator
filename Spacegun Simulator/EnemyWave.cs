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
        public double TimeToImpact => CurrentDistance / AverageVelocity;

        public EnemyWave(int waveNumber)
        {
            WaveNumber = waveNumber;
        }

        public static EnemyWave GenerateWave(int waveNumber, Random rng)
        {
            var wave = new EnemyWave(waveNumber);

            int tier = (waveNumber - 1) / 5;

            wave.InitialDistance = tier switch
            {
                0 => 50_000_000 + rng.Next(50_000_000),
                1 => 150_000_000 + rng.Next(100_000_000),
                2 => 300_000_000 + rng.Next(150_000_000),
                _ => 384_400_000 + rng.Next(200_000_000)
            };

            wave.CurrentDistance = wave.InitialDistance;

            wave.AverageVelocity = tier switch
            {
                0 => 8_000 + rng.Next(4_000),
                1 => 15_000 + rng.Next(10_000),
                2 => 30_000 + rng.Next(20_000),
                3 => 50_000 + rng.Next(50_000),
                _ => 100_000 + rng.Next(200_000)
            };

            int targetCount = 2 + tier + rng.Next(3);

            for (int i = 0; i < targetCount; i++)
            {
                wave.Targets.Add(GenerateTarget(waveNumber, tier, rng));
            }

            wave.AverageRadarCrossSection = wave.Targets.Average(t => t.CrossSection);
            wave.AverageEvasiveness = wave.Targets.Average(t => t.Evasiveness);
            wave.HasStealthCoating = tier >= 2 && rng.NextDouble() < 0.3;

            return wave;
        }

        private static EnemyTarget GenerateTarget(int waveNumber, int tier, Random rng)
        {
            string[] earlyTypes = { "Scout", "Fighter", "Light Cruiser" };
            string[] midTypes = { "Cruiser", "Destroyer", "Heavy Fighter" };
            string[] lateTypes = { "Battlecruiser", "Dreadnought", "Carrier" };

            string[] typePool = tier switch
            {
                0 => earlyTypes,
                1 => earlyTypes.Concat(midTypes).ToArray(),
                2 => midTypes.Concat(lateTypes).ToArray(),
                _ => lateTypes
            };

            string type = typePool[rng.Next(typePool.Length)];

            var target = new EnemyTarget
            {
                Name = $"{type} #{rng.Next(100, 999)}",
                Altitude = 0,
                Velocity = 0,
                CrossSection = type switch
                {
                    "Scout" => 10 + rng.Next(20),
                    "Fighter" => 20 + rng.Next(30),
                    "Light Cruiser" => 40 + rng.Next(40),
                    "Cruiser" => 80 + rng.Next(60),
                    "Destroyer" => 100 + rng.Next(80),
                    "Heavy Fighter" => 50 + rng.Next(50),
                    "Battlecruiser" => 150 + rng.Next(100),
                    "Dreadnought" => 250 + rng.Next(150),
                    "Carrier" => 300 + rng.Next(200),
                    _ => 50
                },
                Evasiveness = type switch
                {
                    "Scout" => 0.6 + rng.NextDouble() * 0.3,
                    "Fighter" => 0.5 + rng.NextDouble() * 0.3,
                    _ => 0.2 + rng.NextDouble() * 0.3
                },
                ArmorThickness = 50 + tier * 50 + rng.Next(100),
                ArmorQuality = 1.0 + tier * 0.3 + rng.NextDouble() * 0.5,
                MaxHitPoints = 200 + tier * 200 + rng.Next(300)
            };

            target.HitPoints = target.MaxHitPoints;
            return target;
        }
    }
}
