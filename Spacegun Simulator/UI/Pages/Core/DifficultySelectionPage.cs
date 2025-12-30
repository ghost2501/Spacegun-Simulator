using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.Core
{
    /// <summary>
    /// New-UI difficulty selection. Uses key input (no ReadLine) and exits the UI flow when a choice is made.
    /// </summary>
    public sealed class DifficultySelectionPage : PageBase
    {
        public override string Id => PageId.DifficultySelection;
        public override string Title => "DIFFICULTY SELECTION";

        /// <summary>
        /// Default behavior matches the boot flow: Esc navigates to the Main Menu page.
        /// Diagnostic callers can set this to false to make Esc simply exit the page.
        /// </summary>
        public bool EscapeNavigatesToMainMenu { get; init; } = true;

        public override PageChrome Chrome => new(
            ShowStatusBar: true,
            ShowSidePanels: true,
			FooterHint: "Select(↩)  1-4=Select  (M)enu"
        );

        private IReadOnlyList<DifficultyConfig> _configs = Array.Empty<DifficultyConfig>();
		private int _selectedIndex;

        public GameDifficulty? SelectedDifficulty { get; private set; }

        public override void OnEnter(UiContext ui)
        {
            base.OnEnter(ui);
            SelectedDifficulty = null;
            _configs = DifficultyConfig.GetAllConfigs();
			_selectedIndex = 0;
        }

        protected override void RenderBody(UiContext ui)
        {
            ui.WriteLine("Choose your scenario:");
            ui.WriteLine();

            for (int i = 0; i < _configs.Count; i++)
            {
                var c = _configs[i];
				string cursor = (i == _selectedIndex) ? ">" : " ";
				ui.WriteLine($"{cursor} [{i + 1}] {c.DisplayName}");
                if (!string.IsNullOrWhiteSpace(c.NarrativeDescription))
                {
                    var lines = c.NarrativeDescription.Replace("\r", "").Split('\n');
                    foreach (var line in lines)
                        ui.WriteLine($"     {line}");
                }
                ui.WriteLine();
            }

            ui.WriteLine("Tip: Tutorial mode skips resource phases.");
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            if (_configs.Count > 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        _selectedIndex = Math.Clamp(_selectedIndex - 1, 0, _configs.Count - 1);
                        return PageResult.Stay;

                    case ConsoleKey.DownArrow:
                        _selectedIndex = Math.Clamp(_selectedIndex + 1, 0, _configs.Count - 1);
                        return PageResult.Stay;

                    case ConsoleKey.Enter:
                        _selectedIndex = Math.Clamp(_selectedIndex, 0, _configs.Count - 1);
                        SelectedDifficulty = _configs[_selectedIndex].Difficulty;
                        return PageResult.Exit;
                }
            }

            int? n = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
                ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
                _ => null
            };

            if (n is null)
                return PageResult.Stay;

            int idx = n.Value - 1;
            if (idx < 0 || idx >= _configs.Count)
                return PageResult.Stay;

            SelectedDifficulty = _configs[idx].Difficulty;
            return PageResult.Exit;
        }

        protected override PageResult HandleEscape(UiContext ui, ConsoleKeyInfo key)
            => EscapeNavigatesToMainMenu ? PageResult.Go(PageId.MainMenu) : PageResult.Exit;

		protected override PageResult HandleMenu(UiContext ui, ConsoleKeyInfo key)
			=> EscapeNavigatesToMainMenu ? PageResult.Go(PageId.MainMenu) : PageResult.Exit;
    }
}