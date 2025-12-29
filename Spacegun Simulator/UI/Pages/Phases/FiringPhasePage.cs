using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

public sealed class FiringPhasePage : PageBase
{
    public override string Id => PageId.Firing;
    public override string Title => "FIRING SOLUTION";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "1=Motion  2=Tables  3=Trajectory  4=Sim  5=Commit   Esc=Menu  Q=Quit   ↑/↓/PgUp/PgDn=Scroll"
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

    private GameState.FiringPhaseResult? _firingResult;
    private DifficultyConfig? _diff;

    public override void OnEnter(UiContext ui)
    {
        Action = FiringMenuAction.None;
        _scroll = 0;

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhasePage requires GameState).");
        _diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
        _firingResult = game.ExecuteFiringPhase();

        BuildLines(ui);
    }

    private void BuildLines(UiContext ui)
    {
        _lines.Clear();

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

        if (game.SelectedGunProjectileSpec == null)
        {
            _lines.Add("✗ Critical error: No weapon selected!");
            _lines.Add("");
            _lines.Add("Press any key...");
            return;
        }

        double muzzleVelocity = game.SelectedGunProjectileSpec.MuzzleVelocityMs;
        double projectileMass = game.SelectedGunProjectileSpec.ProjectileMassKg;

        var calculator = new FiringSolution(
            (float)projectileMass,
            (float)target.FractureEnergy,
            target.Mass);

        float minVelocity = calculator.CalculateRequiredVelocity();
        float maxVelocity = (float)muzzleVelocity;

        double displayRcs = target.CrossSection * diff.TargetRcsMultiplier;

        _lines.Add("=== YOUR WEAPON ===");
        _lines.Add($"Projectile Mass: {FiringPhaseFormatter.FormatMass(projectileMass, game.SelectedDifficulty)} kg");
        _lines.Add($"Max Muzzle Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, game.SelectedDifficulty)} m/s");
        _lines.Add($"Barrel Integrity: {game.Gun.BarrelIntegrity:P2}");
        _lines.Add($"Has Guidance System: {(game.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}");
        _lines.Add($"Gun Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(game.CurrentWaveNumber).MaxEffectiveGunRange)}");
        _lines.Add("");

        _lines.Add("=== TARGET DATA FOR CALCULATIONS ===");
        _lines.Add($"Designation: {target.Name}");
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
        _lines.Add($"Required Velocity Range: {minVelocity:N0} - {maxVelocity:N0} m/s");

        _lines.Add("");
        _lines.Add("=== FIRE CONTROL TOOLS ===");
        _lines.Add("");
        _lines.Add("Motion Computer: Calculate target motion.");
        _lines.Add("Ballistics Tables: Charts for quick lookups.");
        _lines.Add("Trajectory Plotter: Calculate projectile trajectory.");
        _lines.Add("Fire Simulator: Simulate a full firing attempt.");
        _lines.Add("Commit: Enter final inputs and fire.");
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

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhasePage requires GameState).");
        var firingResult = _firingResult ?? throw new InvalidOperationException("Firing result was not computed. (Expected OnEnter to run.)");

        if (!firingResult.CanReachTarget || game.CurrentWave == null || game.CurrentFiringProblem == null || game.SelectedGunProjectileSpec == null)
        {
            game.IsGameOver = true;
            return PageResult.Exit;
        }

        switch (key.Key)
        {
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                Action = FiringMenuAction.MotionComputer;
                return PageResult.Exit;

            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                Action = FiringMenuAction.BallisticsTables;
                return PageResult.Exit;

            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                Action = FiringMenuAction.TrajectoryPlotter;
                return PageResult.Exit;

            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                Action = FiringMenuAction.FireSimulator;
                return PageResult.Exit;

            case ConsoleKey.D5:
            case ConsoleKey.NumPad5:
                Action = FiringMenuAction.Commit;
                return PageResult.Exit;

        }

        return PageResult.Stay;
    }
}
