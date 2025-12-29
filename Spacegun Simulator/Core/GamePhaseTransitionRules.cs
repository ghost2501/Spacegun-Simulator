namespace Spacegun_Simulator.Core;

/// <summary>
/// Domain-level phase transition rules.
///
/// This centralizes when and how the game advances between phases.
/// UI pages and flows should prefer calling these rules rather than mutating
/// <see cref="GameState.CurrentPhase"/> directly.
/// </summary>
public static class GamePhaseTransitionRules
{
    public enum PhaseEvent
    {
        DetectionResolvedProceedToResourceAllocation = 1,
        DetectionResolvedSkipResourcePhases = 2,

        ResourcePhaseCompleted = 3,
        DevelopmentCompleted = 4,
        WaveCompleteAcknowledged = 5,
        FiringResolvedHit = 6,
        FiringResolvedMiss = 7,
    }

    public static void Apply(GameState game, PhaseEvent phaseEvent)
    {
        if (game == null) throw new ArgumentNullException(nameof(game));

        switch (phaseEvent)
        {
            case PhaseEvent.DetectionResolvedProceedToResourceAllocation:
                game.CurrentPhase = GameState.GamePhase.ResourceAllocation;
                break;

            case PhaseEvent.DetectionResolvedSkipResourcePhases:
                game.CurrentPhase = GameState.GamePhase.Firing;
                break;

            case PhaseEvent.ResourcePhaseCompleted:
                game.CurrentPhase = GameState.GamePhase.Development;
                break;

            case PhaseEvent.DevelopmentCompleted:
                game.CurrentPhase = GameState.GamePhase.Firing;
                break;

            case PhaseEvent.WaveCompleteAcknowledged:
                if (game.WavesDefeated >= GameConstants.TotalWaves)
                {
                    game.IsGameOver = true;
                    return;
                }

                game.AdvanceToNextWave();
                break;

            case PhaseEvent.FiringResolvedHit:
                game.WavesDefeated++;
                game.CurrentPhase = GameState.GamePhase.WaveComplete;
                break;

            case PhaseEvent.FiringResolvedMiss:
                game.IsGameOver = true;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(phaseEvent), phaseEvent, "Unknown phase event.");
        }
    }
}
