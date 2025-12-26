using System;
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

        public static Result Run()
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
            var controller = new UiController(ui, PageId.Title);

            // Pages we need for boot flow
            var title = new TitleScreenPage();
            var menu = new MainMenuPage();
            var difficulty = new DifficultySelectionPage();

            controller.Register(title);
            controller.Register(menu);
            controller.Register(difficulty);

            // Run Title -> Menu (Menu exits controller)
            controller.Run();

            var choice = menu.Choice;

            if (choice != MainMenuChoice.NewGame)
                return new Result(choice, Difficulty: null);

            // Difficulty was selected inside the same controller
            return new Result(choice, difficulty.SelectedDifficulty);


        }

    }
}
