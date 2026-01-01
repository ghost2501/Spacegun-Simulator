using Spacegun_Simulator.UI.Pages.FireControl;
using Spacegun_Simulator.UI.Screen;
using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.UI.Flows
{
    public enum CommitFiringOutcome
    {
        None = 0,
        Hit = 1,
        Miss = 2
    }

    public readonly record struct CommitFiringSolutionFlowResult(
        bool RequestExitGame,
        bool RequestReturnToMenu,
        CommitFiringOutcome Outcome
    );

    public static class CommitFiringSolutionFlow
    {
        public static CommitFiringSolutionFlowResult Run(
            ScreenLayout screenLayout,
            TextWriter? originalConsoleOut,
            TextWriter indentWriter,
            int globalIndent,
            GameState game,
            FiringProblem firingProblem,
            EnemyTarget target,
            FiringSolution calculator,
            float maxVelocity,
            double displayRcs)
        {
            if (screenLayout == null) throw new ArgumentNullException(nameof(screenLayout));
            if (indentWriter == null) throw new ArgumentNullException(nameof(indentWriter));
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (firingProblem == null) throw new ArgumentNullException(nameof(firingProblem));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));

            var resolved = game.ResolveShotStats(target);
            bool hasDeltaV = resolved.PropulsionDeltaVCapacityMs > 0.0;

            // Step 1: Collect firing parameters via page-based UI (no Console.ReadLine).
            var ui = new UiContext(
                layout: screenLayout,
                originalOut: originalConsoleOut ?? Console.Out,
                indentWriter: indentWriter,
                globalIndent: globalIndent)
            {
                Game = game,
                DebugEnabled = false
            };

            var diffConfigForInput = DifficultyConfig.GetConfig(game.SelectedDifficulty);
            if (diffConfigForInput.IsTutorialMode)
                maxVelocity = (float)Math.Min(maxVelocity, DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs);
            double effectiveDeltaVAvailableMs = hasDeltaV
                ? resolved.PropulsionDeltaVCapacityMs * ComputeMassEfficiency(resolved)
                : 0.0;
            double baseHitToleranceMeters = ComputeBaseHitToleranceMeters(
                diffConfigForInput,
                enemyMass: target.Mass,
                additionalHitToleranceMultiplier: resolved.AdditionalHitToleranceMultiplier);

            var paramPage = new EnterFiringParametersPage(
                maxVelocity,
                enableDeltaVAllocation: hasDeltaV,
                effectiveDeltaVAvailableMs: effectiveDeltaVAvailableMs,
                baseHitToleranceMeters: baseHitToleranceMeters,
                baseDefenseRating: resolved.ProjectileDefenseRating,
                projectileMassKg: resolved.ProjectileMassKg);
            var controller = new UiController(ui, PageId.EnterFiringParameters);
            controller.Register(paramPage);
            controller.Run();

            if (ui.RequestExitGame)
                return new CommitFiringSolutionFlowResult(RequestExitGame: true, RequestReturnToMenu: false, Outcome: CommitFiringOutcome.None);

            if (ui.RequestReturnToMenu)
                return new CommitFiringSolutionFlowResult(RequestExitGame: false, RequestReturnToMenu: true, Outcome: CommitFiringOutcome.None);

            if (!paramPage.Submitted)
                return default;

            float playerLaunchDelayTime = (float)paramPage.LaunchDelaySeconds;
            float playerTargetElevation = (float)paramPage.TargetElevationDegrees;
            float playerTargetAzimuth = (float)paramPage.TargetAzimuthDegrees;
            float playerLaunchVelocity = (float)paramPage.LaunchVelocityMs;

            int impulsePct = hasDeltaV ? paramPage.DeltaVImpulsePercent : 100;
            int controlPct = hasDeltaV ? paramPage.DeltaVControlPercent : 0;
            int dodgePct = hasDeltaV ? paramPage.DeltaVDodgePercent : 0;

            double impulseFrac = Math.Clamp(impulsePct / 100.0, 0.0, 1.0);
            double controlFrac = Math.Clamp(controlPct / 100.0, 0.0, 1.0);
            double dodgeFrac = Math.Clamp(dodgePct / 100.0, 0.0, 1.0);

            double effectiveControlDeltaV = effectiveDeltaVAvailableMs * controlFrac;
            double controlBonus = ComputeControlGuidanceBonus(effectiveControlDeltaV);

            // Apply the player's Δv split for this shot.
            // - Impulse: feeds the solver's impact KE via propulsion delta-v.
            // - Control: improves hit tolerance in the solver (and may also improve Guidance below).
            var allocated = resolved with
            {
                PropulsionDeltaVCapacityMs = resolved.PropulsionDeltaVCapacityMs * impulseFrac,
                AdditionalHitToleranceMultiplier = resolved.AdditionalHitToleranceMultiplier * (1.0 + controlBonus)
            };
            calculator.ConfigureProjectileModifiers(allocated);

            var header = new List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               COMMIT FIRING SOLUTION                        ",
                string.Empty
            };

            // Step 2: Render the commit result in a framed/buffered view.
            int contentLeftNoOffset;
            int promptRowNoOffset;
            {
                const string pageKey = PageId.CommitFiringSolution;
                var (left, right) = PageArtOverrides.Get(pageKey);

                (contentLeftNoOffset, int contentTop) = screenLayout.BeginBufferedFrame(
                    header,
                    originalConsoleOut,
                    indentWriter,
                    left,
                    right);

                try
                {
                    var diffConfig = DifficultyConfig.GetConfig(game.SelectedDifficulty);
                    Console.WriteLine("=== FIRING PARAMETERS ===");
                    Console.WriteLine($"Launch delay: {diffConfig.FormatLaunchDelay(playerLaunchDelayTime)}");
                    Console.WriteLine($"Elevation: {diffConfig.FormatElevation(playerTargetElevation)}");
                    Console.WriteLine($"Azimuth: {diffConfig.FormatAzimuth(playerTargetAzimuth)}");
                    Console.WriteLine($"Velocity: {diffConfig.FormatVelocity(playerLaunchVelocity)}");
                    if (hasDeltaV)
                        Console.WriteLine($"Δv split: Impulse {impulsePct}% / Control {controlPct}% / Dodge {dodgePct}%");
                    Console.WriteLine();
                }
                finally
                {
                    promptRowNoOffset = screenLayout.EndBufferedFrame(contentLeftNoOffset, contentTop);
                }
            }

            static double ComputeMassEfficiency(in ResolvedShotStats stats)
            {
                double massKg = Math.Max(0.0, stats.ProjectileMassKg);
                double refMassKg = Math.Max(0.01, stats.PropulsionReferenceMassKg);
                return refMassKg / (refMassKg + massKg);
            }

            static double ComputeBaseHitToleranceMeters(
                DifficultyConfig diffConfig,
                double enemyMass,
                double additionalHitToleranceMultiplier)
            {
                // Mirrors FiringSolution.CalculateHitTolerance.
                if (diffConfig.IsTutorialMode)
                    return DifficultyConfig.TutorialBeachball.RadiusMeters;

                double diameterM = BallisticsCalculator.CalculateDiameterFromMass(enemyMass);

                if (diffConfig.TargetRcsMultiplier > 1.0)
                {
                    double rcsLinear = Math.Sqrt(diffConfig.TargetRcsMultiplier);
                    diameterM *= rcsLinear;
                }

                double baseTolerance = diameterM * 0.5;
                return baseTolerance * diffConfig.HitToleranceMultiplier * additionalHitToleranceMultiplier;
            }

            static double ComputeControlGuidanceBonus(double effectiveDeltaVMs)
            {
                // Diminishing returns, tuned conservatively.
                // 0 m/s => +0.00, 2000 m/s => ~+0.38, 5000 m/s => ~+0.62; cap at +0.75.
                if (effectiveDeltaVMs <= 0.0) return 0.0;
                double x = Math.Clamp(effectiveDeltaVMs / 2000.0, 0.0, 20.0);
                return Math.Clamp(0.75 * (1.0 - Math.Exp(-x)), 0.0, 0.75);
            }

            static double ComputeDodgeDefenseBonus(double effectiveDeltaVMs)
            {
                // Diminishing returns; cap at +0.25 defense.
                if (effectiveDeltaVMs <= 0.0) return 0.0;
                double x = Math.Clamp(effectiveDeltaVMs / 2500.0, 0.0, 20.0);
                return Math.Clamp(0.25 * (1.0 - Math.Exp(-x)), 0.0, 0.25);
            }

            var rawOut = originalConsoleOut ?? Console.Out;
            var frameOut = FramedPrompts.CreateFrameWriter(rawOut, contentLeftNoOffset);

            // From this point onward, keep output anchored to the frame-left.
            FramedPrompts.Anchor(frameOut, contentLeftNoOffset, promptRowNoOffset);

            if (game.SelectedGunProjectileSpec == null)
            {
                game.IsGameOver = true;
                return default;
            }

            var solution = calculator.CalculateSolution(
                firingProblem.EnemyPosition,
                firingProblem.EnemyVelocity,
                playerLaunchDelayTime,
                playerTargetElevation,
                playerTargetAzimuth,
                playerLaunchVelocity,
                maxVelocity,
                (float)game.GetCurrentEffectiveGunRangeMeters(),
                game.CurrentWaveNumber,
                target.Mass,
                game.SelectedDifficulty);

            DisplayFiringAnalysis(solution, playerLaunchDelayTime, playerTargetElevation, playerTargetAzimuth, playerLaunchVelocity);

            Console.WriteLine("Firing...\n");
            System.Threading.Thread.Sleep(1000);

            bool hitResult = solution.CanDestroy && solution.CanHit;

            // Full-mode maneuverability: apply a terminal evasion chance, countered by Guidance.
            bool evaded = false;
            if (hitResult && target.Maneuverability > 0.0)
            {
                double maneuver = Math.Clamp(target.Maneuverability, 0.0, 1.0);
                bool hasGuidanceMod =
                    (game.CraftedProjectile?.Enhancement?.Id == "guidance")
                    || (game.Gun?.DefaultProjectile?.HasGuidance ?? false);

                // Guidance is a projectile-linked capability. If the guidance mod isn't installed,
                // Guidance stays at baseline (1.0) regardless of any gun-side tuning/upgrades.
                double guidance = hasGuidanceMod
                    ? Math.Max(0.0, game.Gun?.Guidance ?? 1.0)
                    : 1.0;

                if (hasDeltaV && hasGuidanceMod && controlPct > 0)
                    guidance *= (1.0 + controlBonus);

                double lockBreakChance = CombatCurves.ComputeEvasionChance(maneuver, guidance);

                var evasionRng = game.CreateFiringRng("Evasion");
                evaded = evasionRng.NextDouble() < lockBreakChance;
                if (evaded)
                    hitResult = false;
            }

            // Full-mode offense vs projectile defense: target may destroy the projectile before impact.
            bool projectileIntercepted = false;
            if (hitResult && target.Offense > 0.0)
            {
                double offense = Math.Clamp(target.Offense, 0.0, 1.0);
                double defense = Math.Clamp(resolved.ProjectileDefenseRating, 0.0, 1.0);

                if (hasDeltaV && dodgePct > 0)
                {
                    double effectiveDodgeDeltaV = effectiveDeltaVAvailableMs * dodgeFrac;
                    defense = Math.Clamp(defense + ComputeDodgeDefenseBonus(effectiveDodgeDeltaV), 0.0, 1.0);
                }

                double killChance = CombatCurves.ComputeInterceptKillChance(offense, defense);

                var interceptRng = game.CreateFiringRng("Intercept");
                projectileIntercepted = interceptRng.NextDouble() < killChance;
                if (projectileIntercepted)
                    hitResult = false;
            }

            double animFlightTime = firingProblem.EnemyPosition.Magnitude / Math.Max(1.0, playerLaunchVelocity) * 1.5;

            // Step 3: Run visualization as a UI page (supports ESC/Q intents).
            var vizUi = new UiContext(
                layout: screenLayout,
                originalOut: originalConsoleOut ?? Console.Out,
                indentWriter: indentWriter,
                globalIndent: globalIndent)
            {
                Game = game,
                DebugEnabled = false
            };

            var vizPage = new FiringVisualizationPage(
                firingProblem.EnemyPosition,
                firingProblem.EnemyVelocity,
                playerLaunchDelayTime,
                playerTargetElevation,
                playerTargetAzimuth,
                playerLaunchVelocity,
                Math.Min(animFlightTime, 10.0),
                hitResult);

            var vizController = new UiController(vizUi, PageId.FiringVisualization);
            vizController.Register(vizPage);
            vizController.Run();

            if (vizUi.RequestExitGame)
                return new CommitFiringSolutionFlowResult(RequestExitGame: true, RequestReturnToMenu: false, Outcome: CommitFiringOutcome.None);

            if (vizUi.RequestReturnToMenu)
                return new CommitFiringSolutionFlowResult(RequestExitGame: false, RequestReturnToMenu: true, Outcome: CommitFiringOutcome.None);

            // Step 4: Build results (keeps existing game logic order), then show as a framed page.
            string? barrelLine1 = null;
            string? barrelLine2 = null;
            if (game.Gun != null)
            {
                bool barrelStillOk = game.Gun.RegisterShot();
                barrelLine1 = $"Barrel Integrity (post-shot): {game.Gun.BarrelIntegrity:P2}";
                if (!barrelStillOk)
                {
                    barrelLine2 = "✗ Barrel integrity failed after shot. The gun is unusable until repaired.";
                    game.IsGameOver = true;
                }
            }

            string outcomeLine;
            if (hitResult)
            {
                outcomeLine = "✓ DIRECT HIT! Enemy destroyed!";
            }
            else
            {
                outcomeLine = evaded
                    ? "✗ EVADED! Target maneuvered away from intercept."
                    : projectileIntercepted
                        ? "✗ INTERCEPTED! Enemy defenses destroyed the projectile."
                    : "✗ MISS! Your ballistic solution was inaccurate or lacked sufficient energy.";
            }

            var resultsLines = BuildResultsLines(
                solution,
                game.SelectedGunProjectileSpec.ProjectileMassKg,
                playerLaunchVelocity,
                displayRcs,
                game.SelectedDifficulty,
                GameModeTuning.Current.GetHitToleranceMultiplier(game.Mode),
                barrelLine1,
                barrelLine2,
                outcomeLine,
                evaded,
                projectileIntercepted);

            var resultsUi = new UiContext(
                layout: screenLayout,
                originalOut: originalConsoleOut ?? Console.Out,
                indentWriter: indentWriter,
                globalIndent: globalIndent)
            {
                Game = game,
                DebugEnabled = false
            };

            var resultsController = new UiController(resultsUi, PageId.FiringResults);
            resultsController.Register(new FiringResultsPage(resultsLines));
            resultsController.Run();

            if (resultsUi.RequestExitGame)
                return new CommitFiringSolutionFlowResult(RequestExitGame: true, RequestReturnToMenu: false, Outcome: CommitFiringOutcome.None);

            if (resultsUi.RequestReturnToMenu)
                return new CommitFiringSolutionFlowResult(RequestExitGame: false, RequestReturnToMenu: true, Outcome: CommitFiringOutcome.None);

            try { Console.SetOut(indentWriter); } catch { }

            return new CommitFiringSolutionFlowResult(
                RequestExitGame: false,
                RequestReturnToMenu: false,
                Outcome: hitResult ? CommitFiringOutcome.Hit : CommitFiringOutcome.Miss);
        }

        private static void DisplayFiringAnalysis(FiringSolutionResult solution, float delayTime, float elevation, float azimuth, float velocity)
        {
            Console.WriteLine("\n=== FIRING ANALYSIS SUMMARY ===");
            Console.WriteLine($"  Launch Delay: {delayTime:F2}s");
            Console.WriteLine($"  Elevation: {elevation:F2}°   Azimuth: {azimuth:F2}°");
            Console.WriteLine($"  Launch Velocity: {velocity:F0} m/s");
            if (solution.ImpactVelocityMs > 0)
                Console.WriteLine($"  Impact Velocity: {solution.ImpactVelocityMs:F0} m/s (flight: {solution.FlightTimeSeconds:F2}s)");
            Console.WriteLine($"  Energy Needed: {solution.FractureEnergyRequired:F0} MJ");
            Console.WriteLine($"  Can Destroy: {(solution.CanDestroy ? "Yes" : "No")}");
            Console.WriteLine($"  Can Hit: {(solution.SolutionValid ? (solution.CanHit ? "Yes" : "No") : "N/A")}");
            Console.WriteLine($"  Solution Valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}\n");
        }

        private static List<string> BuildResultsLines(
            FiringSolutionResult solution,
            double mass,
            float velocity,
            double targetRcs,
            GameDifficulty difficulty,
            double modeHitToleranceMultiplier,
            string? barrelLine1,
            string? barrelLine2,
            string outcomeLine,
            bool evaded,
            bool projectileIntercepted)
        {
            var lines = new List<string>();

            lines.Add("=== ENERGY CALCULATION ===");
            lines.Add("Formula: KE = 0.5 × mass × velocity²");
            lines.Add($"  Mass: {mass:F1} kg");
            lines.Add($"  Launch Velocity: {velocity:F0} m/s");

            double impactVelocity = solution.ImpactVelocityMs > 0.0 ? solution.ImpactVelocityMs : velocity;
            if (solution.ImpactVelocityMs > 0.0)
                lines.Add($"  Impact Velocity: {impactVelocity:F0} m/s");

            double displayEnergyMj = BallisticsCalculator.CalculateKineticEnergyMJ(mass, impactVelocity);
            lines.Add($"  Calculation: 0.5 × {mass:F1} × ({impactVelocity:F0})²");
            lines.Add($"  = {displayEnergyMj:F1} MJ");
            lines.Add($"Required: {solution.FractureEnergyRequired:F0} MJ");
            lines.Add($"✓ Energy Check: {(solution.CanDestroy ? "PASS" : "FAIL")}");
            lines.Add($"  ({displayEnergyMj:F1} MJ vs {solution.FractureEnergyRequired:F0} MJ threshold)");
            lines.Add(string.Empty);

            lines.Add("=== INTERCEPT ACCURACY ===");
            if (solution.EnemyInterceptPoint.HasValue)
            {
                Vector3 enemyAtT = solution.EnemyInterceptPoint.Value;
                lines.Add("Enemy at intercept:");
                lines.Add($"  {enemyAtT}");
                lines.Add($"  Position deviation: {solution.InterceptDeviation:F1} meters");

                var diffConfig = DifficultyConfig.GetConfig(difficulty);
                double hitTolerance;
                if (diffConfig.IsTutorialMode)
                {
                    hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;
                    lines.Add($"  Hit tolerance: {hitTolerance:F1} m (beachball radius)");
                }
                else
                {
                    double diameterFromRcs = 2.0 * Math.Sqrt(targetRcs / Math.PI);
                    hitTolerance = diameterFromRcs * 0.5 * diffConfig.HitToleranceMultiplier * modeHitToleranceMultiplier;
                    lines.Add($"  Hit tolerance: {hitTolerance:F1} m (from {targetRcs:F1} m² RCS)");
                }

                lines.Add($"✓ Accuracy Check: {(solution.CanHit ? "PASS" : "FAIL")}");
                lines.Add($"  ({solution.InterceptDeviation:F1}m deviation vs {hitTolerance:F1}m tolerance)");
            }
            else
            {
                lines.Add("  ERROR: No intercept point calculated");
                lines.Add("✗ Accuracy Check: FAIL");
            }

            lines.Add(string.Empty);
            lines.Add("=== OVERALL SOLUTION VALIDITY ===");
            lines.Add($"Energy sufficient: {(solution.CanDestroy ? "✓ Yes" : "✗ No")}");
            lines.Add($"Accuracy valid: {(solution.SolutionValid ? (solution.CanHit ? "✓ Yes" : "✗ No") : "N/A")}");
            lines.Add($"Solution valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}");

            if (solution.CanDestroy && solution.CanHit)
            {
                lines.Add($"Evasion check: {(evaded ? "✗ Failed" : "✓ Passed")}");
                lines.Add($"Projectile survival: {(projectileIntercepted ? "✗ Failed" : "✓ Passed")}");
            }

            lines.Add($"Result: {(solution.CanDestroy && solution.CanHit && !evaded && !projectileIntercepted ? "✓ HIT" : "✗ MISS")}");

            if (!string.IsNullOrWhiteSpace(barrelLine1) || !string.IsNullOrWhiteSpace(barrelLine2))
            {
                lines.Add(string.Empty);
                lines.Add("=== WEAPON STATUS ===");
                if (!string.IsNullOrWhiteSpace(barrelLine1)) lines.Add(barrelLine1!);
                if (!string.IsNullOrWhiteSpace(barrelLine2)) lines.Add(barrelLine2!);
            }

            lines.Add(string.Empty);
            lines.Add(outcomeLine);
            lines.Add(string.Empty);
            lines.Add("Press any key to continue...");

            return lines;
        }
    }
}
