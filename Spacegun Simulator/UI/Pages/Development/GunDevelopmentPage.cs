using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Development.Technology;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class GunDevelopmentPage : PageBase
{
    public override string Id => PageId.GunDevelopment;
    public override string Title => "GUN DEVELOPMENT";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
		FooterHint: "Select(↩)   (B)ack (M)enu (Q)uit"
    );

    private sealed record UpgradeOption(
        string Name,
        string Description,
        ResourceCost Cost,
        Action<GameState> Apply
    );

    private enum Mode
    {
        List,
        Confirm,
        Result
    }

    private readonly List<UpgradeOption> _upgrades = new();
    private readonly List<string> _lines = new();
    private readonly List<(int Start, int EndExclusive)> _upgradeLineRanges = new();
    private int _scroll;
    private int _selectedIndex;
    private Mode _mode;
    private string _resultMessage = string.Empty;

    public override void OnEnter(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (GunDevelopmentPage requires GameState). ");

        _selectedIndex = 0;
        _scroll = 0;
        _mode = Mode.List;
        _resultMessage = string.Empty;
        BuildUpgrades(game);
    }

    private void BuildUpgrades(GameState game)
    {
        _upgrades.Clear();

        int weaponsTech = game.TechTree.CurrentLevel[TechTree.TechType.Weapons];
        int projectilesTech = game.TechTree.CurrentLevel[TechTree.TechType.Projectiles];
        game.Gun.UpdateBaseMuzzleVelocity(weaponsTech);

        bool hasGuidanceMod =
            (game.CraftedProjectile?.Enhancement?.Id == "guidance")
            || game.Gun.DefaultProjectile.HasGuidance;

        // NOTE (maintenance): Upgrades intentionally target distinct underlying stats.
        // - Chemical propulsion cares about propellant mass/energy density (MJ/KE path is energy-based).
        // - EM launchers (rail/coil/hybrid) care about electrical power capacity.
        // We keep these separate (instead of re-labeling the same stat) so future mods/content can
        // tweak one path without affecting the other.
        // Also note: chemical propellant energy density is material-capped at runtime via
        // GunConfiguration.GetEffectivePropellantEnergyDensity().

        // Mirrors the legacy gun upgrade list.
        _upgrades.Add(new UpgradeOption(
            Name: "Barrel Repair",
            Description: "Restore barrel integrity to 100%",
            Cost: new ResourceCost(budget: 100, steel: 50, exotic: 0),
            Apply: game => game.Gun.BarrelIntegrity = 1.0
        ));

        if (game.Gun.PropulsionSystem == Spacegun_Simulator.Development.PropulsionType.Chemical)
        {
            _upgrades.Add(new UpgradeOption(
                Name: "Propellant Optimization",
                Description: "Increase propellant energy density by 20% (material-capped)",
                Cost: new ResourceCost(budget: 150, steel: 80, exotic: 20),
                Apply: g => g.Gun.PropellantEnergyDensity = Math.Max(0.0, g.Gun.PropellantEnergyDensity * 1.2)
            ));
        }
        else
        {
            _upgrades.Add(new UpgradeOption(
                Name: "Power Capacitor Upgrade",
                Description: "Increase power capacity by 20%",
                Cost: new ResourceCost(budget: 150, steel: 80, exotic: 20),
                Apply: g => g.Gun.PowerCapacity = Math.Max(0.0, g.Gun.PowerCapacity * 1.2)
            ));
        }

        _upgrades.Add(new UpgradeOption(
            Name: "Barrel Extension",
            Description: "Increase barrel length by 10% (improves effective range)",
            Cost: new ResourceCost(budget: 180, steel: 140, exotic: 20),
            Apply: game => game.Gun.BarrelLength = Math.Min(200.0, game.Gun.BarrelLength * 1.10)
        ));

        if (hasGuidanceMod && projectilesTech >= 3)
        {
            _upgrades.Add(new UpgradeOption(
                Name: "Guidance Calibration",
                Description: "Improve guidance to counter enemy maneuverability (+15%)",
                Cost: new ResourceCost(budget: 220, steel: 100, exotic: 40),
                Apply: game => game.Gun.Guidance = Math.Min(3.0, Math.Max(0.1, game.Gun.Guidance) * 1.15)
            ));
        }

        _upgrades.Add(new UpgradeOption(
            Name: "Reinforced Barrel",
            Description: "Reduce barrel degradation per shot by 50%",
            Cost: new ResourceCost(budget: 200, steel: 120, exotic: 40),
            Apply: _ => { /* Future: implement barrel reinforcement tracking */ }
        ));
    }

    protected override void RenderBody(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (GunDevelopmentPage requires GameState). ");

        switch (_mode)
        {
            case Mode.List:
                RenderScrollableList(ui, game);
                return;

            case Mode.Confirm:
                RenderConfirm(ui, game);
                return;

            case Mode.Result:
                RenderResult(ui);
                return;
        }
    }

    private void RenderScrollableList(UiContext ui, GameState game)
    {
        BuildScrollableLines(game);
        EnsureSelectedVisible(ui);

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
        viewport = Math.Max(1, viewport);

        int maxScroll = Math.Max(0, _lines.Count - viewport);
        _scroll = Math.Clamp(_scroll, 0, maxScroll);

        int end = Math.Min(_lines.Count, _scroll + viewport);
        for (int i = _scroll; i < end; i++)
            ui.WriteLine(_lines[i]);
    }

    private void BuildScrollableLines(GameState game)
    {
        _lines.Clear();
        _upgradeLineRanges.Clear();

        _lines.Add("=== AVAILABLE RESOURCES ===");
        _lines.Add($"  Budget: {game.AccumulatedResources["Budget"]:F0}");
        _lines.Add($"  Steel:  {game.AccumulatedResources["Steel"]:F0} tons");
        _lines.Add($"  Exotic: {game.AccumulatedResources["Exotic"]:F1} units");
        _lines.Add(string.Empty);

        int weaponsTech = game.TechTree.CurrentLevel[TechTree.TechType.Weapons];
        game.Gun.UpdateBaseMuzzleVelocity(weaponsTech);

        _lines.Add("=== CURRENT GUN STATUS ===");
        _lines.Add($"  Barrel Integrity: {game.Gun.BarrelIntegrity:P0}");
        _lines.Add($"  Barrel Length: {game.Gun.BarrelLength:F0} m");
        _lines.Add($"  Range Multiplier (barrel): {game.Gun.RangeMultiplierFromBarrelLength:F2}x");
        _lines.Add($"  Propulsion: {game.Gun.PropulsionSystem}");
        if (game.Gun.PropulsionSystem == Spacegun_Simulator.Development.PropulsionType.Chemical)
        {
            _lines.Add($"  Propellant Mass: {game.Gun.PropellantMass:F0} kg");
            _lines.Add($"  Propellant Energy Density: {game.Gun.GetEffectivePropellantEnergyDensity():F2} GJ/kg");
        }
        else
        {
            _lines.Add($"  Power Capacity: {game.Gun.PowerCapacity:F0} MW");
        }
        _lines.Add($"  Guidance: {game.Gun.Guidance:F2}x");
        _lines.Add($"  Weapons Tech Level: {weaponsTech}");
        _lines.Add(string.Empty);

        _lines.Add("=== AVAILABLE UPGRADES ===");
        _lines.Add(string.Empty);

        if (_upgrades.Count == 0)
        {
            _lines.Add("[No upgrades available]");
            _lines.Add(string.Empty);
            _lines.Add("[B] Back");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _upgrades.Count - 1);

        for (int i = 0; i < _upgrades.Count; i++)
        {
            var up = _upgrades[i];
            bool canAfford = CanAfford(game, up.Cost);
            string affordMark = canAfford ? "✓" : "✗";
            string cursor = i == _selectedIndex ? ">" : " ";

            int start = _lines.Count;
            _lines.Add($"{cursor} [{i + 1}] {affordMark} {up.Name}");
            _lines.Add($"    {up.Description}");
            _lines.Add($"    Cost: {up.Cost.Budget:F0} Budget, {up.Cost.Steel:F0} Steel, {up.Cost.ExoticMaterials:F0} Exotic");
            _lines.Add(string.Empty);
            int endExclusive = _lines.Count;

            _upgradeLineRanges.Add((start, endExclusive));
        }

        _lines.Add("[B] Back");
    }

    private void EnsureSelectedVisible(UiContext ui)
    {
        if (_upgrades.Count == 0) return;
        _selectedIndex = Math.Clamp(_selectedIndex, 0, _upgrades.Count - 1);
        if (_selectedIndex < 0 || _selectedIndex >= _upgradeLineRanges.Count) return;

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
        viewport = Math.Max(1, viewport);

        var (startLine, endLineExclusive) = _upgradeLineRanges[_selectedIndex];
        int viewTop = _scroll;
        int viewBottomExclusive = _scroll + viewport;

        if (startLine < viewTop)
        {
            _scroll = startLine;
        }
        else if (endLineExclusive > viewBottomExclusive)
        {
            _scroll = Math.Max(0, endLineExclusive - viewport);
        }
    }

    private void RenderConfirm(UiContext ui, GameState game)
    {
        var up = _upgrades[Math.Clamp(_selectedIndex, 0, Math.Max(0, _upgrades.Count - 1))];

        ui.WriteLine("=== CONFIRM UPGRADE ===");
        ui.WriteLine();
        ui.WriteLine($"Apply: {up.Name}");
        ui.WriteLine($"  {up.Description}");
        ui.WriteLine($"Cost: {up.Cost.Budget:F0} Budget, {up.Cost.Steel:F0} Steel, {up.Cost.ExoticMaterials:F0} Exotic");
        ui.WriteLine();

        if (!CanAfford(game, up.Cost))
        {
            ui.WriteLine("✗ Cannot afford this upgrade.");
            ui.WriteLine();
            ui.WriteLine("Press any key to continue...");
            return;
        }

        ui.WriteLine("Apply upgrade? (Y/N)");
    }

    private void RenderResult(UiContext ui)
    {
        ui.WriteLine("=== RESULT ===");
        ui.WriteLine();
        ui.WriteLine(_resultMessage);
        ui.WriteLine();
        ui.WriteLine("Press any key to continue...");
    }

    protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (GunDevelopmentPage requires GameState). ");

        if (key.Key == ConsoleKey.B)
            return PageResult.Back(PageId.WeaponDevelopment);

        // Numeric quick-select (1..9)
        if (_mode == Mode.List && char.IsDigit(key.KeyChar))
        {
            int n = key.KeyChar - '0';
            if (n >= 1 && n <= _upgrades.Count)
            {
                _selectedIndex = n - 1;
                _mode = Mode.Confirm;
                return PageResult.Stay;
            }
        }

        switch (_mode)
        {
            case Mode.List:
                return HandleListInput(game, key);

            case Mode.Confirm:
                return HandleConfirmInput(game, key);

            case Mode.Result:
                _mode = Mode.List;
                return PageResult.Stay;

            default:
                return PageResult.Stay;
        }
    }

    private PageResult HandleListInput(GameState game, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                return PageResult.Stay;

            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(Math.Max(0, _upgrades.Count - 1), _selectedIndex + 1);
                return PageResult.Stay;

            case ConsoleKey.Enter:
                if (_upgrades.Count > 0)
                    _mode = Mode.Confirm;
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private PageResult HandleConfirmInput(GameState game, ConsoleKeyInfo key)
    {
        if (_upgrades.Count == 0)
        {
            _mode = Mode.List;
            return PageResult.Stay;
        }

        var up = _upgrades[Math.Clamp(_selectedIndex, 0, _upgrades.Count - 1)];

        // If unaffordable, any key returns to list.
        if (!CanAfford(game, up.Cost))
        {
            _mode = Mode.List;
            return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.Y)
        {
            // Deduct resources and apply upgrade.
            game.AccumulatedResources["Budget"] -= up.Cost.Budget;
            game.AccumulatedResources["Steel"] -= up.Cost.Steel;
            game.AccumulatedResources["Exotic"] -= up.Cost.ExoticMaterials;

            up.Apply(game);

            _resultMessage = $"✓ {up.Name} applied successfully!";
            _mode = Mode.Result;
            return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.N)
        {
            _resultMessage = "Upgrade cancelled.";
            _mode = Mode.Result;
            return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private static bool CanAfford(GameState game, ResourceCost cost)
    {
        return game.AccumulatedResources["Budget"] >= cost.Budget
            && game.AccumulatedResources["Steel"] >= cost.Steel
            && game.AccumulatedResources["Exotic"] >= cost.ExoticMaterials;
    }
}
