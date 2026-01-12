using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Diagnostics;
using Spacegun_Simulator.UI.Flows;
using Spacegun_Simulator.UI.Pages.Core;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Enemies;
using TechTree = Spacegun_Simulator.Development.Technology.TechTree;
using Spacegun_Simulator.Development.Projectiles;
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

            // Headless full-campaign simulation.
            // Purpose: quickly test economy + tech progression without manual firing input.
            // Notes:
            // - Auto-allocates all available years each wave.
            // - Auto-researches any affordable tech (priority-ordered).
            // - Forces a successful firing outcome (still consumes barrel wear).
            if (args.Any(a => string.Equals(a, "--test-campaign", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(a, "--autoplay-campaign", StringComparison.OrdinalIgnoreCase)))
            {
                RunTestCampaign(args);
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

        private static void RunTestCampaign(string[] args)
        {
            // Match the tuning harness and main game: load config/tuning overrides.
            GameConfigLoader.LoadIfExists();
            EnemyConfigLoader.LoadOrThrow();
            DevelopmentTuningLoader.LoadIfExists();
            WeaponsTuningLoader.LoadIfExists();
            WeaponsUpgradesLoader.LoadIfExists();
            ProjectilesCatalogLoader.LoadIfExists();

            int? seed = TryParseIntArg(args, "--seed");
            int? seedStart = TryParseIntArg(args, "--seed-start");
            int seedCount = TryParseIntArg(args, "--seed-count") ?? 0;
            int waves = TryParseIntArg(args, "--waves") ?? GameConstants.TotalWaves;
            waves = Math.Max(1, waves);

            bool auditBallistics = args.Any(a => string.Equals(a, "--ballistics-audit", StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(a, "--audit-ballistics", StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(a, "--audit-can-hit", StringComparison.OrdinalIgnoreCase));

            string? auditBallisticsCsvPath = TryParseStringArg(args, "--ballistics-audit-csv")
                                          ?? TryParseStringArg(args, "--campaign-audit-csv");
            if (!string.IsNullOrWhiteSpace(auditBallisticsCsvPath))
                auditBallistics = true;

            bool ballisticsDrivenResearch = args.Any(a => string.Equals(a, "--ballistics-driven-research", StringComparison.OrdinalIgnoreCase)
                                                      || string.Equals(a, "--audit-driven-research", StringComparison.OrdinalIgnoreCase));

            GameModeId mode = TryParseEnumArg<GameModeId>(args, "--mode")
                ?? GameModeId.Economy_KineticDronesVsRobotAsteroids;

            // Multi-seed sweep mode: run multiple campaigns in-process.
            // Example:
            //   --test-campaign --seed-start 12340 --seed-count 10 --ballistics-audit --ballistics-audit-csv Releases/CampaignAudit.csv
            if (!seed.HasValue && seedStart.HasValue && seedCount > 0)
            {
                if (!string.IsNullOrWhiteSpace(auditBallisticsCsvPath))
                {
                    // Fresh sweep output by default.
                    try
                    {
                        if (File.Exists(auditBallisticsCsvPath))
                            File.Delete(auditBallisticsCsvPath);
                    }
                    catch { }
                }

                for (int i = 0; i < seedCount; i++)
                {
                    int s = seedStart.Value + i;
                    Console.WriteLine($"[TEST CAMPAIGN SWEEP] seed={s} ({i + 1}/{seedCount})");

                    var nextArgs = BuildArgsForSeed(args, s);
                    RunTestCampaign(nextArgs.ToArray());
                }

                return;

                static List<string> BuildArgsForSeed(string[] originalArgs, int seedValue)
                {
                    var list = new List<string>(originalArgs.Length + 2);
                    for (int i = 0; i < originalArgs.Length; i++)
                    {
                        var a = originalArgs[i];
                        if (a is null)
                            continue;

                        if (a.Equals("--seed", StringComparison.OrdinalIgnoreCase)
                         || a.Equals("--seed-start", StringComparison.OrdinalIgnoreCase)
                         || a.Equals("--seed-count", StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip this flag and its value (if present).
                            if (i < originalArgs.Length - 1)
                                i++;
                            continue;
                        }

                        if (a.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase)
                         || a.StartsWith("--seed-start=", StringComparison.OrdinalIgnoreCase)
                         || a.StartsWith("--seed-count=", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        list.Add(a);
                    }

                    list.Add("--seed");
                    list.Add(seedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return list;
                }
            }

            Console.WriteLine($"[TEST CAMPAIGN] mode={mode} waves={waves} seed={(seed.HasValue ? seed.Value.ToString() : "(random)")}" + (auditBallistics ? " ballisticsAudit=on" : ""));

            var game = new GameState(seed: seed, mode: mode);

            // Aggregate metrics.
            long totalYears = 0;
            double totalBudgetGathered = 0;
            double totalSteelGathered = 0;
            double totalExoticGathered = 0;
            int totalTechUpgrades = 0;

            // Track tech pacing signals needed for balance verification.
            var techEndByTier = new Dictionary<int, (int Radar, int Mining, int Production, int Weapons, int Projectiles)>();
            var firstTierAtTech3 = new Dictionary<TechTree.TechType, int?>
            {
                { TechTree.TechType.Radar, null },
                { TechTree.TechType.Mining, null },
                { TechTree.TechType.Production, null },
                { TechTree.TechType.Weapons, null },
                { TechTree.TechType.Projectiles, null },
            };

            var auditByTier = new Dictionary<int, (int Waves, int NoModsKill, int NoModsHitOnly, int NoModsEnergyOnly, int NoModsNeither, int CurrentKill, int Helpful, int Necessary)>();
            var auditBestProjectileByTier = new Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)>();
            var auditBestBuildByTier = new Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)>();
            var auditBestProjectileMaxTechByTier = new Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)>();
            var auditBestBuildMaxTechByTier = new Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)>();

            static void AccumulateAudit(
                Dictionary<int, (int Waves, int NoModsKill, int NoModsHitOnly, int NoModsEnergyOnly, int NoModsNeither, int CurrentKill, int Helpful, int Necessary)> byTier,
                int tierIndex,
                bool noModsHit,
                bool noModsDestroy,
                bool currentHit,
                bool currentDestroy)
            {
                byTier.TryGetValue(tierIndex, out var m);
                m.Waves++;

                bool noModsKill = noModsHit && noModsDestroy;
                bool currentKill = currentHit && currentDestroy;

                if (noModsKill) m.NoModsKill++;
                else if (noModsHit && !noModsDestroy) m.NoModsHitOnly++;
                else if (!noModsHit && noModsDestroy) m.NoModsEnergyOnly++;
                else m.NoModsNeither++;

                if (currentKill) m.CurrentKill++;

                // Helpful: baseline can't kill, but current can.
                if (!noModsKill && currentKill) m.Helpful++;

                // Necessary: in late tiers, baseline can't kill but current can.
                if (tierIndex >= 3 && !noModsKill && currentKill) m.Necessary++;

                byTier[tierIndex] = m;
            }

            static void AccumulateBestProjectileAudit(
                Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)> byTier,
                int tierIndex,
                bool noModsKill,
                bool bestKill)
            {
                byTier.TryGetValue(tierIndex, out var m);
                m.Waves++;

                if (noModsKill) m.NoModsKill++;
                if (bestKill) m.BestKill++;
                if (!noModsKill && bestKill) m.Helpful++;
                if (tierIndex >= 3 && !noModsKill && bestKill) m.Necessary++;

                byTier[tierIndex] = m;
            }

            static void SetTechLevelsForAudit(GameState game, int weaponsLevel, int projectilesLevel, out int savedWeaponsLevel, out int savedProjectilesLevel)
            {
                if (game.TechTree?.CurrentLevel == null)
                    throw new InvalidOperationException("GameState.TechTree.CurrentLevel is null.");

                if (!game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out savedWeaponsLevel))
                    savedWeaponsLevel = 1;
                if (!game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out savedProjectilesLevel))
                    savedProjectilesLevel = 1;

                game.TechTree.CurrentLevel[TechTree.TechType.Weapons] = Math.Clamp(weaponsLevel, 1, 3);
                game.TechTree.CurrentLevel[TechTree.TechType.Projectiles] = Math.Clamp(projectilesLevel, 1, 3);
            }

            static void RestoreTechLevelsForAudit(GameState game, int savedWeaponsLevel, int savedProjectilesLevel)
            {
                if (game.TechTree?.CurrentLevel == null)
                    return;

                game.TechTree.CurrentLevel[TechTree.TechType.Weapons] = Math.Clamp(savedWeaponsLevel, 1, 3);
                game.TechTree.CurrentLevel[TechTree.TechType.Projectiles] = Math.Clamp(savedProjectilesLevel, 1, 3);
            }

            static ResolvedShotStats ApplyModeHitToleranceMultiplier(GameModeDefinition mode, in ResolvedShotStats shot)
            {
                double modeHitToleranceMultiplier = GameModeTuning.Current.GetHitToleranceMultiplier(mode);
                return shot with
                {
                    AdditionalHitToleranceMultiplier = shot.AdditionalHitToleranceMultiplier * modeHitToleranceMultiplier
                };
            }

            static (bool Found, FiringSolutionResult Result) FindBestSolution(
                FiringProblem problem,
                EnemyTarget target,
                in ResolvedShotStats shot,
                double gunEffectiveRange,
                int waveNumber,
                GameDifficulty difficulty,
                bool requireDestroy)
            {
                var calc = new FiringSolution(
                    projectileMass: (float)shot.ProjectileMassKg,
                    enemyFractureEnergy: (float)shot.EffectiveFractureEnergyMJ,
                    enemyMass: target.Mass,
                    enemyCrossSectionM2: target.CrossSection);

                calc.ConfigureProjectileModifiers(shot);

                bool found = FireSimulatorDiagnostics.TryFindBaselineBallisticSolution(
                    calculator: calc,
                    enemyPosition: problem.EnemyPosition,
                    enemyVelocity: problem.EnemyVelocity,
                    maxGunVelocity: shot.MaxLaunchVelocityMs,
                    gunEffectiveRange: gunEffectiveRange,
                    waveNumber: waveNumber,
                    enemyMass: target.Mass,
                    difficulty: difficulty,
                    requireDestroy: requireDestroy,
                    baseline: out _,
                    result: out var res);

                return (found, res ?? new FiringSolutionResult());
            }

            static (CraftedProjectile Projectile, List<Spacegun_Simulator.Core.Stats.StatModifier> Mods, (bool Found, FiringSolutionResult Result) Solution, double TolMeters) FindBestUnlockedBuildBySolver(
                GameState game,
                FiringProblem problem,
                EnemyTarget target,
                double gunEffectiveRange,
                int waveNumber,
                GameDifficulty difficulty,
                GameModeDefinition mode,
                bool requireDestroy,
                Func<CraftedProjectile, List<Spacegun_Simulator.Core.Stats.StatModifier>> getStatMods)
            {
                if (game.Gun == null)
                    throw new InvalidOperationException("GameState.Gun is null.");
                if (game.TechTree?.CurrentLevel == null)
                    throw new InvalidOperationException("GameState.TechTree.CurrentLevel is null.");

                int projectilesTechLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int p)
                    ? Math.Clamp(p, 1, 3)
                    : 1;

                // Performance note:
                // Full cross-product over all unlocked projectile parts is expensive.
                // We cap to a high-quality candidate set, then choose the best via the solver.
                const int MaxCoresToTry = 3;
                const int MaxPropulsionsToTry = 3;
                const int MaxModulesPerSlotToTry = 8;
                const int MaxModuleTriplesToTry = 64;
                const int MaxCandidatesToEvaluate = 800;

                // If we find a build that cleanly kills with good margin, bail early.
                // This keeps seed sweeps tractable while still being solver-driven.
                const double EarlyStopMarginFractionOfTol = 0.33;
                const double EarlyStopMinMarginMeters = 1.0;

                var (minMassKg, maxMassKg) = game.Gun.GetSupportedProjectileMassRangeKg();
                var cores = ProjectileCore.All
                    .Where(c => c is not null
                             && c.RequiredTechLevel <= projectilesTechLevel
                             && c.MassKg >= minMassKg
                             && c.MassKg <= maxMassKg)
                    .OrderByDescending(c => c.RequiredTechLevel)
                    .ThenByDescending(c => c.MassKg)
                    .Take(MaxCoresToTry)
                    .ToList();

                if (cores.Count == 0)
                {
                    cores = ProjectileCore.All.Where(c => c is not null).Take(1).ToList();
                }

                var propulsions = PropulsionSystem.All
                    .Where(ps => ps is not null && ps.RequiredTechLevel <= projectilesTechLevel)
                    .OrderByDescending(ps => ps.DeltaVCapacityMs)
                    .Take(MaxPropulsionsToTry)
                    .ToList();

                if (propulsions.Count == 0)
                    propulsions.Add(PropulsionSystem.None);

                static double DamageScore(ProjectileEnhancement m) => m.Penetration * m.ImpactCoupling;

                static double ModuleTripleScore(ProjectileEnhancement g, ProjectileEnhancement p, ProjectileEnhancement a)
                {
                    double tol = g.HitToleranceBonus + p.HitToleranceBonus + a.HitToleranceBonus;
                    double dmg = DamageScore(g) + DamageScore(p) + DamageScore(a);
                    double tech = Math.Max(g.RequiredTechLevel, Math.Max(p.RequiredTechLevel, a.RequiredTechLevel));
                    return (tol * 1000.0) + (dmg * 10.0) + tech;
                }

                static List<ProjectileEnhancement> GetUnlockedWithNoneCapped(TechTree techTree, ProjectileEnhancementSlot slot, int cap)
                {
                    var list = CraftedProjectile.GetUnlockedModules(techTree, slot)
                        .Where(m => m is not null)
                        .ToList();

                    // Ensure a None module exists (represents empty slot).
                    if (!list.Any(m => m.IsNone))
                        list.Add(ProjectilesCatalog.GetNoneModule(slot));

                    // De-dup by Id to keep cross-loaded configs stable.
                    list = list
                        .GroupBy(m => m.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    // Always keep None, then take the top N by (HitToleranceBonus, Damage).
                    var none = list.FirstOrDefault(m => m.IsNone);
                    var ranked = list
                        .Where(m => !m.IsNone)
                        .OrderByDescending(m => m.HitToleranceBonus)
                        .ThenByDescending(DamageScore)
                        .ThenByDescending(m => m.RequiredTechLevel)
                        .ThenBy(m => m.Name)
                        .Take(Math.Max(1, cap))
                        .ToList();

                    if (none != null)
                        ranked.Insert(0, none);

                    return ranked;
                }

                var guidanceModules = GetUnlockedWithNoneCapped(game.TechTree, ProjectileEnhancementSlot.Guidance, MaxModulesPerSlotToTry);
                var payloadModules = GetUnlockedWithNoneCapped(game.TechTree, ProjectileEnhancementSlot.Payload, MaxModulesPerSlotToTry);
                var armorModules = GetUnlockedWithNoneCapped(game.TechTree, ProjectileEnhancementSlot.Armor, MaxModulesPerSlotToTry);

                var noneGuidance = guidanceModules.FirstOrDefault(m => m.IsNone) ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
                var nonePayload = payloadModules.FirstOrDefault(m => m.IsNone) ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
                var noneArmor = armorModules.FirstOrDefault(m => m.IsNone) ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);

                // Instead of the full per-slot cross-product, cap to top-ranked triplets.
                // This shrinks the candidate space from ~9^3=729 to ~64 combinations.
                var moduleTriples = new List<(ProjectileEnhancement Guidance, ProjectileEnhancement Payload, ProjectileEnhancement Armor)>
                {
                    (noneGuidance, nonePayload, noneArmor)
                };

                var rankedTriples = new List<(ProjectileEnhancement Guidance, ProjectileEnhancement Payload, ProjectileEnhancement Armor, double Score)>();
                foreach (var g in guidanceModules)
                {
                    foreach (var pMod in payloadModules)
                    {
                        foreach (var aMod in armorModules)
                        {
                            if (g.IsNone && pMod.IsNone && aMod.IsNone)
                                continue;
                            rankedTriples.Add((g, pMod, aMod, ModuleTripleScore(g, pMod, aMod)));
                        }
                    }
                }

                foreach (var t in rankedTriples
                    .OrderByDescending(t => t.Score)
                    .ThenByDescending(t => t.Guidance.HitToleranceBonus + t.Payload.HitToleranceBonus + t.Armor.HitToleranceBonus)
                    .ThenBy(t => t.Guidance.Name)
                    .ThenBy(t => t.Payload.Name)
                    .ThenBy(t => t.Armor.Name)
                    .Take(Math.Max(1, MaxModuleTriplesToTry - 1)))
                {
                    moduleTriples.Add((t.Guidance, t.Payload, t.Armor));
                }

                // Save/restore mutable game state.
                var savedCrafted = game.CraftedProjectile;
                var savedStatMods = game.Gun.InstalledStatModifiers;

                (CraftedProjectile Projectile, List<Spacegun_Simulator.Core.Stats.StatModifier> Mods) best = (null!, new List<Spacegun_Simulator.Core.Stats.StatModifier>());
                (bool Found, FiringSolutionResult Result) bestSol = (false, new FiringSolutionResult());
                double bestTol = 0;
                double bestMargin = double.NegativeInfinity;
                int evaluated = 0;

                static bool IsKill(bool found, in FiringSolutionResult r) => found && r.CanHit && r.CanDestroy;
                static bool IsHit(bool found, in FiringSolutionResult r) => found && r.CanHit;
                static double MarginOrNegInf(bool found, in FiringSolutionResult r, double tol)
                    => found ? tol - r.InterceptDeviation : double.NegativeInfinity;

                bool IsBetterCandidate(
                    bool found,
                    in FiringSolutionResult r,
                    double tol,
                    double margin,
                    bool bestFound,
                    in FiringSolutionResult bestR,
                    double bestTolLocal,
                    double bestMarginLocal)
                {
                    bool kill = IsKill(found, r);
                    bool bestKill = IsKill(bestFound, bestR);
                    if (kill != bestKill) return kill;

                    bool hit = IsHit(found, r);
                    bool bestHit = IsHit(bestFound, bestR);
                    if (hit != bestHit) return hit;

                    if (found != bestFound) return found;

                    // Prefer larger hit margin, then smaller deviation as tiebreak.
                    if (margin != bestMarginLocal) return margin > bestMarginLocal;
                    if (found)
                    {
                        if (r.InterceptDeviation != bestR.InterceptDeviation)
                            return r.InterceptDeviation < bestR.InterceptDeviation;

                        // Prefer higher KE margin as last tiebreak.
                        double keMargin = r.KineticEnergyMJ - r.FractureEnergyRequired;
                        double bestKeMargin = bestR.KineticEnergyMJ - bestR.FractureEnergyRequired;
                        if (keMargin != bestKeMargin) return keMargin > bestKeMargin;
                    }

                    // Stable fallback.
                    return tol > bestTolLocal;
                }

                try
                {
                    foreach (var core in cores)
                    {
                        foreach (var prop in propulsions)
                        {
                            foreach (var (guidance, payload, armor) in moduleTriples)
                            {
                                var candidate = new CraftedProjectile(
                                    core: core,
                                    propulsion: prop,
                                    guidanceModule: guidance,
                                    payloadModule: payload,
                                    armorModule: armor,
                                    gunBaseMuzzleVelocityMs: game.Gun.BaseMuzzleVelocityMs);

                                var mods = getStatMods(candidate) ?? new List<Spacegun_Simulator.Core.Stats.StatModifier>();
                                game.CraftedProjectile = candidate;
                                game.Gun.InstalledStatModifiers = mods;

                                var resolved = game.ResolveWeaponStats(target);
                                var shot = ApplyModeHitToleranceMultiplier(mode, resolved.Shot);
                                var sol = FindBestSolution(problem, target, shot, gunEffectiveRange, waveNumber, difficulty, requireDestroy);
                                double tol = FiringSolution.CalculateHitToleranceMeters(difficulty, waveNumber, target.CrossSection, target.Mass, shot.AdditionalHitToleranceMultiplier);
                                double margin = MarginOrNegInf(sol.Found, sol.Result, tol);

                                if (best.Projectile == null || IsBetterCandidate(sol.Found, sol.Result, tol, margin, bestSol.Found, bestSol.Result, bestTol, bestMargin))
                                {
                                    best = (candidate, mods);
                                    bestSol = sol;
                                    bestTol = tol;
                                    bestMargin = margin;
                                }

                                evaluated++;
                                if (evaluated >= MaxCandidatesToEvaluate)
                                    break;

                                bool success = sol.Found && sol.Result.CanHit && (!requireDestroy || sol.Result.CanDestroy);
                                if (success)
                                {
                                    double threshold = Math.Max(EarlyStopMinMarginMeters, tol * EarlyStopMarginFractionOfTol);
                                    if (margin >= threshold)
                                        return (candidate, mods, sol, tol);
                                }
                            }

                            if (evaluated >= MaxCandidatesToEvaluate)
                                break;
                        }

                        if (evaluated >= MaxCandidatesToEvaluate)
                            break;
                    }
                }
                finally
                {
                    game.CraftedProjectile = savedCrafted;
                    game.Gun.InstalledStatModifiers = savedStatMods;
                }

                if (best.Projectile == null)
                {
                    // Fallback: an empty projectile; should never happen.
                    best = (new CraftedProjectile(
                        core: ProjectileCore.All.First(c => c is not null),
                        propulsion: PropulsionSystem.None,
                        guidanceModule: ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance),
                        payloadModule: ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload),
                        armorModule: ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor),
                        gunBaseMuzzleVelocityMs: game.Gun.BaseMuzzleVelocityMs),
                        new List<Spacegun_Simulator.Core.Stats.StatModifier>());
                }

                return (best.Projectile, best.Mods, bestSol, bestTol);
            }

            static List<Spacegun_Simulator.Core.Stats.StatModifier> CreateBestUnlockedUpgradeModifiers(
                GameState game,
                CraftedProjectile projectile)
            {
                if (game.TechTree?.CurrentLevel == null)
                    return new List<Spacegun_Simulator.Core.Stats.StatModifier>();

                int weaponsTechLevel = 1;
                if (game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int w))
                    weaponsTechLevel = Math.Max(1, w);

                int projectilesTechLevel = 1;
                if (game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int p))
                    projectilesTechLevel = Math.Max(1, p);

                bool hasGuidance = projectile?.HasGuidance ?? false;
                string propulsionId = projectile?.Propulsion?.Id ?? "none";

                bool MeetsTechAndComponentRequirements(WeaponsUpgrades.UpgradeDefinition def)
                {
                    if (def.MinWeaponsTechLevel.HasValue && weaponsTechLevel < def.MinWeaponsTechLevel.Value)
                        return false;
                    if (def.MinProjectilesTechLevel.HasValue && projectilesTechLevel < def.MinProjectilesTechLevel.Value)
                        return false;
                    if (def.RequiresGuidanceMod && !hasGuidance)
                        return false;
                    if (!string.IsNullOrWhiteSpace(def.RequiresPropulsion)
                        && !string.Equals(def.RequiresPropulsion, propulsionId, StringComparison.OrdinalIgnoreCase))
                        return false;
                    return true;
                }

                var candidates = WeaponsUpgrades.Definitions
                    .Where(d => d is not null && MeetsTechAndComponentRequirements(d))
                    .ToList();

                var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool progress;
                do
                {
                    progress = false;
                    foreach (var d in candidates)
                    {
                        if (included.Contains(d.Id))
                            continue;

                        bool prereqsMet = d.Prerequisites == null
                            || d.Prerequisites.Length == 0
                            || d.Prerequisites.All(p => included.Contains(p));

                        if (!prereqsMet)
                            continue;

                        included.Add(d.Id);
                        progress = true;
                    }
                }
                while (progress);

                var mods = new List<Spacegun_Simulator.Core.Stats.StatModifier>();
                foreach (var d in candidates)
                {
                    if (!included.Contains(d.Id))
                        continue;

                    if (d.Modifiers == null)
                        continue;

                    foreach (var m in d.Modifiers)
                    {
                        if (m is null) continue;
                        mods.Add(m);
                    }
                }

                return mods;
            }

            while (!game.IsGameOver && game.WavesDefeated < waves)
            {
                int waveNumber = game.CurrentWaveNumber;

                // Ensure per-wave event rolls can happen (GameState currently preserves CurrentWaveEvent).
                game.CurrentWaveEvent = null;

                // ===== Detection =====
                var det = game.ExecuteDetectionPhase();
                if (!det.WaveDetected || game.IsGameOver)
                {
                    Console.WriteLine($"Wave {waveNumber}: NOT DETECTED -> game over");
                    break;
                }

                // ===== Resource allocation =====
                if (game.CurrentPhase == GameState.GamePhase.ResourceAllocation)
                {
                    game.GenerateWaveEvent();

                    long yearsThisWave = game.RemainingYears;
                    totalYears += yearsThisWave;

                    var gathered = AutoAllocateAllYears(game);
                    totalSteelGathered += gathered.Steel;
                    totalBudgetGathered += gathered.Budget;
                    totalExoticGathered += gathered.Exotic;

                    // Tech pacing hint: how close are we to at least one upgrade?
                    // NOTE: GameState.GetAvailableTechs() returns only *already-affordable* techs.
                    // For pacing analysis we want the whole set of upgrades that are researchable.
                    var availableTechs = Development.Technology.TechUnlock.GetAvailableUnlocks(game.TechTree);
                    int techCount = availableTechs?.Count ?? 0;
                    int affordable = 0;
                    long cheapestYears = long.MaxValue;
                    if (availableTechs != null && techCount > 0)
                    {
                        foreach (var t in availableTechs)
                            if (Development.Technology.TechUnlock.CanAffordResearch(t, game.AccumulatedResources))
                                affordable++;

                        double eventMultiplier = game.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;
                        double steelRate = Economy.ResourceGathering.GetEffectiveProductionRate(Economy.ResourceType.Steel, game.TechTree, game.SelectedDifficulty, eventMultiplier);
                        double budgetRate = Economy.ResourceGathering.GetEffectiveProductionRate(Economy.ResourceType.Budget, game.TechTree, game.SelectedDifficulty, eventMultiplier);
                        double exoticRate = Economy.ResourceGathering.GetEffectiveProductionRate(Economy.ResourceType.ExoticMaterials, game.TechTree, game.SelectedDifficulty, eventMultiplier);

                        foreach (var t in availableTechs)
                        {
                            long y = EstimateYearsToAfford(t, steelRate, budgetRate, exoticRate);
                            if (y < cheapestYears)
                                cheapestYears = y;
                        }
                    }

                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.ResourcePhaseCompleted);

                    string techHint = techCount == 0
                        ? "Tech: none"
                        : cheapestYears == long.MaxValue
                            ? $"Tech: affordable {affordable}/{techCount}"
                            : $"Tech: affordable {affordable}/{techCount}, cheapest≈{cheapestYears}y";

                    Console.WriteLine($"Wave {waveNumber}: Resources gathered | Budget={game.AccumulatedResources.GetValueOrDefault("Budget", 0):F0} Steel={game.AccumulatedResources.GetValueOrDefault("Steel", 0):F0} Exotic={game.AccumulatedResources.GetValueOrDefault("Exotic", 0):F1} | {techHint}");
                }

                // ===== Development (auto-research) =====
                if (game.CurrentPhase == GameState.GamePhase.Development)
                {
                    totalTechUpgrades += AutoResearchAffordableTech(game, preferOffense: ballisticsDrivenResearch);
                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.DevelopmentCompleted);
                }

                // ===== Firing (forced hit, no user input) =====
                if (game.CurrentPhase == GameState.GamePhase.Firing)
                {
                    // Ensure a firing problem exists (save/load expects it).
                    var firingPhase = game.ExecuteFiringPhase();

                    if (auditBallistics)
                    {
                        try
                        {
                            if (game.CurrentWave == null)
                                throw new InvalidOperationException("No active wave for ballistics audit.");

                            var target = game.CurrentWave.Targets[0];
                            var problem = game.CurrentFiringProblem;
                            if (problem == null)
                                throw new InvalidOperationException("Firing problem was not generated.");

                            int tierIndex = GameConstants.GetTierForWave(waveNumber).TierIndex;
                            double gunRange = firingPhase.GunRange;

                            int techWeapons = game.TechTree?.CurrentLevel?.TryGetValue(TechTree.TechType.Weapons, out int w0) == true ? w0 : 1;
                            int techProjectiles = game.TechTree?.CurrentLevel?.TryGetValue(TechTree.TechType.Projectiles, out int p0) == true ? p0 : 1;

                            // Current build (includes upgrades/modifiers and crafted projectile).
                            var resolvedCurrent = game.ResolveWeaponStats(target);
                            var shotCurrent = ApplyModeHitToleranceMultiplier(game.Mode, resolvedCurrent.Shot);

                            // Baseline: strip persistent stat modifiers and crafted projectile modules.
                            // Keep tech levels intact; this is a "no mods" comparator, not a tech rollback.
                            var savedCrafted = game.CraftedProjectile;
                            var savedStatMods = game.Gun?.InstalledStatModifiers;

                            int savedWeaponsTech;
                            int savedProjectilesTech;

                            if (game.Gun == null)
                                throw new InvalidOperationException("GameState.Gun is null.");

                            game.CraftedProjectile = null;
                            game.Gun.InstalledStatModifiers = new List<Spacegun_Simulator.Core.Stats.StatModifier>();

                            var resolvedNoMods = game.ResolveWeaponStats(target);
                            var shotNoMods = ApplyModeHitToleranceMultiplier(game.Mode, resolvedNoMods.Shot);

                            // Best unlocked projectile (still with no persistent stat modifiers).
                            var bestProjPick = FindBestUnlockedBuildBySolver(
                                game,
                                problem,
                                target,
                                gunRange,
                                waveNumber,
                                game.SelectedDifficulty,
                                game.Mode,
                                requireDestroy: true,
                                getStatMods: _ => new List<Spacegun_Simulator.Core.Stats.StatModifier>());

                            game.CraftedProjectile = bestProjPick.Projectile;
                            game.Gun.InstalledStatModifiers = bestProjPick.Mods;
                            var resolvedBestProjectile = game.ResolveWeaponStats(target);
                            var shotBestProjectile = ApplyModeHitToleranceMultiplier(game.Mode, resolvedBestProjectile.Shot);

                            // Best "modded" build at current tech: best projectile + all unlocked upgrade modifiers.
                            var bestBuildPick = FindBestUnlockedBuildBySolver(
                                game,
                                problem,
                                target,
                                gunRange,
                                waveNumber,
                                game.SelectedDifficulty,
                                game.Mode,
                                requireDestroy: true,
                                getStatMods: proj => CreateBestUnlockedUpgradeModifiers(game, proj));

                            game.CraftedProjectile = bestBuildPick.Projectile;
                            game.Gun.InstalledStatModifiers = bestBuildPick.Mods;
                            var resolvedBestBuild = game.ResolveWeaponStats(target);
                            var shotBestBuild = ApplyModeHitToleranceMultiplier(game.Mode, resolvedBestBuild.Shot);

                            // Best unlocked projectile/build at max tech (Weapons=3, Projectiles=3).
                            // This isolates "is it possible with the best build" from economy/research variance.
                            SetTechLevelsForAudit(game, weaponsLevel: 3, projectilesLevel: 3, out savedWeaponsTech, out savedProjectilesTech);

                            var bestProjMaxTechPick = FindBestUnlockedBuildBySolver(
                                game,
                                problem,
                                target,
                                gunRange,
                                waveNumber,
                                game.SelectedDifficulty,
                                game.Mode,
                                requireDestroy: true,
                                getStatMods: _ => new List<Spacegun_Simulator.Core.Stats.StatModifier>());

                            game.CraftedProjectile = bestProjMaxTechPick.Projectile;
                            game.Gun.InstalledStatModifiers = bestProjMaxTechPick.Mods;
                            var resolvedBestProjectileMaxTech = game.ResolveWeaponStats(target);
                            var shotBestProjectileMaxTech = ApplyModeHitToleranceMultiplier(game.Mode, resolvedBestProjectileMaxTech.Shot);

                            var bestBuildMaxTechPick = FindBestUnlockedBuildBySolver(
                                game,
                                problem,
                                target,
                                gunRange,
                                waveNumber,
                                game.SelectedDifficulty,
                                game.Mode,
                                requireDestroy: true,
                                getStatMods: proj => CreateBestUnlockedUpgradeModifiers(game, proj));

                            game.CraftedProjectile = bestBuildMaxTechPick.Projectile;
                            game.Gun.InstalledStatModifiers = bestBuildMaxTechPick.Mods;
                            var resolvedBestBuildMaxTech = game.ResolveWeaponStats(target);
                            var shotBestBuildMaxTech = ApplyModeHitToleranceMultiplier(game.Mode, resolvedBestBuildMaxTech.Shot);

                            RestoreTechLevelsForAudit(game, savedWeaponsTech, savedProjectilesTech);

                            // Restore before doing anything else.
                            game.CraftedProjectile = savedCrafted;
                            game.Gun.InstalledStatModifiers = savedStatMods ?? new List<Spacegun_Simulator.Core.Stats.StatModifier>();

                            // Feasibility checks: deterministic baseline search.
                            var noModsRes = FindBestSolution(problem, target, shotNoMods, gunRange, waveNumber, game.SelectedDifficulty, requireDestroy: true);
                            var currentRes = FindBestSolution(problem, target, shotCurrent, gunRange, waveNumber, game.SelectedDifficulty, requireDestroy: true);
                            var bestProjectileRes = bestProjPick.Solution;
                            var bestBuildRes = bestBuildPick.Solution;
                            var bestProjectileMaxTechRes = bestProjMaxTechPick.Solution;
                            var bestBuildMaxTechRes = bestBuildMaxTechPick.Solution;

                            double tolNoMods = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotNoMods.AdditionalHitToleranceMultiplier);
                            double tolBestProj = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotBestProjectile.AdditionalHitToleranceMultiplier);
                            double tolBestBuild = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotBestBuild.AdditionalHitToleranceMultiplier);
                            double tolBestProjMaxTech = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotBestProjectileMaxTech.AdditionalHitToleranceMultiplier);
                            double tolBestBuildMaxTech = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotBestBuildMaxTech.AdditionalHitToleranceMultiplier);
                            double tolCurrent = FiringSolution.CalculateHitToleranceMeters(game.SelectedDifficulty, waveNumber, target.CrossSection, target.Mass, shotCurrent.AdditionalHitToleranceMultiplier);

                            static bool IsKill(in FiringSolutionResult r) => r.CanHit && r.CanDestroy;
                            static bool IsHit(in FiringSolutionResult r) => r.CanHit;
                            static bool IsDestroy(in FiringSolutionResult r) => r.CanDestroy;
                            static string FmtFoundDev((bool Found, FiringSolutionResult Result) r)
                                => r.Found ? $"{r.Result.InterceptDeviation:F1}m" : "NA";
                            static string FmtFoundKe((bool Found, FiringSolutionResult Result) r)
                                => r.Found ? $"{r.Result.KineticEnergyMJ:F0}/{r.Result.FractureEnergyRequired:F0}MJ" : "NA";
                            static string FmtFoundMargin((bool Found, FiringSolutionResult Result) r, double tol)
                                => r.Found ? $"{tol - r.Result.InterceptDeviation:F1}" : "NA";

                            AccumulateAudit(
                                auditByTier,
                                tierIndex,
                                noModsHit: noModsRes.Found && IsHit(noModsRes.Result),
                                noModsDestroy: noModsRes.Found && IsDestroy(noModsRes.Result),
                                currentHit: currentRes.Found && IsHit(currentRes.Result),
                                currentDestroy: currentRes.Found && IsDestroy(currentRes.Result));

                            AccumulateBestProjectileAudit(
                                auditBestProjectileByTier,
                                tierIndex,
                                noModsKill: noModsRes.Found && IsKill(noModsRes.Result),
                                bestKill: bestProjectileRes.Found && IsKill(bestProjectileRes.Result));

                            AccumulateBestProjectileAudit(
                                auditBestBuildByTier,
                                tierIndex,
                                noModsKill: noModsRes.Found && IsKill(noModsRes.Result),
                                bestKill: bestBuildRes.Found && IsKill(bestBuildRes.Result));

                            AccumulateBestProjectileAudit(
                                auditBestProjectileMaxTechByTier,
                                tierIndex,
                                noModsKill: noModsRes.Found && IsKill(noModsRes.Result),
                                bestKill: bestProjectileMaxTechRes.Found && IsKill(bestProjectileMaxTechRes.Result));

                            AccumulateBestProjectileAudit(
                                auditBestBuildMaxTechByTier,
                                tierIndex,
                                noModsKill: noModsRes.Found && IsKill(noModsRes.Result),
                                bestKill: bestBuildMaxTechRes.Found && IsKill(bestBuildMaxTechRes.Result));

                            Console.WriteLine(
                                $"Wave {waveNumber}: BallisticsAudit | Tier={tierIndex} Tech(W={techWeapons} P={techProjectiles}) " +
                                $"NoMods: Found={noModsRes.Found} Hit={(noModsRes.Found && IsHit(noModsRes.Result))} Destroy={(noModsRes.Found && IsDestroy(noModsRes.Result))} Dev={FmtFoundDev(noModsRes)} Tol={tolNoMods:F1}m (m={FmtFoundMargin(noModsRes, tolNoMods)}) KE={FmtFoundKe(noModsRes)} " +
                                $"| BestProj: Found={bestProjectileRes.Found} Hit={(bestProjectileRes.Found && IsHit(bestProjectileRes.Result))} Destroy={(bestProjectileRes.Found && IsDestroy(bestProjectileRes.Result))} Dev={FmtFoundDev(bestProjectileRes)} Tol={tolBestProj:F1}m (m={FmtFoundMargin(bestProjectileRes, tolBestProj)}) KE={FmtFoundKe(bestProjectileRes)} " +
                                $"| BestBuild: Found={bestBuildRes.Found} Hit={(bestBuildRes.Found && IsHit(bestBuildRes.Result))} Destroy={(bestBuildRes.Found && IsDestroy(bestBuildRes.Result))} Dev={FmtFoundDev(bestBuildRes)} Tol={tolBestBuild:F1}m (m={FmtFoundMargin(bestBuildRes, tolBestBuild)}) KE={FmtFoundKe(bestBuildRes)} " +
                                $"| BestBuildMaxTech: Found={bestBuildMaxTechRes.Found} Hit={(bestBuildMaxTechRes.Found && IsHit(bestBuildMaxTechRes.Result))} Destroy={(bestBuildMaxTechRes.Found && IsDestroy(bestBuildMaxTechRes.Result))} Dev={FmtFoundDev(bestBuildMaxTechRes)} Tol={tolBestBuildMaxTech:F1}m (m={FmtFoundMargin(bestBuildMaxTechRes, tolBestBuildMaxTech)}) KE={FmtFoundKe(bestBuildMaxTechRes)} " +
                                $"| Current: Found={currentRes.Found} Hit={(currentRes.Found && IsHit(currentRes.Result))} Destroy={(currentRes.Found && IsDestroy(currentRes.Result))} Dev={FmtFoundDev(currentRes)} Tol={tolCurrent:F1}m (m={FmtFoundMargin(currentRes, tolCurrent)}) KE={FmtFoundKe(currentRes)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Wave {waveNumber}: BallisticsAudit ERROR: {ex.Message}");
                        }
                    }

                    // Consume barrel wear (matches in-UI behavior) but don't require manual solution input.
                    if (game.Gun != null)
                    {
                        bool barrelStillOk = game.Gun.RegisterShot();
                        if (!barrelStillOk)
                        {
                            game.IsGameOver = true;
                            Console.WriteLine($"Wave {waveNumber}: barrel failed after shot -> game over");
                            break;
                        }
                    }

                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.FiringResolvedHit);
                }

                // ===== Wave complete acknowledgement (auto-continue) =====
                if (game.CurrentPhase == GameState.GamePhase.WaveComplete)
                {
                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.WaveCompleteAcknowledged);
                }


                Console.WriteLine($"Wave {waveNumber}: OK | Years={det.AvailableYears} | Tech: Radar={game.TechTree.CurrentLevel[TechTree.TechType.Radar]} Mining={game.TechTree.CurrentLevel[TechTree.TechType.Mining]} Prod={game.TechTree.CurrentLevel[TechTree.TechType.Production]} Weapons={game.TechTree.CurrentLevel[TechTree.TechType.Weapons]} Proj={game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}");

                // Record end-of-wave tech state for pacing checks.
                int tierIndexNow = GameConstants.GetTierForWave(waveNumber).TierIndex;
                techEndByTier[tierIndexNow] = (
                    Radar: game.TechTree.CurrentLevel[TechTree.TechType.Radar],
                    Mining: game.TechTree.CurrentLevel[TechTree.TechType.Mining],
                    Production: game.TechTree.CurrentLevel[TechTree.TechType.Production],
                    Weapons: game.TechTree.CurrentLevel[TechTree.TechType.Weapons],
                    Projectiles: game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]);

                foreach (var techType in firstTierAtTech3.Keys.ToList())
                {
                    if (firstTierAtTech3[techType].HasValue)
                        continue;

                    if (game.TechTree.CurrentLevel.TryGetValue(techType, out int lvl) && lvl >= 3)
                        firstTierAtTech3[techType] = tierIndexNow;
                }
            }

            Console.WriteLine("\n[TEST CAMPAIGN SUMMARY]");
            Console.WriteLine($"WavesDefeated: {game.WavesDefeated}/{waves}");
            Console.WriteLine($"TotalYearsAllocated: {totalYears}");
            Console.WriteLine($"Gathered: Budget={totalBudgetGathered:F0} Steel={totalSteelGathered:F0} Exotic={totalExoticGathered:F1}");
            Console.WriteLine($"TechUpgradesPurchased: {totalTechUpgrades}");
            Console.WriteLine($"FinalTech: Radar={game.TechTree.CurrentLevel[TechTree.TechType.Radar]} Mining={game.TechTree.CurrentLevel[TechTree.TechType.Mining]} Prod={game.TechTree.CurrentLevel[TechTree.TechType.Production]} Weapons={game.TechTree.CurrentLevel[TechTree.TechType.Weapons]} Proj={game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}");

            if (auditBallistics)
            {
                Console.WriteLine("\n[TEST CAMPAIGN BALLISTICS AUDIT]");
                foreach (var kv in auditByTier.OrderBy(k => k.Key))
                {
                    var t = kv.Key;
                    var m = kv.Value;
                    Console.WriteLine(
                        $"Tier {t}: Waves={m.Waves} " +
                        $"NoModsKill={m.NoModsKill} (HitOnly={m.NoModsHitOnly} EnergyOnly={m.NoModsEnergyOnly} Neither={m.NoModsNeither}) " +
                        $"CurrentKill={m.CurrentKill} Helpful={m.Helpful} Necessary(T>=3)={m.Necessary}");
                }

                Console.WriteLine("\n[TEST CAMPAIGN BALLISTICS AUDIT: BEST UNLOCKED PROJECTILE]\n" +
                                  "(Comparator uses same tech level, no persistent stat modifiers, and selects the best unlocked projectile by solver-evaluating all unlocked component combinations.)");
                foreach (var kv in auditBestProjectileByTier.OrderBy(k => k.Key))
                {
                    var t = kv.Key;
                    var m = kv.Value;
                    Console.WriteLine(
                        $"Tier {t}: Waves={m.Waves} NoModsKill={m.NoModsKill} BestProjKill={m.BestKill} Helpful={m.Helpful} Necessary(T>=3)={m.Necessary}");
                }

                Console.WriteLine("\n[TEST CAMPAIGN BALLISTICS AUDIT: BEST BUILD]\n" +
                                  "(Comparator uses same tech level and selects the best build by solver-evaluating all unlocked projectile combinations with all unlocked upgrade StatModifiers applied.)");
                foreach (var kv in auditBestBuildByTier.OrderBy(k => k.Key))
                {
                    var t = kv.Key;
                    var m = kv.Value;
                    Console.WriteLine(
                        $"Tier {t}: Waves={m.Waves} NoModsKill={m.NoModsKill} BestBuildKill={m.BestKill} Helpful={m.Helpful} Necessary(T>=3)={m.Necessary}");
                }

                Console.WriteLine("\n[TEST CAMPAIGN BALLISTICS AUDIT: BEST BUILD (MAX TECH)]\n" +
                                  "(Comparator forces Weapons=3 and Projectiles=3 during build selection; isolates ballistics feasibility from economy/research variance. Selection is solver-driven across unlocked combinations.)");
                foreach (var kv in auditBestBuildMaxTechByTier.OrderBy(k => k.Key))
                {
                    var t = kv.Key;
                    var m = kv.Value;
                    Console.WriteLine(
                        $"Tier {t}: Waves={m.Waves} NoModsKill={m.NoModsKill} BestBuildMaxTechKill={m.BestKill} Helpful={m.Helpful} Necessary(T>=3)={m.Necessary}");
                }

                // Optional CSV report (one row per tier for this seed).
                if (!string.IsNullOrWhiteSpace(auditBallisticsCsvPath))
                {
                    try
                    {
                        AppendCampaignBallisticsAuditCsv(
                            path: auditBallisticsCsvPath!,
                            mode: mode,
                            seed: game.BaseSeed,
                            wavesRequested: waves,
                            wavesDefeated: game.WavesDefeated,
                            ballisticsDrivenResearch: ballisticsDrivenResearch,
                            totalYearsAllocated: totalYears,
                            totalBudgetGathered: totalBudgetGathered,
                            totalSteelGathered: totalSteelGathered,
                            totalExoticGathered: totalExoticGathered,
                            totalTechUpgradesPurchased: totalTechUpgrades,
                            finalTech: game.TechTree.CurrentLevel,
                            techEndByTier: techEndByTier,
                            firstTierAtTech3: firstTierAtTech3,
                            auditByTier: auditByTier,
                            auditBestBuildByTier: auditBestBuildByTier,
                            auditBestBuildMaxTechByTier: auditBestBuildMaxTechByTier);

                        Console.WriteLine($"[TEST CAMPAIGN] ballistics audit CSV appended: {auditBallisticsCsvPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TEST CAMPAIGN] ballistics audit CSV ERROR: {ex.Message}");
                    }
                }
            }

            static void AppendCampaignBallisticsAuditCsv(
                string path,
                GameModeId mode,
                int seed,
                int wavesRequested,
                int wavesDefeated,
                bool ballisticsDrivenResearch,
                long totalYearsAllocated,
                double totalBudgetGathered,
                double totalSteelGathered,
                double totalExoticGathered,
                int totalTechUpgradesPurchased,
                Dictionary<TechTree.TechType, int> finalTech,
                Dictionary<int, (int Radar, int Mining, int Production, int Weapons, int Projectiles)> techEndByTier,
                Dictionary<TechTree.TechType, int?> firstTierAtTech3,
                Dictionary<int, (int Waves, int NoModsKill, int NoModsHitOnly, int NoModsEnergyOnly, int NoModsNeither, int CurrentKill, int Helpful, int Necessary)> auditByTier,
                Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)> auditBestBuildByTier,
                Dictionary<int, (int Waves, int NoModsKill, int BestKill, int Helpful, int Necessary)> auditBestBuildMaxTechByTier)
            {
                static string CsvEscape(string s)
                {
                    if (s.IndexOfAny([',', '"', '\n', '\r']) < 0)
                        return s;
                    return '"' + s.Replace("\"", "\"\"") + '"';
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs);

                if (writeHeader)
                {
                    sw.WriteLine(string.Join(",",
                        "Seed",
                        "Mode",
                        "BallisticsDrivenResearch",
                        "WavesRequested",
                        "WavesDefeated",
                        "Tier",
                        "TierWaves",
                        "NoModsKill",
                        "NoModsHitOnly",
                        "NoModsEnergyOnly",
                        "NoModsNeither",
                        "BestBuildKill",
                        "BestBuildMaxTechKill",
                        "TierEndTech_Radar",
                        "TierEndTech_Mining",
                        "TierEndTech_Production",
                        "TierEndTech_Weapons",
                        "TierEndTech_Projectiles",
                        "FirstTierTech3_Radar",
                        "FirstTierTech3_Mining",
                        "FirstTierTech3_Production",
                        "FirstTierTech3_Weapons",
                        "FirstTierTech3_Projectiles",
                        "TotalYearsAllocated",
                        "GatheredBudget",
                        "GatheredSteel",
                        "GatheredExotic",
                        "TechUpgradesPurchased",
                        "FinalTech_Radar",
                        "FinalTech_Mining",
                        "FinalTech_Production",
                        "FinalTech_Weapons",
                        "FinalTech_Projectiles"));
                }

                int GetTech(TechTree.TechType t)
                    => finalTech.TryGetValue(t, out var v) ? v : 1;

                int GetFirstTierTech3(TechTree.TechType t)
                    => firstTierAtTech3.TryGetValue(t, out var v) && v.HasValue ? v.Value : -1;

                for (int tier = 0; tier < GameConstants.TierCount; tier++)
                {
                    auditByTier.TryGetValue(tier, out var baseTier);
                    auditBestBuildByTier.TryGetValue(tier, out var bestTier);
                    auditBestBuildMaxTechByTier.TryGetValue(tier, out var bestMaxTier);

                    int tierWaves = baseTier.Waves;
                    int noModsKill = baseTier.NoModsKill;
                    int bestBuildKill = bestTier.BestKill;
                    int bestBuildMaxTechKill = bestMaxTier.BestKill;

                    techEndByTier.TryGetValue(tier, out var techEnd);

                    sw.WriteLine(string.Join(",",
                        seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        CsvEscape(mode.ToString()),
                        (ballisticsDrivenResearch ? "1" : "0"),
                        wavesRequested.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        wavesDefeated.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        tier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        tierWaves.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        noModsKill.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        baseTier.NoModsHitOnly.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        baseTier.NoModsEnergyOnly.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        baseTier.NoModsNeither.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        bestBuildKill.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        bestBuildMaxTechKill.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        techEnd.Radar.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        techEnd.Mining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        techEnd.Production.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        techEnd.Weapons.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        techEnd.Projectiles.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetFirstTierTech3(TechTree.TechType.Radar).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetFirstTierTech3(TechTree.TechType.Mining).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetFirstTierTech3(TechTree.TechType.Production).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetFirstTierTech3(TechTree.TechType.Weapons).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetFirstTierTech3(TechTree.TechType.Projectiles).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalYearsAllocated.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalBudgetGathered.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalSteelGathered.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalExoticGathered.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalTechUpgradesPurchased.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetTech(TechTree.TechType.Radar).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetTech(TechTree.TechType.Mining).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetTech(TechTree.TechType.Production).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetTech(TechTree.TechType.Weapons).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        GetTech(TechTree.TechType.Projectiles).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
            }

            static long EstimateYearsToAfford(Development.Technology.TechUnlock unlock, double steelRate, double budgetRate, double exoticRate)
            {
                static long YearsRequired(double cost, double rate)
                {
                    if (cost <= 0) return 0;
                    if (rate <= 0) return long.MaxValue;
                    return (long)Math.Ceiling(cost / rate);
                }

                long ySteel = YearsRequired(unlock.ResearchCost.Steel, steelRate);
                long yBudget = YearsRequired(unlock.ResearchCost.Budget, budgetRate);
                long yExotic = YearsRequired(unlock.ResearchCost.ExoticMaterials, exoticRate);
                if (ySteel == long.MaxValue || yBudget == long.MaxValue || yExotic == long.MaxValue)
                    return long.MaxValue;
                return ySteel + yBudget + yExotic;
            }
        }

        private static (double Steel, double Budget, double Exotic) AutoAllocateAllYears(GameState game)
        {
            long years = game.RemainingYears;
            if (years <= 0)
                return (0, 0, 0);

            // Return deltas for reporting.
            double steelBefore = game.AccumulatedResources.GetValueOrDefault("Steel", 0);
            double budgetBefore = game.AccumulatedResources.GetValueOrDefault("Budget", 0);
            double exoticBefore = game.AccumulatedResources.GetValueOrDefault("Exotic", 0);

            // Match UI behavior: effective rates depend on difficulty, tech, and wave event.
            double eventMultiplier = game.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;

            var resourceKeys = new Dictionary<Economy.ResourceType, string>
            {
                { Economy.ResourceType.Steel, "Steel" },
                { Economy.ResourceType.Budget, "Budget" },
                { Economy.ResourceType.SpecializedAlloys, "SpecializedAlloys" },
                { Economy.ResourceType.RareEarthElements, "RareEarthElements" },
                { Economy.ResourceType.PowerCells, "PowerCells" },
                { Economy.ResourceType.ExoticMaterials, "Exotic" },
            };

            var effectiveRates = new Dictionary<Economy.ResourceType, double>();
            foreach (Economy.ResourceType resource in Enum.GetValues(typeof(Economy.ResourceType)))
            {
                double rate = Economy.ResourceGathering.GetEffectiveProductionRate(resource, game.TechTree, game.SelectedDifficulty, eventMultiplier);
                if (rate > 0)
                    effectiveRates[resource] = rate;
            }

            if (effectiveRates.Count == 0)
            {
                game.RemainingYears = 0;
                return (0, 0, 0);
            }

            // Frugal policy: only use tech-targeted allocation early, when years are plentiful.
            // Later waves should preserve budget for development decisions.
            bool needsInvestmentTech =
                game.TechTree.CurrentLevel[TechTree.TechType.Radar] < 3 ||
                game.TechTree.CurrentLevel[TechTree.TechType.Mining] < 3 ||
                game.TechTree.CurrentLevel[TechTree.TechType.Production] < 3;

            // Frugal-but-efficient: keep funding key investment tech into mid-game,
            // then switch to budget-preserving mode once the economy/survival tech is online.
            bool allowTechTargeting = years >= 7
                && game.CurrentWaveNumber <= 14
                && needsInvestmentTech;
            if (allowTechTargeting)
            {
                var techTargetAllocation = TryBuildTechFocusedAllocation(game, effectiveRates, years);
                foreach (var kvp in techTargetAllocation)
                {
                    ApplyGathered(kvp.Key, kvp.Value);
                    years -= kvp.Value;
                }
            }

            if (years <= 0)
            {
                game.RemainingYears = 0;
                return SummarizeDelta();
            }

            // Frugal policy: always prioritize Budget; increase priority when waves are short.
            bool shortWave = years <= 7;
            bool lateGame = game.CurrentWaveNumber >= 12;

            bool exoticUnlocked = effectiveRates.TryGetValue(Economy.ResourceType.ExoticMaterials, out var exoticRate) && exoticRate > 0;
            bool productionTier3 = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Production, out int prodLvl) && prodLvl >= 3;

            double targetBudgetFrac;
            if (game.CurrentWaveNumber <= 6)
                targetBudgetFrac = 0.45; // early game: don't starve Steel; invest into tech.
            else if (lateGame || shortWave)
                targetBudgetFrac = 0.70; // late game: preserve budget for development choices.
            else
                targetBudgetFrac = 0.60;

            if (game.CurrentWaveNumber >= 18)
                targetBudgetFrac = 0.80;

            if (effectiveRates.TryGetValue(Economy.ResourceType.Budget, out var budgetRate) && budgetRate > 0)
            {
                // Reserve at least a little capacity for key non-budget resources so we don't
                // completely starve Steel/PowerCells in short late-game waves.
                long reservedOtherYears = 0;
                if (years >= 2 && effectiveRates.TryGetValue(Economy.ResourceType.Steel, out var steelRate) && steelRate > 0)
                    reservedOtherYears += 1;
                if (years - reservedOtherYears >= 2 && effectiveRates.TryGetValue(Economy.ResourceType.PowerCells, out var pcRate) && pcRate > 0)
                    reservedOtherYears += 1;
                if (years - reservedOtherYears >= 2 && exoticUnlocked && productionTier3)
                    reservedOtherYears += 1;

                long budgetYears = (long)Math.Ceiling(years * targetBudgetFrac);
                budgetYears = Math.Clamp(budgetYears, 0, Math.Max(0, years - reservedOtherYears));
                ApplyGathered(Economy.ResourceType.Budget, budgetYears);
                years -= budgetYears;
            }

            // Minimum trickle into Steel/PowerCells (if still possible).
            if (years > 0 && effectiveRates.TryGetValue(Economy.ResourceType.Steel, out var minSteelRate) && minSteelRate > 0)
            {
                ApplyGathered(Economy.ResourceType.Steel, 1);
                years -= 1;
            }

            if (years > 0 && effectiveRates.TryGetValue(Economy.ResourceType.PowerCells, out var minPcRate) && minPcRate > 0)
            {
                ApplyGathered(Economy.ResourceType.PowerCells, 1);
                years -= 1;
            }

            // Exotic is a Tier-3 production resource. Allocate a small trickle once unlocked so the
            // test harness reflects accumulation and later-game purchases can be modeled.
            if (years > 0 && exoticUnlocked && productionTier3)
            {
                ApplyGathered(Economy.ResourceType.ExoticMaterials, 1);
                years -= 1;
            }

            // Spend remaining years across other unlocked resources.
            // Keep Steel non-zero for general development costs, but don't starve Budget.
            var weights = new Dictionary<Economy.ResourceType, double>
            {
                { Economy.ResourceType.Steel, 0.35 },
                { Economy.ResourceType.PowerCells, 0.35 },
                { Economy.ResourceType.SpecializedAlloys, 0.15 },
                { Economy.ResourceType.RareEarthElements, 0.10 },
                { Economy.ResourceType.ExoticMaterials, 0.05 },
            };

            double totalWeight = 0;
            foreach (var kvp in effectiveRates)
            {
                // Budget already handled above; only distribute remaining years to non-budget resources.
                if (kvp.Key == Economy.ResourceType.Budget)
                    continue;
                totalWeight += weights.TryGetValue(kvp.Key, out var w) ? w : 0.0;
            }

            // Fallback: if none of the unlocked resources had explicit weights, just put all years into Budget.
            if (totalWeight <= 0.0)
            {
                ApplyGathered(Economy.ResourceType.Budget, years);
                game.RemainingYears = 0;
                return SummarizeDelta();
            }

            var allocations = new List<(Economy.ResourceType Resource, long Years, double Fractional)>();
            long assigned = 0;
            foreach (var kvp in effectiveRates)
            {
                var resource = kvp.Key;
                if (resource == Economy.ResourceType.Budget)
                    continue;
                double w = weights.TryGetValue(resource, out var ww) ? ww : 0.0;
                if (w <= 0.0) { allocations.Add((resource, 0, 0)); continue; }

                double exact = years * (w / totalWeight);
                long whole = (long)Math.Floor(exact);
                assigned += whole;
                allocations.Add((resource, whole, exact - whole));
            }

            long remaining = years - assigned;
            if (remaining > 0)
            {
                foreach (var item in allocations
                    .OrderByDescending(a => a.Fractional)
                    .ThenBy(a => a.Resource.ToString(), StringComparer.Ordinal))
                {
                    if (remaining <= 0) break;
                    if (effectiveRates[item.Resource] <= 0) continue;
                    // Increase year count for this resource.
                    int idx = allocations.FindIndex(a => a.Resource == item.Resource);
                    allocations[idx] = (allocations[idx].Resource, allocations[idx].Years + 1, allocations[idx].Fractional);
                    remaining--;
                }
            }

            foreach (var a in allocations)
            {
                if (a.Years <= 0) continue;
                ApplyGathered(a.Resource, a.Years);
            }

            game.RemainingYears = 0;
            return SummarizeDelta();

            void ApplyGathered(Economy.ResourceType resource, long yearsToSpend)
            {
                if (yearsToSpend <= 0) return;
                if (!effectiveRates.TryGetValue(resource, out var rate) || rate <= 0) return;
                if (!resourceKeys.TryGetValue(resource, out var key)) return;

                double gathered = yearsToSpend * rate;
                if (!game.AccumulatedResources.ContainsKey(key))
                    game.AccumulatedResources[key] = 0;
                game.AccumulatedResources[key] += gathered;
            }

            Dictionary<Economy.ResourceType, long> TryBuildTechFocusedAllocation(
                GameState g,
                Dictionary<Economy.ResourceType, double> rates,
                long availableYears)
            {
                var result = new Dictionary<Economy.ResourceType, long>();
                if (availableYears <= 0)
                    return result;

                double steelRate = rates.GetValueOrDefault(Economy.ResourceType.Steel, 0);
                double budgetRate = rates.GetValueOrDefault(Economy.ResourceType.Budget, 0);
                double exoticRate = rates.GetValueOrDefault(Economy.ResourceType.ExoticMaterials, 0);

                var techs = Development.Technology.TechUnlock.GetAvailableUnlocks(g.TechTree);
                if (techs == null || techs.Count == 0)
                    return result;

                int Priority(TechTree.TechType techType)
                {
                    return techType switch
                    {
                        TechTree.TechType.Radar => 0,
                        TechTree.TechType.Mining => 1,
                        TechTree.TechType.Production => 2,
                        TechTree.TechType.Projectiles => 3,
                        TechTree.TechType.Weapons => 4,
                        _ => 99
                    };
                }

                // Choose a tech to fund this wave.
                // Prefer investment/survival tech, prefer higher tier (III over II), then choose the cheapest.
                var best = techs
                    .Select(t => new
                    {
                        Tech = t,
                        YearsNeeded = EstimateYearsToAfford(t, steelRate, budgetRate, exoticRate)
                    })
                    .Where(x => x.YearsNeeded < long.MaxValue)
                    .OrderBy(x => Priority(x.Tech.TechType))
                    .ThenByDescending(x => x.Tech.ToLevel)
                    .ThenBy(x => x.YearsNeeded)
                    .FirstOrDefault();

                if (best == null)
                    return result;

                // Allocate just enough years (or as many as we have) to move toward affording it.
                AllocateTowardCost(best.Tech.ResearchCost.Steel, steelRate, Economy.ResourceType.Steel);
                AllocateTowardCost(best.Tech.ResearchCost.Budget, budgetRate, Economy.ResourceType.Budget);
                AllocateTowardCost(best.Tech.ResearchCost.ExoticMaterials, exoticRate, Economy.ResourceType.ExoticMaterials);

                // Cap to availableYears.
                long used = result.Values.Sum();
                if (used > availableYears)
                {
                    // Trim (deterministically) from Budget first, then Steel, then Exotic.
                    Trim(Economy.ResourceType.Budget);
                    Trim(Economy.ResourceType.Steel);
                    Trim(Economy.ResourceType.ExoticMaterials);
                }

                return result;

                long EstimateYearsToAfford(Development.Technology.TechUnlock unlock, double sr, double br, double er)
                {
                    long ySteel = YearsRequired(unlock.ResearchCost.Steel, sr);
                    long yBudget = YearsRequired(unlock.ResearchCost.Budget, br);
                    long yExotic = YearsRequired(unlock.ResearchCost.ExoticMaterials, er);
                    if (ySteel == long.MaxValue || yBudget == long.MaxValue || yExotic == long.MaxValue)
                        return long.MaxValue;
                    return ySteel + yBudget + yExotic;
                }

                static long YearsRequired(double cost, double rate)
                {
                    if (cost <= 0) return 0;
                    if (rate <= 0) return long.MaxValue;
                    return (long)Math.Ceiling(cost / rate);
                }

                void AllocateTowardCost(double cost, double rate, Economy.ResourceType resource)
                {
                    if (cost <= 0) return;
                    if (rate <= 0) return;
                    long yrs = (long)Math.Ceiling(cost / rate);
                    if (yrs <= 0) return;
                    result[resource] = yrs;
                }

                void Trim(Economy.ResourceType resource)
                {
                    if (!result.TryGetValue(resource, out long yrs) || yrs <= 0)
                        return;

                    long over = result.Values.Sum() - availableYears;
                    if (over <= 0)
                        return;

                    long trimmed = Math.Min(yrs, over);
                    result[resource] = yrs - trimmed;
                    if (result[resource] <= 0)
                        result.Remove(resource);
                }
            }

            (double Steel, double Budget, double Exotic) SummarizeDelta()
            {
                double steelAfter = game.AccumulatedResources.GetValueOrDefault("Steel", 0);
                double budgetAfter = game.AccumulatedResources.GetValueOrDefault("Budget", 0);
                double exoticAfter = game.AccumulatedResources.GetValueOrDefault("Exotic", 0);
                return (steelAfter - steelBefore, budgetAfter - budgetBefore, exoticAfter - exoticBefore);
            }
        }

        private static int AutoResearchAffordableTech(GameState game, bool preferOffense = false)
        {
            int upgrades = 0;

            // Frugal policy: don't spend down to zero; preserve a buffer for development choices.
            int waveNumber = game.CurrentWaveNumber;
            double budgetReserve = waveNumber <= 3 ? 0
                : waveNumber <= 10 ? 75
                : waveNumber <= 15 ? 150
                : 250;

            double steelReserve = waveNumber <= 3 ? 0
                : waveNumber <= 10 ? 75
                : waveNumber <= 15 ? 125
                : 200;

            int maxUpgradesThisWave = preferOffense
                ? 2
                : (game.TechTree.CurrentLevel[TechTree.TechType.Radar] < 2 ? 2 : 1);

            // Priority: survivability/info first, then economy, then offense.
            // In ballistics-driven mode, offense moves ahead so late tiers can be reached.
            var priority = preferOffense
                ? new Dictionary<TechTree.TechType, int>
                {
                    { TechTree.TechType.Projectiles, 0 },
                    { TechTree.TechType.Weapons, 1 },
                    { TechTree.TechType.Radar, 2 },
                    { TechTree.TechType.Mining, 3 },
                    { TechTree.TechType.Production, 4 },
                }
                : new Dictionary<TechTree.TechType, int>
                {
                    { TechTree.TechType.Radar, 0 },
                    { TechTree.TechType.Mining, 1 },
                    { TechTree.TechType.Production, 2 },
                    { TechTree.TechType.Projectiles, 3 },
                    { TechTree.TechType.Weapons, 4 },
                };

            while (upgrades < maxUpgradesThisWave)
            {
                var available = Development.Technology.TechUnlock
                    .GetAvailableUnlocks(game.TechTree)
                    .Where(t => Development.Technology.TechUnlock.CanAffordResearch(t, game.AccumulatedResources))
                    .ToList();

                if (available.Count == 0)
                    break;

                if (preferOffense)
                {
                    int weaponsLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int wl) ? wl : 1;
                    int projectilesLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int pl) ? pl : 1;
                    int radarLevel = game.TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Radar, out int rl) ? rl : 1;

                    // In an offense-optimized playthrough, prioritize reaching late-game lethality first.
                    // Keep Radar at least level 2 for basic survivability, but avoid sinking resources into economy tech
                    // until core offensive trees (Weapons/Projectiles) are maxed.
                    if (weaponsLevel < 3 || projectilesLevel < 3)
                    {
                        available = available
                            .Where(t => t.TechType == TechTree.TechType.Weapons
                                     || t.TechType == TechTree.TechType.Projectiles
                                     || (t.TechType == TechTree.TechType.Radar && radarLevel < 2))
                            .ToList();

                        if (available.Count == 0)
                            break;
                    }
                }

                double curBudget = game.AccumulatedResources.GetValueOrDefault("Budget", 0);
                double curSteel = game.AccumulatedResources.GetValueOrDefault("Steel", 0);

                var next = available
                    .Where(t =>
                    {
                        int currentLevel = game.TechTree.CurrentLevel.TryGetValue(t.TechType, out int lvl) ? lvl : 1;
                        // Survival + economy upgrades are treated as "investments".
                        bool survivalUpgrade = t.TechType == TechTree.TechType.Radar && currentLevel < 3;
                        bool economyUpgrade = (t.TechType == TechTree.TechType.Mining || t.TechType == TechTree.TechType.Production) && currentLevel < 3;
                        bool offenseUpgrade = (t.TechType == TechTree.TechType.Projectiles || t.TechType == TechTree.TechType.Weapons) && currentLevel < 3;

                        // Allow early/mid-game investment even if it temporarily drains buffers.
                        if (survivalUpgrade)
                            return true;
                        if (economyUpgrade && waveNumber <= 14)
                            return true;
                        if (preferOffense && offenseUpgrade)
                            return true;

                        return (curBudget - t.ResearchCost.Budget) >= budgetReserve
                            && (curSteel - t.ResearchCost.Steel) >= steelReserve;
                    })
                    .OrderBy(t => priority.TryGetValue(t.TechType, out var p) ? p : 999)
                    .ThenBy(t => t.ToLevel)
                    .FirstOrDefault();

                if (next == null)
                    break;

                if (!game.ResearchTech(next))
                    break;

                upgrades++;
            }

            return upgrades;
        }

        private static int? TryParseIntArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (int.TryParse(args[i + 1], out int v))
                    return v;
            }
            return null;
        }

        private static string? TryParseStringArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is null)
                    continue;

                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return Unquote(args[i][(name.Length + 1)..]);

                if (i < args.Length - 1 && string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return Unquote(args[i + 1]);
            }
            return null;

            static string? Unquote(string? value)
            {
                if (value is null)
                    return null;

                var v = value.Trim();
                if (v.Length >= 2)
                {
                    if ((v.StartsWith("\"", StringComparison.Ordinal) && v.EndsWith("\"", StringComparison.Ordinal))
                     || (v.StartsWith("'", StringComparison.Ordinal) && v.EndsWith("'", StringComparison.Ordinal)))
                    {
                        v = v[1..^1];
                    }
                }

                return v;
            }
        }

        private static TEnum? TryParseEnumArg<TEnum>(string[] args, string name)
            where TEnum : struct
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Enum.TryParse<TEnum>(args[i + 1], ignoreCase: true, out var v))
                    return v;
            }
            return null;
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