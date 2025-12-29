using Spacegun_Simulator.UI.Pages.Development;
using Spacegun_Simulator.UI.Screen;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Flows
{
    public enum GameSessionExitAction
    {
        None = 0,
        ReturnToMenu = 1,
        ExitGame = 2
    }

    /// <summary>
    /// Runs a full gameplay session using the page-based UI for phases.
    /// </summary>
    public static class GameSessionFlow
    {
        private const string SaveDirectory = "Saves";

        public static GameSessionExitAction Run(GameState game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            EnsureSaveDirectory();

            // Preserve legacy "global indent" behavior so any remaining Console.WriteLine
            // callsites outside page-buffered rendering still look correct.
            var originalOut = Console.Out;
            var indentWriter = new IndentTextWriter(originalOut, indentSpaces: 30);
            Console.SetOut(indentWriter);

            var layout = new ScreenLayout(offset: indentWriter.IndentLength, frameWidth: 60);

            var ui = new UiContext(
                layout: layout,
                originalOut: originalOut,
                indentWriter: indentWriter,
                globalIndent: indentWriter.IndentLength)
            {
                Game = game,
                DebugEnabled = false
            };

            while (!game.IsGameOver && !ui.RequestReturnToMenu && !ui.RequestExitGame)
            {
                switch (game.CurrentPhase)
                {
                    case GameState.GamePhase.Detection:
                        RunDetection(ui);
                        break;

                    case GameState.GamePhase.ResourceAllocation:
                        RunResourceAllocation(ui);
                        break;

                    case GameState.GamePhase.Development:
                        RunDevelopment(ui);
                        break;

                    case GameState.GamePhase.Firing:
                        _ = FiringPhaseFlow.Run(ui);
                        break;

                    case GameState.GamePhase.WaveComplete:
                        RunWaveComplete(ui);
                        break;

                    default:
                        game.IsGameOver = true;
                        break;
                }
            }

            if (ui.RequestExitGame)
                return GameSessionExitAction.ExitGame;

            if (ui.RequestReturnToMenu)
            {
                game.AutoSaveGame();
                return GameSessionExitAction.ReturnToMenu;
            }

            if (game.IsGameOver)
            {
                ShowGameOver(ui);
                DeleteAutoSave();

                // Reset game state for next game.
                game.IsGameOver = false;
                ui.RequestExitGame = false;
                ui.RequestReturnToMenu = false;

                return GameSessionExitAction.ReturnToMenu;
            }

            return GameSessionExitAction.None;
        }

        private static void RunDetection(UiContext ui)
        {
            var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null.");

            var controller = new UiController(ui, PageId.Detection);
            PageCatalog.RegisterDetection(controller);
            controller.Run();

            if (!ui.RequestExitGame && !ui.RequestReturnToMenu && !game.IsGameOver)
                game.AutoSaveGame();
        }

        private static void RunResourceAllocation(UiContext ui)
        {
            var controller = new UiController(ui, PageId.ResourceAllocation);
            PageCatalog.RegisterResourcePhasePages(controller);
            controller.Run();

            GamePhaseRouter.ApplyAfterPhaseControllerRun(ui, GameState.GamePhase.ResourceAllocation);
        }

        private static void RunDevelopment(UiContext ui)
        {
            var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null.");

            var page = new DevelopmentPage();
            var controller = new UiController(ui, PageId.WeaponDevelopment);
            controller.Register(page);
            PageCatalog.RegisterDevelopmentSubpages(controller);
            controller.Run();

            GamePhaseRouter.ApplyAfterDevelopmentRun(ui, page);
        }

        private static void RunWaveComplete(UiContext ui)
        {
            var controller = new UiController(ui, PageId.WaveComplete);
            PageCatalog.RegisterWaveComplete(controller);
            controller.Run();

            GamePhaseRouter.ApplyAfterPhaseControllerRun(ui, GameState.GamePhase.WaveComplete);
        }

        private static void ShowGameOver(UiContext ui)
        {
            var controller = new UiController(ui, PageId.GameOver);
            PageCatalog.RegisterGameOver(controller);
            controller.Run();

            // GameOverPage treats any key as "continue"; don't propagate intents.
            ui.RequestReturnToMenu = false;
            ui.RequestExitGame = false;
        }

        private static void EnsureSaveDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }

        private static void DeleteAutoSave()
        {
            try
            {
                string savePath = Path.Combine(SaveDirectory, "AutoSave.json");
                if (File.Exists(savePath))
                    File.Delete(savePath);
            }
            catch
            {
                // Non-critical
            }
        }
    }
}
