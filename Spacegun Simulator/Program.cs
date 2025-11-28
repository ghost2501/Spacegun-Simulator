using Spacegun_Simulator;
using System;

namespace SpaceGunSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== SPACE GUN SIMULATOR ===\n");

            var gun = new GunConfiguration();
            var detection = new DetectionSystem();
            var resources = new ResourcePool();
            var rng = new Random();

            // Generate a wave
            var wave = EnemyWave.GenerateWave(1, rng);

            // Check detection
            var detectionStatus = detection.GetDetectionStatus(wave);

            Console.WriteLine($"=== WAVE {wave.WaveNumber} ===");
            Console.WriteLine(detectionStatus.Message);
            Console.WriteLine($"Distance: {wave.CurrentDistance / 1_000_000:F1}k km");
            Console.WriteLine($"Velocity: {wave.AverageVelocity / 1_000:F1} km/s");
            Console.WriteLine($"Targets: {wave.TargetCount}");

            if (detectionStatus.IsDetected)
            {
                Console.WriteLine($"Warning Time: {detectionStatus.WarningTime / 60:F1} minutes");
            }

            Console.WriteLine("\n=== GUN STATUS ===");
            Console.WriteLine($"Barrel: {gun.BarrelLength}m, {gun.BarrelMaterial}");
            Console.WriteLine($"Integrity: {gun.BarrelIntegrity * 100:F0}%");
            Console.WriteLine($"Ammunition: {gun.AmmunitionCount} rounds");

            Console.WriteLine("\n=== RESOURCES ===");
            Console.WriteLine($"Budget: {resources.Budget:F0}");
            Console.WriteLine($"Steel: {resources.Steel:F0} tons");
            Console.WriteLine($"Exotic Materials: {resources.ExoticMaterials:F0} units");

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}