using System;
using System.Threading;
using Spacegun_Simulator.UI;

namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            Thread.Sleep(500);

            // Boot into new page-based UI for Title + Main Menu (+ difficulty selection for New Game).
            var entry = UiEntryPoint.Run();

            switch (entry.Choice)
            {
                case UI.Pages.Core.MainMenuChoice.Exit:
                case UI.Pages.Core.MainMenuChoice.None:
                    return;

                case UI.Pages.Core.MainMenuChoice.NewGame:
                    {
                        var difficulty = entry.Difficulty ?? GameDifficulty.CometsAndAsteroids;
                        var gameState = new GameState(difficulty: difficulty);
                        // Start legacy single-session gameplay loop (phases) until game over.
                        var legacy = new ConsoleUI(gameState);
                        legacy.Run();
                        return;
                    }

                case UI.Pages.Core.MainMenuChoice.Resume:
                    {
                        var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids)
;
                        if (!gameState.LoadAutoSave())
                        {
                            Console.Clear();
                            Console.WriteLine("No autosave found (or failed to load). Press any key...");
                            Console.ReadKey(true);
                            return;
                        }

                        var legacy = new ConsoleUI(gameState);
                        legacy.Run();
                        return;
                    }

                case UI.Pages.Core.MainMenuChoice.TestMode:
                    {
                        // Keep diagnostics available for playtesters while pages are migrated.
                        var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids);
                        var legacy = new ConsoleUI(gameState);
                        legacy.RunDiagnosticsMenu();
                        return;
                    }

                default:
                    return;
            }
        }
    }
}
