using System;
using System.Collections.Generic;
using Spacegun_Simulator;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Pages;
using Spacegun_Simulator.UI.Theme;

public sealed class WaveCompletePage : PageBase
{
    public override string Id => PageId.WaveComplete;
    public override string Title => "WAVE COMPLETE";

    public override PageChrome Chrome { get; } = new(
        ShowStatusBar: true,
        ShowSidePanels: true,
        FooterHint: "Any key=Continue   Esc=Back/Menu   Q=Quit"
    );

    private readonly List<string> _lines = new();
    private int _scroll;

    public override void OnEnter(UiContext ui)
    {
        BuildLines(ui);
        _scroll = 0;
    }

    private void BuildLines(UiContext ui)
    {
        _lines.Clear();

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (WaveCompletePage requires GameState).");

        _lines.Add($"Wave {game.CurrentWaveNumber} complete.");
        _lines.Add($"Waves defeated: {game.WavesDefeated}/{GameConstants.TotalWaves}");
        _lines.Add("");

        if (game.WavesDefeated >= GameConstants.TotalWaves)
        {
            _lines.Add("✓ Campaign complete.");
            _lines.Add("Press any key to proceed to Game Over.");
        }
        else
        {
            _lines.Add("Preparing next contact...");
            _lines.Add("Press any key to proceed to Detection.");
        }

        _lines.Add("");
        _lines.Add("Tip: Use Test Mode → UI Page Launcher to jump between pages.");
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
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: _scroll -= 1; return PageResult.Stay;
            case ConsoleKey.DownArrow: _scroll += 1; return PageResult.Stay;
            case ConsoleKey.PageUp: _scroll -= 6; return PageResult.Stay;
            case ConsoleKey.PageDown: _scroll += 6; return PageResult.Stay;
        }

        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (WaveCompletePage requires GameState).");

        // Safety: if campaign is complete, transition to game over instead of advancing beyond total waves.
        if (game.WavesDefeated >= GameConstants.TotalWaves)
        {
            game.IsGameOver = true;
            return PageResult.Exit;
        }

        game.AdvanceToNextWave();
        return PageResult.Exit;
    }
}
