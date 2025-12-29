using System;
using Spacegun_Simulator.Tests;
using Spacegun_Simulator.UI.Pages;

namespace Spacegun_Simulator.UI.Diagnostics
{
    internal static class DiagnosticsRunner
    {
        public static void EnsureWaveAndPhaseReady(GameState game, GameState.GamePhase desiredPhase)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            bool needDetection = game.CurrentWave == null || game.AvailableYears <= 0 || game.RemainingYears <= 0;
            if (needDetection)
            {
                try
                {
                    game.CurrentPhase = GameState.GamePhase.Detection;
                    game.ExecuteDetectionPhase();
                }
                catch
                {
                    // Best-effort setup; diagnostics should not crash.
                }
            }

            game.CurrentPhase = desiredPhase;
        }

        public static GameDifficulty? SelectDifficulty(UiContext ui)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));

            var page = new Spacegun_Simulator.UI.Pages.Core.DifficultySelectionPage
            {
                EscapeNavigatesToMainMenu = false
            };

            var controller = new UiController(ui, PageId.DifficultySelection);
            controller.Register(page);
            controller.Run();

            // Diagnostics selector: Esc/back cancels selection.
            ui.RequestReturnToMenu = false;
            ui.RequestExitGame = false;

            return page.SelectedDifficulty;
        }

        public static void RunTestHarness()
        {
            _ = FireSimulatorDiagnostics.RunConsistencyChecks();
            _ = FireSimulatorDiagnostics.RunTechAuditAndWriteCsv();
        }

        public static void RunDevelopmentLauncher(UiContext ui, GameState game)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (game == null) throw new ArgumentNullException(nameof(game));

            var prevGame = ui.Game;
            ui.Game = game;

            var page = new DevelopmentPage();
            var controller = new UiController(ui, PageId.WeaponDevelopment);
            controller.Register(page);
            controller.Register(new DetailedWeaponStatusPage());
            controller.Register(new GunDevelopmentPage());
            controller.Register(new ProjectileDevelopmentPage());
            controller.Run();

            ui.RequestReturnToMenu = false;
            ui.RequestExitGame = false;

            if (page.Action == DevelopmentPage.DevelopmentMenuAction.Done)
                game.CurrentPhase = GameState.GamePhase.Firing;

            ui.Game = prevGame;
        }

        public static void RunFireControlTool(UiContext ui, GameState game, string startPageId)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (game == null) throw new ArgumentNullException(nameof(game));

            var prevGame = ui.Game;
            ui.Game = game;

            var controller = new UiController(ui, startPageId);
            controller.Register(new Spacegun_Simulator.UI.Pages.FireControl.MotionComputerPage());
            controller.Register(new Spacegun_Simulator.UI.Pages.FireControl.BallisticsTablesPage());
            controller.Register(new Spacegun_Simulator.UI.Pages.FireControl.TrajectoryPlotterPage());
            controller.Register(new Spacegun_Simulator.UI.Pages.FireControl.FireSimulatorPage());
            controller.Run();

            ui.RequestReturnToMenu = false;
            ui.RequestExitGame = false;

            ui.Game = prevGame;
        }

        public static void RunSinglePage(UiContext ui, string startPageId, GameState game)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (game == null) throw new ArgumentNullException(nameof(game));

            var prevGame = ui.Game;
            ui.Game = game;

            var controller = new UiController(ui, startPageId);
            controller.Register(new Spacegun_Simulator.UI.Pages.Core.TitleScreenPage());
            controller.Register(new Spacegun_Simulator.UI.Pages.Core.MainMenuPage());
            controller.Register(new Spacegun_Simulator.UI.Pages.Core.DifficultySelectionPage());

            controller.Register(new DetectionPage());
            controller.Register(new ResourceAllocationPage());
            controller.Register(new ResourceOptionsPage());
            controller.Register(new ResearchMenuPage());
            controller.Register(new PreparationStatusPage());
            controller.Register(new PreparationSummaryPage());
            controller.Register(new DevelopmentPage());
            controller.Register(new DetailedWeaponStatusPage());
            controller.Register(new GunDevelopmentPage());
            controller.Register(new ProjectileDevelopmentPage());
            controller.Register(new FiringPhasePage());
            controller.Register(new WaveCompletePage());
            controller.Register(new Spacegun_Simulator.UI.Pages.Core.GameOverPage());

            controller.Run();

            ui.RequestReturnToMenu = false;
            ui.RequestExitGame = false;

            ui.Game = prevGame;
        }

        public static void RunFiringChallenge(UiContext ui, GameState engine)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            var selected = SelectDifficulty(ui);
            if (selected is null)
                return;

            var difficulty = selected.Value;
            engine.SelectedDifficulty = difficulty;

            engine.CurrentWaveNumber = 1;
            engine.IsGameOver = false;
            engine.WavesDefeated = 0;
            engine.CurrentPhase = GameState.GamePhase.Detection;

            if (engine.CampaignEnemyType == null)
                engine.CampaignEnemyType = EnemyType.GenerateForCampaign(engine.rng ?? new Random());

            try
            {
                var detectionResult = engine.ExecuteDetectionPhase();
                if (!detectionResult.WaveDetected)
                    return;

                engine.CurrentPhase = GameState.GamePhase.Firing;

                // Run the normal firing-phase page loop (tools + commit).
                var prevGame = ui.Game;
                ui.Game = engine;
                    _ = Spacegun_Simulator.UI.Flows.FiringPhaseFlow.Run(ui, propagateSessionExitFromTools: false);
                ui.Game = prevGame;

                ui.RequestReturnToMenu = false;
                ui.RequestExitGame = false;
            }
            catch
            {
                // Swallow in diagnostics.
            }
        }
    }
}
