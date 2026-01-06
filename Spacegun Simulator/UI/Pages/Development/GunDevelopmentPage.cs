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

        var defs = WeaponsUpgrades.Definitions;
        if (defs is null || defs.Count == 0)
            return;

        bool isChemical = game.Gun.PropulsionSystem == Spacegun_Simulator.Development.PropulsionType.Chemical;

        foreach (var def in defs)
        {
            if (def.MinWeaponsTechLevel.HasValue && weaponsTech < def.MinWeaponsTechLevel.Value)
                continue;
            if (def.MinProjectilesTechLevel.HasValue && projectilesTech < def.MinProjectilesTechLevel.Value)
                continue;
            if (def.RequiresGuidanceMod && !hasGuidanceMod)
                continue;
            if (!string.IsNullOrWhiteSpace(def.RequiresPropulsion))
            {
                if (string.Equals(def.RequiresPropulsion, "Chemical", StringComparison.OrdinalIgnoreCase) && !isChemical)
                    continue;
                if (string.Equals(def.RequiresPropulsion, "NonChemical", StringComparison.OrdinalIgnoreCase) && isChemical)
                    continue;
            }

            _upgrades.Add(new UpgradeOption(
                Name: def.Name,
                Description: def.Description,
                Cost: def.Cost,
                Apply: g => ApplyUpgradeDefinition(def, g)
            ));
        }
    }

    private static void ApplyUpgradeDefinition(WeaponsUpgrades.UpgradeDefinition def, GameState game)
    {
        if (def is null) return;

        // Keep upgrade math identical to the legacy implementation, but source all numbers from JSON.
        switch (def.Id)
        {
            case "BarrelRepair":
                {
                    double setTo = def.Parameters.TryGetValue("SetIntegrityTo", out double v) ? v : 1.0;
                    game.Gun.BarrelIntegrity = setTo;
                    break;
                }

            case "PropellantOptimization":
                {
                    double mult = def.Parameters.TryGetValue("Multiplier", out double v) ? v : 1.0;
                    game.Gun.PropellantEnergyDensity = Math.Max(0.0, game.Gun.PropellantEnergyDensity * mult);
                    break;
                }

            case "PowerCapacitorUpgrade":
                {
                    double mult = def.Parameters.TryGetValue("Multiplier", out double v) ? v : 1.0;
                    game.Gun.PowerCapacity = Math.Max(0.0, game.Gun.PowerCapacity * mult);
                    break;
                }

            case "BarrelExtension":
                {
                    double mult = def.Parameters.TryGetValue("Multiplier", out double v) ? v : 1.0;
                    double max = def.Parameters.TryGetValue("Max", out double m) ? m : double.PositiveInfinity;
                    game.Gun.BarrelLength = Math.Min(max, game.Gun.BarrelLength * mult);
                    break;
                }

            case "GuidanceCalibration":
                {
                    double mult = def.Parameters.TryGetValue("Multiplier", out double v) ? v : 1.0;
                    double minInput = def.Parameters.TryGetValue("MinInput", out double mi) ? mi : 0.1;
                    double max = def.Parameters.TryGetValue("Max", out double mx) ? mx : double.PositiveInfinity;
                    game.Gun.Guidance = Math.Min(max, Math.Max(minInput, game.Gun.Guidance) * mult);
                    break;
                }

            default:
                // Unknown IDs are no-ops (future expansion).
                break;
        }
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
        _lines.Add($"  Guidance Quality (x): {game.Gun.Guidance:F2}x");
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
