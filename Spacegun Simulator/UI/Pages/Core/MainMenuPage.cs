using Spacegun_Simulator.Core;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Core
{
    public sealed class MainMenuPage : PageBase
    {
        public override string Id => PageId.MainMenu;
        public override string Title => "MAIN MENU";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            FooterHint: "1=New  2=Resume/Test  3=Test  4=Music  5=Exit"
        );

        private bool _autoSaveExists;
        private string _autoSaveTimestamp = "";

        public MainMenuChoice Choice { get; private set; } = MainMenuChoice.None;

        public override void OnEnter(UiContext ui)
        {
            Choice = MainMenuChoice.None;
            _autoSaveExists = GameState.AutoSaveExists();
            _autoSaveTimestamp = _autoSaveExists ? GameState.GetAutoSaveTimestamp() : "";
        }

        protected override void RenderBody(UiContext ui)
        {
            ui.WriteLine("Select an option:");
            ui.WriteLine();

            if (_autoSaveExists)
            {
                ui.WriteLine("  1) New Game");
                ui.WriteLine($"  2) Resume {_autoSaveTimestamp}");
                ui.WriteLine("  3) Test Mode");
                ui.WriteLine("  4) Music Configuration");
                ui.WriteLine("  5) Exit");
            }
            else
            {
                ui.WriteLine("  1) New Game");
                ui.WriteLine("  2) Test Mode");
                ui.WriteLine("  3) Music Configuration");
                ui.WriteLine("  4) Exit");
            }
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            // accept digit keys (top row and numpad)
            int? n = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
                ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
                ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
                _ => null
            };

            if (n is null) return PageResult.Stay;

            if (_autoSaveExists)
            {
                switch (n.Value)
                {
                    case 1: Choice = MainMenuChoice.NewGame; return PageResult.Go(PageId.DifficultySelection);
                    case 2: Choice = MainMenuChoice.Resume; return PageResult.Exit;
                    case 3: Choice = MainMenuChoice.TestMode; return PageResult.Exit;
                    case 4: return PageResult.Go(PageId.MusicConfiguration);
                    case 5: Choice = MainMenuChoice.Exit; return PageResult.Exit;
                }
            }
            else
            {
                switch (n.Value)
                {
                    case 1: Choice = MainMenuChoice.NewGame; return PageResult.Go(PageId.DifficultySelection);
                    case 2: Choice = MainMenuChoice.TestMode; return PageResult.Exit;
                    case 3: return PageResult.Go(PageId.MusicConfiguration);
                    case 4: Choice = MainMenuChoice.Exit; return PageResult.Exit;
                }
            }

            return PageResult.Stay;
        }

        protected override PageResult HandleQuit(UiContext ui, ConsoleKeyInfo key)
        {
            ui.RequestExitGame = true;
            Choice = MainMenuChoice.Exit;
            return PageResult.Exit;
        }

        protected override PageResult HandleEscape(UiContext ui, ConsoleKeyInfo key)
        {
            Choice = MainMenuChoice.Exit;
            return PageResult.Exit;
        }
    }

    public enum MainMenuChoice
    {
        None,
        NewGame,
        Resume,
        TestMode,
        Exit
    }
}