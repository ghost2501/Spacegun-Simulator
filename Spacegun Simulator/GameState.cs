using Spacegun_Simulator;
using System;
using System.Collections.Generic;

namespace SpaceGunSimulator
{
    public class GameState
    {
        public GunConfiguration Gun { get; set; }
        public DetectionSystem Detection { get; set; }
        public ResourcePool Resources { get; set; }
        public int CurrentWaveNumber { get; set; }
        public List<EnemyWave> CompletedWaves { get; set; }
        public bool IsGameOver { get; set; }
        public int WavesDefeated { get; set; }
        public int TotalEnemiesDestroyed { get; set; }

        private Random rng;

        public GameState()
        {
            Gun = new GunConfiguration();
            Detection = new DetectionSystem();
            Resources = new ResourcePool();
            CurrentWaveNumber = 1;
            CompletedWaves = new List<EnemyWave>();
            IsGameOver = false;
            WavesDefeated = 0;
            TotalEnemiesDestroyed = 0;
            rng = new Random();
        }

        /// <summary>
        /// Main game loop - processes one complete turn
        /// </summary>
        public void ProcessTurn()
        {
            // Generate next wave
            var wave = EnemyWave.GenerateWave(CurrentWaveNumber, rng);

            // Check detection
            var detectionStatus = Detection.GetDetectionStatus(wave);

            Console.Clear();
            Console.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║           SPACE GUN DEFENSE SIMULATOR                     ║");
            Console.WriteLine($"║           Wave {CurrentWaveNumber} of 25                               ║");
            Console.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

            // Display detection information
            DisplayDetectionInfo(wave, detectionStatus);

            if (!detectionStatus.IsDetected)
            {
                Console.WriteLine("\n=== CRITICAL FAILURE ===");
                Console.WriteLine("Wave not detected until impact!");
                Console.WriteLine("Catastrophic damage to infrastructure.");
                Console.WriteLine("\n*** GAME OVER ***");
                IsGameOver = true;
                return;
            }

            // Display current status
            DisplayStatus();

            // Planning phase - player makes decisions
            PlanningPhase(wave, detectionStatus);

            // Combat phase - resolve the engagement
            CombatPhase(wave);

            // Check if wave was defeated
            bool waveDefeated = wave.Targets.TrueForAll(t => t.IsDestroyed);

            if (waveDefeated)
            {
                WavesDefeated++;
                TotalEnemiesDestroyed += wave.TargetCount;
                Console.WriteLine("\n✓ WAVE DEFEATED!");

                // Reward resources
                RewardResources();

                CurrentWaveNumber++;

                if (CurrentWaveNumber > 25)
                {
                    Victory();
                    IsGameOver = true;
                    return;
                }
            }
            else
            {
                Console.WriteLine("\n✗ WAVE NOT FULLY DEFEATED");
                Console.WriteLine("Surviving enemies have inflicted damage.");

                // Apply penalties for failed defense
                ApplyDefeatPenalties(wave);
            }

            CompletedWaves.Add(wave);

            Console.WriteLine("\nPress any key to continue to next wave...");
            Console.ReadKey();
        }

        private void DisplayDetectionInfo(EnemyWave wave, DetectionStatus status)
        {
            Console.WriteLine("=== DETECTION REPORT ===");
            Console.WriteLine(status.Message);
            Console.WriteLine($"Distance: {wave.CurrentDistance / 1_000_000:F1}k km");
            Console.WriteLine($"Velocity: {wave.AverageVelocity / 1_000:F1} km/s");
            Console.WriteLine($"Targets: {wave.TargetCount}");

            if (status.IsDetected)
            {
                Console.WriteLine($"Warning Time: {status.WarningTime / 60:F1} minutes");

                if (status.Quality == DetectionQuality.Emergency)
                {
                    Console.WriteLine("\n⚠ INSUFFICIENT WARNING TIME");
                    Console.WriteLine("Limited preparation options available.");
                }
            }
            Console.WriteLine();
        }

        private void DisplayStatus()
        {
            Console.WriteLine("=== CURRENT STATUS ===");
            Console.WriteLine($"Gun Barrel: {Gun.BarrelLength}m {Gun.BarrelMaterial}");
            Console.WriteLine($"Integrity: {Gun.BarrelIntegrity * 100:F0}%");
            Console.WriteLine($"Ammunition: {Gun.AmmunitionCount} rounds");
            Console.WriteLine($"Propulsion: {Gun.PropulsionSystem}");
            Console.WriteLine();

            Console.WriteLine("=== RESOURCES ===");
            Console.WriteLine($"Budget: {Resources.Budget:F0}");
            Console.WriteLine($"Steel: {Resources.Steel:F0} tons");
            Console.WriteLine($"Exotic Materials: {Resources.ExoticMaterials:F0} units");
            Console.WriteLine($"Power: {Resources.PowerCapacity:F0} MW");
            Console.WriteLine();
        }

