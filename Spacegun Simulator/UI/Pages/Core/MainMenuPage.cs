using System;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Core
{
    public sealed class MainMenuPage : PageBase
    {
        public override string Id => PageId.MainMenu;
        public override string Title => "MAIN MENU";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: false,
            ShowSidePanels: false,
            FooterHint: "1=New  2=Resume/Test  3=Test/Exit  4=Exit (if Resume exists)"
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

        public override void Render(UiContext ui)
        {
            // No generic PageBase frame here; render menu cleanly.
            Console.Clear();

            Console.WriteLine("SPACEGUN SIMULATOR");
            Console.WriteLine();

            if (_autoSaveExists)
            {
                Console.WriteLine($"Auto-save found (last saved: {_autoSaveTimestamp})");
                Console.WriteLine();
                Console.WriteLine("[1] Start New Game");
                Console.WriteLine("[2] Resume Game");
                Console.WriteLine("[3] Test Mode (Debug Tools)");
                Console.WriteLine("[4] Exit");
            }
            else
            {
                Console.WriteLine("No auto-save found.");
                Console.WriteLine();
                Console.WriteLine("[1] Start New Game");
                Console.WriteLine("[2] Test Mode (Debug Tools)");
                Console.WriteLine("[3] Exit");
            }

            Console.WriteLine();
            Console.Write("Select option: ");
        }

        protected override void RenderBody(UiContext ui) { /* unused; Render overridden */ }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            // accept digit keys (top row and numpad)
            int? n = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
                ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
                _ => null
            };

            if (n is null) return PageResult.Stay;

            if (_autoSaveExists)
            {
                switch (n.Value)
                {
                    case 1: Choice = MainMenuChoice.NewGame; return PageResult.Exit;
                    case 2: Choice = MainMenuChoice.Resume; return PageResult.Exit;
                    case 3: Choice = MainMenuChoice.TestMode; return PageResult.Exit;
                    case 4: Choice = MainMenuChoice.Exit; return PageResult.Exit;
                }
            }
            else
            {
                switch (n.Value)
                {
                    case 1: Choice = MainMenuChoice.NewGame; return PageResult.Exit;
                    case 2: Choice = MainMenuChoice.TestMode; return PageResult.Exit;
                    case 3: Choice = MainMenuChoice.Exit; return PageResult.Exit;
                }
            }

            return PageResult.Stay;
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
