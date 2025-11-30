using System;

namespace Spacegun_Simulator
{
    // Simple console UI that drives GameState simulation and formats SI values.
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
                var result = engine.SimulateTurn();
                RenderTurn(result);

                if (result.GameOver)
                {
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey();
                    break;
                }

                Console.WriteLine("\nPress any key to continue to next wave...");
                Console.ReadKey();
            }
        }

        private void RenderTurn(GameState.TurnResult result)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           SPACE GUN DEFENSE SIMULATOR                     ║");
            Console.WriteLine($"║           Wave {result.Wave.WaveNumber} of {GameConstants.TotalWaves}".PadRight(57) + "║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            // Detection
            Console.WriteLine("=== DETECTION REPORT ===");
            Console.WriteLine(result.DetectionStatus.Message);
            Console.WriteLine($"Distance: {GameConstants.FormatDistance(result.Wave.CurrentDistance)}");
            Console.WriteLine($"Velocity: {GameConstants.FormatVelocity(result.Wave.AverageVelocity)}");
            Console.WriteLine($"Targets: {result.Wave.TargetCount}");
            if (result.DetectionStatus.IsDetected)
            {
                Console.WriteLine($"Warning Time: {GameConstants.FormatTime(result.DetectionStatus.WarningTime)}");
            }
            Console.WriteLine();

            // Gun status
            Console.WriteLine("=== GUN STATUS ===");
            Console.WriteLine($"Barrel: {engine.Gun.BarrelLength:F1} m, {engine.Gun.BarrelMaterial}");
            Console.WriteLine($"Integrity: {engine.Gun.BarrelIntegrity * 100:F0}%");
            Console.WriteLine($"Ammunition: {engine.Gun.AmmunitionCount} rounds");
            Console.WriteLine($"Propulsion: {engine.Gun.PropulsionSystem}");
            Console.WriteLine();

            // Engagement results
            Console.WriteLine("=== ENGAGEMENT ===");
            foreach (var er in result.EngagementResults)
            {
                Console.WriteLine($"\nTarget: {er.TargetName}");
                Console.WriteLine($"  Hit Probability: {er.HitProbability * 100:F1}%");
                Console.WriteLine($"  Damage Applied: {er.Damage:F1} J (approx)");
                Console.WriteLine($"  Remaining HP: {er.RemainingHp:F0}");
                Console.WriteLine(er.Hit ? "  ✓ HIT" : "  ✗ MISS");
                if (er.Destroyed) Console.WriteLine("  *** DESTROYED ***");
            }

            if (result.WaveDefeated)
            {
                Console.WriteLine("\n✓ WAVE DEFEATED!");
                if (result.Reward != null)
                {
                    Console.WriteLine("\nResources Gained:");
                    Console.WriteLine($"  +{result.Reward.Budget:F0} Budget");
                    Console.WriteLine($"  +{result.Reward.Steel:F0} Steel");
                    Console.WriteLine($"  +{result.Reward.ExoticMaterials:F0} Exotic Materials");
                }
            }
            else if (result.GameOver)
            {
                Console.WriteLine("\n✗ WAVE NOT FULLY DEFEATED");
                Console.WriteLine(result.Message);
            }
        }
    }
}