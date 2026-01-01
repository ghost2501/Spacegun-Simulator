// New simple test harness to validate wear curves without affecting main tests.
// This is a lightweight console test (not an NUnit/xUnit test) that can be executed
// manually by developers. It purposely calls RegisterShot() to exercise wear logic.

using Spacegun_Simulator.Development;
using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// Lightweight developer tool to exercise barrel wear curves.
    /// Intentionally standalone and not part of the main automated tests.
    /// </summary>
    public static class BarrelWearTests
    {
        public static void RunWearCurveTest()
        {
            var gun = new GunConfiguration
            {
                BarrelMaterial = "Steel",
                CoolingCapacity = 10.0,
                PropulsionSystem = PropulsionType.Railgun,
                PowerCapacity = 200.0,
                BaseWearPerShot = 0.0005
            };

            Console.WriteLine("=== BARREL WEAR CURVE TEST ===");
            Console.WriteLine($"Initial Integrity: {gun.BarrelIntegrity:P2}");
            Console.WriteLine($"Base wear per shot: {gun.BaseWearPerShot:P6}");
            Console.WriteLine();

            int shots = 0;
            using var writer = new StreamWriter("BarrelWearCurve.csv", false);
            writer.WriteLine("Shot,Integrity,CumulativeWear");
            while (shots < 10000 && !gun.IsBarrelFailed())
            {
                shots++;
                gun.RegisterShot();
                writer.WriteLine($"{shots},{gun.BarrelIntegrity:F6},{gun.CumulativeWear:F6}");

                if (shots % 1000 == 0)
                {
                    Console.WriteLine($"Shot {shots:0000} -> Integrity: {gun.BarrelIntegrity:P4}");
                }
            }

            Console.WriteLine($"\nCompleted {shots} shots. Final integrity: {gun.BarrelIntegrity:P4}");
            Console.WriteLine("CSV written to: BarrelWearCurve.csv");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}