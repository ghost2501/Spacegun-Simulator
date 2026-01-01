using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Diagnostics;
using Spacegun_Simulator.UI.Flows;
using Spacegun_Simulator.UI.Pages.Core;
using Spacegun_Simulator.Enemies;
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

            // Diagnostics smoke checks (headless).
            // Keeps normal UX unchanged unless an explicit flag is provided.
            if (args.Any(a => string.Equals(a, "--tuninglab-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                RunTuningLabSmoke();
                return;
            }

            GameConfigLoader.LoadIfExists();

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

            var res = FireSimulatorDiagnostics.ComputeTuningCurveByTier(
                ruleset: EnemyGenerationRuleset.Full,
                difficulty: GameDifficulty.CometsAndAsteroids,
                radarLevel: 1,
                overrideEnemyMass: false,
                enemyMassKg: 1_000_000.0,
                overrideEnemyFractureEnergy: false,
                enemyFractureEnergy: 10_000.0,
                overrideEnemyManeuverability: false,
                enemyManeuverability: 1.0,
                overrideEnemyOffense: false,
                enemyOffense: 1.0,
                overrideBarrelLength: false,
                barrelLength: 100.0,
                overrideFireControlQuality: false,
                fireControlQuality: 1.0,
                overrideMuzzleVelocityMultiplier: false,
                muzzleVelocityMultiplier: 1.0,
                overrideProjectileMass: false,
                projectileMassKg: 100.0,
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
                samplesPerWave: 1,
                shotsPerSample: 200,
                simulateAimError: false);

            int tierCount = Math.Min(
                res.ExpectedHitRateByTier.Length,
                Math.Min(res.DetectionRateByTier.Length, res.BallisticsOkRateByTier.Length));

            Console.WriteLine($"TuningLab smoke: tiers={tierCount}");
            for (int i = 0; i < Math.Min(3, tierCount); i++)
            {
                Console.WriteLine(
                    $"Tier {i}: Det={res.DetectionRateByTier[i]:F3} BallisticsOK={res.BallisticsOkRateByTier[i]:F3} ExpectedHit={res.ExpectedHitRateByTier[i]:F3} ObservedHit={res.ObservedHitRateByTier[i]:F3}");
            }
        }

        private static void RunTuningLabHeadless()
        {
            Console.WriteLine("TuningLab headless: computing and appending CSV...");

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
                radarLevel: radarLevel,
                overrideEnemyMass: overrideEnemyMass,
                enemyMassKg: enemyMassKg,
                overrideEnemyFractureEnergy: overrideEnemyFractureEnergy,
                enemyFractureEnergy: enemyFractureEnergy,
                overrideEnemyManeuverability: overrideEnemyManeuverability,
                enemyManeuverability: enemyManeuverability,
                overrideEnemyOffense: overrideEnemyOffense,
                enemyOffense: enemyOffense,
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
                simulateAimError: simulateAimError);

            try
            {
                string csvPath = FireSimulatorDiagnostics.AppendTuningLabRunCsv(
                    ruleset: ruleset,
                    difficulty: difficulty,
                    radarLevel: radarLevel,
                    samplesPerWave: samplesPerWave,
                    shotsPerSample: shotsPerSample,
                    simulateAimError: simulateAimError,
                    overrideEnemyMass: overrideEnemyMass,
                    enemyMassKg: enemyMassKg,
                    overrideEnemyFractureEnergy: overrideEnemyFractureEnergy,
                    enemyFractureEnergy: enemyFractureEnergy,
                    overrideEnemyManeuverability: overrideEnemyManeuverability,
                    enemyManeuverability: enemyManeuverability,
                    overrideEnemyOffense: overrideEnemyOffense,
                    enemyOffense: enemyOffense,
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