using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Technology;
using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class DetailedWeaponStatusPage : PageBase
{
	public override string Id => PageId.DetailedWeaponStatus;
	public override string Title => "DETAILED WEAPON STATUS";

	public override PageChrome Chrome { get; } = new(
		ShowStatusBar: true,
		ShowSidePanels: true,
		FooterHint: "Arrows=Scroll  Any key=Back  Esc=Menu  Q=Quit"
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
		var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DetailedWeaponStatusPage requires GameState). ");

		// Tech Levels
		_lines.Add("=== TECHNOLOGY LEVELS ===");
		_lines.Add(Clamp60($"  Weapons:     Level {game.TechTree.CurrentLevel[TechTree.TechType.Weapons]}"));
		_lines.Add(Clamp60($"               {TechTree.GetTechDescription(TechTree.TechType.Weapons, game.TechTree.CurrentLevel[TechTree.TechType.Weapons])}"));
		_lines.Add(Clamp60($"  Projectiles: Level {game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}"));
		_lines.Add(Clamp60($"               {TechTree.GetTechDescription(TechTree.TechType.Projectiles, game.TechTree.CurrentLevel[TechTree.TechType.Projectiles])}"));
		_lines.Add("");

		// Gun Base Velocity
		int weaponsTechLevel = game.TechTree.CurrentLevel[TechTree.TechType.Weapons];
		double gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);
		_lines.Add("=== GUN BASE VELOCITY ===");
		_lines.Add(Clamp60($"  Base Muzzle Velocity: {gunBaseVelocity:N0} m/s ({gunBaseVelocity / 1000:N0} km/s)"));
		_lines.Add("");

		// Unlocked Components
		_lines.Add("=== UNLOCKED COMPONENTS ===");
		var cores = CraftedProjectile.GetUnlockedCores(game.TechTree);
		_lines.Add(Clamp60($"  Cores ({cores.Count} available):"));
		foreach (var core in cores)
			_lines.Add(Clamp60($"    - {core.Name} ({core.MassKg} kg)"));
		_lines.Add("");

		var propulsion = CraftedProjectile.GetUnlockedPropulsion(game.TechTree);
		_lines.Add(Clamp60($"  Propulsion ({propulsion.Count} available):"));
		foreach (var prop in propulsion)
		{
			if (prop.Id == "none")
				_lines.Add(Clamp60($"    - {prop.Name} (no boost)"));
			else
				_lines.Add(Clamp60($"    - {prop.Name} (+{prop.DeltaVCapacityMs / 1000:N0} km/s Δv over {prop.BurnDurationSeconds:F1}s)"));
		}
		_lines.Add("");

		var enhancements = CraftedProjectile.GetUnlockedEnhancements(game.TechTree);
		_lines.Add(Clamp60($"  Enhancements ({enhancements.Count} available):"));
		foreach (var enh in enhancements)
			_lines.Add(Clamp60($"    - {enh.Name}"));
		_lines.Add("");

		// Gun Status
		_lines.Add("=== GUN CONFIGURATION ===");
		_lines.Add(Clamp60($"  Barrel Integrity: {game.Gun.BarrelIntegrity:P0}"));
		_lines.Add(Clamp60($"  Power Capacity: {game.Gun.PowerCapacity:F0} MW"));
		_lines.Add(Clamp60($"  Weapons Tech Level: {weaponsTechLevel}"));
		_lines.Add("");

		// Current Projectile
		_lines.Add("=== CURRENT PROJECTILE ===");
		if (game.CraftedProjectile != null)
		{
			var proj = game.CraftedProjectile;
			_lines.Add(Clamp60($"  Configuration: {proj.DisplayName}"));
			_lines.Add(Clamp60($"  Mass: {proj.MassKg} kg"));
			_lines.Add(Clamp60($"  Gun Base Velocity: {proj.GunBaseMuzzleVelocityMs:N0} m/s"));

			if (proj.Propulsion.Id != "none")
			{
				double maxDeltaV = proj.Propulsion.CalculateEffectiveDeltaV(proj.MassKg, proj.Propulsion.BurnDurationSeconds);
				_lines.Add(Clamp60($"  Propulsion Δv: +{maxDeltaV:N0} m/s"));
				_lines.Add(Clamp60($"  Max Velocity: {proj.MaxVelocityMs:N0} m/s"));
			}

			_lines.Add(Clamp60($"  Max KE: {proj.RawKineticEnergyMJ:N0} MJ"));
			_lines.Add(Clamp60($"  Effective KE: {proj.EffectiveKineticEnergyMJ:N0} MJ"));
			if (proj.HitToleranceMultiplier != 1.0)
				_lines.Add(Clamp60($"  Hit Tolerance: {(proj.HitToleranceMultiplier - 1) * 100:+0}%"));
		}
		else
		{
			_lines.Add("  [NOT CONFIGURED]");
		}
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
			ui.WriteLine(Clamp60(_lines[i]));
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

		// Any other key returns (Back).
		return PageResult.Back(PageId.WeaponDevelopment);
	}
}
