namespace Spacegun_Simulator.UI.Flows;

using Spacegun_Simulator.UI.Pages.Development;
using Spacegun_Simulator.Core;

/// <summary>
/// Centralizes phase progression decisions that were previously spread across pages.
/// Keeps pages focused on UI and returns (PageResult), while the session flow owns phase advancement.
/// </summary>
internal static class GamePhaseRouter
{
    public static void ApplyAfterFiringCommit(UiContext ui, CommitFiringOutcome outcome)
    {
        if (ui == null) throw new ArgumentNullException(nameof(ui));
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null.");

        if (ui.RequestExitGame || ui.RequestReturnToMenu || game.IsGameOver)
            return;

        switch (outcome)
        {
            case CommitFiringOutcome.Hit:
                GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.FiringResolvedHit);
                game.AutoSaveGame();
                break;

            case CommitFiringOutcome.Miss:
                GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.FiringResolvedMiss);
                break;

            case CommitFiringOutcome.None:
            default:
                break;
        }
    }

    public static void ApplyAfterDevelopmentRun(UiContext ui, DevelopmentPage developmentPage)
    {
        if (ui == null) throw new ArgumentNullException(nameof(ui));
        if (developmentPage == null) throw new ArgumentNullException(nameof(developmentPage));
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null.");

        if (ui.RequestExitGame || ui.RequestReturnToMenu || game.IsGameOver)
            return;

        if (developmentPage.Action == DevelopmentPage.DevelopmentMenuAction.Done)
        {
            GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.DevelopmentCompleted);
            // Development is where the player commits upgrades and projectile choices.
            // Persist immediately so Resume doesn't rewind to pre-development.
            game.AutoSaveGame();
        }
    }

    public static void ApplyAfterPhaseControllerRun(UiContext ui, GameState.GamePhase phaseThatWasRunning)
    {
        if (ui == null) throw new ArgumentNullException(nameof(ui));
        var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null.");

        // Never advance gameplay if the player is exiting, returning to menu, or the run ended.
        if (ui.RequestExitGame || ui.RequestReturnToMenu || game.IsGameOver)
            return;

        // If the controller didn't record anything, don't guess.
        if (ui.LastPageId == null || ui.LastPageResult == null)
            return;

        switch (phaseThatWasRunning)
        {
            case GameState.GamePhase.ResourceAllocation:
                // The resource phase is considered complete when the player proceeds past the summary.
                if (ui.LastPageId == PageId.PreparationSummary && ui.LastPageResult.Value.ExitRequested)
                {
                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.ResourcePhaseCompleted);
                    game.AutoSaveGame();
                }
                break;

            case GameState.GamePhase.WaveComplete:
                // Wave complete is acknowledged when leaving the phase page.
                if (ui.LastPageId == PageId.WaveComplete && ui.LastPageResult.Value.ExitRequested)
                {
                    GamePhaseTransitionRules.Apply(game, GamePhaseTransitionRules.PhaseEvent.WaveCompleteAcknowledged);
                }
                break;

            default:
                break;
        }
    }
}
