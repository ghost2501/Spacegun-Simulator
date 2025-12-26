using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

public sealed class DetectionPage : PageBase
{
    public override string Id => PageId.Detection;
    public override string Title => "";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true
        // FooterHint: ""
    );

    private GameState.DetectionPhaseResult? _result;
    private DifficultyConfig? _diff;

    private readonly List<string> _lines = new();
    private int _scroll;

    public override void OnEnter(UiContext ui)
    {
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DetectionPage requires GameState).");

        _diff = DifficultyConfig.GetConfig(game.SelectedDifficulty);
        _result = game.ExecuteDetectionPhase();

        BuildLines(ui);
        _scroll = 0;
    }

    private void BuildLines(UiContext ui)
    {
        _lines.Clear();

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DetectionPage requires GameState).");
        var result = _result ?? throw new InvalidOperationException("Detection result was not computed. (Expected OnEnter to run.)");
        var diff = _diff ?? DifficultyConfig.GetConfig(game.SelectedDifficulty);

        _lines.Add("               THREAT DETECTED!                              ");
        _lines.Add($"               Wave {game.CurrentWaveNumber} of {GameConstants.TotalWaves}".PadRight(57) + " ");
        _lines.Add("");

        _lines.Add("=== DETECTION PHASE ===");
        _lines.Add("");
        _lines.Add(result.Message);

        if (!result.WaveDetected)
        {
            _lines.Add("");
            _lines.Add("✗ MISSION FAILED");
            return;
        }

        var archetype = result.Wave.Archetype;

        _lines.Add("");
        _lines.Add("=== THREAT ARCHETYPE ===");
        _lines.Add($"Class: {archetype.Name}");
        _lines.Add($"Description: {archetype.Description}");
        _lines.Add("");

        _lines.Add("=== BALLISTIC REQUIREMENTS ===");
        _lines.Add($"Enemy Mass Range: {archetype.MassRange.Min:N0} - {archetype.MassRange.Max:N0} metric tons");
        _lines.Add($"Required Fracture Energy Range: {archetype.FractureEnergyRange.Min:N0} - {archetype.FractureEnergyRange.Max:N0} MJ");
        _lines.Add($"Difficulty: {DifficultyText.DescribeStars(archetype.BaseDifficultyRating)}");
        _lines.Add("");

        _lines.Add("=== ENEMY PROFILE ===");
        _lines.Add($"Type: {result.Wave.Targets[0].Name}");
        _lines.Add($"Detection Distance: {GameConstants.FormatDistance(result.Wave.CurrentDistance)}");
        _lines.Add($"Velocity: {GameConstants.FormatVelocity(result.Wave.AverageVelocity)}");

        if (diff.IsTutorialMode)
        {
            _lines.Add($"Radar Cross-Section: {DifficultyConfig.TutorialBeachball.CrossSectionM2:F2} m² (beachball)");
        }
        else
        {
            double displayRcs = result.Wave.AverageRadarCrossSection * diff.TargetRcsMultiplier;
            _lines.Add($"Radar Cross-Section: {displayRcs:F1} m²");
        }

        _lines.Add("");
        _lines.Add("=== TIME BUDGET ===");
        _lines.Add($"Years Available: {result.AvailableYears} years");

        _lines.Add("");
        _lines.Add("=== CURRENT RESOURCES ===");
        _lines.Add($"Budget: {game.Resources.Budget:F0}");
        _lines.Add($"Steel: {game.Resources.Steel:F0} tons");
        _lines.Add($"Exotic Materials: {game.Resources.ExoticMaterials:F1} units");
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

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (DetectionPage requires GameState).");
        var result = _result ?? throw new InvalidOperationException("Detection result was not computed. (Expected OnEnter to run.)");
        var diff = _diff ?? DifficultyConfig.GetConfig(game.SelectedDifficulty);

        if (!result.WaveDetected)
        {
            game.IsGameOver = true;
            return PageResult.Exit;
        }

        game.CurrentPhase = diff.SkipResourcePhases
            ? GameState.GamePhase.Firing
            : GameState.GamePhase.ResourceAllocation;

        return PageResult.Exit;
    }
}
