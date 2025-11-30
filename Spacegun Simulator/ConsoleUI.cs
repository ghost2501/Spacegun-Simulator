using System;
using System.Collections.Generic;

namespace Spacegun_Simulator
{
    // Console UI implementing 4-turn sequence:
    // Turn 1: Detection → Show threat
    // Turn 2: Resource Allocation → Gather resources
    // Turn 3: Development → Apply upgrades
    // Turn 4: Firing Solution → Single shot engagement
    public class ConsoleUI
    {
        private readonly GameState engine;

        public ConsoleUI(GameState engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void Run()
        {
            while (!engine.IsGameOver)
            {
                switch (engine.CurrentPhase)
                {
                    case GameState.GamePhase.Detection:
                        RunDetectionPhase();
                        break;

                    case GameState.GamePhase.ResourceAllocation:
                        RunResourceAllocationPhase();
                        break;

                    case GameState.GamePhase.Development:
                        RunDevelopmentPhase();
                        break;

                    case GameState.GamePhase.Firing:
                        RunFiringPhase();
                        break;

                    case GameState.GamePhase.WaveComplete:
                        RunWaveCompletePhase();
                        break;
                }
            }

            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    GAME OVER                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private void RunDetectionPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           SPACE GUN DEFENSE SIMULATOR                     ║");
            Console.WriteLine($"║           Wave {engine.CurrentWaveNumber} of {GameConstants.TotalWaves}".PadRight(57) + "║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var detectionResult = engine.ExecuteDetectionPhase();

            Console.WriteLine("=== DETECTION PHASE ===\n");
            Console.WriteLine(detectionResult.Message);

            if (!detectionResult.WaveDetected)
            {
                Console.WriteLine("\n✗ MISSION FAILED");
                return;
            }

            Console.WriteLine($"\n=== ENEMY PROFILE ===");
            Console.WriteLine($"Type: {detectionResult.Wave.Targets[0].Name}");
            Console.WriteLine($"Detection Distance: {GameConstants.FormatDistance(detectionResult.Wave.CurrentDistance)}");
            Console.WriteLine($"Velocity: {GameConstants.FormatVelocity(detectionResult.Wave.AverageVelocity)}");
            Console.WriteLine($"Radar Cross-Section: {detectionResult.Wave.AverageRadarCrossSection:F1} m²");
            Console.WriteLine($"Evasiveness: {detectionResult.Wave.AverageEvasiveness * 100:F0}%");
            
            Console.WriteLine($"\n=== TIME BUDGET ===");
            Console.WriteLine($"Available Time: {GameConstants.FormatTime(detectionResult.AvailableYears * GameConstants.SecondsPerYear)}");
            Console.WriteLine($"Years Available: {(long)detectionResult.AvailableYears} years");

            Console.WriteLine($"\n=== CURRENT RESOURCES ===");
            Console.WriteLine($"Budget: {engine.Resources.Budget:F0}");
            Console.WriteLine($"Steel: {engine.Resources.Steel:F0} tons");
            Console.WriteLine($"Exotic Materials: {engine.Resources.ExoticMaterials:F1} units");

            Console.WriteLine("\nPress any key to proceed to Resource Allocation phase...");
            Console.ReadKey();
        }

        private void RunResourceAllocationPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           RESOURCE ALLOCATION PHASE                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Remaining Available Time: {GameConstants.FormatTime(engine.RemainingYears * GameConstants.SecondsPerYear)}");
            Console.WriteLine($"({(long)engine.RemainingYears} years)\n");

            Console.WriteLine("=== RESOURCE PRODUCTION RATES (per year) ===");
            Console.WriteLine($"Steel: {GameConstants.SteelProductionPerYear:F0} tons/year");
            Console.WriteLine($"Exotic Materials: {GameConstants.ExoticProductionPerYear:F1} units/year");
            Console.WriteLine($"Budget: {GameConstants.BudgetProductionPerYear:F0} currency/year\n");

            // Simple allocation: ask player for years to spend on each resource
            Console.Write("Years to spend on Steel gathering: ");
            if (!double.TryParse(Console.ReadLine(), out double steelYears) || steelYears < 0)
                steelYears = 0;

            Console.Write("Years to spend on Exotic gathering: ");
            if (!double.TryParse(Console.ReadLine(), out double exoticYears) || exoticYears < 0)
                exoticYears = 0;

            Console.Write("Years to spend on Budget accumulation: ");
            if (!double.TryParse(Console.ReadLine(), out double budgetYears) || budgetYears < 0)
                budgetYears = 0;

            try
            {
                var allocationResult = engine.AllocateResources(steelYears, exoticYears, budgetYears);

                Console.WriteLine("\n=== ALLOCATION RESULT ===");
                Console.WriteLine(allocationResult.Message);
                Console.WriteLine($"\nAccumulated Resources (this wave):");
                Console.WriteLine($"  Steel: {engine.AccumulatedResources["Steel"]:F0} tons");
                Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units");
                Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0} currency");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to proceed to Development phase...");
            Console.ReadKey();
        }

