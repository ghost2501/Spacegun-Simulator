using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Technology;
using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.UI.Pages.Development;

public sealed class ProjectileDevelopmentPage : PageBase
{
    public override string Id => PageId.ProjectileDevelopment;
    public override string Title => "PROJECTILE DEVELOPMENT";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "Arrows=Select  PgUp/PgDn=Scroll  Enter=Choose  B=Back  Esc=Menu  Q=Quit"
    );

    private enum Step
    {
        SelectCore,
        SelectPropulsion,
        SelectEnhancement,
        Summary,
        Result
    }

    private Step _step;

    private readonly List<ProjectileCore> _cores = new();
    private readonly List<PropulsionSystem> _propulsion = new();
    private readonly List<ProjectileEnhancement> _enhancements = new();

    private int _selectedIndex;
    private int _scroll;
    private readonly List<string> _lines = new();
    private readonly List<(int start, int end)> _optionLineRanges = new();

    private ProjectileCore? _selectedCore;
    private PropulsionSystem _selectedPropulsion = PropulsionSystem.None;
    private ProjectileEnhancement _selectedEnhancement = ProjectileEnhancement.None;

    private int _weaponsTechLevel;
    private double _gunBaseVelocity;

    private string _resultMessage = string.Empty;

    public override void OnEnter(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        _weaponsTechLevel = game.TechTree.CurrentLevel[TechTree.TechType.Weapons];
        _gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(_weaponsTechLevel);

        _cores.Clear();
        _cores.AddRange(CraftedProjectile.GetUnlockedCores(game.TechTree));

        _propulsion.Clear();
        _propulsion.AddRange(CraftedProjectile.GetUnlockedPropulsion(game.TechTree));

        _enhancements.Clear();
        _enhancements.AddRange(CraftedProjectile.GetUnlockedEnhancements(game.TechTree));

        _step = Step.SelectCore;
        _selectedIndex = 0;
        _scroll = 0;

        _selectedCore = null;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedEnhancement = ProjectileEnhancement.None;

        _resultMessage = string.Empty;
        RebuildLines(ui);
    }

    protected override void RenderBody(UiContext ui)
    {
        if (_lines.Count == 0)
            RebuildLines(ui);

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
        int maxScroll = Math.Max(0, _lines.Count - viewport);
        if (_scroll < 0) _scroll = 0;
        if (_scroll > maxScroll) _scroll = maxScroll;

        int end = Math.Min(_lines.Count, _scroll + viewport);
        for (int i = _scroll; i < end; i++)
            ui.WriteLine(_lines[i]);
    }

    private void RebuildLines(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        _lines.Clear();
        _optionLineRanges.Clear();

        _lines.Add(Clamp60($"  Budget: {game.AccumulatedResources["Budget"]:F0}"));
        _lines.Add(Clamp60($"  Steel:  {game.AccumulatedResources["Steel"]:F0} tons"));
        _lines.Add(Clamp60($"  Exotic: {game.AccumulatedResources["Exotic"]:F1} units"));
        _lines.Add(string.Empty);

        _lines.Add("=== GUN SPECIFICATIONS ===");
        _lines.Add(Clamp60($"  Weapons Tech Level: {_weaponsTechLevel}"));
        _lines.Add(Clamp60($"  Base Muzzle Velocity: {_gunBaseVelocity:N0} m/s ({_gunBaseVelocity / 1000:N0} km/s)"));
        _lines.Add(Clamp60($"  Barrel Integrity: {game.Gun.BarrelIntegrity:P2}"));
        _lines.Add(string.Empty);

        switch (_step)
        {
            case Step.SelectCore:
                BuildCoreLines();
                break;
            case Step.SelectPropulsion:
                BuildPropulsionLines();
                break;
            case Step.SelectEnhancement:
                BuildEnhancementLines();
                break;
            case Step.Summary:
                BuildSummaryLines(game);
                break;
            case Step.Result:
                BuildResultLines(game);
                break;
        }
    }

    private void BuildCoreLines()
    {
        _lines.Add("=== STEP 1: SELECT PROJECTILE CORE ===");
        _lines.Add("(Determines projectile mass)");
        _lines.Add(string.Empty);

        if (_cores.Count == 0)
        {
            _lines.Add("[No cores unlocked]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _cores.Count - 1);

        for (int i = 0; i < _cores.Count; i++)
        {
            int start = _lines.Count;
            var core = _cores[i];
            double baseKe = BallisticsCalculator.CalculateKineticEnergyMJ(core.MassKg, _gunBaseVelocity);

            string cursor = i == _selectedIndex ? ">" : " ";
            _lines.Add(Clamp60($"{cursor} [{i + 1}] {core.Name}"));
            _lines.Add(Clamp60($"    Mass: {core.MassKg} kg"));
            _lines.Add(Clamp60($"    Base KE (gun only): {baseKe:N0} MJ"));
            _lines.Add(Clamp60($"    Cost: {core.Cost.Budget:F0} Budget, {core.Cost.Steel:F0} Steel, {core.Cost.ExoticMaterials:F0} Exotic"));
            _lines.Add(Clamp60($"    {core.Description}"));
            _lines.Add(string.Empty);
            int end = _lines.Count - 1;
            _optionLineRanges.Add((start, end));
        }
    }

    private void BuildPropulsionLines()
    {
        _lines.Add("=== STEP 2: SELECT PROPULSION SYSTEM ===");
        _lines.Add("(Provides Delta-V boost during flight)");
        _lines.Add(string.Empty);

        if (_selectedCore == null)
        {
            _lines.Add("[Error: No core selected]");
            return;
        }

        if (_propulsion.Count == 0)
        {
            _lines.Add("[No propulsion options unlocked]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _propulsion.Count - 1);

        for (int i = 0; i < _propulsion.Count; i++)
        {
            int start = _lines.Count;
            var prop = _propulsion[i];
            string cursor = i == _selectedIndex ? ">" : " ";

            if (prop.Id == "none")
            {
                double ke = BallisticsCalculator.CalculateKineticEnergyMJ(_selectedCore.MassKg, _gunBaseVelocity);
                _lines.Add(Clamp60($"{cursor} [{i + 1}] {prop.Name} (no boost)"));
                _lines.Add(Clamp60($"    Velocity: {_gunBaseVelocity:N0} m/s (gun only)"));
                _lines.Add(Clamp60($"    KE: {ke:N0} MJ"));
                _lines.Add(Clamp60("    Cost: FREE"));
            }
            else
            {
                double maxDeltaV = prop.CalculateEffectiveDeltaV(_selectedCore.MassKg, prop.BurnDurationSeconds);
                double maxVelocity = _gunBaseVelocity + maxDeltaV;
                double maxKe = BallisticsCalculator.CalculateKineticEnergyMJ(_selectedCore.MassKg, maxVelocity);

                _lines.Add(Clamp60($"{cursor} [{i + 1}] {prop.Name}"));
                _lines.Add(Clamp60($"    Delta-V: +{prop.DeltaVCapacityMs:N0} m/s over {prop.BurnDurationSeconds:F1}s"));
                _lines.Add(Clamp60($"    Effective Delta-V: +{maxDeltaV:N0} m/s"));
                _lines.Add(Clamp60($"    Max Velocity: {maxVelocity:N0} m/s ({maxVelocity / 1000:N0} km/s)"));
                _lines.Add(Clamp60($"    Max KE: {maxKe:N0} MJ"));
                _lines.Add(Clamp60($"    Cost: {prop.Cost.Budget:F0} Budget, {prop.Cost.Steel:F0} Steel, {prop.Cost.ExoticMaterials:F0} Exotic"));
                _lines.Add(Clamp60($"    {prop.Description}"));
            }

            _lines.Add(string.Empty);
            int end = _lines.Count - 1;
            _optionLineRanges.Add((start, end));
        }
    }

    private void BuildEnhancementLines()
    {
        _lines.Add("=== STEP 3: SELECT ENHANCEMENT ===");
        _lines.Add("(Modifies accuracy or damage)");
        _lines.Add(string.Empty);

        if (_enhancements.Count == 0)
        {
            _lines.Add("[No enhancements unlocked]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _enhancements.Count - 1);

        for (int i = 0; i < _enhancements.Count; i++)
        {
            int start = _lines.Count;
            var enh = _enhancements[i];
            string cursor = i == _selectedIndex ? ">" : " ";

            string bonusText = "";
            if (enh.HitToleranceBonus != 1.0)
                bonusText += $"Hit Tolerance: {(enh.HitToleranceBonus - 1) * 100:+0;-0}%  ";
            if (enh.EnergyEfficiencyBonus != 1.0)
                bonusText += $"Damage: {(enh.EnergyEfficiencyBonus - 1) * 100:+0;-0}%";

            _lines.Add(Clamp60($"{cursor} [{i + 1}] {enh.Name}"));
            if (!string.IsNullOrEmpty(bonusText))
                _lines.Add(Clamp60($"    Bonuses: {bonusText}"));
            if (enh.Id != "none")
                _lines.Add(Clamp60($"    Cost: {enh.Cost.Budget:F0} Budget, {enh.Cost.Steel:F0} Steel, {enh.Cost.ExoticMaterials:F0} Exotic"));
            _lines.Add(Clamp60($"    {enh.Description}"));
            _lines.Add(string.Empty);
            int end = _lines.Count - 1;
            _optionLineRanges.Add((start, end));
        }
    }

    private void BuildSummaryLines(GameState game)
    {
        if (_selectedCore == null)
        {
            _lines.Add("[Error: No core selected]");
            return;
        }

        var crafted = new CraftedProjectile(_selectedCore, _selectedPropulsion, _selectedEnhancement, _gunBaseVelocity);

        _lines.Add("=== PROJECTILE CONFIGURATION - SUMMARY ===");
        _lines.Add(string.Empty);

        _lines.Add(Clamp60($"  Configuration: {crafted.DisplayName}"));
        _lines.Add(Clamp60($"  Projectile Mass: {crafted.MassKg} kg"));
        _lines.Add(Clamp60($"  Gun Base Velocity: {crafted.GunBaseMuzzleVelocityMs:N0} m/s"));

        if (_selectedPropulsion.Id != "none")
        {
            double maxDeltaV = _selectedPropulsion.CalculateEffectiveDeltaV(crafted.MassKg, _selectedPropulsion.BurnDurationSeconds);
            _lines.Add(Clamp60($"  Propulsion Delta-V: +{maxDeltaV:N0} m/s"));
            _lines.Add(Clamp60($"  Max Velocity: {crafted.MaxVelocityMs:N0} m/s"));
        }

        _lines.Add(Clamp60($"  Max KE: {crafted.RawKineticEnergyMJ:N0} MJ"));
        _lines.Add(Clamp60($"  Effective Kinetic Energy: {crafted.EffectiveKineticEnergyMJ:N0} MJ"));
        if (crafted.HitToleranceMultiplier != 1.0)
            _lines.Add(Clamp60($"  Hit Tolerance: {(crafted.HitToleranceMultiplier - 1) * 100:+0}%"));

        _lines.Add(string.Empty);
        _lines.Add("  TOTAL COST:");
        _lines.Add(Clamp60($"    Budget: {crafted.TotalCost.Budget:F0}"));
        _lines.Add(Clamp60($"    Steel:  {crafted.TotalCost.Steel:F0}"));
        _lines.Add(Clamp60($"    Exotic: {crafted.TotalCost.ExoticMaterials:F0}"));

        if (game.CurrentWave?.Archetype != null)
        {
            bool meets = crafted.EffectiveKineticEnergyMJ >= game.CurrentWave.Archetype.FractureEnergyRange.Min;
            _lines.Add(string.Empty);
            _lines.Add(Clamp60($"  Target Requirement: {(meets ? "✓ MEETS REQUIREMENT" : "✗ INSUFFICIENT ENERGY")}"));

            var diffConfig = DifficultyConfig.GetConfig(game.SelectedDifficulty);
            if (diffConfig.IsTutorialMode)
                _lines.Add(Clamp60("  Note: Tutorial mode uses a fixed beachball target."));
        }

        bool canAfford = CraftedProjectile.CanAfford(crafted, game.AccumulatedResources);
        _lines.Add(Clamp60($"  Affordability: {(canAfford ? "✓ CAN AFFORD" : "✗ INSUFFICIENT RESOURCES")}"));

        _lines.Add(string.Empty);
        if (!canAfford)
        {
            _lines.Add("✗ Cannot afford this configuration.");
            _lines.Add("Enter/B = Back   PgUp/PgDn/Arrows = Scroll");
        }
        else
        {
            _lines.Add("Confirm build? (Y/N)");
        }
    }

    private void BuildResultLines(GameState game)
    {
        _lines.Add("=== PROJECTILE DEVELOPMENT ===");
        _lines.Add(string.Empty);
        _lines.Add(_resultMessage);
        _lines.Add(string.Empty);

        _lines.Add("Remaining Resources:");
        _lines.Add(Clamp60($"  Budget: {game.AccumulatedResources["Budget"]:F0}"));
        _lines.Add(Clamp60($"  Steel:  {game.AccumulatedResources["Steel"]:F0}"));
        _lines.Add(Clamp60($"  Exotic: {game.AccumulatedResources["Exotic"]:F1}"));
        _lines.Add(string.Empty);

        _lines.Add("Press any key to go back...");
    }

    protected override PageResult HandleInputBody(UiContext ui, ConsoleKeyInfo key)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        const int lineStep = 1;
        const int pageStep = 6;

        // Summary/Result are long read-only screens; treat arrows as scrolling there.
        if (_step is Step.Summary or Step.Result)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _scroll -= lineStep;
                    return PageResult.Stay;
                case ConsoleKey.DownArrow:
                    _scroll += lineStep;
                    return PageResult.Stay;
            }
        }

        switch (key.Key)
        {
            case ConsoleKey.PageUp:
                _scroll -= pageStep;
                return PageResult.Stay;
            case ConsoleKey.PageDown:
                _scroll += pageStep;
                return PageResult.Stay;
            case ConsoleKey.Home:
                _scroll = 0;
                return PageResult.Stay;
            case ConsoleKey.End:
                _scroll = int.MaxValue;
                return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.B)
        {
            var result = _step switch
            {
                Step.SelectCore => PageResult.Back(PageId.WeaponDevelopment),
                Step.SelectPropulsion => GoBackToCore(),
                Step.SelectEnhancement => GoBackToPropulsion(),
                Step.Summary => PageResult.Back(PageId.WeaponDevelopment),
                Step.Result => PageResult.Back(PageId.WeaponDevelopment),
                _ => PageResult.Back(PageId.WeaponDevelopment)
            };
            _scroll = 0;
            RebuildLines(ui);
            return result;
        }

        switch (_step)
        {
            case Step.SelectCore:
                return HandleSelection(ui, key, _cores.Count, onChoose: () =>
                {
                    _selectedCore = _cores[_selectedIndex];
                    _step = Step.SelectPropulsion;
                    _selectedIndex = 0;
                    _scroll = 0;
                    RebuildLines(ui);
                });

            case Step.SelectPropulsion:
                return HandleSelection(ui, key, _propulsion.Count, onChoose: () =>
                {
                    _selectedPropulsion = _propulsion[_selectedIndex];
                    _step = Step.SelectEnhancement;
                    _selectedIndex = 0;
                    _scroll = 0;
                    RebuildLines(ui);
                });

            case Step.SelectEnhancement:
                return HandleSelection(ui, key, _enhancements.Count, onChoose: () =>
                {
                    _selectedEnhancement = _enhancements[_selectedIndex];
                    _step = Step.Summary;
                    _scroll = 0;
                    RebuildLines(ui);
                });

            case Step.Summary:
                {
                    var result = HandleSummaryInput(game, key);
                    RebuildLines(ui);
                    return result;
                }

            case Step.Result:
                return PageResult.Back(PageId.WeaponDevelopment);
        }

        return PageResult.Stay;
    }

    private PageResult HandleSelection(UiContext ui, ConsoleKeyInfo key, int count, Action onChoose)
    {
        if (count <= 0)
            return PageResult.Stay;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(count - 1, _selectedIndex + 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.Enter:
                _selectedIndex = Math.Clamp(_selectedIndex, 0, count - 1);
                onChoose();
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private void EnsureSelectedVisible(UiContext ui)
    {
        if (_optionLineRanges.Count == 0)
            return;

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _optionLineRanges.Count - 1);
        (int start, int end) = _optionLineRanges[_selectedIndex];

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;
        if (viewport <= 0)
            return;

        // If the selected block is above the viewport, scroll up.
        if (start < _scroll)
        {
            _scroll = start;
            return;
        }

        // If the selected block is below the viewport, scroll down.
        int viewEnd = _scroll + viewport - 1;
        if (end > viewEnd)
        {
            _scroll = Math.Max(0, end - (viewport - 1));
        }
    }

    private PageResult HandleSummaryInput(GameState game, ConsoleKeyInfo key)
    {
        if (_selectedCore == null)
            return PageResult.Back(PageId.WeaponDevelopment);

        var crafted = new CraftedProjectile(_selectedCore, _selectedPropulsion, _selectedEnhancement, _gunBaseVelocity);
        bool canAfford = CraftedProjectile.CanAfford(crafted, game.AccumulatedResources);

        if (!canAfford)
        {
            // Allow scrolling on the unaffordable summary; only Enter (or B in HandleInputBody)
            // returns to Weapon Development.
            if (key.Key == ConsoleKey.Enter)
                return PageResult.Back(PageId.WeaponDevelopment);

            return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.Y)
        {
            // Deduct resources
            game.AccumulatedResources["Budget"] -= crafted.TotalCost.Budget;
            game.AccumulatedResources["Steel"] -= crafted.TotalCost.Steel;
            game.AccumulatedResources["Exotic"] -= crafted.TotalCost.ExoticMaterials;

            // Store crafted projectile
            game.CraftedProjectile = crafted;

            _resultMessage = "✓ Projectile built successfully!";
            _step = Step.Result;
            return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.N)
        {
            // Legacy behavior: cancel returns to Weapon Development.
            return PageResult.Back(PageId.WeaponDevelopment);
        }

        return PageResult.Stay;
    }

    private PageResult GoBackToCore()
    {
        _step = Step.SelectCore;
        _selectedIndex = 0;
        _selectedCore = null;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedEnhancement = ProjectileEnhancement.None;
        return PageResult.Stay;
    }

    private PageResult GoBackToPropulsion()
    {
        _step = Step.SelectPropulsion;
        _selectedIndex = 0;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedEnhancement = ProjectileEnhancement.None;
        return PageResult.Stay;
    }
}
