using Spacegun_Simulator.Core;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class DiagnosticsUiPageLauncherPage : PageBase
    {
        public override string Id => PageId.DiagnosticsUiPageLauncher;
        public override string Title => "UI PAGE LAUNCHER (NEW UI)";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            FooterHint: "Arrows=Select  Enter=Choose  B=Back  Esc=Menu  Q=Quit"
        );

        private enum Step
        {
            SelectTarget,
            SelectState
        }

        private enum LauncherTarget
        {
            Title,
            MainMenu,
            DifficultySelection,
            Detection,
            ResourceAllocation,
            Development,
            Firing,
            WaveComplete,
            GameOver
        }

        private enum StateMode
        {
            UseCurrentSession,
            SandboxTutorial,
            SandboxNormal
        }

        private Step _step;
        private int _selected;
        private LauncherTarget _target;

        private readonly List<string> _lines = new();

        public override void OnEnter(UiContext ui)
        {
            _step = Step.SelectTarget;
            _selected = 0;
            _target = LauncherTarget.Title;
            BuildLines();
        }

        private void BuildLines()
        {
            _lines.Clear();
            _lines.Add("Pick a page to launch:".PadRight(60));
            _lines.Add("");

            if (_step == Step.SelectTarget)
            {
                var values = (LauncherTarget[])Enum.GetValues(typeof(LauncherTarget));
                for (int i = 0; i < values.Length; i++)
                {
                    string cursor = i == _selected ? ">" : " ";
                    _lines.Add(Clamp60($"{cursor} {FormatTarget(values[i])}"));
                }
                _lines.Add("");
                _lines.Add("Enter=Choose target".PadRight(60));
                return;
            }

            _lines.Add(Clamp60($"Target: {FormatTarget(_target)}"));
            _lines.Add("");
            _lines.Add("Choose state setup:".PadRight(60));

            var modes = (StateMode[])Enum.GetValues(typeof(StateMode));
            for (int i = 0; i < modes.Length; i++)
            {
                string cursor = i == _selected ? ">" : " ";
                _lines.Add(Clamp60($"{cursor} {FormatState(modes[i])}"));
            }

            _lines.Add("");
            _lines.Add("Enter=Launch".PadRight(60));
        }

        private static string FormatTarget(LauncherTarget t) => t switch
        {
            LauncherTarget.Title => "Title",
            LauncherTarget.MainMenu => "Main Menu",
            LauncherTarget.DifficultySelection => "Difficulty Selection",
            LauncherTarget.Detection => "Detection (Phase)",
            LauncherTarget.ResourceAllocation => "Resource Allocation (Phase)",
            LauncherTarget.Development => "Development (Phase)",
            LauncherTarget.Firing => "Firing (Phase)",
            LauncherTarget.WaveComplete => "Wave Complete (Phase)",
            LauncherTarget.GameOver => "Game Over",
            _ => t.ToString()
        };

        private static string FormatState(StateMode m) => m switch
        {
            StateMode.UseCurrentSession => "Use current session state",
            StateMode.SandboxTutorial => "Sandbox (Tutorial)",
            StateMode.SandboxNormal => "Sandbox (Normal)",
            _ => m.ToString()
        };

        protected override void RenderBody(UiContext ui)
        {
            foreach (var line in _lines)
                ui.WriteLine(Clamp60(line));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.B)
                return PageResult.Back(PageId.TestModeMenu);

            int max = _step == Step.SelectTarget
                ? Enum.GetValues(typeof(LauncherTarget)).Length
                : Enum.GetValues(typeof(StateMode)).Length;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selected = Math.Max(0, _selected - 1);
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.DownArrow:
                    _selected = Math.Min(max - 1, _selected + 1);
                    BuildLines();
                    return PageResult.Stay;

                case ConsoleKey.Enter:
                    break;

                default:
                    return PageResult.Stay;
            }

            if (_step == Step.SelectTarget)
            {
                _target = ((LauncherTarget[])Enum.GetValues(typeof(LauncherTarget)))[_selected];
                _step = Step.SelectState;
                _selected = 0;
                BuildLines();
                return PageResult.Stay;
            }

            var mode = ((StateMode[])Enum.GetValues(typeof(StateMode)))[_selected];
            var currentEngine = ui.Game;
            if (currentEngine == null)
                return PageResult.Back(PageId.TestModeMenu);

            GameState gameForUi = currentEngine;
            if (mode != StateMode.UseCurrentSession)
            {
                var diff = mode == StateMode.SandboxTutorial
                    ? GameDifficulty.PotatoCannonsAndBeachballs
                    : GameDifficulty.CometsAndAsteroids;
                gameForUi = new GameState(difficulty: diff);
            }

            string startPageId = _target switch
            {
                LauncherTarget.Title => PageId.Title,
                LauncherTarget.MainMenu => PageId.MainMenu,
                LauncherTarget.DifficultySelection => PageId.DifficultySelection,
                LauncherTarget.Detection => PageId.Detection,
                LauncherTarget.ResourceAllocation => PageId.ResourceAllocation,
                LauncherTarget.Development => PageId.WeaponDevelopment,
                LauncherTarget.Firing => PageId.Firing,
                LauncherTarget.WaveComplete => PageId.WaveComplete,
                LauncherTarget.GameOver => PageId.GameOver,
                _ => PageId.MainMenu
            };

            if (startPageId == PageId.ResourceAllocation)
                DiagnosticsRunner.EnsureWaveAndPhaseReady(gameForUi, GameState.GamePhase.ResourceAllocation);
            else if (startPageId == PageId.WeaponDevelopment)
                DiagnosticsRunner.EnsureWaveAndPhaseReady(gameForUi, GameState.GamePhase.Development);
            else if (startPageId == PageId.Firing)
                DiagnosticsRunner.EnsureWaveAndPhaseReady(gameForUi, GameState.GamePhase.Firing);
            else if (startPageId == PageId.WaveComplete)
            {
                if (gameForUi.WavesDefeated <= 0)
                    gameForUi.WavesDefeated = 1;
                gameForUi.CurrentPhase = GameState.GamePhase.WaveComplete;
            }

            ui.Clear();

            if (startPageId == PageId.Firing)
            {
                var prevGame = ui.Game;
                ui.Game = gameForUi;
                    _ = Spacegun_Simulator.UI.Flows.FiringPhaseFlow.Run(ui, propagateSessionExitFromTools: false);
                ui.Game = prevGame;
                ui.RequestReturnToMenu = false;
                ui.RequestExitGame = false;
            }
            else if (startPageId == PageId.WeaponDevelopment)
            {
                DiagnosticsRunner.RunDevelopmentLauncher(ui, gameForUi);
            }
            else
            {
                DiagnosticsRunner.RunSinglePage(ui, startPageId, gameForUi);
            }

            ui.Clear();
            return PageResult.Back(PageId.TestModeMenu);
        }
    }
}
