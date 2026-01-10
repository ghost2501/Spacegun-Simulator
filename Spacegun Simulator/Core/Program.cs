using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Diagnostics;
using Spacegun_Simulator.UI.Flows;
using Spacegun_Simulator.UI.Pages.Core;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Development.Weapons;
using Spacegun_Simulator.Tests;

namespace Spacegun_Simulator.Core
{
    public class Program
    {
        public static void Main(string[] args)
        {
            _ = ConsoleUiBootstrap.ConfigureForUi();

            // Testing/compat flag: force ASCII-only rendering even if UTF-8 is available.
            if (args.Any(a => string.Equals(a, "--ascii-ui", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(a, "--ascii", StringComparison.OrdinalIgnoreCase)))
            {
                ConsoleTextMode.EnableAsciiOnly(forcedByUser: true);
            }

            if (args.Any(a => string.Equals(a, "--console-diag", StringComparison.OrdinalIgnoreCase)))
            {
                try { ConsoleUiBootstrap.WriteDiagnostics(Console.Error); } catch { }
            }

            // Headless internal consistency checks (no UI).
            // Useful as a quick gate when editing JSON catalogs and tuning.
            if (args.Any(a => string.Equals(a, "--consistency-check", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(a, "--consistency", StringComparison.OrdinalIgnoreCase)))
            {
                // Match the tuning harness and main game: load config/tuning overrides.
                GameConfigLoader.LoadIfExists();
                EnemyConfigLoader.LoadOrThrow();
                DevelopmentTuningLoader.LoadIfExists();
                WeaponsTuningLoader.LoadIfExists();
                WeaponsUpgradesLoader.LoadIfExists();
                ProjectilesCatalogLoader.LoadIfExists();

                var results = FireSimulatorDiagnostics.RunConsistencyChecks();
                int pass = 0;
                foreach (var r in results)
                    if (r.Passed) pass++;

                Console.WriteLine($"Consistency checks: {pass}/{results.Count} passed");
                foreach (var r in results)
                {
                    string mark = r.Passed ? "OK" : "FAIL";
                    Console.WriteLine($"  [{mark}] {r.Name}: {r.Message}");
                }

                if (pass != results.Count)
                    Environment.ExitCode = 1;

                return;
            }

            // Diagnostics smoke checks (headless).
            // Keeps normal UX unchanged unless an explicit flag is provided.
            if (args.Any(a => string.Equals(a, "--tuninglab-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                RunTuningLabSmoke();
                return;
            }

            if (args.Any(a => string.Equals(a, "--tuninglab-report", StringComparison.OrdinalIgnoreCase)))
            {
                RunTuningLabEnergyReport();
                return;
            }

            GameConfigLoader.LoadIfExists();
            EnemyConfigLoader.LoadOrThrow();
            DevelopmentTuningLoader.LoadIfExists();
            WeaponsTuningLoader.LoadIfExists();
            WeaponsUpgradesLoader.LoadIfExists();
            ProjectilesCatalogLoader.LoadIfExists();

            // Save files should live in a per-user location (no admin required).
            // Best-effort migrate any legacy install-relative saves.
            UserDataPaths.MigrateLegacySavesIfNeeded();
            UserDataPaths.EnsureSavesDirectoryExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            Thread.Sleep(500);

            if (args is not null && args.Length > 0 && args.Contains("--tuninglab-run", StringComparer.OrdinalIgnoreCase))
            {
                RunTuningLabHeadless();
                return;
            }
            // Single source of truth for the app session:
            // boot UI -> gameplay -> back to boot UI (when requested) -> exit
            string nextBootStartPage = PageId.Title;
            while (true)
            {
                // Boot into new page-based UI for Title + Main Menu (+ difficulty selection for New Game).
                var entry = UiEntryPoint.Run(nextBootStartPage);
                nextBootStartPage = PageId.Title;

                switch (entry.Choice)
                {
                    case MainMenuChoice.Exit:
                        return;

                    // "None" means boot UI ended without starting a game
                    // (e.g. cancelled difficulty selection).
                    // Re-run boot UI instead of exiting the process.
                    case MainMenuChoice.None:
                        ResetConsoleForBootUi();
                        continue;

                    case MainMenuChoice.NewGame:
                        {
                            var mode = entry.Mode ?? GameModeId.Economy_KineticDronesVsRobotAsteroids;
                            var gameState = new GameState(mode: mode);

                            var exitAction = GameSessionFlow.Run(gameState);

                            // IMPORTANT: gameplay/UI flows can leave Console.Out/cursor state altered.
                            ResetConsoleForBootUi();

                            if (exitAction == GameSessionExitAction.ExitGame)
                                return;

                            if (exitAction == GameSessionExitAction.ReturnToMenu)
                                nextBootStartPage = PageId.MainMenu;
                            continue;
                        }

                    case MainMenuChoice.Resume:
                        {
                            var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids);
                            if (!gameState.LoadAutoSave())
                            {
                                ResetConsoleForBootUi();
                                Console.WriteLine("No autosave found (or failed to load). Press any key...");
                                Console.ReadKey(true);
                                ResetConsoleForBootUi();
                                continue;
                            }

                            var exitAction = GameSessionFlow.Run(gameState);

                            ResetConsoleForBootUi();

                            if (exitAction == GameSessionExitAction.ExitGame)
                                return;

                            if (exitAction == GameSessionExitAction.ReturnToMenu)
                                nextBootStartPage = PageId.MainMenu;
                            continue;
                        }

                    case MainMenuChoice.TestMode:
                        {
                            var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids);
                            DiagnosticsEntryPoint.Run(gameState);

                            ResetConsoleForBootUi();
                            continue;
                        }

                    default:
                        ResetConsoleForBootUi();
                        continue;
                }
            }
        }

        private static void RunTuningLabSmoke()
        {
            Console.WriteLine("TuningLab smoke: computing tuning curve...");

            // Match the tuning harness and main game: load config/tuning overrides.
            GameConfigLoader.LoadIfExists();
            EnemyConfigLoader.LoadOrThrow();
            DevelopmentTuningLoader.LoadIfExists();
            WeaponsTuningLoader.LoadIfExists();
            WeaponsUpgradesLoader.LoadIfExists();
            ProjectilesCatalogLoader.LoadIfExists();

            Console.WriteLine(
                "TuningLab smoke: baselines loaded: " +
                $"MuzzleVelocityMultiplier={GameConstants.MuzzleVelocityMultiplier:F3}, " +
                $"DefaultBarrelLength={WeaponsTuning.Gun.DefaultBarrelLength:F1}, " +
                $"DefaultFireControlQuality={WeaponsTuning.Gun.DefaultFireControlQuality:F3}, " +
                $"ProjectileDefaults.Mass={DevelopmentTuning.ProjectileDefaults.Mass:F1}");

            var res = FireSimulatorDiagnostics.ComputeTuningCurveByTier(
                ruleset: EnemyGenerationRuleset.Full,
                difficulty: GameDifficulty.CometsAndAsteroids,
                weaponsTechLevel: 1,
                radarLevel: 1,
                overrideEnemyMass: false,
                enemyMassKg: 1_000_000.0,
                overrideEnemyFractureEnergy: false,
                enemyFractureEnergy: 10_000.0,
                overrideEnemyManeuverability: false,
                enemyManeuverability: 1.0,
                overrideEnemyOffense: false,
                enemyOffense: 1.0,
                overrideEnemyDefense: false,
                enemyDefense: 1.0,
                overrideBarrelLength: false,
                barrelLength: 100.0,
                overrideFireControlQuality: false,
                fireControlQuality: 1.0,
                overrideMuzzleVelocityMultiplier: false,
                muzzleVelocityMultiplier: 1.0,
                overrideProjectileMass: false,
                projectileMassKg: 10.0,
                overrideProjectileDefense: false,
                projectileDefense: 0.0,
                overridePenetration: false,
                penetration: 1.0,
                overrideHitToleranceMultiplier: false,
                hitToleranceMultiplier: 1.0,
                overridePropulsionDeltaV: false,
                propulsionDeltaVCapacityMs: 0.0,
                overridePropulsionBurnDuration: false,
                propulsionBurnDurationSeconds: 1.0,
                overridePropulsionReferenceMass: false,
                propulsionReferenceMassKg: 10.0,
                samplesPerWave: 3,
                shotsPerSample: 20,
                simulateAimError: false,
                smoothTierSampling: true,
                overrideEnemyVelocity: false,
                enemyVelocityMs: 0.0,
                overrideEnemyDensity: false,
                enemyDensityGcm3: 7.85,
                overrideEnemyMaterialStrength: false,
                enemyBulkModulusGpa: 200.0);

            int tierCount = Math.Min(
                res.ExpectedHitRateByTier.Length,
                Math.Min(res.DetectionRateByTier.Length, res.BallisticsOkRateByTier.Length));

            Console.WriteLine($"TuningLab smoke: tiers={tierCount}");
            for (int i = 0; i < Math.Min(3, tierCount); i++)
            {
                Console.WriteLine(
                    $"Tier {i}: Det={res.DetectionRateByTier[i]:F3} BallisticsOK={res.BallisticsOkRateByTier[i]:F3} ExpectedHit={res.ExpectedHitRateByTier[i]:F3} ObservedHit={res.ObservedHitRateByTier[i]:F3}");
            }

            // Phase 6: deterministic regression check for ResolveWeaponStats pipeline.
            try
            {
                var golden = FireSimulatorDiagnostics.RunGoldenScenarioRegressionCheck();
                Console.WriteLine($"TuningLab smoke: {golden.Name}: {(golden.Passed ? "PASS" : "FAIL")} - {golden.Message}");
                if (!golden.Passed)
                    throw new InvalidOperationException(golden.Message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("TuningLab smoke: golden scenarios FAILED: " + ex.Message);
                throw;
            }

            try
            {
                double baseV = Math.Max(1.0, WeaponsTuning.GetBaseMuzzleVelocityForTechLevel(1));
                double barrelEfficiency = Math.Min(1.0, 100.0 / 200.0);
                double barrelVelocityMultiplier = (0.5 + 0.5 * barrelEfficiency);
                double effV = baseV * barrelVelocityMultiplier;
                Console.WriteLine($"Smoke tuning: WeaponTech1BaseV={baseV:F0} m/s, BarrelMult={barrelVelocityMultiplier:F3}, EffectiveMaxV={effV:F0} m/s");
                Console.WriteLine($"Smoke tuning: TierEnemyMaxVelocity=[{string.Join(", ", GameConstants.TierEnemyMaxVelocity.Select(v => v.ToString("F0")))}] m/s");
            }
            catch { }

            // Extra debug: validate a single sample in tiers 0/1/2 using the solver's cached inputs.
            // This helps distinguish energy gating vs geometry/accuracy invalidity.
            try
            {
                const double barrelLengthMeters = 100.0;
                double barrelEfficiency = Math.Min(1.0, barrelLengthMeters / 200.0);
                double barrelVelocityMultiplier = (0.5 + 0.5 * barrelEfficiency);
                double maxGunVelocity = Math.Max(1.0, WeaponsTuning.GetBaseMuzzleVelocityForTechLevel(1) * barrelVelocityMultiplier);

                var campaignType = EnemyType.GenerateForCampaign(new Random(4242));
                foreach (int waveNumber in new[] { 1, 6, 11 })
                {
                    var rng = new Random(StableSeed($"smoke-sample|wave{waveNumber}"));
                    var wave = EnemyWave.GenerateWave(waveNumber, rng, EnemyGenerationRuleset.Full, campaignType);
                    var target = wave.Targets[0];

                    var tier = GameConstants.GetTierForWave(waveNumber);
                    double gunRangeMeters = tier.MaxEffectiveGunRange;

                    double defenseScale = Math.Max(0.0, GameModeTuning.Current.FractureEnergyDefenseScale);
                    double armoredFracture = Math.Max(0.0, target.FractureEnergy * (1.0 + defenseScale * Math.Clamp(target.Defense, 0.0, 1.0)));
                    double effectiveFractureEnergy = armoredFracture; // penetration=1.0

                    var calc = new FiringSolution(
                        projectileMass: 10.0f,
                        enemyFractureEnergy: (float)effectiveFractureEnergy,
                        enemyMass: target.Mass,
                        enemyCrossSectionM2: target.CrossSection);
                    calc.ConfigureProjectileModifiers(
                        additionalHitToleranceMultiplier: 1.0,
                        propulsionDeltaVCapacityMs: 0.0,
                        propulsionBurnDurationSeconds: 1.0,
                        propulsionReferenceMassKg: 10.0);

                    var problem = calc.GenerateFiringProblem(
                        wave,
                        playerGunMaxVelocity: (float)maxGunVelocity,
                        gunEffectiveRange: (float)gunRangeMeters,
                        rng: new Random(StableSeed($"smoke-problem|wave{waveNumber}")));

                    var sol = calc.CalculateSolution(
                        problem.EnemyPosition,
                        problem.EnemyVelocity,
                        problem.CorrectLaunchDelayTime,
                        problem.CorrectElevation,
                        problem.CorrectAzimuth,
                        problem.CorrectVelocity,
                        (float)maxGunVelocity,
                        (float)gunRangeMeters,
                        waveNumber,
                        target.Mass,
                        GameDifficulty.CometsAndAsteroids);

                    // Also ask the tuning harness's baseline search what the best shot is for this geometry.
                    // This helps confirm whether the issue is in cached hints or in the solver itself.
                    string baselineInfo = string.Empty;
                    if (FireSimulatorDiagnostics.TryFindBaselineBallisticSolution(
                        calculator: calc,
                        enemyPosition: problem.EnemyPosition,
                        enemyVelocity: problem.EnemyVelocity,
                        maxGunVelocity: maxGunVelocity,
                        gunEffectiveRange: gunRangeMeters,
                        waveNumber: waveNumber,
                        enemyMass: target.Mass,
                        difficulty: GameDifficulty.CometsAndAsteroids,
                        requireDestroy: true,
                        baseline: out var baseline,
                        result: out var baselineRes))
                    {
                        baselineInfo = $" | BaselineFound: Hit={baselineRes.CanHit} Destroy={baselineRes.CanDestroy} Dev={baselineRes.InterceptDeviation:F1}m (delay={baseline.DelaySeconds:F1} elev={baseline.ElevDeg:F2} azim={baseline.AzimDeg:F2} v={baseline.VelocityMs:F0})";
                    }
                    else
                    {
                        baselineInfo = $" | BaselineMissBest: Destroy={baselineRes.CanDestroy} Dev={baselineRes.InterceptDeviation:F1}m (delay={baseline.DelaySeconds:F1} elev={baseline.ElevDeg:F2} azim={baseline.AzimDeg:F2} v={baseline.VelocityMs:F0})";
                    }

                    double keMj = BallisticsCalculator.CalculateKineticEnergyMJ(10.0, sol.ImpactVelocityMs > 0 ? sol.ImpactVelocityMs : problem.CorrectVelocity);
                    Console.WriteLine(
                        $"Tier {tier.TierIndex} sample (wave {waveNumber}): Mass={target.Mass * 1000.0:F0}kg RCS={target.CrossSection:F1}m^2 Fracture={effectiveFractureEnergy:F0}MJ KE~={keMj:F0}MJ Cached: Valid={sol.SolutionValid} Hit={sol.CanHit} Destroy={sol.CanDestroy} Dev={sol.InterceptDeviation:F1}m{baselineInfo}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tier 0 sample: ERROR: {ex.Message}");
            }
        }

        private static void RunTuningLabEnergyReport()
        {
            Console.WriteLine("TuningLab report: computing tier energy/feasibility breakdown...");

            // Load config/tuning overrides so the report matches gameplay.
            GameConfigLoader.LoadIfExists();
            EnemyConfigLoader.LoadOrThrow();
            DevelopmentTuningLoader.LoadIfExists();
            WeaponsTuningLoader.LoadIfExists();
            WeaponsUpgradesLoader.LoadIfExists();
            ProjectilesCatalogLoader.LoadIfExists();

            // Optional CLI overrides to make this report match a specific Tuning Lab setup.
            // Examples:
            //   --tuninglab-report --samples=10 --muzzle=2.0
            //   --tuninglab-report --samples=10 --muzzle=2.0 --tech=2 --radar=2
            int GetIntArg(string name, int fallback)
            {
                try
                {
                    var arg = Environment.GetCommandLineArgs()
                        .FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
                    if (arg is null) return fallback;
                    var raw = arg[(name.Length + 1)..];
                    return int.TryParse(raw, out var v) ? v : fallback;
                }
                catch { return fallback; }
            }

            double GetDoubleArg(string name, double fallback)
            {
                try
                {
                    var arg = Environment.GetCommandLineArgs()
                        .FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
                    if (arg is null) return fallback;
                    var raw = arg[(name.Length + 1)..];
                    return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
                        ? v
                        : fallback;
                }
                catch { return fallback; }
            }

            int samplesPerTier = GetIntArg("--samples", 75);
            int weaponsTechLevel = GetIntArg("--tech", 1);
            int radarLevel = GetIntArg("--radar", 1);
            double muzzleVelocityMultiplier = GetDoubleArg("--muzzle", 1.0);

            var report = FireSimulatorDiagnostics.ComputeTuningEnergyReportByTier(
                ruleset: EnemyGenerationRuleset.Full,
                difficulty: GameDifficulty.CometsAndAsteroids,
                weaponsTechLevel: weaponsTechLevel,
                radarLevel: radarLevel,
                overrideEnemyMass: false,
                enemyMassKg: 1_000_000.0,
                overrideEnemyFractureEnergy: false,
                enemyFractureEnergy: 10_000.0,
                overrideEnemyDensity: false,
                enemyDensityGcm3: 7.85,
                overrideEnemyMaterialStrength: false,
                enemyBulkModulusGpa: 200.0,
                overrideEnemyManeuverability: false,
                enemyManeuverability: 1.0,
                overrideEnemyOffense: false,
                enemyOffense: 1.0,
                overrideEnemyDefense: false,
                enemyDefense: 1.0,
                overrideBarrelLength: false,
                barrelLength: 100.0,
                overrideFireControlQuality: false,
                fireControlQuality: 1.0,
                overrideMuzzleVelocityMultiplier: true,
                muzzleVelocityMultiplier: muzzleVelocityMultiplier,
                overrideProjectileMass: false,
                projectileMassKg: 10.0,
                overrideProjectileDefense: false,
                projectileDefense: 0.0,
                overridePenetration: false,
                penetration: 1.0,
                overrideHitToleranceMultiplier: false,
                hitToleranceMultiplier: 1.0,
                overridePropulsionDeltaV: false,
                propulsionDeltaVCapacityMs: 0.0,
                overridePropulsionBurnDuration: false,
                propulsionBurnDurationSeconds: 1.0,
                overridePropulsionReferenceMass: false,
                propulsionReferenceMassKg: 10.0,
                overrideEnemyVelocity: false,
                enemyVelocityMs: 0.0,
                samplesPerTier: samplesPerTier,
                smoothTierSampling: true);

            string csvPath = FireSimulatorDiagnostics.WriteTuningLabEnergyReportCsv(report, includeMissedButDetected: true);
            Console.WriteLine($"TuningLab report: wrote {csvPath}");

            // Small console summary for quick tuning iteration.
            for (int t = 0; t < Math.Min(GameConstants.TierCount, report.SamplesByTier.Length); t++)
            {
                int detCount = report.DetectedByTier[t];
                int detDenom = Math.Max(1, detCount);
                int energyOkCount = report.EnergySufficientByTier[t];
                int canHitCount = report.CanHitByTier[t];
                int ballisticsOkCount = report.BallisticsOkByTier[t];
                int energyGatedCount = report.EnergyGatedByTier[t];
                int missedButDetectedCount = Math.Max(0, detCount - canHitCount);

                double energySufficientRate = (double)energyOkCount / detDenom;
                double canHitRate = (double)canHitCount / detDenom;
                double ballisticsOkRate = (double)ballisticsOkCount / detDenom;
                double energyGateRate = (double)energyGatedCount / detDenom;
                double missedButDetectedRate = (double)missedButDetectedCount / detDenom;

                Console.WriteLine(
                    $"Tier {t}: Det={report.DetectedByTier[t]}/{report.SamplesByTier[t]} " +
                    $"EnergyOk={energyOkCount}/{detCount} ({energySufficientRate:F3}) " +
                    $"CanHit={canHitCount}/{detCount} ({canHitRate:F3}) " +
                    $"MissedButDetected={missedButDetectedCount}/{detCount} ({missedButDetectedRate:F3}) " +
                    $"BallisticsOk={ballisticsOkCount}/{detCount} ({ballisticsOkRate:F3}) " +
                    $"EnergyGated={energyGatedCount}/{detCount} ({energyGateRate:F3}) " +
                    $"AvgKE={report.AvgKineticEnergyMJByTier[t]:F0}MJ AvgFracture={report.AvgEffectiveFractureEnergyMJByTier[t]:F0}MJ " +
                    $"AvgKE/Frac={report.AvgKeToFractureRatioByTier[t]:F3}"
                );
            }
        }

        private static int StableSeed(string key)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(key ?? string.Empty);
            byte[] hash = sha.ComputeHash(bytes);
            int seed = BitConverter.ToInt32(hash, 0);
            return seed == int.MinValue ? 0 : Math.Abs(seed);
        }

        private static void RunTuningLabHeadless()
        {
            Console.WriteLine("TuningLab headless: computing and appending CSV...");

            // Match the tuning harness and main game: load config/tuning overrides.
            GameConfigLoader.LoadIfExists();
            EnemyConfigLoader.LoadOrThrow();
            DevelopmentTuningLoader.LoadIfExists();
            WeaponsTuningLoader.LoadIfExists();
            WeaponsUpgradesLoader.LoadIfExists();
            ProjectilesCatalogLoader.LoadIfExists();

            // Match TuningLabPage defaults (except we use Hard difficulty by default for balancing).
            const EnemyGenerationRuleset ruleset = EnemyGenerationRuleset.Full;
            const GameDifficulty difficulty = GameDifficulty.CometsAndAsteroids;
            const int radarLevel = 1;
            const int samplesPerWave = 5;
            const int shotsPerSample = 200;
            const bool simulateAimError = false;

            const bool overrideEnemyMass = false;
            const double enemyMassKg = 1_000_000.0;
            const bool overrideEnemyFractureEnergy = false;
            const double enemyFractureEnergy = 10_000.0;
            const bool overrideEnemyManeuverability = false;
            const double enemyManeuverability = 1.0;
            const bool overrideEnemyOffense = false;
            const double enemyOffense = 1.0;
            const bool overrideEnemyDefense = false;
            const double enemyDefense = 1.0;

            const bool overrideBarrelLength = false;
            const double barrelLength = 100.0;
            const bool overrideFireControlQuality = false;
            const double fireControlQuality = 1.0;
            const bool overrideMuzzleVelocityMultiplier = false;
            const double muzzleVelocityMultiplier = 1.0;
            const bool overrideProjectileMass = false;
            const double projectileMassKg = 100.0;
            const bool overrideProjectileDefense = false;
            const double projectileDefense = 0.0;
            const bool overridePenetration = false;
            const double penetration = 1.0;
            const bool overrideHitToleranceMultiplier = false;
            const double hitToleranceMultiplier = 1.0;
            const bool overridePropulsionDeltaV = false;
            const double propulsionDeltaVCapacityMs = 0.0;
            const bool overridePropulsionBurnDuration = false;
            const double propulsionBurnDurationSeconds = 1.0;
            const bool overridePropulsionReferenceMass = false;
            const double propulsionReferenceMassKg = 10.0;

            var res = FireSimulatorDiagnostics.ComputeTuningCurveByTier(
                ruleset: ruleset,
                difficulty: difficulty,
                weaponsTechLevel: 1,
                radarLevel: radarLevel,
                overrideEnemyMass: overrideEnemyMass,
                enemyMassKg: enemyMassKg,
                overrideEnemyFractureEnergy: overrideEnemyFractureEnergy,
                enemyFractureEnergy: enemyFractureEnergy,
                overrideEnemyDensity: false,
                enemyDensityGcm3: 7.85,
                overrideEnemyMaterialStrength: false,
                enemyBulkModulusGpa: 200.0,
                overrideEnemyManeuverability: overrideEnemyManeuverability,
                enemyManeuverability: enemyManeuverability,
                overrideEnemyOffense: overrideEnemyOffense,
                enemyOffense: enemyOffense,
                overrideEnemyDefense: overrideEnemyDefense,
                enemyDefense: enemyDefense,
                overrideBarrelLength: overrideBarrelLength,
                barrelLength: barrelLength,
                overrideFireControlQuality: overrideFireControlQuality,
                fireControlQuality: fireControlQuality,
                overrideMuzzleVelocityMultiplier: overrideMuzzleVelocityMultiplier,
                muzzleVelocityMultiplier: muzzleVelocityMultiplier,
                overrideProjectileMass: overrideProjectileMass,
                projectileMassKg: projectileMassKg,
                overrideProjectileDefense: overrideProjectileDefense,
                projectileDefense: projectileDefense,
                overridePenetration: overridePenetration,
                penetration: penetration,
                overrideHitToleranceMultiplier: overrideHitToleranceMultiplier,
                hitToleranceMultiplier: hitToleranceMultiplier,
                overridePropulsionDeltaV: overridePropulsionDeltaV,
                propulsionDeltaVCapacityMs: propulsionDeltaVCapacityMs,
                overridePropulsionBurnDuration: overridePropulsionBurnDuration,
                propulsionBurnDurationSeconds: propulsionBurnDurationSeconds,
                overridePropulsionReferenceMass: overridePropulsionReferenceMass,
                propulsionReferenceMassKg: propulsionReferenceMassKg,
                samplesPerWave: samplesPerWave,
                shotsPerSample: shotsPerSample,
                simulateAimError: simulateAimError,
                smoothTierSampling: false,
                overrideEnemyVelocity: false,
                enemyVelocityMs: 0.0);

            try
            {
                string csvPath = FireSimulatorDiagnostics.AppendTuningLabRunCsv(
                    ruleset: ruleset,
                    difficulty: difficulty,
                    weaponsTechLevel: 1,
                    radarLevel: radarLevel,
                    samplesPerWave: samplesPerWave,
                    shotsPerSample: shotsPerSample,
                    simulateAimError: simulateAimError,
                    smoothTierSampling: false,
                    overrideEnemyDensity: false,
                    enemyDensityGcm3: 0.0,
                    overrideEnemyMaterialStrength: false,
                    enemyBulkModulusGpa: 0.0,
                    overrideEnemyMass: overrideEnemyMass,
                    enemyMassKg: enemyMassKg,
                    overrideEnemyFractureEnergy: overrideEnemyFractureEnergy,
                    enemyFractureEnergy: enemyFractureEnergy,
                    overrideEnemyVelocity: false,
                    enemyVelocityMs: 0.0,
                    overrideEnemyManeuverability: overrideEnemyManeuverability,
                    enemyManeuverability: enemyManeuverability,
                    overrideEnemyOffense: overrideEnemyOffense,
                    enemyOffense: enemyOffense,
                    overrideEnemyDefense: overrideEnemyDefense,
                    enemyDefense: enemyDefense,
                    barrelLengthMeters: barrelLength,
                    fireControlQuality: fireControlQuality,
                    muzzleVelocityMultiplier: muzzleVelocityMultiplier,
                    projectileMassKg: projectileMassKg,
                    projectileDefense: projectileDefense,
                    penetration: penetration,
                    hitToleranceMultiplier: hitToleranceMultiplier,
                    propulsionDeltaVCapacityMs: propulsionDeltaVCapacityMs,
                    propulsionBurnDurationSeconds: propulsionBurnDurationSeconds,
                    propulsionReferenceMassKg: propulsionReferenceMassKg,
                    result: res);

                Console.WriteLine($"TuningLab headless: appended {csvPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TuningLab headless: CSV export failed: {ex.Message}");
            }
        }

        private static void ResetConsoleForBootUi()
        {
            try
            {
                Console.ResetColor();
                Console.CursorVisible = true;
            }
            catch { }

            // Best-effort: ensure we're not still writing through a legacy PageBuffer/IndentTextWriter.
            try
            {
                // NOTE: Some code paths may have swapped Console.Out to a custom writer.
                // We can't "recover" the original standard output stream perfectly here,
                // but we can at least avoid leaving Console in an indented/buffered writer.
                Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            }
            catch { }

            try { Console.Clear(); } catch { }
        }
    }
}