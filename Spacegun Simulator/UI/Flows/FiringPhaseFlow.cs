using Spacegun_Simulator.UI.Pages.FireControl;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Flows
{
    public static class FiringPhaseFlow
    {
        public static bool Run(UiContext ui, bool propagateSessionExitFromTools = true)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            var game = ui.Game ?? throw new InvalidOperationException("UiContext.Game is null (FiringPhaseFlow requires GameState).");

            bool workflowComplete = false;

            while (!workflowComplete && !game.IsGameOver && !ui.RequestReturnToMenu && !ui.RequestExitGame)
            {
                var page = new FiringPhasePage();
                var controller = new UiController(ui, PageId.Firing);
                controller.Register(page);
                controller.Run();

                if (ui.RequestExitGame || ui.RequestReturnToMenu || game.IsGameOver)
                    break;

                // If the player already fired and advanced phase, exit the firing loop.
                if (game.CurrentPhase != GameState.GamePhase.Firing)
                {
                    workflowComplete = true;
                    continue;
                }

                var diffConfig = DifficultyConfig.GetConfig(game.SelectedDifficulty);
                var firingProblem = game.CurrentFiringProblem;
                var target = game.CurrentWave?.Targets.Count > 0 ? game.CurrentWave.Targets[0] : null;

                if (firingProblem == null || target == null || game.SelectedGunProjectileSpec == null)
                {
                    game.IsGameOver = true;
                    break;
                }

                var resolved = game.ResolveShotStats(target);

                var calculator = new FiringSolution(
                    (float)resolved.ProjectileMassKg,
                    (float)resolved.EffectiveFractureEnergyMJ,
                    target.Mass);
                calculator.ConfigureProjectileModifiers(resolved);

                float maxVelocity = (float)resolved.MaxLaunchVelocityMs;
                if (diffConfig.IsTutorialMode)
                    maxVelocity = (float)Math.Min(maxVelocity, DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs);
                double displayRcs = target.CrossSection * diffConfig.TargetRcsMultiplier;

                switch (page.Action)
                {
                    case FiringPhasePage.FiringMenuAction.MotionComputer:
                        RunFireControlTool(ui, PageId.MotionComputer, propagateSessionExitFromTools);
                        break;

                    case FiringPhasePage.FiringMenuAction.BallisticsTables:
                        RunFireControlTool(ui, PageId.BallisticsTables, propagateSessionExitFromTools);
                        break;

                    case FiringPhasePage.FiringMenuAction.TrajectoryPlotter:
                        RunFireControlTool(ui, PageId.TrajectoryPlotter, propagateSessionExitFromTools);
                        break;

                    case FiringPhasePage.FiringMenuAction.FireSimulator:
                        RunFireControlTool(ui, PageId.FireSimulator, propagateSessionExitFromTools);
                        break;

                    case FiringPhasePage.FiringMenuAction.Commit:
                        {
                            var result = CommitFiringSolutionFlow.Run(
                                screenLayout: ui.Layout,
                                originalConsoleOut: ui.OriginalOut,
                                indentWriter: ui.IndentWriter,
                                globalIndent: ui.GlobalIndent,
                                game: game,
                                firingProblem: firingProblem,
                                target: target,
                                calculator: calculator,
                                maxVelocity: maxVelocity,
                                displayRcs: displayRcs);

                            if (result.RequestExitGame)
                                ui.RequestExitGame = true;

                            if (result.RequestReturnToMenu)
                                ui.RequestReturnToMenu = true;

                            GamePhaseRouter.ApplyAfterFiringCommit(ui, result.Outcome);

                            workflowComplete = true;
                        }
                        break;

                    default:
                        break;
                }
            }

            return workflowComplete;
        }

        private static void RunFireControlTool(UiContext ui, string startPageId, bool propagateSessionExitFromTools)
        {
            var controller = new UiController(ui, startPageId);
            PageCatalog.RegisterFireControlTools(controller);
            controller.Run();

            if (!propagateSessionExitFromTools)
            {
                // Used by diagnostics: keep tools from escaping the diagnostics UI.
                ui.RequestReturnToMenu = false;
                ui.RequestExitGame = false;
            }
        }
    }
}
