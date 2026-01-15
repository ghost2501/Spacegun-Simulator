using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI.Theme;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Pages.FireControl;

public sealed class FiringPhasePage : PageBase
{
    public override string Id => PageId.Firing;
    public override string Title => "FIRING SOLUTION";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "Select(↩)    G=Gun Stats     (B)ack       (M)enu      (Q)uit"
    );

    public enum FiringMenuAction
    {
        None,
        MotionComputer,
        BallisticsTables,
        TrajectoryPlotter,
        FireSimulator,
        Commit,
    }

    public FiringMenuAction Action { get; private set; } = FiringMenuAction.None;

    private readonly List<string> _lines = new();
    private int _scroll;
    private bool _followSelection;
    private int _selectedToolIndex;
    private int _toolListFirstLineIndex;

    private GameState.FiringPhaseResult? _firingResult;
    private DifficultyConfig? _diff;

    public override void OnEnter(UiContext ui)
    {
        Action = FiringMenuAction.None;
        _scroll = 0;
        _followSelection = true;
        _selectedToolIndex = 0;
        _toolListFirstLineIndex = -1;

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhasePage requires GameState).");
        _diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
        _firingResult = game.ExecuteFiringPhase();

        BuildLines(ui);
    }

    private void BuildLines(UiContext ui)
    {
        _lines.Clear();
        _toolListFirstLineIndex = -1;

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhasePage requires GameState).");
        var firingResult = _firingResult ?? throw new InvalidOperationException("Firing result was not computed. (Expected OnEnter to run.)");
        var diff = _diff ?? DifficultyConfig.GetConfig(game.SelectedDifficulty);

        if (!firingResult.CanReachTarget)
        {
            _lines.Add("✗ " + firingResult.Message);
            _lines.Add("");
            _lines.Add("Target is beyond effective gun range. Mission failed.");
            _lines.Add("");
            _lines.Add("Press any key...");
            return;
        }

        var firingProblem = game.CurrentFiringProblem;
        var target = game.CurrentWave?.Targets.Count > 0 ? game.CurrentWave.Targets[0] : null;

        if (target == null)
        {
            _lines.Add("✗ No valid target found!");
            _lines.Add("");
            _lines.Add("Press any key...");
            return;
        }

        if (game.CurrentWave == null)
        {
            _lines.Add("✗ Critical error: Wave data lost during firing phase!");
            _lines.Add("");
            _lines.Add("Press any key...");
            return;
        }

        if (firingProblem == null)
        {
            _lines.Add("✗ Critical error: Firing problem not initialized!");
            _lines.Add("");
            _lines.Add("Press any key...");
            return;
        }

        var weapon = game.ResolveWeaponStats(target);
        var resolved = weapon.Shot;

        double modeHitToleranceMultiplier = GameModeTuning.Current.GetHitToleranceMultiplier(game.Mode);
        var resolvedForMode = resolved with
        {
            AdditionalHitToleranceMultiplier = resolved.AdditionalHitToleranceMultiplier * modeHitToleranceMultiplier
        };

        double muzzleVelocity = resolvedForMode.MaxLaunchVelocityMs;
        double projectileMass = resolvedForMode.ProjectileMassKg;

        var calculator = new FiringSolution(
            (float)projectileMass,
            (float)resolvedForMode.EffectiveFractureEnergyMJ,
            target.Mass,
            enemyCrossSectionM2: target.CrossSection);
        calculator.ConfigureProjectileModifiers(resolvedForMode);

        float requiredImpactVelocity = calculator.CalculateRequiredVelocity();
        float maxVelocity = (float)muzzleVelocity;

        double bestCaseDeltaV = 0.0;
        if (resolvedForMode.PropulsionDeltaVCapacityMs > 0.0)
        {
            double massEfficiency = resolvedForMode.PropulsionReferenceMassKg / (resolvedForMode.PropulsionReferenceMassKg + projectileMass);
            bestCaseDeltaV = resolvedForMode.PropulsionDeltaVCapacityMs * massEfficiency;
        }

        double requiredLaunchVelocityBestCase = Math.Max(0.0, requiredImpactVelocity - bestCaseDeltaV);

        double displayRcs = target.CrossSection * diff.TargetRcsMultiplier;

        _lines.Add("=== YOUR WEAPON ===");
        _lines.Add($"Projectile Mass: {FiringPhaseFormatter.FormatMass(projectileMass, game.SelectedDifficulty)} kg");
        _lines.Add($"Max Launch Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, game.SelectedDifficulty)} m/s");
        _lines.Add($"Barrel Integrity: {game.Gun.BarrelIntegrity:P2}");
        bool hasGuidanceMod = (game.CraftedProjectile?.HasGuidance == true) || game.Gun.DefaultProjectile.HasGuidance;
        _lines.Add($"Guidance System Installed: {(hasGuidanceMod ? "Yes" : "No")}");
        _lines.Add($"Guidance Quality (x): {game.Gun.Guidance:F2}x");
        _lines.Add($"Gun Effective Range: {GameConstants.FormatDistance(game.GetCurrentEffectiveGunRangeMeters())}");
        _lines.Add("");

        _lines.Add("=== TARGET DATA FOR CALCULATIONS ===");
        _lines.Add($"Designation: {target.Name}");

            if (game.CurrentWave.Doctrine != Spacegun_Simulator.Enemies.EnemyDoctrine.None)
            {
                string tag = game.CurrentWave.IsGuestDoctrine ? " (Guest)" : string.Empty;
                _lines.Add($"Doctrine{tag}: {game.CurrentWave.DoctrineName} — {game.CurrentWave.DoctrineDescription}");
            }

        _lines.Add("Enemy Approach Vector:");
        _lines.Add($"  Elevation: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachElevation, game.SelectedDifficulty)}° (in sky)");
        _lines.Add($"  Azimuth: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachAzimuth, game.SelectedDifficulty)}° (bearing)");
        _lines.Add($"  Distance: {GameConstants.FormatDistance((double)firingProblem.EngagementDistance)}");
        _lines.Add($"  Cartesian Position: {FiringPhaseFormatter.FormatVector3(firingProblem.EnemyPosition, game.SelectedDifficulty)}");
        _lines.Add($"Enemy Velocity Vector: ({FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.X, game.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Y, game.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Z, game.SelectedDifficulty)}) m/s");
        _lines.Add($"Approach Speed: {FiringPhaseFormatter.FormatVelocity(firingProblem.ApproachSpeed, game.SelectedDifficulty)} m/s");
        _lines.Add($"Fracture Energy Required: {FiringPhaseFormatter.FormatEnergy(firingProblem.FractureEnergyRequired, game.SelectedDifficulty)}");

        if (diff.IsTutorialMode)
        {
            double hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;
            _lines.Add($"Hit Tolerance: {hitTolerance:F1} m (beachball radius)");
        }
        else
        {
            _lines.Add($"Target Radar Cross-Section: {FiringPhaseFormatter.FormatRadarCrossSection(displayRcs, game.SelectedDifficulty)} m²");
        }

        _lines.Add("");
        if (bestCaseDeltaV > 0.0)
        {
            _lines.Add($"Required Impact Velocity (KE): {requiredImpactVelocity:N0} m/s");
            _lines.Add($"Required Launch Velocity (best-case Δv if spent on Impulse): {requiredLaunchVelocityBestCase:N0} - {maxVelocity:N0} m/s");
        }
        else
        {
            _lines.Add($"Required Velocity Range: {requiredImpactVelocity:N0} - {maxVelocity:N0} m/s");
        }

        _lines.Add("");
        _lines.Add("=== FIRE CONTROL TOOLS (scroll & select) ===");
        _lines.Add("");

        _toolListFirstLineIndex = _lines.Count;

        string Cursor(int idx) => _selectedToolIndex == idx ? ">" : " ";
        _lines.Add($"{Cursor(0)} Motion Computer     - Calculate target motion");
        _lines.Add($"{Cursor(1)} Ballistics Tables   - Charts for quick lookups");
        _lines.Add($"{Cursor(2)} Trajectory Plotter  - Calculate projectile trajectory");
        _lines.Add($"{Cursor(3)} Fire Simulator      - Simulate a full firing attempt");
        _lines.Add($"{Cursor(4)} Commit              - Enter final inputs and fire");
    }

    private void EnsureSelectedToolVisible(int viewportHeight)
    {
        if (_toolListFirstLineIndex < 0)
            return;

        int selectedLine = _toolListFirstLineIndex + Math.Clamp(_selectedToolIndex, 0, 4);
        int maxScroll = Math.Max(0, _lines.Count - Math.Max(0, viewportHeight));

        if (selectedLine < _scroll)
            _scroll = selectedLine;
        else if (selectedLine >= _scroll + viewportHeight)
            _scroll = selectedLine - viewportHeight + 1;

        _scroll = Math.Clamp(_scroll, 0, maxScroll);
    }

    protected override void RenderBody(UiContext ui)
    {
        if (_lines.Count == 0)
            BuildLines(ui);

        int viewport = ui.ContentViewportHeight > 0 ? ui.ContentViewportHeight : 18;

        if (_followSelection)
            EnsureSelectedToolVisible(viewport);

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

        if (key.Key == ConsoleKey.B)
            return PageResult.Back();

        if (key.Key == ConsoleKey.G)
            return PageResult.Go(PageId.DetailedWeaponStatus);

        switch (key.Key)
        {
            case ConsoleKey.PageUp:
                _followSelection = false;
                _scroll -= pageStep;
                return PageResult.Stay;
            case ConsoleKey.PageDown:
                _followSelection = false;
                _scroll += pageStep;
                return PageResult.Stay;
        }

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhasePage requires GameState).");
        var firingResult = _firingResult ?? throw new InvalidOperationException("Firing result was not computed. (Expected OnEnter to run.)");

        if (!firingResult.CanReachTarget || game.CurrentWave == null || game.CurrentFiringProblem == null)
        {
            game.IsGameOver = true;
            return PageResult.Exit;
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            if (_selectedToolIndex > 0)
            {
                _followSelection = true;
                _selectedToolIndex = Math.Max(0, _selectedToolIndex - 1);
                BuildLines(ui);
            }
            else
            {
                // At top of tool list: UpArrow scrolls page up.
                _followSelection = false;
                _scroll -= lineStep;
            }
            return PageResult.Stay;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            if (_selectedToolIndex < 4)
            {
                _followSelection = true;
                _selectedToolIndex = Math.Min(4, _selectedToolIndex + 1);
                BuildLines(ui);
            }
            else
            {
                // At bottom of tool list: DownArrow scrolls page down.
                _followSelection = false;
                _scroll += lineStep;
            }
            return PageResult.Stay;
        }

        if (key.Key != ConsoleKey.Enter)
            return PageResult.Stay;

        switch (_selectedToolIndex)
        {
            case 0: Action = FiringMenuAction.MotionComputer; return PageResult.Exit;
            case 1: Action = FiringMenuAction.BallisticsTables; return PageResult.Exit;
            case 2: Action = FiringMenuAction.TrajectoryPlotter; return PageResult.Exit;
            case 3: Action = FiringMenuAction.FireSimulator; return PageResult.Exit;
            case 4: Action = FiringMenuAction.Commit; return PageResult.Exit;
        }

        return PageResult.Stay;
    }
}