        private void PlanningPhase(EnemyWave wave, DetectionStatus status)
        {
            Console.WriteLine("=== PLANNING PHASE ===");
            Console.WriteLine("(Upgrade system will be implemented here)");
            Console.WriteLine("For now, proceeding with current configuration...\n");

            // TODO: Implement upgrade menu
            // - Display available upgrades
            // - Allow player to spend resources
            // - Modify gun configuration
        }

        private void CombatPhase(EnemyWave wave)
        {
            Console.WriteLine("=== ENGAGEMENT ===");

            foreach (var target in wave.Targets)
            {
                if (Gun.AmmunitionCount <= 0)
                {
                    Console.WriteLine("\n⚠ OUT OF AMMUNITION!");
                    break;
                }

                // Set target velocity from wave
                target.Velocity = wave.AverageVelocity;
                target.Altitude = wave.CurrentDistance;

                // Calculate firing solution
                double muzzleVelocity = BallisticsCalculator.CalculateMuzzleVelocity(Gun, Gun.DefaultProjectile);
                double hitProbability = BallisticsCalculator.CalculateInterceptProbability(
                    Gun, Gun.DefaultProjectile, target, muzzleVelocity);
                double damage = BallisticsCalculator.CalculateDamage(
                    Gun.DefaultProjectile, muzzleVelocity * 0.9, target);

                Console.WriteLine($"\nTarget: {target.Name}");
                Console.WriteLine($"  Hit Probability: {hitProbability * 100:F1}%");
                Console.WriteLine($"  Potential Damage: {damage:F1} HP");

                // Fire!
                bool hit = rng.NextDouble() < hitProbability;
                Gun.AmmunitionCount--;

                if (hit)
                {
                    target.TakeDamage(damage);
                    Console.WriteLine($"  ✓ HIT! ({target.HitPoints:F0}/{target.MaxHitPoints:F0} HP remaining)");

                    if (target.IsDestroyed)
                    {
                        Console.WriteLine($"  *** DESTROYED ***");
                    }
                }
                else
                {
                    Console.WriteLine($"  ✗ MISS");
                }

                // Barrel degradation
                Gun.BarrelIntegrity -= 0.01;
            }
        }

        private void RewardResources()
        {
            // Grant resources for successful defense
            double budgetReward = 100 + (CurrentWaveNumber * 10);
            double steelReward = 50 + (CurrentWaveNumber * 5);
            double exoticReward = 5 + (CurrentWaveNumber * 2);

            Resources.Budget += budgetReward;
            Resources.Steel += steelReward;
            Resources.ExoticMaterials += exoticReward;

            Console.WriteLine($"\nResources Gained:");
            Console.WriteLine($"  +{budgetReward:F0} Budget");
            Console.WriteLine($"  +{steelReward:F0} Steel");
            Console.WriteLine($"  +{exoticReward:F0} Exotic Materials");
        }

        private void ApplyDefeatPenalties(EnemyWave wave)
        {
            int survivingEnemies = wave.Targets.FindAll(t => !t.IsDestroyed).Count;

            // Each surviving enemy reduces resources
            double budgetLoss = survivingEnemies * 50;
            Resources.Budget = Math.Max(0, Resources.Budget - budgetLoss);

            Console.WriteLine($"Resource Loss: -{budgetLoss:F0} Budget");

            // Too many failures end the game
            if (Resources.Budget < 100)
            {
                Console.WriteLine("\n=== ECONOMIC COLLAPSE ===");
                Console.WriteLine("Insufficient resources to continue defense.");
                Console.WriteLine("\n*** GAME OVER ***");
                IsGameOver = true;
            }
        }

        private void Victory()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                                           ║");
            Console.WriteLine("║                   *** VICTORY ***                         ║");
            Console.WriteLine("║                                                           ║");
            Console.WriteLine("║          All 25 waves successfully repelled!              ║");
            Console.WriteLine("║                                                           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Waves Defeated: {WavesDefeated}");
            Console.WriteLine($"Total Enemies Destroyed: {TotalEnemiesDestroyed}");
            Console.WriteLine($"Final Gun Integrity: {Gun.BarrelIntegrity * 100:F0}%");
            Console.WriteLine($"Remaining Resources: {Resources.Budget:F0} Budget");
        }
    }
}