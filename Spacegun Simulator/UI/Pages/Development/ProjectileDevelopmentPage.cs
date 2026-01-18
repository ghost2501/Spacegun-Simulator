using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Shared;
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
        FooterHint: "Select(↩)   (B)ack (M)enu (Q)uit"
    );

    private enum ComponentOptionKind
    {
        Owned,
        Offer,
    }

    private sealed record CoreOption(ProjectileCore Core, ComponentOptionKind Kind);

    private sealed record PropulsionOption(PropulsionSystem Propulsion, ComponentOptionKind Kind);

    private enum ModuleOptionKind
    {
        Owned,
        Offer,
    }

    private sealed record ModuleOption(ProjectileEnhancement Module, ModuleOptionKind Kind);

    private enum Step
    {
        SelectCore,
        SelectPropulsion,
        SelectGuidanceModule,
        SelectPayloadModule,
        SelectArmorModule,
        Summary,
        Result
    }

    private Step _step;

    private readonly List<CoreOption> _coreOptions = new();
    private readonly List<PropulsionOption> _propulsionOptions = new();
    private readonly List<ModuleOption> _guidanceOptions = new();
    private readonly List<ModuleOption> _payloadOptions = new();
    private readonly List<ModuleOption> _armorOptions = new();

    private int _selectedIndex;
    private int _scroll;
    private readonly List<string> _lines = new();
    private readonly List<(int start, int end)> _optionLineRanges = new();

    private ProjectileCore? _selectedCore;
    private PropulsionSystem _selectedPropulsion = PropulsionSystem.None;
    private ProjectileEnhancement _selectedGuidance = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
    private ProjectileEnhancement _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
    private ProjectileEnhancement _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);

    private int _weaponsTechLevel;
    private int _projectilesTechLevel;
    private double _gunBaseVelocity;

    private string _resultMessage = string.Empty;
    private string _inlineMessage = string.Empty;

    public override void OnEnter(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        _weaponsTechLevel = game.TechTree.CurrentLevel[TechTree.TechType.Weapons];
        _projectilesTechLevel = game.TechTree.CurrentLevel[TechTree.TechType.Projectiles];
        _gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(_weaponsTechLevel);

        game.EnsureProjectileModShopOffersForCurrentWave();
        RebuildCoreOptions(game);
        RebuildPropulsionOptions(game);
        RebuildModuleOptions(game);

        _step = Step.SelectCore;
        _selectedIndex = 0;
        _scroll = 0;

        _selectedCore = null;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedGuidance = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
        _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
        _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);

        _resultMessage = string.Empty;
        _inlineMessage = string.Empty;
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

		ResourceCostLedger.EnsureKeys(game.AccumulatedResources);

        _lines.Clear();
        _optionLineRanges.Clear();

        _lines.Add(Clamp60($"  Budget:            {game.AccumulatedResources["Budget"]:F0}"));
        _lines.Add(Clamp60($"  Steel:             {game.AccumulatedResources["Steel"]:F0} tons"));
        _lines.Add(Clamp60($"  Power Cells:       {game.AccumulatedResources["PowerCells"]:F0}"));
        _lines.Add(Clamp60($"  Specialized Alloys:{game.AccumulatedResources["SpecializedAlloys"]:F0}"));
        _lines.Add(Clamp60($"  Rare Earth:        {game.AccumulatedResources["RareEarthElements"]:F0}"));
        _lines.Add(Clamp60($"  Advanced Ore:      {game.AccumulatedResources["AdvancedOre"]:F0}"));
        _lines.Add(Clamp60($"  Exotic:            {game.AccumulatedResources["Exotic"]:F1} units"));
        _lines.Add(string.Empty);

        _lines.Add("=== GUN SPECIFICATIONS ===");
        _lines.Add(Clamp60($"  Weapons Tech Level: {_weaponsTechLevel}"));
        _lines.Add(Clamp60($"  Projectiles Tech Level: {_projectilesTechLevel}"));
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
            case Step.SelectGuidanceModule:
                BuildModuleLines(
                    stepTitle: "=== STEP 3: SELECT GUIDANCE MODULE ===",
                    stepSubtitle: "(Guidance, brains, and stabilization)",
                    list: _guidanceOptions);
                break;
            case Step.SelectPayloadModule:
                BuildModuleLines(
                    stepTitle: "=== STEP 4: SELECT PAYLOAD MODULE ===",
                    stepSubtitle: "(Penetration, coupling, and terminal effect)",
                    list: _payloadOptions);
                break;
            case Step.SelectArmorModule:
                BuildModuleLines(
                    stepTitle: "=== STEP 5: SELECT ARMOR MODULE ===",
                    stepSubtitle: "(Survivability against interception)",
                    list: _armorOptions);
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
        _lines.Add("Owned cores: select to equip. Shop offers: Enter to buy.");
        _lines.Add(string.Empty);

        if (!string.IsNullOrWhiteSpace(_inlineMessage))
        {
            _lines.Add(Clamp60(_inlineMessage));
            _lines.Add(string.Empty);
        }

        if (_coreOptions.Count == 0)
        {
            _lines.Add("[No cores available]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _coreOptions.Count - 1);

        for (int i = 0; i < _coreOptions.Count; i++)
        {
            int start = _lines.Count;
            var opt = _coreOptions[i];
            var core = opt.Core;
            double baseKe = BallisticsCalculator.CalculateKineticEnergyMJ(core.MassKg, _gunBaseVelocity);

            string cursor = i == _selectedIndex ? ">" : " ";
            string tag = opt.Kind == ComponentOptionKind.Offer ? "(Shop)" : "(Owned)";
            _lines.Add(Clamp60($"{cursor} [{i + 1}] {core.Name} {tag}"));
            _lines.Add(Clamp60($"    Mass: {core.MassKg} kg"));
            _lines.Add(Clamp60($"    Base KE (gun only): {baseKe:N0} MJ"));
            if (opt.Kind == ComponentOptionKind.Offer)
				_lines.Add(Clamp60($"    Buy Cost: {ResourceCostLedger.FormatCost(core.Cost)}"));
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
        _lines.Add("Owned propulsion: select to equip. Shop offers: Enter to buy.");
        _lines.Add(string.Empty);

        if (!string.IsNullOrWhiteSpace(_inlineMessage))
        {
            _lines.Add(Clamp60(_inlineMessage));
            _lines.Add(string.Empty);
        }

        if (_selectedCore == null)
        {
            _lines.Add("[Error: No core selected]");
            return;
        }

        if (_propulsionOptions.Count == 0)
        {
            _lines.Add("[No propulsion options available]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _propulsionOptions.Count - 1);

        for (int i = 0; i < _propulsionOptions.Count; i++)
        {
            int start = _lines.Count;
            var opt = _propulsionOptions[i];
            var prop = opt.Propulsion;
            string cursor = i == _selectedIndex ? ">" : " ";
            string tag = opt.Kind == ComponentOptionKind.Offer ? "(Shop)" : "(Owned)";

            if (prop.Id == "none")
            {
                double ke = BallisticsCalculator.CalculateKineticEnergyMJ(_selectedCore.MassKg, _gunBaseVelocity);
                _lines.Add(Clamp60($"{cursor} [{i + 1}] {prop.Name} (no boost) {tag}"));
                _lines.Add(Clamp60($"    Velocity: {_gunBaseVelocity:N0} m/s (gun only)"));
                _lines.Add(Clamp60($"    KE: {ke:N0} MJ"));
                _lines.Add(Clamp60("    Buy Cost: FREE"));
            }
            else
            {
                double maxDeltaV = prop.CalculateEffectiveDeltaV(_selectedCore.MassKg, prop.BurnDurationSeconds);
                double maxVelocity = _gunBaseVelocity + maxDeltaV;
                double maxKe = BallisticsCalculator.CalculateKineticEnergyMJ(_selectedCore.MassKg, maxVelocity);

                _lines.Add(Clamp60($"{cursor} [{i + 1}] {prop.Name} {tag}"));
                _lines.Add(Clamp60($"    Delta-V: +{prop.DeltaVCapacityMs:N0} m/s over {prop.BurnDurationSeconds:F1}s"));
                _lines.Add(Clamp60($"    Effective Delta-V: +{maxDeltaV:N0} m/s"));
                _lines.Add(Clamp60($"    Max Velocity: {maxVelocity:N0} m/s ({maxVelocity / 1000:N0} km/s)"));
                _lines.Add(Clamp60($"    Max KE: {maxKe:N0} MJ"));
                if (opt.Kind == ComponentOptionKind.Offer)
					_lines.Add(Clamp60($"    Buy Cost: {ResourceCostLedger.FormatCost(prop.Cost)}"));
                _lines.Add(Clamp60($"    {prop.Description}"));
            }

            _lines.Add(string.Empty);
            int end = _lines.Count - 1;
            _optionLineRanges.Add((start, end));
        }
    }

    private void BuildModuleLines(string stepTitle, string stepSubtitle, List<ModuleOption> list)
    {
        _lines.Add(stepTitle);
        _lines.Add(stepSubtitle);
        _lines.Add("Owned modules: select to equip. Shop offers: Enter to buy.");
        _lines.Add(string.Empty);

        if (!string.IsNullOrWhiteSpace(_inlineMessage))
        {
            _lines.Add(Clamp60(_inlineMessage));
            _lines.Add(string.Empty);
        }

        if (list.Count == 0)
        {
            _lines.Add("[No modules available]");
            _lines.Add("Press B to return.");
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, list.Count - 1);

        for (int i = 0; i < list.Count; i++)
        {
            int start = _lines.Count;
            var opt = list[i];
            var enh = opt.Module;
            string cursor = i == _selectedIndex ? ">" : " ";

            var bonuses = new List<string>();
            if (enh.HitToleranceBonus != 1.0)
                bonuses.Add($"Hit Tolerance: {(enh.HitToleranceBonus - 1) * 100:+0;-0}%");
            if (enh.Penetration != 1.0)
                bonuses.Add($"Penetration: {(enh.Penetration - 1) * 100:+0;-0}%");
            if (enh.ImpactCoupling != 1.0)
                bonuses.Add($"Coupling: {(enh.ImpactCoupling - 1) * 100:+0;-0}%");
            if (enh.DefenseBonus != 0.0)
                bonuses.Add($"Defense: {enh.DefenseBonus:+0.00;-0.00}");
            string bonusText = string.Join("  ", bonuses);

            string tag = opt.Kind == ModuleOptionKind.Offer ? "(Shop)" : "(Owned)";
            _lines.Add(Clamp60($"{cursor} [{i + 1}] {enh.Name} {tag}"));
            if (!string.IsNullOrEmpty(bonusText))
                _lines.Add(Clamp60($"    Bonuses: {bonusText}"));
            if (opt.Kind == ModuleOptionKind.Offer && !enh.IsNone)
				_lines.Add(Clamp60($"    Buy Cost: {ResourceCostLedger.FormatCost(enh.Cost)}"));
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

        var crafted = new CraftedProjectile(_selectedCore, _selectedPropulsion, _selectedGuidance, _selectedPayload, _selectedArmor, _gunBaseVelocity);
        var buildCost = new Spacegun_Simulator.Development.Shared.ResourceCost(budget: 0, steel: 0, exotic: 0);
        var shopPrice = new Spacegun_Simulator.Development.Shared.ResourceCost(
            budget: _selectedCore.Cost.Budget
                + _selectedPropulsion.Cost.Budget
                + _selectedGuidance.Cost.Budget
                + _selectedPayload.Cost.Budget
                + _selectedArmor.Cost.Budget,
            steel: _selectedCore.Cost.Steel
                + _selectedPropulsion.Cost.Steel
                + _selectedGuidance.Cost.Steel
                + _selectedPayload.Cost.Steel
                + _selectedArmor.Cost.Steel,
			powerCells: _selectedCore.Cost.PowerCells
				+ _selectedPropulsion.Cost.PowerCells
				+ _selectedGuidance.Cost.PowerCells
				+ _selectedPayload.Cost.PowerCells
				+ _selectedArmor.Cost.PowerCells,
			specializedAlloys: _selectedCore.Cost.SpecializedAlloys
				+ _selectedPropulsion.Cost.SpecializedAlloys
				+ _selectedGuidance.Cost.SpecializedAlloys
				+ _selectedPayload.Cost.SpecializedAlloys
				+ _selectedArmor.Cost.SpecializedAlloys,
			rareEarthElements: _selectedCore.Cost.RareEarthElements
				+ _selectedPropulsion.Cost.RareEarthElements
				+ _selectedGuidance.Cost.RareEarthElements
				+ _selectedPayload.Cost.RareEarthElements
				+ _selectedArmor.Cost.RareEarthElements,
			advancedOre: _selectedCore.Cost.AdvancedOre
				+ _selectedPropulsion.Cost.AdvancedOre
				+ _selectedGuidance.Cost.AdvancedOre
				+ _selectedPayload.Cost.AdvancedOre
				+ _selectedArmor.Cost.AdvancedOre,
            exotic: _selectedCore.Cost.ExoticMaterials
                + _selectedPropulsion.Cost.ExoticMaterials
                + _selectedGuidance.Cost.ExoticMaterials
                + _selectedPayload.Cost.ExoticMaterials
                + _selectedArmor.Cost.ExoticMaterials);

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
        double p = crafted.PenetrationMultiplier;
        if (p != 1.0)
            _lines.Add(Clamp60($"  Penetration: {(p - 1) * 100:+0}%"));
        if (crafted.HitToleranceMultiplier != 1.0)
            _lines.Add(Clamp60($"  Hit Tolerance: {(crafted.HitToleranceMultiplier - 1) * 100:+0}%"));
        if (crafted.ImpactCouplingMultiplier != 1.0)
            _lines.Add(Clamp60($"  Impact Coupling: {(crafted.ImpactCouplingMultiplier - 1) * 100:+0}%"));
        if (crafted.DefenseRating > 0.0)
            _lines.Add(Clamp60($"  Defense: {crafted.DefenseRating:P0}"));

        _lines.Add(string.Empty);
        _lines.Add("  COST MODEL:");
        _lines.Add(Clamp60("    Build: FREE"));
        _lines.Add(Clamp60("    Note: You pay only when purchasing items in the Mod Shop."));
        _lines.Add("    Current loadout prices (reference only):");

        _lines.Add(Clamp60($"      Core:      {FormatCost(_selectedCore.Cost)}"));
        _lines.Add(Clamp60($"      Propulsion:{FormatCost(_selectedPropulsion.Cost)}"));
        _lines.Add(Clamp60($"      Guidance:  {FormatCost(_selectedGuidance.Cost)}"));
        _lines.Add(Clamp60($"      Payload:   {FormatCost(_selectedPayload.Cost)}"));
        _lines.Add(Clamp60($"      Armor:     {FormatCost(_selectedArmor.Cost)}"));
        _lines.Add(Clamp60($"      Total:     {FormatCost(shopPrice)}"));

        if (game.CurrentWave?.Archetype != null)
        {
            double penetration = Math.Max(0.1, crafted.PenetrationMultiplier);
            double baseRequiredMJ = game.CurrentWave.Targets.Count > 0 ? game.CurrentWave.Targets[0].FractureEnergy : 0.0;
            double requiredMJ = baseRequiredMJ / penetration;
            bool meets = crafted.RawKineticEnergyMJ >= requiredMJ;
            _lines.Add(string.Empty);
            _lines.Add(Clamp60($"  Target Requirement: {(meets ? "✓ MEETS REQUIREMENT" : "✗ INSUFFICIENT ENERGY")}"));

            var diffConfig = DifficultyConfig.GetConfig(game.SelectedDifficulty);
            if (diffConfig.IsTutorialMode)
                _lines.Add(Clamp60("  Note: Tutorial mode uses a fixed beachball target."));
        }

        bool canAfford = CanAffordCost(buildCost, game.AccumulatedResources);
        _lines.Add(Clamp60($"  Build Affordability: {(canAfford ? "✓ OK" : "✗ INSUFFICIENT RESOURCES")}"));

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

		static string FormatCost(Spacegun_Simulator.Development.Shared.ResourceCost cost) => ResourceCostLedger.FormatCost(cost);
    }

    private void BuildResultLines(GameState game)
    {
        _lines.Add("=== PROJECTILE DEVELOPMENT ===");
        _lines.Add(string.Empty);
        _lines.Add(_resultMessage);
        _lines.Add(string.Empty);

        _lines.Add("Remaining Resources:");
        _lines.Add(Clamp60($"  Budget:            {game.AccumulatedResources["Budget"]:F0}"));
        _lines.Add(Clamp60($"  Steel:             {game.AccumulatedResources["Steel"]:F0}"));
        _lines.Add(Clamp60($"  Power Cells:       {game.AccumulatedResources["PowerCells"]:F0}"));
        _lines.Add(Clamp60($"  Specialized Alloys:{game.AccumulatedResources["SpecializedAlloys"]:F0}"));
        _lines.Add(Clamp60($"  Rare Earth:        {game.AccumulatedResources["RareEarthElements"]:F0}"));
        _lines.Add(Clamp60($"  Advanced Ore:      {game.AccumulatedResources["AdvancedOre"]:F0}"));
        _lines.Add(Clamp60($"  Exotic:            {game.AccumulatedResources["Exotic"]:F1}"));
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
                Step.SelectGuidanceModule => GoBackToPropulsion(),
                Step.SelectPayloadModule => GoBackToGuidance(),
                Step.SelectArmorModule => GoBackToPayload(),
                Step.Summary => PageResult.Back(PageId.WeaponDevelopment),
                Step.Result => PageResult.Back(PageId.WeaponDevelopment),
                _ => PageResult.Back(PageId.WeaponDevelopment)
            };
            _scroll = 0;
            _inlineMessage = string.Empty;
            RebuildLines(ui);
            return result;
        }

        switch (_step)
        {
            case Step.SelectCore:
                return HandleCoreSelection(ui, key);

            case Step.SelectPropulsion:
                return HandlePropulsionSelection(ui, key);

            case Step.SelectGuidanceModule:
                return HandleModuleSelection(ui, key, ProjectileEnhancementSlot.Guidance, _guidanceOptions, onOwnedChoose: m =>
                {
                    _selectedGuidance = m;
                    _step = Step.SelectPayloadModule;
                    _selectedIndex = 0;
                    _scroll = 0;
                    _inlineMessage = string.Empty;
                    RebuildLines(ui);
                });

            case Step.SelectPayloadModule:
                return HandleModuleSelection(ui, key, ProjectileEnhancementSlot.Payload, _payloadOptions, onOwnedChoose: m =>
                {
                    _selectedPayload = m;
                    _step = Step.SelectArmorModule;
                    _selectedIndex = 0;
                    _scroll = 0;
                    _inlineMessage = string.Empty;
                    RebuildLines(ui);
                });

            case Step.SelectArmorModule:
                return HandleModuleSelection(ui, key, ProjectileEnhancementSlot.Armor, _armorOptions, onOwnedChoose: m =>
                {
                    _selectedArmor = m;
                    _step = Step.Summary;
                    _scroll = 0;
                    _inlineMessage = string.Empty;
                    RebuildLines(ui);
                });

            case Step.Summary:
                return HandleSummaryInput(game, key);

            case Step.Result:
                return PageResult.Back(PageId.WeaponDevelopment);
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

        var crafted = new CraftedProjectile(_selectedCore, _selectedPropulsion, _selectedGuidance, _selectedPayload, _selectedArmor, _gunBaseVelocity);
        var buildCost = new Spacegun_Simulator.Development.Shared.ResourceCost(budget: 0, steel: 0, exotic: 0);
        bool canAfford = CanAffordCost(buildCost, game.AccumulatedResources);

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

    private void RebuildCoreOptions(GameState game)
    {
        _coreOptions.Clear();

        var owned = new List<ProjectileCore>();
        foreach (var id in game.ProjectileModShop.OwnedCoreIds)
        {
            if (ProjectilesCatalog.TryGetCoreById(id, out var core))
                owned.Add(core);
        }

        owned = owned
            .OrderBy(c => c.RequiredTechLevel)
            .ThenBy(c => c.Name)
            .ToList();

        foreach (var c in owned)
            _coreOptions.Add(new CoreOption(c, ComponentOptionKind.Owned));

        foreach (var id in game.ProjectileModShop.CoreOfferIds)
        {
            if (game.ProjectileModShop.OwnedCoreIds.Contains(id))
                continue;

            if (ProjectilesCatalog.TryGetCoreById(id, out var core))
                _coreOptions.Add(new CoreOption(core, ComponentOptionKind.Offer));
        }
    }

    private void RebuildPropulsionOptions(GameState game)
    {
        _propulsionOptions.Clear();

        var owned = new List<PropulsionSystem>();
        foreach (var id in game.ProjectileModShop.OwnedPropulsionIds)
        {
            if (ProjectilesCatalog.TryGetPropulsionById(id, out var prop))
                owned.Add(prop);
        }

        owned = owned
            .OrderByDescending(p => string.Equals(p.Id, "none", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.RequiredTechLevel)
            .ThenBy(p => p.Name)
            .ToList();

        foreach (var p in owned)
            _propulsionOptions.Add(new PropulsionOption(p, ComponentOptionKind.Owned));

        foreach (var id in game.ProjectileModShop.PropulsionOfferIds)
        {
            if (game.ProjectileModShop.OwnedPropulsionIds.Contains(id))
                continue;

            if (ProjectilesCatalog.TryGetPropulsionById(id, out var prop))
                _propulsionOptions.Add(new PropulsionOption(prop, ComponentOptionKind.Offer));
        }
    }

    private void RebuildModuleOptions(GameState game)
    {
        _guidanceOptions.Clear();
        _payloadOptions.Clear();
        _armorOptions.Clear();

        BuildOptionsForSlot(ProjectileEnhancementSlot.Guidance, _guidanceOptions, game.ProjectileModShop.OwnedGuidanceModuleIds, game.ProjectileModShop.GuidanceOfferModuleIds);
        BuildOptionsForSlot(ProjectileEnhancementSlot.Payload, _payloadOptions, game.ProjectileModShop.OwnedPayloadModuleIds, game.ProjectileModShop.PayloadOfferModuleIds);
        BuildOptionsForSlot(ProjectileEnhancementSlot.Armor, _armorOptions, game.ProjectileModShop.OwnedArmorModuleIds, game.ProjectileModShop.ArmorOfferModuleIds);

        static void BuildOptionsForSlot(
            ProjectileEnhancementSlot slot,
            List<ModuleOption> options,
            HashSet<string> ownedIds,
            List<string> offerIds)
        {
            var owned = new List<ProjectileEnhancement>();
            foreach (var id in ownedIds)
            {
                if (ProjectilesCatalog.TryGetEnhancementById(id, out var enh) && enh.Slot == slot)
                    owned.Add(enh);
            }

            // Stable ordering for owned list.
            owned = owned
                .OrderByDescending(m => m.IsNone)
                .ThenBy(m => m.RequiredTechLevel)
                .ThenBy(m => m.Name)
                .ToList();

            foreach (var m in owned)
                options.Add(new ModuleOption(m, ModuleOptionKind.Owned));

            foreach (var id in offerIds)
            {
                if (ProjectilesCatalog.TryGetEnhancementById(id, out var enh) && enh.Slot == slot)
                    options.Add(new ModuleOption(enh, ModuleOptionKind.Offer));
            }
        }
    }

    private PageResult HandleCoreSelection(UiContext ui, ConsoleKeyInfo key)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        if (_coreOptions.Count <= 0)
            return PageResult.Stay;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(_coreOptions.Count - 1, _selectedIndex + 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.Enter:
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _coreOptions.Count - 1);
                var opt = _coreOptions[_selectedIndex];

                if (opt.Kind == ComponentOptionKind.Offer)
                {
					ResourceCostLedger.EnsureKeys(game.AccumulatedResources);
					if (!ResourceCostLedger.CanAfford(game.AccumulatedResources, opt.Core.Cost))
                    {
                        _inlineMessage = "✗ Cannot afford this core offer.";
                        RebuildLines(ui);
                        return PageResult.Stay;
                    }

					ResourceCostLedger.Spend(game.AccumulatedResources, opt.Core.Cost);

                    game.ProjectileModShop.OwnedCoreIds.Add(opt.Core.Id);
                    RemoveCoreOfferId(opt.Core.Id, game);
                    RebuildCoreOptions(game);
                    _inlineMessage = "✓ Core purchased and added to owned.";
                }

                _selectedCore = opt.Core;
                _step = Step.SelectPropulsion;
                _selectedIndex = 0;
                _scroll = 0;
                RebuildLines(ui);
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private PageResult HandlePropulsionSelection(UiContext ui, ConsoleKeyInfo key)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        if (_propulsionOptions.Count <= 0)
            return PageResult.Stay;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(_propulsionOptions.Count - 1, _selectedIndex + 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.Enter:
                _selectedIndex = Math.Clamp(_selectedIndex, 0, _propulsionOptions.Count - 1);
                var opt = _propulsionOptions[_selectedIndex];

                if (opt.Kind == ComponentOptionKind.Offer)
                {
					ResourceCostLedger.EnsureKeys(game.AccumulatedResources);
					if (!ResourceCostLedger.CanAfford(game.AccumulatedResources, opt.Propulsion.Cost))
                    {
                        _inlineMessage = "✗ Cannot afford this propulsion offer.";
                        RebuildLines(ui);
                        return PageResult.Stay;
                    }

					ResourceCostLedger.Spend(game.AccumulatedResources, opt.Propulsion.Cost);

                    game.ProjectileModShop.OwnedPropulsionIds.Add(opt.Propulsion.Id);
                    RemovePropulsionOfferId(opt.Propulsion.Id, game);
                    RebuildPropulsionOptions(game);
                    _inlineMessage = "✓ Propulsion purchased and added to owned.";
                }

                _selectedPropulsion = opt.Propulsion;
                _step = Step.SelectGuidanceModule;
                _selectedIndex = 0;
                _scroll = 0;
                RebuildLines(ui);
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private PageResult HandleModuleSelection(
        UiContext ui,
        ConsoleKeyInfo key,
        ProjectileEnhancementSlot slot,
        List<ModuleOption> options,
        Action<ProjectileEnhancement> onOwnedChoose)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (ProjectileDevelopmentPage requires GameState). ");

        if (options.Count <= 0)
            return PageResult.Stay;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(options.Count - 1, _selectedIndex + 1);
                RebuildLines(ui);
                EnsureSelectedVisible(ui);
                return PageResult.Stay;

            case ConsoleKey.Enter:
                _selectedIndex = Math.Clamp(_selectedIndex, 0, options.Count - 1);
                var opt = options[_selectedIndex];
                if (opt.Kind == ModuleOptionKind.Owned)
                {
                    onOwnedChoose(opt.Module);
                    return PageResult.Stay;
                }

                // Offer: attempt purchase.
				ResourceCostLedger.EnsureKeys(game.AccumulatedResources);
				if (!ResourceCostLedger.CanAfford(game.AccumulatedResources, opt.Module.Cost))
                {
                    _inlineMessage = "✗ Cannot afford this module offer.";
                    RebuildLines(ui);
                    return PageResult.Stay;
                }

				ResourceCostLedger.Spend(game.AccumulatedResources, opt.Module.Cost);

                game.ProjectileModShop.TryAddOwned(slot, opt.Module.Id);
                RemoveOfferId(slot, opt.Module.Id, game);
                RebuildModuleOptions(game);

                _inlineMessage = "✓ Module purchased and added to owned.";
                onOwnedChoose(opt.Module);
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }

    private static void RemoveOfferId(ProjectileEnhancementSlot slot, string moduleId, GameState game)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return;

        var list = slot switch
        {
            ProjectileEnhancementSlot.Guidance => game.ProjectileModShop.GuidanceOfferModuleIds,
            ProjectileEnhancementSlot.Payload => game.ProjectileModShop.PayloadOfferModuleIds,
            ProjectileEnhancementSlot.Armor => game.ProjectileModShop.ArmorOfferModuleIds,
            _ => null,
        };

        if (list is null || list.Count == 0)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], moduleId, StringComparison.OrdinalIgnoreCase))
                list.RemoveAt(i);
        }
    }

    private static void RemoveCoreOfferId(string coreId, GameState game)
    {
        if (string.IsNullOrWhiteSpace(coreId))
            return;

        var list = game.ProjectileModShop.CoreOfferIds;
        if (list.Count == 0)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], coreId, StringComparison.OrdinalIgnoreCase))
                list.RemoveAt(i);
        }
    }

    private static void RemovePropulsionOfferId(string propulsionId, GameState game)
    {
        if (string.IsNullOrWhiteSpace(propulsionId))
            return;

        var list = game.ProjectileModShop.PropulsionOfferIds;
        if (list.Count == 0)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], propulsionId, StringComparison.OrdinalIgnoreCase))
                list.RemoveAt(i);
        }
    }

	private static bool CanAffordCost(Spacegun_Simulator.Development.Shared.ResourceCost cost, Dictionary<string, double> resources)
	{
		ResourceCostLedger.EnsureKeys(resources);
		return ResourceCostLedger.CanAfford(resources, cost);
	}

    private PageResult GoBackToCore()
    {
        _step = Step.SelectCore;
        _selectedIndex = 0;
        _selectedCore = null;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedGuidance = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
        _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
        _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);
        return PageResult.Stay;
    }

    private PageResult GoBackToPropulsion()
    {
        _step = Step.SelectPropulsion;
        _selectedIndex = 0;
        _selectedPropulsion = PropulsionSystem.None;
        _selectedGuidance = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
        _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
        _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);
        return PageResult.Stay;
    }

    private PageResult GoBackToGuidance()
    {
        _step = Step.SelectGuidanceModule;
        _selectedIndex = 0;
        _selectedGuidance = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
        _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
        _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);
        return PageResult.Stay;
    }

    private PageResult GoBackToPayload()
    {
        _step = Step.SelectPayloadModule;
        _selectedIndex = 0;
        _selectedPayload = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
        _selectedArmor = ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);
        return PageResult.Stay;
    }
}
