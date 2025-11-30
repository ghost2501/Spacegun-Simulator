using Spacegun_Simulator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME STATE - 4-TURN SEQUENCE ARCHITECTURE
    // ============================================================================
    // Turn 1: Detection     → Identify threat, calculate available time
    // Turn 2: Allocation    → Spend years on resource gathering
    // Turn 3: Development   → Spend resources on gun upgrades
    // Turn 4: Firing        → Single shot engagement (hit = victory, miss = defeat)
    // ============================================================================

    public class GameState
    {
        public GunConfiguration Gun { get; set; }
        public DetectionSystem Detection { get; set; }
        public ResourcePool Resources { get; set; }
        public int CurrentWaveNumber { get; set; }
        public List<EnemyWave> CompletedWaves { get; set; }
        public bool IsGameOver { get; set; }  // Changed from { get; private set; }
        public int WavesDefeated { get; private set; }
        public int TotalEnemiesDestroyed { get; private set; }

        // 4-Turn sequence state
        public enum GamePhase
        {
            Detection,
            ResourceAllocation,
            Development,
            Firing,
            WaveComplete
        }

        public GamePhase CurrentPhase { get; set; }
        public EnemyWave? CurrentWave { get; private set; }
        public DetectionStatus? CurrentDetectionStatus { get; private set; }

        // Available time budget for current wave (in WHOLE years only)
        public long AvailableYears { get; private set; }
        public long RemainingYears { get; private set; }
        
        // Store the actual seconds available for precise calculation
        private double availableSecondsForGunRange = 0;

        // Accumulated resources during allocation phase (time spent as tokens)
        public Dictionary<string, double> AccumulatedResources { get; private set; } = new();

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
            CurrentPhase = GamePhase.Detection;
            rng = seed.HasValue ? new Random(seed.Value) : new Random();

            InitializeResourceAccumulation();
        }

        private void InitializeResourceAccumulation()
        {
            AccumulatedResources.Clear();
            AccumulatedResources["Steel"] = 0;
            AccumulatedResources["Exotic"] = 0;
            AccumulatedResources["Budget"] = 0;
        }

        // ====================================================================
        // PHASE 1: DETECTION
        // ====================================================================

        public class DetectionPhaseResult
        {
            public EnemyWave Wave { get; set; } = null!;
            public DetectionStatus DetectionStatus { get; set; } = null!;
            public long AvailableYears { get; set; }
            public bool WaveDetected { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public DetectionPhaseResult ExecuteDetectionPhase()
        {
            var result = new DetectionPhaseResult();

            // Generate wave with single target
            CurrentWave = EnemyWave.GenerateWave(CurrentWaveNumber, rng);
            CurrentWave.Targets = CurrentWave.Targets.Take(1).ToList();

            result.Wave = CurrentWave;

            // Get detection status
            CurrentDetectionStatus = Detection.GetDetectionStatus(CurrentWave);
            result.DetectionStatus = CurrentDetectionStatus;

            if (!CurrentDetectionStatus.IsDetected)
            {
                IsGameOver = true;
                result.WaveDetected = false;
                result.Message = "Wave not detected until impact. GLOBAL DESTRUCTION.";
                return result;
            }

            // Calculate time available to reach gun range
            // This is: (InitialDistance - GunRange) / Velocity
            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            double distanceToGunRange = CurrentWave.InitialDistance - tier.MaxEffectiveGunRange;
            
            // Store in BOTH seconds and years for consistency
            availableSecondsForGunRange = distanceToGunRange / CurrentWave.AverageVelocity;
            
            // Round to whole years, minimum 1 year
            AvailableYears = Math.Max(1, (long)Math.Round(availableSecondsForGunRange / GameConstants.SecondsPerYear));
            RemainingYears = AvailableYears;
            InitializeResourceAccumulation();

            result.WaveDetected = true;
            result.AvailableYears = AvailableYears;
            result.Message = $"Enemy detected at {GameConstants.FormatDistance(CurrentWave.InitialDistance)}! {GameConstants.FormatTime(availableSecondsForGunRange)} until target enters gun range.";

            CurrentPhase = GamePhase.ResourceAllocation;
            return result;
        }

        // ====================================================================
        // PHASE 2: RESOURCE ALLOCATION
        // ====================================================================

        public class ResourceAllocationResult
        {
            public double SteelGathered { get; set; }
            public double ExoticGathered { get; set; }
            public double BudgetGathered { get; set; }
            public long YearsSpent { get; set; }
            public long RemainingYears { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// Allocate available years to resource gathering.
        /// steelYears, exoticYears, budgetYears are year tokens to spend.
        /// Returns gathered resources.
        /// </summary>
        public ResourceAllocationResult AllocateResources(double steelYears, double exoticYears, double budgetYears)
        {
            var result = new ResourceAllocationResult();

            // Round input years to whole numbers
            long steelYearsWhole = (long)Math.Round(steelYears);
            long exoticYearsWhole = (long)Math.Round(exoticYears);
            long budgetYearsWhole = (long)Math.Round(budgetYears);

            long totalYears = steelYearsWhole + exoticYearsWhole + budgetYearsWhole;
            if (totalYears > RemainingYears)
                throw new InvalidOperationException($"Cannot allocate {totalYears} years, only {RemainingYears} available.");

            // Convert years to resources (1 year = 1 production token)
            double steelGathered = steelYearsWhole * GameConstants.SteelProductionPerYear;
            double exoticGathered = exoticYearsWhole * GameConstants.ExoticProductionPerYear;
            double budgetGathered = budgetYearsWhole * GameConstants.BudgetProductionPerYear;

            // Add to accumulated
            AccumulatedResources["Steel"] += steelGathered;
            AccumulatedResources["Exotic"] += exoticGathered;
            AccumulatedResources["Budget"] += budgetGathered;

            RemainingYears -= totalYears;

            result.SteelGathered = steelGathered;
            result.ExoticGathered = exoticGathered;
            result.BudgetGathered = budgetGathered;
            result.YearsSpent = totalYears;
            result.RemainingYears = RemainingYears;
            result.Message = $"Gathered {steelGathered:F0} steel, {exoticGathered:F0} exotic, {budgetGathered:F0} budget. {RemainingYears} years remaining.";

            // Move to development if all time allocated or player is done
            CurrentPhase = GamePhase.Development;
            return result;
        }

        // ====================================================================
        // PHASE 3: DEVELOPMENT
        // ====================================================================

        public class DevelopmentResult
        {
            public bool UpgradeApplied { get; set; }
            public string UpgradeName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public double ResourcesRemaining { get; set; }
        }

        /// <summary>
        /// Apply accumulated resources to gun upgrades.
        /// Returns result of upgrade application.
        /// </summary>
        public DevelopmentResult ApplyUpgrade(UpgradeSystem upgrade)
        {
            var result = new DevelopmentResult();

            if (upgrade is null)
            {
                result.Message = "No upgrade specified.";
                return result;
            }

            // Convert accumulated resources to ResourceCost for checking
            var availableCost = new ResourceCost(
                budget: AccumulatedResources["Budget"],
                steel: AccumulatedResources["Steel"],
                exotic: AccumulatedResources["Exotic"]
            );

            // Check if we can afford it
            if (!CanAffordUpgrade(upgrade.Cost, availableCost))
            {
                result.Message = $"Insufficient accumulated resources for {upgrade.Name}.";
                return result;
            }

            // Apply upgrade
            try
            {
                upgrade.Apply(Gun, new ResourcePool
                {
                    Budget = AccumulatedResources["Budget"],
                    Steel = AccumulatedResources["Steel"],
                    ExoticMaterials = AccumulatedResources["Exotic"],
                    PowerCapacity = Gun.PowerCapacity,
                    ResearchPoints = 0
                });

                // Deduct from accumulated
                AccumulatedResources["Budget"] -= upgrade.Cost.Budget;
                AccumulatedResources["Steel"] -= upgrade.Cost.Steel;
                AccumulatedResources["Exotic"] -= upgrade.Cost.ExoticMaterials;

                result.UpgradeApplied = true;
                result.UpgradeName = upgrade.Name;
                result.Message = $"Applied upgrade: {upgrade.Name}";
                result.ResourcesRemaining = AccumulatedResources["Budget"] + AccumulatedResources["Steel"] + AccumulatedResources["Exotic"];

                CurrentPhase = GamePhase.Firing;
            }
            catch (Exception ex)
            {
                result.Message = $"Failed to apply upgrade: {ex.Message}";
            }

            return result;
        }

        private bool CanAffordUpgrade(ResourceCost cost, ResourceCost available)
        {
            if (cost is null) return true;
            if (cost.Budget > available.Budget) return false;
            if (cost.Steel > available.Steel) return false;
            if (cost.ExoticMaterials > available.ExoticMaterials) return false;
            return true;
        }

        // ====================================================================
        // PHASE 4: FIRING SOLUTION
        // ====================================================================

        public class FiringPhaseResult
        {
            public bool CanReachTarget { get; set; }
            public double GunRange { get; set; }
            public double TargetDistance { get; set; }
            public bool Hit { get; set; }
            public double HitProbability { get; set; }
            public bool WaveDefeated { get; set; }
            public bool GameOver { get; set; }
            public string Message { get; set; } = string.Empty;
            public ResourceCost? Reward { get; set; }
        }

        public FiringPhaseResult ExecuteFiringPhase()
        {
            var result = new FiringPhaseResult();

            if (CurrentWave is null || CurrentWave.Targets.Count == 0)
            {
                result.Message = "No valid target for engagement.";
                result.GameOver = true;
                IsGameOver = true;
                return result;
            }

            var target = CurrentWave.Targets[0];
            target.Velocity = CurrentWave.AverageVelocity;

            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            result.GunRange = tier.MaxEffectiveGunRange;

            // Calculate how much time was spent in resource allocation phase
            long timeSpentInAllocation = AvailableYears - RemainingYears;
            
            // Convert to seconds - use the portion of available time that was allocated
            double proportionOfTimeSpent = timeSpentInAllocation / (double)AvailableYears;
            double secondsSpent = proportionOfTimeSpent * availableSecondsForGunRange;

            // Calculate enemy's new distance
            // NewDistance = InitialDistance - (Velocity × TimeSpent)
            double distanceTraveledMeters = CurrentWave.AverageVelocity * secondsSpent;
            double newDistanceMeters = CurrentWave.InitialDistance - distanceTraveledMeters;

            // Enemy can't go past Earth (0)
            newDistanceMeters = Math.Max(0, newDistanceMeters);

            CurrentWave.CurrentDistance = newDistanceMeters;
            target.Altitude = newDistanceMeters;
            result.TargetDistance = newDistanceMeters;

            // If we're still beyond gun range, we failed
            if (newDistanceMeters > tier.MaxEffectiveGunRange)
            {
                result.CanReachTarget = false;
                result.Message = $"Target still beyond effective gun range after {timeSpentInAllocation} years. ({GameConstants.FormatDistance(newDistanceMeters)} vs {GameConstants.FormatDistance(tier.MaxEffectiveGunRange)})";
                IsGameOver = true;
                result.GameOver = true;
                return result;
            }

            result.CanReachTarget = true;

            // Calculate and execute shot
            double muzzleVelocity = BallisticsCalculator.CalculateMuzzleVelocity(Gun, Gun.DefaultProjectile);
            double hitProbability = BallisticsCalculator.CalculateInterceptProbability(Gun, Gun.DefaultProjectile, target, muzzleVelocity);
            double damage = BallisticsCalculator.CalculateDamage(Gun.DefaultProjectile, muzzleVelocity * 0.9, target);

            result.HitProbability = hitProbability;
            bool hit = rng.NextDouble() < hitProbability;
            Gun.AmmunitionCount--;
            Gun.BarrelIntegrity = Math.Max(0.0, Gun.BarrelIntegrity - GameConstants.BarrelIntegrityLossPerShot);
            result.Hit = hit;

            if (hit)
            {
                target.TakeDamage(damage);
                result.WaveDefeated = true;
                result.Message = $"✓ DIRECT HIT! Enemy destroyed at {GameConstants.FormatDistance(newDistanceMeters)}. Wave {CurrentWaveNumber} defeated!";

                var reward = new ResourceCost(
                    budget: GameConstants.BudgetRewardBase + CurrentWaveNumber * GameConstants.BudgetRewardPerWave + (long)AccumulatedResources["Budget"],
                    steel: GameConstants.SteelRewardBase + CurrentWaveNumber * GameConstants.SteelRewardPerWave + (long)AccumulatedResources["Steel"],
                    exotic: GameConstants.ExoticRewardBase + CurrentWaveNumber * GameConstants.ExoticRewardPerWave + (long)AccumulatedResources["Exotic"]
                );

                Resources.Grant(reward);
                result.Reward = reward;
                WavesDefeated++;
                TotalEnemiesDestroyed++;
                CurrentWaveNumber++;

                if (CurrentWaveNumber > GameConstants.TotalWaves)
                {
                    IsGameOver = true;
                    result.GameOver = true;
                    result.Message = "VICTORY: All 25 waves repelled. Humanity saved!";
                }
            }
            else
            {
                result.WaveDefeated = false;
                result.Message = "✗ MISS! The enemy reaches Earth. GLOBAL DESTRUCTION.";
                IsGameOver = true;
                result.GameOver = true;
            }

            CompletedWaves.Add(CurrentWave);
            CurrentPhase = GamePhase.WaveComplete;
            return result;
        }

        // ====================================================================
        // HELPER: Advance to next wave
        // ====================================================================

        public void AdvanceToNextWave()
        {
            CurrentWave = null;
            CurrentDetectionStatus = null;
            CurrentPhase = GamePhase.Detection;
        }
    }
}