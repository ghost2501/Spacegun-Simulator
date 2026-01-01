using System.Text;
using System.Security.Cryptography;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Detection;
using Spacegun_Simulator.Enemies;

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

                Console.WriteLine("[E] ENEMY CURVE - Export enemy generation stats to CSV");
                Console.WriteLine("    Generates 5 samples per mode and tier for inspection/plotting\n");

                Console.WriteLine("[B] BALANCE CURVE - Export counters (enemy vs radar/gun/projectile) to CSV");
                Console.WriteLine("    Generates many samples per wave and writes a wide CSV for balancing\n");

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

                    case "E":
                        RunEnemyGenerationCurve();
                        break;

                    case "B":
                        RunBalanceCurve();
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

        private void RunBalanceCurve()
        {
            GameConfigLoader.LoadIfExists();

            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        BALANCE CURVE - COUNTERS CSV EXPORT (WIDE)         ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("This exporter samples enemy generation across waves and combines it with");
            Console.WriteLine("radar/gun/projectile counter configurations, including intel visibility.");
            Console.WriteLine();

            const int samplesPerWave = 20;
            Console.WriteLine($"Default samples per wave: {samplesPerWave}");
            Console.WriteLine("Press any key to begin...");
            Console.ReadKey();

            var result = FireSimulatorDiagnostics.RunCounterCurveAndWriteCsv(samplesPerWave: samplesPerWave);
            Console.WriteLine($"\n✓ Balance curve export complete. Rows: {result.RowCount}");
            Console.WriteLine($"CSV written to: {result.CsvPath}");
            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey();
        }

        private static int StableSeed(string key)
        {
            // Stable across processes/machines (unlike string.GetHashCode()).
            byte[] bytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            int seed = BitConverter.ToInt32(hash, 0);
            return seed == int.MinValue ? 0 : Math.Abs(seed);
        }

        private void RunEnemyGenerationCurve()
        {
            GameConfigLoader.LoadIfExists();

            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            ENEMY CURVE - MODE/TIER CSV EXPORT             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            const int samplesPerTier = 5;
            var detection = new DetectionSystem();
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",",
                "ModeId",
                "ModeName",
                "Ruleset",
                "Difficulty",
                "TierIndex",
                "WaveNumber",
                "Sample",
                "CampaignArchetypeId",
                "CampaignName",
                "WaveArchetypeId",
                "WaveArchetypeName",
                "ShipCount",
                "InitialDistance",
                "AverageVelocity",
                "RcsRaw",
                "RcsModeAdjusted",
                "RcsDisplay",
                "HasStealthCoating",
                "Acceleration",
                "Maneuverability",
                "Defense",
                "Mass",
                "FractureEnergy",

                // Derived difficulty metrics
                "WaveDistanceAU",
                "EffectiveDetectionRangeAU",
                "Detected",
                "DetectionQuality",
                "WarningTimeSeconds",
                "MinSafeTimeSeconds",

                "TimeToImpactSeconds",
                "TimeToGunRangeSecondsBase",
                "TimeToGunRangeSecondsTuned",
                "AvailableYearsRounded",

                "TierMaxEffectiveGunRange",
                "DifficultyHitToleranceMultiplier",
                "ModeHitToleranceMultiplier",
                "DifficultyTargetRcsMultiplier",
                "HitToleranceMeters"
            ));

            var modes = GameModeCatalog.GetAll();

            foreach (var mode in modes)
            {
                if (mode.IsTutorial)
                {
                    for (int sample = 1; sample <= samplesPerTier; sample++)
                    {
                        int waveNumber = sample;
                        var rng = new Random(StableSeed($"{mode.Id}|tutorial|{sample}"));
                        var wave = EnemyWave.GenerateTutorialWave(waveNumber, rng);
                        var target = wave.Targets[0];

                        var diffConfig = DifficultyConfig.GetConfig(mode.Difficulty);
                        var tier = GameConstants.GetTierForWave(wave.WaveNumber);
                        double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                        var det = detection.GetDetectionStatus(wave);
                        double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);
                        double warningTimeSeconds = detection.CalculateWarningTime(wave);
                        double minSafeTimeSeconds = tier.TimeToImpactMin * 0.1;

                        double timeToImpactSeconds = wave.CurrentDistance / Math.Max(1e-9, wave.AverageVelocity);
                        double timeToGunRangeBaseSeconds = Math.Max(0.0, (wave.InitialDistance - tier.MaxEffectiveGunRange) / Math.Max(1e-9, wave.AverageVelocity));
                        double timeMult = GameModeTuning.Current.GetTimeBudgetMultiplier(mode);
                        double timeToGunRangeTunedSeconds = timeToGunRangeBaseSeconds * timeMult;
                        long availableYearsRounded = Math.Max(1, (long)Math.Round(timeToGunRangeTunedSeconds / GameConstants.SECONDS_PER_YEAR));

                        double rcsRaw = wave.AverageRadarCrossSection;
                        double rcsModeAdjusted = rcsRaw * GameModeTuning.Current.GetDetectionRcsMultiplier(mode);
                        double rcsDisplay = rcsModeAdjusted * diffConfig.TargetRcsMultiplier;

                        double hitToleranceMeters = diffConfig.IsTutorialMode
                            ? DifficultyConfig.TutorialBeachball.RadiusMeters
                            : 0.5 * (2.0 * Math.Sqrt(rcsDisplay / Math.PI)) * diffConfig.HitToleranceMultiplier * GameModeTuning.Current.GetHitToleranceMultiplier(mode);

                        csv.AppendLine(string.Join(",",
                            EscapeCsv(mode.Id.ToString()),
                            EscapeCsv(mode.DisplayName),
                            "Tutorial",
                            EscapeCsv(GameModeCatalog.GetDifficultyLabel(mode)),
                            (-1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                            waveNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            sample.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "",
                            "",
                            EscapeCsv(wave.Archetype?.Id),
                            EscapeCsv(wave.Archetype?.Name),
                            wave.ShipCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            wave.InitialDistance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            wave.AverageVelocity.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            rcsRaw.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            rcsModeAdjusted.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            rcsDisplay.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            wave.HasStealthCoating ? "1" : "0",
                            target.Acceleration.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Maneuverability.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Defense.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Mass.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            target.FractureEnergy.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),

                            waveDistanceAu.ToString("F9", System.Globalization.CultureInfo.InvariantCulture),
                            effectiveDetRangeAu.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            det.IsDetected ? "1" : "0",
                            EscapeCsv(det.Quality.ToString()),
                            warningTimeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            minSafeTimeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),

                            timeToImpactSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            timeToGunRangeBaseSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            timeToGunRangeTunedSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            availableYearsRounded.ToString(System.Globalization.CultureInfo.InvariantCulture),

                            tier.MaxEffectiveGunRange.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            diffConfig.HitToleranceMultiplier.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            GameModeTuning.Current.GetHitToleranceMultiplier(mode).ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            diffConfig.TargetRcsMultiplier.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            hitToleranceMeters.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
                        ));
                    }

                    continue;
                }

                var ruleset = mode.UsesEconomyAndDevelopment ? EnemyGenerationRuleset.Full : EnemyGenerationRuleset.Pure;
                string rulesetLabel = ruleset.ToString();

                var diffConfigFull = DifficultyConfig.GetConfig(mode.Difficulty);

                // Use a deterministic campaign type so the curve is comparable across tiers.
                var campaignRng = new Random(StableSeed($"{mode.Id}|campaign"));
                var campaignType = EnemyType.GenerateForCampaign(campaignRng);

                for (int tierIndex = 0; tierIndex < GameConstants.TierCount; tierIndex++)
                {
                    int waveNumber = GameConstants.WaveTiers[tierIndex].StartWave;

                    for (int sample = 1; sample <= samplesPerTier; sample++)
                    {
                        var rng = new Random(StableSeed($"{mode.Id}|tier{tierIndex}|wave{waveNumber}|{sample}"));
                        var wave = EnemyWave.GenerateWave(waveNumber, rng, ruleset, campaignType);
                        var target = wave.Targets[0];

                        var tier = GameConstants.GetTierForWave(wave.WaveNumber);

                        double waveDistanceAu = wave.CurrentDistance / GameConstants.AU_TO_METERS;
                        var det = detection.GetDetectionStatus(wave);
                        double effectiveDetRangeAu = detection.CalculateEffectiveDetectionRange(wave);
                        double warningTimeSeconds = detection.CalculateWarningTime(wave);
                        double minSafeTimeSeconds = tier.TimeToImpactMin * 0.1;

                        double timeToImpactSeconds = wave.CurrentDistance / Math.Max(1e-9, wave.AverageVelocity);
                        double timeToGunRangeBaseSeconds = Math.Max(0.0, (wave.InitialDistance - tier.MaxEffectiveGunRange) / Math.Max(1e-9, wave.AverageVelocity));
                        double timeMult = GameModeTuning.Current.GetTimeBudgetMultiplier(mode);
                        double timeToGunRangeTunedSeconds = timeToGunRangeBaseSeconds * timeMult;
                        long availableYearsRounded = Math.Max(1, (long)Math.Round(timeToGunRangeTunedSeconds / GameConstants.SECONDS_PER_YEAR));

                        double rcsRaw = wave.AverageRadarCrossSection;
                        double rcsModeAdjusted = rcsRaw * GameModeTuning.Current.GetDetectionRcsMultiplier(mode);
                        double rcsDisplay = rcsModeAdjusted * diffConfigFull.TargetRcsMultiplier;

                        double hitToleranceMeters;
                        if (diffConfigFull.IsTutorialMode)
                        {
                            hitToleranceMeters = DifficultyConfig.TutorialBeachball.RadiusMeters;
                        }
                        else
                        {
                            double diameterFromRcs = 2.0 * Math.Sqrt(rcsDisplay / Math.PI);
                            hitToleranceMeters = diameterFromRcs * 0.5 * diffConfigFull.HitToleranceMultiplier * GameModeTuning.Current.GetHitToleranceMultiplier(mode);
                        }

                        csv.AppendLine(string.Join(",",
                            EscapeCsv(mode.Id.ToString()),
                            EscapeCsv(mode.DisplayName),
                            EscapeCsv(rulesetLabel),
                            EscapeCsv(mode.Difficulty.ToString()),
                            tierIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            waveNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            sample.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            EscapeCsv(campaignType.Archetype.Id),
                            EscapeCsv(campaignType.CustomName),
                            EscapeCsv(wave.Archetype?.Id),
                            EscapeCsv(wave.Archetype?.Name),
                            wave.ShipCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            wave.InitialDistance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            wave.AverageVelocity.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            rcsRaw.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            rcsModeAdjusted.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            rcsDisplay.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            wave.HasStealthCoating ? "1" : "0",
                            target.Acceleration.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Maneuverability.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Defense.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            target.Mass.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            target.FractureEnergy.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),

                            waveDistanceAu.ToString("F9", System.Globalization.CultureInfo.InvariantCulture),
                            effectiveDetRangeAu.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            det.IsDetected ? "1" : "0",
                            EscapeCsv(det.Quality.ToString()),
                            warningTimeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            minSafeTimeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),

                            timeToImpactSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            timeToGunRangeBaseSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            timeToGunRangeTunedSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            availableYearsRounded.ToString(System.Globalization.CultureInfo.InvariantCulture),

                            tier.MaxEffectiveGunRange.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            diffConfigFull.HitToleranceMultiplier.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            GameModeTuning.Current.GetHitToleranceMultiplier(mode).ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            diffConfigFull.TargetRcsMultiplier.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                            hitToleranceMeters.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
                        ));
                    }
                }
            }

            string fileName = $"EnemyCurve_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            try
            {
                File.WriteAllText(fileName, csv.ToString(), Encoding.UTF8);
                Console.WriteLine($"\n✓ Enemy curve CSV written to: {Path.GetFullPath(fileName)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Failed to write CSV file: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to return to test menu...");
            Console.ReadKey();
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