using Spacegun_Simulator;
using System;

namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("=== SPACE GUN SIMULATOR ===\n");

            var gameState = new GameState();
            var ui = new ConsoleUI(gameState);

            ui.Run();
        }
    }
}