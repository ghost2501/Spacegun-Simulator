using Spacegun_Simulator.Core;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Core
{
    /// <summary>
    /// Scenario/mode selection.
    /// Keeps the existing page-based boot flow, but selects a higher-level GameMode (economy/dev vs pure).
    /// </summary>
    public sealed class ModeSelectionPage : PageBase
    {
        public override string Id => PageId.ModeSelection;
        public override string Title => "MODE SELECTION";

        public bool EscapeNavigatesToMainMenu { get; init; } = true;

        public override PageChrome Chrome => new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            FooterHint: "Select(↩)  ↑↓=Move  (M)enu"
        );

        private enum Step
        {
            Category = 0,
            Difficulty = 1
        }

        private enum Category
        {
            Tutorial = 0,
            Pure = 1,
            Full = 2
        }

        private enum Difficulty
        {
            Easy = 0,
            Hard = 1,
            Extreme = 2
        }

        private Step _step;
        private int _selectedIndex;
        private Category _selectedCategory;

        public GameModeId? SelectedMode { get; private set; }

        public override void OnEnter(UiContext ui)
        {
            base.OnEnter(ui);
            SelectedMode = null;
            _selectedIndex = 0;
            _step = Step.Category;
            _selectedCategory = Category.Tutorial;
        }

        private int GetItemCount() => _step switch
        {
            Step.Category => 3,
            Step.Difficulty => 3,
            _ => 0
        };

        private void ApplyChromeFooterHint(UiContext ui)
        {
            // Keep the chrome stable, but adjust the hint text by step.
            // (PageBase reads Chrome per frame; we can't mutate Chrome itself.)
            // So we provide guidance inline in the body instead.
        }

        protected override void RenderBody(UiContext ui)
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, GetItemCount() - 1));

            if (_step == Step.Category)
            {
                ui.WriteLine("Choose mode type:");
                ui.WriteLine();

                WriteChoice(ui, 0, "Tutorial", "Starts immediately. No economy/dev.");
                WriteChoice(ui, 1, "Pure", "No economy/dev. Randomized per run.");
                WriteChoice(ui, 2, "Full Game", "Some RNG. Tech development & economy.");

                ui.WriteLine();
                ui.WriteLine("Tip: After Pure/Full, choose difficulty.");
                return;
            }

            ui.WriteLine($"Choose difficulty ({_selectedCategory switch { Category.Pure => "Pure", Category.Full => "Full Game", _ => "" }}):");
            ui.WriteLine();

            WriteChoice(ui, 0, "Easy", "Forgiving tolerance.");
            WriteChoice(ui, 1, "Hard", "Moderate tolerance.");
            WriteChoice(ui, 2, "Extreme", "Tight tolerance.");

            ui.WriteLine();
            ui.WriteLine("Tip: Esc goes back.");
        }

        private void WriteChoice(UiContext ui, int index, string label, string description)
        {
            string cursor = (index == _selectedIndex) ? ">" : " ";
            ui.WriteLine($"{cursor} [{index + 1}] {label}");
            if (!string.IsNullOrWhiteSpace(description))
                ui.WriteLine($"     {description}");
            ui.WriteLine();
        }

        protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
        {
            int itemCount = GetItemCount();
            if (itemCount > 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        _selectedIndex = Math.Clamp(_selectedIndex - 1, 0, itemCount - 1);
                        return PageResult.Stay;

                    case ConsoleKey.DownArrow:
                        _selectedIndex = Math.Clamp(_selectedIndex + 1, 0, itemCount - 1);
                        return PageResult.Stay;

                    case ConsoleKey.Enter:
                        return ApplySelectionAndMaybeAdvance();
                }
            }

            int? n = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
                ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
                ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
                ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
                ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
                ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
                ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
                _ => null
            };

            if (n is null)
                return PageResult.Stay;

            int idx = n.Value - 1;
            if (idx < 0 || idx >= itemCount)
                return PageResult.Stay;

            _selectedIndex = idx;
            return ApplySelectionAndMaybeAdvance();
        }

        private PageResult ApplySelectionAndMaybeAdvance()
        {
            if (_step == Step.Category)
            {
                _selectedCategory = _selectedIndex switch
                {
                    0 => Category.Tutorial,
                    1 => Category.Pure,
                    2 => Category.Full,
                    _ => Category.Tutorial
                };

                if (_selectedCategory == Category.Tutorial)
                {
                    SelectedMode = GameModeId.Tutorial_PotatoCannonsAndBeachballs;
                    return PageResult.Exit;
                }

                _step = Step.Difficulty;
                _selectedIndex = 0;
                return PageResult.Stay;
            }

            var difficulty = _selectedIndex switch
            {
                0 => Difficulty.Easy,
                1 => Difficulty.Hard,
                2 => Difficulty.Extreme,
                _ => Difficulty.Hard
            };

            SelectedMode = (_selectedCategory, difficulty) switch
            {
                (Category.Pure, Difficulty.Easy) => GameModeId.Pure_NuclearMissile,
                (Category.Pure, Difficulty.Hard) => GameModeId.Pure_ShootingAsteroidsWithSpaceBullets,
                (Category.Pure, Difficulty.Extreme) => GameModeId.Pure_SpaceBulletsVsSpaceBullets,

                (Category.Full, Difficulty.Easy) => GameModeId.Economy_NuclearTorpedosVsSpaceships,
                (Category.Full, Difficulty.Hard) => GameModeId.Economy_KineticDronesVsRobotAsteroids,
                (Category.Full, Difficulty.Extreme) => GameModeId.Economy_SmartBulletsVsLivingProjectiles,

                _ => GameModeId.Economy_KineticDronesVsRobotAsteroids
            };

            return PageResult.Exit;
        }

        protected override PageResult HandleEscape(UiContext ui, ConsoleKeyInfo key)
        {
            if (_step == Step.Difficulty)
            {
                _step = Step.Category;
                _selectedIndex = (int)_selectedCategory;
                return PageResult.Stay;
            }

            return EscapeNavigatesToMainMenu ? PageResult.Go(PageId.MainMenu) : PageResult.Exit;
        }

        protected override PageResult HandleMenu(UiContext ui, ConsoleKeyInfo key)
            => EscapeNavigatesToMainMenu ? PageResult.Go(PageId.MainMenu) : PageResult.Exit;
    }
}
