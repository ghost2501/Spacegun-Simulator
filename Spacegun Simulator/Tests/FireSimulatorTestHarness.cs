using System.Text;
using Spacegun_Simulator.Ballistics;

namespace Spacegun_Simulator.Tests
{
    public partial class FireSimulatorTestHarness : IDisposable
    {
        // Provide a minimal Dispose implementation that does not reference fields
        // which may live in other partial definitions of this class.
        // This avoids CS0103 when this partial is compiled alone in the IDE.
        public void Dispose()
        {
            // No-op disposal here; real cleanup (if any) is performed in the other partial.
            // Suppress finalization as a safe courtesy.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Minimal CSV escaper used by the tech-audit exporter.
        /// </summary>
        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var escaped = value.Replace("\"", "\"\"");
            if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
                return $"\"{escaped}\"";
            return escaped;
        }

        private void RunTechAudit()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           TECH AUDIT - WEAPONS & UPGRADES MATRIX          ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var scenarios = TestScenarios.GetTechAuditScenarios();
            if (scenarios == null || scenarios.Count == 0)
            {
                Console.WriteLine("No tech-audit scenarios found.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Preparing to run {scenarios.Count} scenarios...");
            Console.WriteLine("This will run quickly; results are written to a CSV file for easy comparison\n");
            Console.WriteLine("Press any key to begin...");
            Console.ReadKey();

            // Build CSV
            var csv = new StringBuilder();

            // Header: Kinetic energy (MJ) replaces fracture energy; sample at T+8s
            csv.AppendLine(string.Join(",",
                "Index",
                "Tier",
                "Tech Level",
                "Core Type",
                "Mass",
                " Muzzle Velocity (Ms) ",
                " Delta-V (Ms)",
                "Kinetic Energy (MJ)",
                "Projectile Pos X @T+8s",
                "Projectile Pos Y @T+8s",
                "Projectile Pos Z @T+8s"
            ));

            int idx = 1;
            const double sampleTime = 8.0; // now sampling T+8s

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"[{idx}/{scenarios.Count}] Running: {scenario.Name}");

                // Determine values from scenario metadata
                string tier = $"Tier{scenario.TechLevel}";
                int techLevel = scenario.TechLevel;
                string coreType = scenario.CoreType;
                double massKg = scenario.ProjectileMass;
                double baseMuzzle = scenario.BaseMuzzleVelocityMs;
                double deltaV = scenario.DeltaVMs;

                // Final launch speed uses base + deltaV (what the projectile actually uses)
                double finalSpeed = baseMuzzle + deltaV;

                // Compute projectile kinetic energy (MJ)
                double projectileKEMJ = BallisticsCalculator.CalculateKineticEnergyMJ(massKg, finalSpeed);

                // Use the FiringSolution solver math (identical trajectory) to get position at T+8s
                var pos = FiringSolution.CalculateProjectilePositionStatic(sampleTime, finalSpeed, 45.0, 0.0);

                // Compose CSV row (use invariant format)
                csv.AppendLine(string.Join(",",
                    idx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EscapeCsv(tier),
                    techLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EscapeCsv(coreType),
                    massKg.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    baseMuzzle.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    deltaV.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    projectileKEMJ.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    pos.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    pos.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    pos.Z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                ));

                idx++;
            }

            // Write CSV to timestamped file
            string fileName = $"TechAuditResults_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            try
            {
                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                Console.WriteLine($"\n✓ Tech audit complete. CSV written to: {Path.GetFullPath(fileName)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Failed to write CSV file: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to return to test menu...");
            Console.ReadKey();
        }

        // Add this public RunAllTests method so callers can invoke the test harness.
        // Minimal menu implemented — keeps focus on the Tech Audit option which the UI relies on.

        public void RunAllTests()
        {
            // Run quick consistency checks before interactive test modes.
            try
            {
                // Tier / array consistency
                TierArraysConsistencyTests.RunAllChecks();
                Console.WriteLine("✓ Tier arrays consistency checks passed.");

                // Constants consumer checks (barrel wear, tech velocity mapping)
                ConstantsConsistencyChecks.RunAllChecks();
                Console.WriteLine("✓ Constants consistency checks passed.");

                // Backwards-compatible quick checks (existing)
                ConstantsConsistencyChecks.RunWeaponTechMappingCheck();
                ConstantsConsistencyChecks.RunBarrelWearMappingCheck();
                Console.WriteLine("✓ Legacy consistency checks passed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Constants consistency check failed: " + ex.Message);
                Console.WriteLine("Fix the mapping in GameConstants, GunConfiguration, or EnemyWave before running tests.");
                Console.WriteLine("Press any key to continue to the test menu (tests may be unreliable)...");
                Console.ReadKey();
            }

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║         FIRE SIMULATOR TEST HARNESS - SELECT MODE         ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("[C] CONSISTENCY CHECK - Run constants mapping checks");
                Console.WriteLine("[T] TECH AUDIT - Run weapons / propulsion / core matrix (fixed target)");
                Console.WriteLine("    Runs a matrix of tech levels & upgrade deltas against a single fixed target");
                Console.WriteLine("    Output written to CSV for easy comparison\n");

                Console.WriteLine("[Q] Return\n");
                Console.Write("Select test mode: ");

                string mode = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();

                switch (mode)
                {
                    case "C":
                        RunConsistencyCheckInteractive();
                        break;

                    case "T":
                        RunTechAudit();
                        break;

                    case "Q":
                        return;

                    default:
                        Console.WriteLine("Invalid selection. Please try again.");
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }
        }

        private void RunConsistencyCheckInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== RUNNING CONSISTENCY CHECKS ===\n");

            // Run both checks and show results separately.
            try
            {
                TierArraysConsistencyTests.RunAllChecks();
                Console.WriteLine("✓ Tier arrays consistency check passed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Tier arrays consistency check failed: {ex.Message}");
            }

            try
            {
                ConstantsConsistencyChecks.RunAllChecks();
                Console.WriteLine("✓ Constants consistency check passed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Constants consistency check failed: {ex.Message}");
            }

            try
            {
                ConstantsConsistencyChecks.RunWeaponTechMappingCheck();
                Console.WriteLine("✓ Legacy weapon-tech mapping check passed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Legacy weapon-tech mapping check failed: {ex.Message}");
            }

            try
            {
                ConstantsConsistencyChecks.RunBarrelWearMappingCheck();
                Console.WriteLine("✓ Legacy barrel-wear mapping check passed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Legacy barrel-wear mapping check failed: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to return to test menu...");
            Console.ReadKey();
        }
    }
}