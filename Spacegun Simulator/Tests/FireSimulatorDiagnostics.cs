using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Spacegun_Simulator.Tests
{
    public static class FireSimulatorDiagnostics
    {
        public readonly record struct CheckResult(string Name, bool Passed, string Message);

        public static IReadOnlyList<CheckResult> RunConsistencyChecks()
        {
            var results = new List<CheckResult>();

            void Run(string name, Action action)
            {
                try
                {
                    action();
                    results.Add(new CheckResult(name, Passed: true, Message: "OK"));
                }
                catch (Exception ex)
                {
                    results.Add(new CheckResult(name, Passed: false, Message: ex.Message));
                }
            }

            Run("Tier arrays consistency", TierArraysConsistencyTests.RunAllChecks);
            Run("Constants consistency", ConstantsConsistencyChecks.RunAllChecks);
            Run("Weapon tech mapping (legacy)", ConstantsConsistencyChecks.RunWeaponTechMappingCheck);
            Run("Barrel wear mapping (legacy)", ConstantsConsistencyChecks.RunBarrelWearMappingCheck);

            return results;
        }

        public readonly record struct TechAuditResult(string CsvPath, int ScenarioCount);

        public static TechAuditResult RunTechAuditAndWriteCsv(string? outputDirectory = null)
        {
            var scenarios = TestScenarios.GetTechAuditScenarios() ?? new List<TestScenario>();
            if (scenarios.Count == 0)
                return new TechAuditResult(CsvPath: string.Empty, ScenarioCount: 0);

            var csv = new StringBuilder();

            csv.AppendLine(string.Join(",",
                "Index",
                "Tier",
                "Tech Level",
                "Core Type",
                "Mass",
                "Muzzle Velocity (Ms)",
                "Delta-V (Ms)",
                "Kinetic Energy (MJ)",
                "Projectile Pos X @T+8s",
                "Projectile Pos Y @T+8s",
                "Projectile Pos Z @T+8s"
            ));

            int idx = 1;
            const double sampleTime = 8.0;

            foreach (var scenario in scenarios)
            {
                string tier = $"Tier{scenario.TechLevel}";
                int techLevel = scenario.TechLevel;
                string coreType = scenario.CoreType;
                double massKg = scenario.ProjectileMass;
                double baseMuzzle = scenario.BaseMuzzleVelocityMs;
                double deltaV = scenario.DeltaVMs;

                double finalSpeed = baseMuzzle + deltaV;
                double projectileKEMJ = BallisticsCalculator.CalculateKineticEnergyMJ(massKg, finalSpeed);

                var pos = FiringSolution.CalculateProjectilePositionStatic(sampleTime, finalSpeed, 45.0, 0.0);

                csv.AppendLine(string.Join(",",
                    idx.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(tier),
                    techLevel.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(coreType),
                    massKg.ToString("F3", CultureInfo.InvariantCulture),
                    baseMuzzle.ToString("F3", CultureInfo.InvariantCulture),
                    deltaV.ToString("F3", CultureInfo.InvariantCulture),
                    projectileKEMJ.ToString("F3", CultureInfo.InvariantCulture),
                    pos.X.ToString("F3", CultureInfo.InvariantCulture),
                    pos.Y.ToString("F3", CultureInfo.InvariantCulture),
                    pos.Z.ToString("F3", CultureInfo.InvariantCulture)
                ));

                idx++;
            }

            string dir = string.IsNullOrWhiteSpace(outputDirectory)
                ? Directory.GetCurrentDirectory()
                : outputDirectory;

            if (string.IsNullOrWhiteSpace(dir))
                dir = Directory.GetCurrentDirectory();

            string fileName = $"TechAuditResults_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.GetFullPath(Path.Combine(dir, fileName));

            string? outDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);
            File.WriteAllText(fullPath, csv.ToString(), Encoding.UTF8);

            return new TechAuditResult(CsvPath: fullPath, ScenarioCount: scenarios.Count);
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var escaped = value.Replace("\"", "\"\"");
            if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
                return $"\"{escaped}\"";
            return escaped;
        }
    }
}
