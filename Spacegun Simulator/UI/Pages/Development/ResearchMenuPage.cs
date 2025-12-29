using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Technology;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class ResearchMenuPage : PageBase
{
	public override string Id => PageId.ResearchMenu;
	public override string Title => "RESEARCH";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Number=Research  B=Back  Esc=Menu  Q=Quit   ↑/↓/PgUp/PgDn=Scroll"
	);

	private readonly List<string> _lines = new();
	private int _scroll;
	private string _message = "";
	private List<TechUnlock> _availableTechs = new();

	public override void OnEnter(UiContext ui)
	{
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResearchMenuPage requires GameState). ");
		_scroll = 0;
		_message = "";
		_availableTechs = TechUnlock.GetAvailableUnlocks(game.TechTree);
		BuildLines(ui);
	}

	private void BuildLines(UiContext ui)
	{
		_lines.Clear();
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResearchMenuPage requires GameState). ");

		_lines.Add("=== AVAILABLE TECH RESEARCH ===");
		_lines.Add("");

		if (!string.IsNullOrWhiteSpace(_message))
		{
			_lines.Add(_message);
			_lines.Add("");
		}

		if (_availableTechs.Count == 0)
		{
			_lines.Add("✗ No techs available for research.");
			_lines.Add("");
			_lines.Add("Press B to return.");
			return;
		}

		for (int i = 0; i < _availableTechs.Count; i++)
		{
			var unlock = _availableTechs[i];
			bool canAfford = TechUnlock.CanAffordResearch(unlock, game.AccumulatedResources);
			string affordMark = canAfford ? "✓" : "✗";

			_lines.Add($"{affordMark} [{i + 1}] {unlock.TechType} ({unlock.FromLevel} → {unlock.ToLevel})");
			_lines.Add($"    {unlock.Description}");
			_lines.Add($"    Cost: {unlock.ResearchCost.Budget:F0} Budget, {unlock.ResearchCost.Steel:F0} Steel, {unlock.ResearchCost.ExoticMaterials:F0} Exotic");
			_lines.Add("");
		}
	}

	protected override void RenderBody(UiContext ui)
	{
		if (_lines.Count == 0)
			BuildLines(ui);

		int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
		int maxScroll = Math.Max(0, _lines.Count - viewport);
		_scroll = Math.Clamp(_scroll, 0, maxScroll);

		int end = Math.Min(_lines.Count, _scroll + viewport);
		for (int i = _scroll; i < end; i++)
			ui.WriteLine(_lines[i]);
	}

	protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
	{
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResearchMenuPage requires GameState). ");

		const int lineStep = 1;
		const int pageStep = 6;

		switch (key.Key)
		{
			case ConsoleKey.UpArrow: _scroll -= lineStep; return PageResult.Stay;
			case ConsoleKey.DownArrow: _scroll += lineStep; return PageResult.Stay;
			case ConsoleKey.PageUp: _scroll -= pageStep; return PageResult.Stay;
			case ConsoleKey.PageDown: _scroll += pageStep; return PageResult.Stay;
		}

		if (key.Key is ConsoleKey.B)
			return PageResult.Back();

		int? n = key.Key switch
		{
			ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
			ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
			ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
			ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
			ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
			ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
			ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
			ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
			ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
			_ => null
		};

		if (n is null)
			return PageResult.Stay;

		int idx = n.Value - 1;
		if (idx < 0 || idx >= _availableTechs.Count)
			return PageResult.Stay;

		var unlock = _availableTechs[idx];
		if (!TechUnlock.CanAffordResearch(unlock, game.AccumulatedResources))
		{
			_message = "✗ Cannot afford this research.";
			BuildLines(ui);
			return PageResult.Stay;
		}

		if (game.ResearchTech(unlock))
		{
			_message = $"✓ Tech research complete: {unlock.TechType} → Level {unlock.ToLevel}";
			ui.FlashMessage = _message;
			_availableTechs = TechUnlock.GetAvailableUnlocks(game.TechTree);
		}
		else
		{
			_message = "✗ Research failed.";
		}

		BuildLines(ui);
		return PageResult.Stay;
	}
}
