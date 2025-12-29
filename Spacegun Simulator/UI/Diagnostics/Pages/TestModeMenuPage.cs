using System;
using System.Collections.Generic;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class TestModeMenuPage : PageBase
    {
        public override string Id => PageId.TestModeMenu;
        public override string Title => "TEST MODE - DEBUG TOOLS";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            FooterHint: "1=Challenge  2=Harness  3=Launcher  4=Return  Enter=Choose  B=Back  Esc=Menu  Q=Quit"
        );

        private readonly List<string> _lines = new();
        private int _selected;

        private static readonly string[] s_options =
        {
            "Firing Challenge (Quick Firing Test)",
            "Test Harness (Automated Validation)",
            "UI Page Launcher (New UI)",
            "Return to Main Menu",
        };

        public override void OnEnter(UiContext ui)
        {
            _selected = 0;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();
            _lines.Add("Select a diagnostic tool:".PadRight(60));
            _lines.Add("");

            for (int i = 0; i < s_options.Length; i++)
            {
                string cursor = i == _selected ? ">" : " ";
                _lines.Add(Clamp60($"{cursor} [{i + 1}] {s_options[i]}"));
            }
        }

        protected override void RenderBody(UiContext ui)
        {
            if (_lines.Count == 0)
                BuildLines();

            foreach (var line in _lines)
                ui.WriteLine(Clamp60(line));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.B)
                return PageResult.Exit;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selected = Math.Max(0, _selected - 1);
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    _selected = Math.Min(s_options.Length - 1, _selected + 1);
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    _selected = 0;
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    _selected = 1;
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    _selected = 2;
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    _selected = 3;
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.Enter:
                    return _selected switch
                    {
                        0 => PageResult.Go(PageId.FiringChallenge),
                        1 => PageResult.Go(PageId.DiagnosticsTestHarness),
                        2 => PageResult.Go(PageId.DiagnosticsUiPageLauncher),
                        _ => PageResult.Exit,
                    };
            }

            return PageResult.Stay;
        }
    }
}
