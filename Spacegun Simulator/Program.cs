using System;
using System.Threading;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Diagnostics;
using Spacegun_Simulator.UI.Flows;
using Spacegun_Simulator.UI.Pages.Core;

namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            Thread.Sleep(500);

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
                            var difficulty = entry.Difficulty ?? GameDifficulty.CometsAndAsteroids;
                            var gameState = new GameState(difficulty: difficulty);

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