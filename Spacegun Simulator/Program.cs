using Spacegun_Simulator;
using System;

namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            System.Threading.Thread.Sleep(1000);

            var gameState = new GameState();
            var ui = new ConsoleUI(gameState);

            ui.Run();
        }
    }
}