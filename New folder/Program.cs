using System;
using System.Threading;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages.Core;

namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            Thread.Sleep(1000);

            // Keep your existing game state creation (we'll attach it to UiContext later)
            var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids);

            var ui = new UiContext();
            var controller = new UiController(ui, PageId.Title);

            controller.Register(new TitleScreenPage());
            controller.Register(new MainMenuPage());

            controller.Run();


        }
    }
}
