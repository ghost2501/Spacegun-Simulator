using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.Resources;

public sealed class ResourceAllocationPage : PageBase
{
	public override string Id => PageId.ResourceAllocation;
	public override string Title => "RESOURCES & RESEARCH";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "R=Resources  T=Research  S=Status  D=Done   Esc=Back/Menu  Q=Quit   ↑/↓/PgUp/PgDn=Scroll"
	);

	private readonly List<string> _lines = new();
	private int _scroll;
	private bool _initialized;
	private readonly Dictionary<ResourceType, double> _effectiveRates = new();

	public override void OnEnter(UiContext ui)
	{
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResourceAllocationPage requires GameState).");

		// Mirror legacy behavior: wave event is generated once at the start of this phase.
		if (!_initialized)
		{
			game.GenerateWaveEvent();
			_initialized = true;
		}

		RecomputeEffectiveRates(game);

		_scroll = 0;
		BuildLines(ui);
	}

	private void RecomputeEffectiveRates(GameState game)
	{
		_effectiveRates.Clear();
		double eventMultiplier = game.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;

		foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
		{
			double rate = ResourceGathering.GetEffectiveProductionRate(
				resource,
				game.TechTree,
				game.SelectedDifficulty,
				eventMultiplier);

			if (rate > 0)
				_effectiveRates[resource] = rate;
		}
	}

	private void BuildLines(UiContext ui)
	{
		_lines.Clear();

		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResourceAllocationPage requires GameState).");

		// One-shot message from sub-pages.
		if (!string.IsNullOrWhiteSpace(ui.FlashMessage))
		{
			_lines.Add(ui.FlashMessage!);
			_lines.Add("");
			ui.FlashMessage = null;
		}

		if (game.CurrentWaveEvent != null)
		{
			_lines.Add("=== RANDOM EVENT ===");
			_lines.Add($"⚡ {game.CurrentWaveEvent.Title}");
			_lines.Add($"   {game.CurrentWaveEvent.Description}");

			if (Math.Abs(game.CurrentWaveEvent.ProductionMultiplier - 1.0) > 0.0001)
			{
				string modifier = game.CurrentWaveEvent.ProductionMultiplier > 1.0 ? "+" : "";
				_lines.Add($"   Production: {modifier}{(game.CurrentWaveEvent.ProductionMultiplier - 1) * 100:F0}%");
			}

			_lines.Add("");
		}

		_lines.Add($"Total Available Time: {(long)game.AvailableYears} years");
		_lines.Add($"Time Remaining: {(long)game.RemainingYears} years");
		_lines.Add("");

		_lines.Add("=== RESOURCE PRODUCTION RATES (per year, with tech & difficulty) ===");
		_lines.Add("");

		_lines.Add("Base Materials:");
		if (_effectiveRates.TryGetValue(ResourceType.Steel, out var steel))
			_lines.Add($"  Steel:                  {steel:F0} tons/year");
		else
			_lines.Add("  Steel:                  [LOCKED]");

		if (_effectiveRates.TryGetValue(ResourceType.Budget, out var budget))
			_lines.Add($"  Budget:                 {budget:F0} currency/year");
		else
			_lines.Add("  Budget:                 [LOCKED]");

		_lines.Add("");
		_lines.Add("Tier 2 Resources (Mining II+):");
		if (_effectiveRates.TryGetValue(ResourceType.SpecializedAlloys, out var alloys))
			_lines.Add($"  Specialized Alloys:     {alloys:F0} tons/year");
		else
			_lines.Add("  Specialized Alloys:     [LOCKED]");

		if (_effectiveRates.TryGetValue(ResourceType.RareEarthElements, out var rare))
			_lines.Add($"  Rare Earth Elements:    {rare:F0} units/year");
		else
			_lines.Add("  Rare Earth Elements:    [LOCKED]");

		_lines.Add("");
		_lines.Add("Tier 3 Resources (Mining III+):");
		if (_effectiveRates.TryGetValue(ResourceType.AdvancedOre, out var adv))
			_lines.Add($"  Advanced Ore:           {adv:F0} units/year");
		else
			_lines.Add("  Advanced Ore:           [LOCKED]");

		if (_effectiveRates.TryGetValue(ResourceType.ExoticMaterials, out var exotic))
			_lines.Add($"  Exotic Materials:       {exotic:F0} units/year");
		else
			_lines.Add("  Exotic Materials:       [LOCKED]");

		_lines.Add("");
		_lines.Add("Other Systems:");
		if (_effectiveRates.TryGetValue(ResourceType.PowerCells, out var power))
			_lines.Add($"  Power Cells:            {power:F0} units/year");
		else
			_lines.Add("  Power Cells:            [LOCKED]");

		_lines.Add("");
	}

	protected override void RenderBody(UiContext ui)
	{
		if (_lines.Count == 0)
			BuildLines(ui);

		int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;

		int maxScroll = Math.Max(0, _lines.Count - viewport);
		if (_scroll < 0) _scroll = 0;
		if (_scroll > maxScroll) _scroll = maxScroll;

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


		switch (key.Key)
		{
			case ConsoleKey.R:
				return PageResult.Go(PageId.ResourceOptions);
			case ConsoleKey.T:
				return PageResult.Go(PageId.ResearchMenu);
			case ConsoleKey.S:
				return PageResult.Go(PageId.PreparationStatus);
			case ConsoleKey.D:
				return PageResult.Go(PageId.PreparationSummary);
		}

		return PageResult.Stay;
	}
}
