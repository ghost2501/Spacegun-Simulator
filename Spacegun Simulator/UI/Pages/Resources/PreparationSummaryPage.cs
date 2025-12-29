using Spacegun_Simulator.UI.Theme;

namespace Spacegun_Simulator.UI.Pages.Resources;

public sealed class PreparationSummaryPage : PageBase
{
	public override string Id => PageId.PreparationSummary;
	public override string Title => "RESOURCES - SUMMARY";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Any key=Proceed  B=Back  Esc=Menu  Q=Quit   ↑/↓/PgUp/PgDn=Scroll"
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
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (PreparationSummaryPage requires GameState). ");

		_lines.Add("Accumulated Resources:");
		_lines.Add("  Base Materials:");
		_lines.Add($"    Steel:                {game.AccumulatedResources["Steel"]:F0} tons");
		_lines.Add($"    Budget:               {game.AccumulatedResources["Budget"]:F0} currency");
		_lines.Add("  Specialized Resources:");
		_lines.Add($"    Specialized Alloys:   {game.AccumulatedResources["SpecializedAlloys"]:F0} tons");
		_lines.Add($"    Rare Earth Elements:  {game.AccumulatedResources["RareEarthElements"]:F0} units");
		_lines.Add("  Advanced Systems:");
		_lines.Add($"    Power Cells:          {game.AccumulatedResources["PowerCells"]:F0} units");
		_lines.Add($"    Exotic Materials:     {game.AccumulatedResources["Exotic"]:F1} units");
		_lines.Add("");
		_lines.Add($"Time Remaining: {(long)game.RemainingYears} years");
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
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (PreparationSummaryPage requires GameState). ");

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

		// Proceed on any non-scroll/non-back key.
		// Phase advancement and autosave are handled by the phase router.
		return PageResult.Exit;
	}
}
