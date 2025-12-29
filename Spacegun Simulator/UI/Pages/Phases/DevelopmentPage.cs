using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

public sealed class DevelopmentPage : PageBase
{
    public override string Id => PageId.WeaponDevelopment;
    public override string Title => "WEAPON DEVELOPMENT";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "P=Projectile  G=Gun  S=Status  D=Done   ↑/↓/PgUp/PgDn=Scroll  Esc=Menu  Q=Quit"
    );

    public enum DevelopmentMenuAction
    {
        None,
        Projectile,
        Gun,
        Status,
        Done
    }

    public DevelopmentMenuAction Action { get; private set; } = DevelopmentMenuAction.None;

    private readonly List<string> _lines = new();
    private int _scroll;
    private string _message = string.Empty;

    public override void OnEnter(UiContext ui)
    {
        Action = DevelopmentMenuAction.None;
        _scroll = 0;
        _message = string.Empty;
        BuildLines(ui);
    }

    private void BuildLines(UiContext ui)
    {
        _lines.Clear();

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DevelopmentPage requires GameState).");

        _lines.Add("=== AVAILABLE RESOURCES ===");
        _lines.Add($"  Budget: {game.AccumulatedResources["Budget"]:F0}");
        _lines.Add($"  Steel:  {game.AccumulatedResources["Steel"]:F0} tons");
        _lines.Add($"  Exotic: {game.AccumulatedResources["Exotic"]:F1} units");
        _lines.Add("");

        if (!string.IsNullOrWhiteSpace(_message))
        {
            _lines.Add(Clamp60(_message));
            _lines.Add("");
        }

        if (game.CurrentWave?.Archetype != null)
        {
            var archetype = game.CurrentWave.Archetype;
            _lines.Add("=== TARGET REQUIREMENT ===");
            _lines.Add($"  Archetype: {archetype.Name}");
            _lines.Add($"  Fracture Energy Needed: {archetype.FractureEnergyRange.Min:N0} - {archetype.FractureEnergyRange.Max:N0} MJ");
            _lines.Add($"  Mass: {archetype.MassRange.Min:N0} - {archetype.MassRange.Max:N0} metric tons");
            _lines.Add($"  Difficulty: {BallisticsCalculator.GetDifficultyDescription(archetype.BaseDifficultyRating)}");
            _lines.Add("");
        }

        _lines.Add("=== CURRENT WEAPON TECHNOLOGY ===");
        _lines.Add($"  Weapons Tech:     Level {game.TechTree.CurrentLevel[TechTree.TechType.Weapons]} - {TechTree.GetTechDescription(TechTree.TechType.Weapons, game.TechTree.CurrentLevel[TechTree.TechType.Weapons])}");
        _lines.Add($"  Projectiles Tech: Level {game.TechTree.CurrentLevel[TechTree.TechType.Projectiles]} - {TechTree.GetTechDescription(TechTree.TechType.Projectiles, game.TechTree.CurrentLevel[TechTree.TechType.Projectiles])}");
        _lines.Add("");

        _lines.Add("=== CURRENT WEAPON CONFIGURATION ===");
        if (game.CraftedProjectile != null)
        {
            var proj = game.CraftedProjectile;
            _lines.Add($"  Projectile: {proj.DisplayName}");
            _lines.Add($"  Mass: {proj.MassKg} kg | Velocity: {proj.MaxVelocityMs:N0} m/s");
            _lines.Add($"  Kinetic Energy: {proj.EffectiveKineticEnergyMJ:N0} MJ");
            if (proj.HitToleranceMultiplier != 1.0)
                _lines.Add($"  Hit Tolerance Bonus: {(proj.HitToleranceMultiplier - 1) * 100:+0}%");
        }
        else
        {
            _lines.Add("  Projectile: [NOT CONFIGURED]");
            _lines.Add("  ⚠ You must develop a projectile before firing!");
        }

        _lines.Add("");
        _lines.Add("  Gun Configuration:");
        _lines.Add($"    Barrel Integrity: {game.Gun.BarrelIntegrity:P0}");
        _lines.Add($"    Power Capacity: {game.Gun.PowerCapacity:F0} MW");
        _lines.Add($"    Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(game.CurrentWaveNumber).MaxEffectiveGunRange)}");
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
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DevelopmentPage requires GameState).");

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
            case ConsoleKey.P:
                _message = string.Empty;
                // Navigate within the current UiController.
                return PageResult.Go(PageId.ProjectileDevelopment);

            case ConsoleKey.G:
                _message = string.Empty;
                // Navigate within the current UiController.
                return PageResult.Go(PageId.GunDevelopment);

            case ConsoleKey.S:
                _message = string.Empty;
                // Navigate within the current UiController so launcher + real game
                // don't "fall out" to their outer loops after a single controller.Run().
                return PageResult.Go(PageId.DetailedWeaponStatus);

            case ConsoleKey.D:
                if (game.CraftedProjectile != null)
                {
                    Action = DevelopmentMenuAction.Done;
                    return PageResult.Exit;
                }

                _message = "✗ Cannot proceed: craft a projectile first (press P).";
                BuildLines(ui);
                return PageResult.Stay;
        }

        return PageResult.Stay;
    }
}
