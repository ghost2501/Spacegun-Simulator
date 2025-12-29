using System;
using System.IO;
using Spacegun_Simulator.UI.Screen;
using Spacegun_Simulator.UI.Pages.Core;

namespace Spacegun_Simulator.UI
{
    /// <summary>
    /// Bridges the new page-based UI (Title/MainMenu/Difficulty selection) into legacy gameplay flow.
    /// Keeps the game playable while pages are migrated incrementally.
    /// </summary>
    public static class UiEntryPoint
    {
        public sealed record Result(
            MainMenuChoice Choice,
            GameDifficulty? Difficulty
        );

        public static Result Run(string startPageId = PageId.Title)
        {
            // ✅ Create layout once for the whole UI session (title -> menu -> difficulty)
            var layout = new ScreenLayout();

            // ✅ Create UiContext once, so pages share the same layout + settings
            var ui = new UiContext(layout);

            // Write Log
            // Debug Page Migration
            ui.Log = msg =>
            {
                try
                {
                    var line = $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
                    File.AppendAllText("ui.log", line);
                }
                catch
                {
                    // Never let logging crash the UI
                }
            };

            // ✅ Create controller using the shared context
            var controller = new UiController(ui, startPageId);

            // Pages we need for boot flow
            var title = new TitleScreenPage();
            var menu = new MainMenuPage();
            var difficulty = new DifficultySelectionPage();

            controller.Register(title);
            controller.Register(menu);
            controller.Register(difficulty);

            controller.Run();

            // If the boot UI ended due to ESC/Q, treat it as Exit (don’t fall through as None).
            if (ui.RequestReturnToMenu || ui.RequestExitGame)
                return new Result(MainMenuChoice.Exit, Difficulty: null);

            var choice = menu.Choice;

            // Any non-NewGame choice just returns directly.
            if (choice != MainMenuChoice.NewGame)
                return new Result(choice, Difficulty: null);

            // NewGame was chosen, but difficulty must be selected.
            // If difficulty is null, the player likely ESC/Q'd out of difficulty selection.
            // Treat this as "return to main menu" by clearing the choice and re-running the UI in Program.
            if (difficulty.SelectedDifficulty is null)
                return new Result(MainMenuChoice.None, Difficulty: null);

            return new Result(choice, difficulty.SelectedDifficulty);
        }
    }
}
