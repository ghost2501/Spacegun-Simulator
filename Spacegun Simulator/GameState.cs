using Spacegun_Simulator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spacegun_Simulator
{
    // Simulation-only game state. No Console I/O here.
    public class GameState
    {
        public GunConfiguration Gun { get; set; }   
        public DetectionSystem Detection { get; set; }
        public ResourcePool Resources { get; set; }
        public int CurrentWaveNumber { get; set; }
        public List<EnemyWave> CompletedWaves { get; set; }
        public bool IsGameOver { get; private set; }
        public int WavesDefeated { get; private set; }
        public int TotalEnemiesDestroyed { get; private set; }

        private readonly Random rng;

        public GameState(int? seed = null)
        {
            Gun = new GunConfiguration();
            Detection = new DetectionSystem();
            Resources = new ResourcePool();
            CurrentWaveNumber = 1;
            CompletedWaves = new();
            IsGameOver = false;
            WavesDefeated = 0;
            TotalEnemiesDestroyed = 0;
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // Result object returned to UI layer
        public class TurnResult
        {
            public EnemyWave Wave { get; set; } = null!;
            public DetectionStatus DetectionStatus { get; set; } = null!;
            public List<EngagementResult> EngagementResults { get; set; } = new();
            public bool WaveDefeated { get; set; }
            public bool GameOver { get; set; }
            public string? Message { get; set; }
            public ResourceCost? Reward { get; set; }
        }

        public class EngagementResult
        {
            public string TargetName { get; set; } = string.Empty;
            public bool Hit { get; set; }
            public double Damage { get; set; }
            public double RemainingHp { get; set; }
            public bool Destroyed { get; set; }
            public double HitProbability { get; set; }
        }

        /// <summary>
        /// Simulate one turn/wave. Returns a TurnResult used by UI.
        /// Uses SI units internally (m, m/s, kg, W, J, s).
        /// </summary>
        public TurnResult SimulateTurn()
        {
            var result = new TurnResult();

            // Generate next wave (wave numbers start at 1)
            var wave = EnemyWave.GenerateWave(CurrentWaveNumber, rng);
            result.Wave = wave;

            // Detection
            var detectionStatus = Detection.GetDetectionStatus(wave);
            result.DetectionStatus = detectionStatus;

            if (!detectionStatus.IsDetected)
            {
                // Immediate catastrophic failure
                IsGameOver = true;
                result.GameOver = true;
                result.Message = "Wave not detected until impact. Catastrophic damage.";
                return result;
            }

            // Combat phase
            var engagementResults = new List<EngagementResult>();

            foreach (var target in wave.Targets)
            {
                if (Gun.AmmunitionCount <= 0)
                {
                    // out of ammo - stop firing
                    break;
                }

                // Set tactical snapshot
                target.Velocity = wave.AverageVelocity;
                target.Altitude = wave.CurrentDistance;

                double muzzleVelocity = BallisticsCalculator.CalculateMuzzleVelocity(Gun, Gun.DefaultProjectile);
                double hitProbability = BallisticsCalculator.CalculateInterceptProbability(
                    Gun, Gun.DefaultProjectile, target, muzzleVelocity);
                double damage = BallisticsCalculator.CalculateDamage(
                    Gun.DefaultProjectile, muzzleVelocity * 0.9, target);

                bool hit = rng.NextDouble() < hitProbability;
                Gun.AmmunitionCount--;

                if (hit)
                {
                    target.TakeDamage(damage);
                }

                // Barrel wear
                Gun.BarrelIntegrity = Math.Max(0.0, Gun.BarrelIntegrity - GameConstants.BarrelIntegrityLossPerShot);

                var er = new EngagementResult
                {
                    TargetName = target.Name,
                    Hit = hit,
                    Damage = damage,
                    RemainingHp = target.HitPoints,
                    Destroyed = target.IsDestroyed,
                    HitProbability = hitProbability
                };
                engagementResults.Add(er);
            }

            result.EngagementResults = engagementResults;

            bool waveDefeated = wave.Targets.All(t => t.IsDestroyed);
            result.WaveDefeated = waveDefeated;

            if (waveDefeated)
            {
                WavesDefeated++;
                TotalEnemiesDestroyed += wave.Targets.Count(t => t.IsDestroyed);

                // Reward resources (convert tuned constants in GameConstants)
                var reward = new ResourceCost(
                    budget: GameConstants.BudgetRewardBase + CurrentWaveNumber * GameConstants.BudgetRewardPerWave,
                    steel: GameConstants.SteelRewardBase + CurrentWaveNumber * GameConstants.SteelRewardPerWave,
                    exotic: GameConstants.ExoticRewardBase + CurrentWaveNumber * GameConstants.ExoticRewardPerWave
                );

                Resources.Grant(reward);
                result.Reward = reward;

                CurrentWaveNumber++;
                if (CurrentWaveNumber > GameConstants.TotalWaves)
                {
                    IsGameOver = true;
                    result.GameOver = true;
                    result.Message = "VICTORY: All waves repelled.";
                }
            }
            else
            {
                // Penalties
                int surviving = wave.Targets.Count(t => !t.IsDestroyed);
                double budgetLoss = surviving * GameConstants.BudgetLossPerSurvivor;
                Resources.Budget = Math.Max(0, Resources.Budget - budgetLoss);

                if (Resources.Budget < GameConstants.MinBudgetToContinue)
                {
                    IsGameOver = true;
                    result.GameOver = true;
                    result.Message = "ECONOMIC COLLAPSE: Insufficient resources to continue defense.";
                }
            }

            CompletedWaves.Add(wave);
            result.GameOver = IsGameOver;
            return result;
        }
    }
}