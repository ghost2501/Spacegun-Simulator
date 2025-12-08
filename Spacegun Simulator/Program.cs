namespace Spacegun_Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GameConfigLoader.LoadIfExists();

            Console.WriteLine("Loading Space Gun Defense Simulator...\n");
            System.Threading.Thread.Sleep(1000);

            // Create game state with default difficulty (will be overridden if new game)
            var gameState = new GameState(difficulty: GameDifficulty.CometsAndAsteroids);
            var ui = new ConsoleUI(gameState);

            ui.Run();
        }
    }
}