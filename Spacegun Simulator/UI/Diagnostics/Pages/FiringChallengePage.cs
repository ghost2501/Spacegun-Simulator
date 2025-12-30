using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Diagnostics.Pages
{
    public sealed class FiringChallengePage : PageBase
    {
        public override string Id => PageId.FiringChallenge;
        public override string Title => "FIRING CHALLENGE (DEBUG)";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
			FooterHint: "Start(↩) (B)ack (M)enu (Q)uit"
        );

        private readonly List<string> _lines = new();

        public override void OnEnter(UiContext ui)
        {
            _lines.Clear();
            _lines.Add("Generates a single firing scenario and jumps to firing.".PadRight(60));
            _lines.Add("You will be asked to pick a difficulty.".PadRight(60));
            _lines.Add("");
            _lines.Add("Press Enter to begin.".PadRight(60));
        }

        protected override void RenderBody(UiContext ui)
        {
            foreach (var line in _lines)
                ui.WriteLine(Clamp60(line));
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.B)
                return PageResult.Back(PageId.TestModeMenu);

            if (key.Key != ConsoleKey.Enter)
                return PageResult.Stay;

            var engine = ui.Game;
            if (engine != null)
            {
                ui.Clear();
                DiagnosticsRunner.RunFiringChallenge(ui, engine);
            }

            ui.Clear();
            return PageResult.Back(PageId.TestModeMenu);
        }
    }
}
