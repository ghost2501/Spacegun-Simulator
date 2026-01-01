using Spacegun_Simulator.UI.Screen;
using Spacegun_Simulator.UI.Pages.Core;
using Spacegun_Simulator.UI.Pages.Audio;
using Spacegun_Simulator.Core;

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
            GameModeId? Mode
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
            var musicConfig = new MusicConfigurationPage();
            var mode = new ModeSelectionPage();

            controller.Register(title);
            controller.Register(menu);
            controller.Register(musicConfig);
            controller.Register(mode);

            controller.Run();

            // If the boot UI ended due to ESC/Q, treat it as Exit (don’t fall through as None).
            if (ui.RequestReturnToMenu || ui.RequestExitGame)
                return new Result(MainMenuChoice.Exit, Mode: null);

            var choice = menu.Choice;

            // Any non-NewGame choice just returns directly.
            if (choice != MainMenuChoice.NewGame)
                return new Result(choice, Mode: null);

            // NewGame was chosen, but mode must be selected.
            // If mode is null, the player likely ESC/Q'd out of mode selection.
            // Treat this as "return to main menu" by clearing the choice and re-running the UI in Program.
            if (mode.SelectedMode is null)
                return new Result(MainMenuChoice.None, Mode: null);

            return new Result(choice, mode.SelectedMode);
        }
    }
}
