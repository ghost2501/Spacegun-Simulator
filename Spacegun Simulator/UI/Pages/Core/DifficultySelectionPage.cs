using System;
using System.Collections.Generic;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Core
{
    /// <summary>
    /// New-UI difficulty selection. Uses key input (no ReadLine) and exits the UI flow when a choice is made.
    /// </summary>
    public sealed class DifficultySelectionPage : PageBase
    {
        public override string Id => PageId.DifficultySelection;
        public override string Title => "DIFFICULTY SELECTION";

        public override PageChrome Chrome { get; } = new(
            ShowStatusBar: true,
            ShowSidePanels: true,
            FooterHint: "1-4 = Select difficulty   Esc = Menu/Back"
        );

        private IReadOnlyList<DifficultyConfig> _configs = Array.Empty<DifficultyConfig>();

        public GameDifficulty? SelectedDifficulty { get; private set; }

        public override void OnEnter(UiContext ui)
        {
            base.OnEnter(ui);
            SelectedDifficulty = null;
            _configs = DifficultyConfig.GetAllConfigs();
        }

        protected override void RenderBody(UiContext ui)
        {
            ui.WriteLine("Choose your scenario:");
            ui.WriteLine();

            for (int i = 0; i < _configs.Count; i++)
            {
                var c = _configs[i];
                ui.WriteLine($"[{i + 1}] {c.DisplayName}");
                if (!string.IsNullOrWhiteSpace(c.NarrativeDescription))
                {
                    // Indent narrative bullets
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
    }
}
