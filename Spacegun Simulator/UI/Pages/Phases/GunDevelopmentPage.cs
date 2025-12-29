using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

public sealed class GunDevelopmentPage : PageBase
{
    public override string Id => PageId.GunDevelopment;
    public override string Title => "GUN DEVELOPMENT";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "Arrows=Select  Enter=Apply  B=Back  Esc=Menu  Q=Quit"
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
    private int _selectedIndex;
    private Mode _mode;
    private string _resultMessage = string.Empty;

    public override void OnEnter(UiContext ui)
    {
        _selectedIndex = 0;
        _mode = Mode.List;
        _resultMessage = string.Empty;
        BuildUpgrades();
    }

    private void BuildUpgrades()
    {
        _upgrades.Clear();

        // Mirrors legacy ConsoleUI.RunGunDevelopment() upgrades.
        _upgrades.Add(new UpgradeOption(
            Name: "Barrel Repair",
            Description: "Restore barrel integrity to 100%",
            Cost: new ResourceCost(budget: 100, steel: 50, exotic: 0),
            Apply: game => game.Gun.BarrelIntegrity = 1.0
        ));

        _upgrades.Add(new UpgradeOption(
            Name: "Power Capacitor Upgrade",
            Description: "Increase power capacity by 20%",
            Cost: new ResourceCost(budget: 150, steel: 80, exotic: 20),
            Apply: game => game.Gun.PowerCapacity *= 1.2
        ));

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

        ui.WriteLine("=== AVAILABLE RESOURCES ===");
        ui.WriteLine($"  Budget: {game.AccumulatedResources["Budget"]:F0}");
        ui.WriteLine($"  Steel:  {game.AccumulatedResources["Steel"]:F0} tons");
        ui.WriteLine($"  Exotic: {game.AccumulatedResources["Exotic"]:F1} units");
        ui.WriteLine();

        ui.WriteLine("=== CURRENT GUN STATUS ===");
        ui.WriteLine($"  Barrel Integrity: {game.Gun.BarrelIntegrity:P0}");
        ui.WriteLine($"  Power Capacity: {game.Gun.PowerCapacity:F0} MW");
        ui.WriteLine($"  Weapons Tech Level: {game.TechTree.CurrentLevel[TechTree.TechType.Weapons]}");
        ui.WriteLine();

        switch (_mode)
        {
            case Mode.List:
                RenderList(ui, game);
                return;

            case Mode.Confirm:
                RenderConfirm(ui, game);
                return;

            case Mode.Result:
                RenderResult(ui);
                return;
        }
    }

    private void RenderList(UiContext ui, GameState game)
    {
        ui.WriteLine("=== AVAILABLE UPGRADES ===");
        ui.WriteLine();

        if (_upgrades.Count == 0)
        {
            ui.WriteLine("[No upgrades available]");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _upgrades.Count - 1);

        for (int i = 0; i < _upgrades.Count; i++)
        {
            var up = _upgrades[i];
            bool canAfford = CanAfford(game, up.Cost);
            string affordMark = canAfford ? "✓" : "✗";
            string cursor = i == _selectedIndex ? ">" : " ";

            ui.WriteLine($"{cursor} [{i + 1}] {affordMark} {up.Name}");
            ui.WriteLine($"    {up.Description}");
            ui.WriteLine($"    Cost: {up.Cost.Budget:F0} Budget, {up.Cost.Steel:F0} Steel, {up.Cost.ExoticMaterials:F0} Exotic");
            ui.WriteLine();
        }

        ui.WriteLine("[B] Back");
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