        private void RunDevelopmentPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              DEVELOPMENT & UPGRADES PHASE                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Accumulated Resources (this wave):");
            Console.WriteLine($"  Steel: {engine.AccumulatedResources["Steel"]:F0} tons");
            Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units");
            Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0} currency\n");

            Console.WriteLine("=== CURRENT GUN STATUS ===");
            Console.WriteLine($"Barrel: {engine.Gun.BarrelLength:F1} m, {engine.Gun.BarrelMaterial}");
            Console.WriteLine($"Integrity: {engine.Gun.BarrelIntegrity * 100:F0}%");
            Console.WriteLine($"Ammunition: {engine.Gun.AmmunitionCount} rounds");
            Console.WriteLine($"Propulsion: {engine.Gun.PropulsionSystem}\n");

            Console.WriteLine("=== AVAILABLE UPGRADES ===");
            Console.WriteLine("(In a full implementation, list available upgrades here)");
            Console.WriteLine("For now, proceeding to Firing phase...\n");

            // TODO: Implement upgrade selection UI
            // For MVP, skip to firing phase
            engine.CurrentPhase = GameState.GamePhase.Firing;

            Console.WriteLine("Press any key to proceed to Firing Solution phase...");
            Console.ReadKey();
        }

        private void RunFiringPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            FIRING SOLUTION & ENGAGEMENT PHASE             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var firingResult = engine.ExecuteFiringPhase();

            Console.WriteLine("=== TARGET INFORMATION ===");
            Console.WriteLine($"Target: {engine.CurrentWave?.Targets[0].Name ?? "Unknown"}");
            Console.WriteLine($"Distance: {GameConstants.FormatDistance(firingResult.TargetDistance)}");
            Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(firingResult.GunRange)}\n");

            if (!firingResult.CanReachTarget)
            {
                Console.WriteLine("✗ " + firingResult.Message);
                Console.WriteLine("\nTarget is beyond effective gun range. Mission failed.");
                engine.IsGameOver = true;
                return;
            }

            Console.WriteLine("=== FIRING SOLUTION ===");
            Console.WriteLine($"Hit Probability: {firingResult.HitProbability * 100:F1}%");

            Console.WriteLine("\nFiring...\n");
            System.Threading.Thread.Sleep(1000);  // Brief pause for dramatic effect

            if (firingResult.Hit)
            {
                Console.WriteLine("✓ " + firingResult.Message);
                if (firingResult.Reward != null)
                {
                    Console.WriteLine("\n=== VICTORY REWARDS ===");
                    Console.WriteLine($"  +{firingResult.Reward.Budget:F0} Budget");
                    Console.WriteLine($"  +{firingResult.Reward.Steel:F0} Steel");
                    Console.WriteLine($"  +{firingResult.Reward.ExoticMaterials:F0} Exotic Materials");
                }

                if (firingResult.GameOver)
                {
                    Console.WriteLine("\n" + firingResult.Message);
                    engine.IsGameOver = true;
                } 
            }
            else
            {
                Console.WriteLine("✗ " + firingResult.Message);
                engine.IsGameOver = true;
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void RunWaveCompletePhase()
        {
            if (engine.IsGameOver)
                return;

            engine.AdvanceToNextWave();
        }
    }
}