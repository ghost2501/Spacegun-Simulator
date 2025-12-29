using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.Resources;

public sealed class ResourceOptionsPage : PageBase
{
	public override string Id => PageId.ResourceOptions;
	public override string Title => "RESOURCE ALLOCATION";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Digits+Enter=Input  U=Undo  B=Back   Esc=Menu  Q=Quit   ↑/↓/PgUp/PgDn=Scroll"
	);

	private readonly List<string> _lines = new();
	private int _scroll;
	private string _message = "";

	private readonly Dictionary<ResourceType, double> _effectiveRates = new();

	private readonly string[] _resourceNames =
	{
		"Steel",
		"Budget",
		"Specialized Alloys",
		"Rare Earth Elements",
		"Power Cells",
		"Exotic Materials"
	};

	// Keys used in GameState.AccumulatedResources
	private readonly string[] _resourceKeys =
	{
		"Steel",
		"Budget",
		"SpecializedAlloys",
		"RareEarthElements",
		"PowerCells",
		"Exotic"
	};

	private readonly ResourceType[] _resourceTypes =
	{
		ResourceType.Steel,
		ResourceType.Budget,
		ResourceType.SpecializedAlloys,
		ResourceType.RareEarthElements,
		ResourceType.PowerCells,
		ResourceType.ExoticMaterials
	};

	private long[] _yearsAllocated = new long[6];
	private double[] _productionRates = new double[6];
	private int _allocationStep;
	private string _inputBuffer = "";

	public override void OnEnter(UiContext ui)
	{
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResourceOptionsPage requires GameState).");

		_scroll = 0;
		_message = "";

		RecomputeEffectiveRates(game);
		ResetAllocationState();
		BuildLines(ui);
	}

	private void RecomputeEffectiveRates(GameState game)
	{
		_effectiveRates.Clear();
		double eventMultiplier = game.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;

		foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
		{
			double rate = ResourceGathering.GetEffectiveProductionRate(resource, game.TechTree, game.SelectedDifficulty, eventMultiplier);
			if (rate > 0)
				_effectiveRates[resource] = rate;
		}
	}

	private void ResetAllocationState()
	{
		_yearsAllocated = new long[6];
		_productionRates = new double[6];
		for (int i = 0; i < _resourceTypes.Length; i++)	
		{
			var t = _resourceTypes[i];
			_productionRates[i] = _effectiveRates.TryGetValue(t, out var v) ? v : 0;
		}

		_allocationStep = 0;
		_inputBuffer = "";
	}

	private void BuildLines(UiContext ui)
	{
		_lines.Clear();

		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResourceOptionsPage requires GameState).");

		_lines.Add("=== RESOURCE ALLOCATION ===");
		_lines.Add("Enter years for each resource (digits + Enter). [U]=Undo.");
		_lines.Add("");

		_lines.Add($"Time Remaining: {(long)game.RemainingYears} years");
		_lines.Add("");

		if (!string.IsNullOrWhiteSpace(_message))
		{
			_lines.Add(_message);
			_lines.Add("");
		}

		for (int i = 0; i < _resourceNames.Length; i++)
		{
			var rate = _productionRates[i];
			var years = _yearsAllocated[i];
			string locked = rate <= 0 ? " (LOCKED)" : "";
			_lines.Add($"{i + 1}/6 - {_resourceNames[i]}{locked}: {years} years");
		}

		_lines.Add("");

		// Skip locked resources automatically
		while (_allocationStep < 6 && _productionRates[_allocationStep] <= 0)
			_allocationStep++;

		if (_allocationStep >= 6)
		{
			_lines.Add("Allocation complete.");
			_lines.Add("Press [Enter] to apply. B returns to the hub.");
			return;
		}

		_lines.Add($"{_allocationStep + 1}/6 - Years for {_resourceNames[_allocationStep]}: {_inputBuffer}_");
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
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ResourceOptionsPage requires GameState).");

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

		// Skip locked resources automatically
		while (_allocationStep < 6 && _productionRates[_allocationStep] <= 0)
			_allocationStep++;

		if (_allocationStep >= 6)
		{
			if (key.Key == ConsoleKey.Enter)
			{
				ApplyAllocation(game);
				ui.FlashMessage = "✓ Resource allocation applied.";
				return PageResult.Back();
			}

			return PageResult.Stay;
		}

		if (key.Key == ConsoleKey.U)
		{
			int prev = _allocationStep - 1;
			while (prev >= 0 && _productionRates[prev] <= 0)
				prev--;

			if (prev >= 0)
			{
				game.RemainingYears += _yearsAllocated[prev];
				_yearsAllocated[prev] = 0;
				_allocationStep = prev;
				_inputBuffer = "";
				_message = "";
			}

			BuildLines(ui);
			return PageResult.Stay;
		}

		if (key.Key == ConsoleKey.Backspace)
		{
			if (_inputBuffer.Length > 0)
				_inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);

			BuildLines(ui);
			return PageResult.Stay;
		}

		char ch = key.KeyChar;
		if (ch >= '0' && ch <= '9')
		{
			if (_inputBuffer.Length < 9)
				_inputBuffer += ch;

			BuildLines(ui);
			return PageResult.Stay;
		}

		if (key.Key == ConsoleKey.Enter)
		{
			long years = 0;
			if (!string.IsNullOrWhiteSpace(_inputBuffer))
				long.TryParse(_inputBuffer, out years);

			if (years < 0) years = 0;
			if (years > game.RemainingYears)
			{
				_message = $"✗ Cannot allocate {years} years. Only {game.RemainingYears} remaining.";
				_inputBuffer = "";
				BuildLines(ui);
				return PageResult.Stay;
			}

			game.RemainingYears -= years;
			_yearsAllocated[_allocationStep] = years;
			_inputBuffer = "";
			_message = "";

			_allocationStep++;
			BuildLines(ui);
			return PageResult.Stay;
		}

		return PageResult.Stay;
	}

	private void ApplyAllocation(GameState game)
	{
		for (int i = 0; i < _resourceTypes.Length; i++)
		{
			var rate = _productionRates[i];
			if (rate <= 0) continue;

			long years = _yearsAllocated[i];
			if (years <= 0) continue;

			double gathered = years * rate;
			string key = _resourceKeys[i];
			if (!game.AccumulatedResources.ContainsKey(key))
				game.AccumulatedResources[key] = 0;

			game.AccumulatedResources[key] += gathered;
		}
	}
}
