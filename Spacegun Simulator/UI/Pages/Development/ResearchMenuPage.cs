using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Development.Technology;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class ResearchMenuPage : PageBase
{
	public override string Id => PageId.ResearchMenu;
	public override string Title => "RESEARCH";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Select(↩)  (B)ack (M)enu (Q)uit"
	);

	private readonly List<string> _lines = new();
	private readonly List<(int StartLine, int EndLineExclusive)> _techLineRanges = new();
	private int _scroll;
	private int _selectedIndex;
	private string _message = "";
	private List<TechUnlock> _availableTechs = new();

	public override void OnEnter(UiContext ui)
	{
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResearchMenuPage requires GameState). ");
		_scroll = 0;
		_selectedIndex = 0;
		_message = "";
		_availableTechs = TechUnlock.GetAvailableUnlocks(game.TechTree);
		BuildLines(ui);
	}

	private void BuildLines(UiContext ui)
	{
		_lines.Clear();
		_techLineRanges.Clear();
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
			_selectedIndex = 0;
			return;
		}

		_selectedIndex = Math.Clamp(_selectedIndex, 0, _availableTechs.Count - 1);

		for (int i = 0; i < _availableTechs.Count; i++)
		{
			var unlock = _availableTechs[i];
			bool canAfford = TechUnlock.CanAffordResearch(unlock, game.AccumulatedResources);
			string affordMark = canAfford ? "✓" : "✗";
			string selectMark = i == _selectedIndex ? ">" : " ";
			int startLine = _lines.Count;

			_lines.Add($"{affordMark} {selectMark} [{i + 1}] {unlock.TechType} ({unlock.FromLevel} → {unlock.ToLevel})");
			_lines.Add($"    {unlock.Description}");
			_lines.Add($"    Cost: {ResourceCostLedger.FormatCost(unlock.ResearchCost)}");
			_lines.Add("");

			int endLineExclusive = _lines.Count;
			_techLineRanges.Add((startLine, endLineExclusive));
		}

		EnsureSelectedVisible(ui);
	}

	private void EnsureSelectedVisible(UiContext ui)
	{
		if (_availableTechs.Count == 0) return;
		if (_selectedIndex < 0 || _selectedIndex >= _availableTechs.Count) return;
		if (_techLineRanges.Count != _availableTechs.Count) return;

		int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
		viewport = Math.Max(1, viewport);

		var (startLine, endLineExclusive) = _techLineRanges[_selectedIndex];
		int viewTop = _scroll;
		int viewBottomExclusive = _scroll + viewport;

		if (startLine < viewTop)
		{
			_scroll = startLine;
			return;
		}

		if (endLineExclusive > viewBottomExclusive)
			_scroll = Math.Max(0, endLineExclusive - viewport);
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

		const int pageStep = 6;

		switch (key.Key)
		{
			case ConsoleKey.UpArrow:
				if (_availableTechs.Count > 0)
				{
					_selectedIndex = Math.Clamp(_selectedIndex - 1, 0, _availableTechs.Count - 1);
					BuildLines(ui);
				}
				return PageResult.Stay;

			case ConsoleKey.DownArrow:
				if (_availableTechs.Count > 0)
				{
					_selectedIndex = Math.Clamp(_selectedIndex + 1, 0, _availableTechs.Count - 1);
					BuildLines(ui);
				}
				return PageResult.Stay;

			case ConsoleKey.PageUp:
				if (_availableTechs.Count > 0)
				{
					_selectedIndex = Math.Clamp(_selectedIndex - pageStep, 0, _availableTechs.Count - 1);
					BuildLines(ui);
				}
				return PageResult.Stay;

			case ConsoleKey.PageDown:
				if (_availableTechs.Count > 0)
				{
					_selectedIndex = Math.Clamp(_selectedIndex + pageStep, 0, _availableTechs.Count - 1);
					BuildLines(ui);
				}
				return PageResult.Stay;
		}

		if (key.Key is ConsoleKey.B)
			return PageResult.Back();

		if (key.Key is not ConsoleKey.Enter)
			return PageResult.Stay;

		if (_availableTechs.Count == 0)
			return PageResult.Stay;

		_selectedIndex = Math.Clamp(_selectedIndex, 0, _availableTechs.Count - 1);
		var unlock = _availableTechs[_selectedIndex];
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
			_selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _availableTechs.Count - 1));
		}
		else
		{
			_message = "✗ Research failed.";
		}

		BuildLines(ui);
		return PageResult.Stay;
	}
}
