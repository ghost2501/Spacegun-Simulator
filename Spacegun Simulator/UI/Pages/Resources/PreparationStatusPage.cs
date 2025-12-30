using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Technology;

namespace Spacegun_Simulator.UI.Pages.Resources;

public sealed class PreparationStatusPage : PageBase
{
	public override string Id => PageId.PreparationStatus;
	public override string Title => "PREPARATION STATUS";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "(B)ack (M)enu (Q)uit"
	);

	private readonly List<string> _lines = new();
	private int _scroll;

	public override void OnEnter(UiContext ui)
	{
		_scroll = 0;
		BuildLines(ui);
	}

	private void BuildLines(UiContext ui)
	{
		_lines.Clear();
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (PreparationStatusPage requires GameState). ");

		_lines.Add("=== ACCUMULATED RESOURCES ===");
		_lines.Add($"Steel:                {game.AccumulatedResources["Steel"]:F0} tons");
		_lines.Add($"Budget:               {game.AccumulatedResources["Budget"]:F0} currency");
		_lines.Add($"Specialized Alloys:   {game.AccumulatedResources["SpecializedAlloys"]:F0} tons");
		_lines.Add($"Rare Earth Elements:  {game.AccumulatedResources["RareEarthElements"]:F0} units");
		_lines.Add($"Power Cells:          {game.AccumulatedResources["PowerCells"]:F0} units");
		_lines.Add($"Exotic Materials:     {game.AccumulatedResources["Exotic"]:F1} units");
		_lines.Add("");

		_lines.Add("=== TIME ===");
		_lines.Add($"Years Remaining: {game.RemainingYears} / {game.AvailableYears}");
		_lines.Add("");

		_lines.Add("=== TECH TREE ===");
		_lines.Add($"Radar:       Level {game.TechTree.CurrentLevel[TechTree.TechType.Radar]}");
		_lines.Add($"Mining:      Level {game.TechTree.CurrentLevel[TechTree.TechType.Mining]}");
		_lines.Add($"Production:  Level {game.TechTree.CurrentLevel[TechTree.TechType.Production]}");
		_lines.Add($"Weapons:     Level {game.TechTree.CurrentLevel[TechTree.TechType.Weapons]}");
		_lines.Add($"Projectiles: Level {game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}");
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

		return PageResult.Stay;
	}
}
