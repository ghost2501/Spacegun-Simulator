using System;
using System.Collections.Generic;
using System.IO;

namespace Spacegun_Simulator
{
    // Console UI implementing 4-turn sequence:
    // Turn 1: Detection → Show threat
    // Turn 2: Resource Allocation → Gather resources
    // Turn 3: Development → Apply upgrades
    // Turn 4: Firing Solution → Single shot engagement
    // 
    // SAVE SYSTEM: Single auto-save slot prevents save-scumming.
    // Players can stop/resume but cannot replay waves.
    // Game Over states are NOT saved.
    public class ConsoleUI
    {
        private readonly GameState engine;
        private const string SaveDirectory = "Saves";

        public ConsoleUI(GameState engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            EnsureSaveDirectory();
        }

        private void EnsureSaveDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        public void Run()
        {
            ShowMainMenu();

            while (!engine.IsGameOver)
            {
                switch (engine.CurrentPhase)
                {
                    case GameState.GamePhase.Detection:
                        RunDetectionPhase();
                        break;

                    case GameState.GamePhase.ResourceAllocation:
                        RunResourceAllocationPhase();
                        break;

                    case GameState.GamePhase.Development:
                        RunDevelopmentPhase();
                        break;

                    case GameState.GamePhase.Firing:
                        RunFiringPhase();
                        break;

                    case GameState.GamePhase.WaveComplete:
                        RunWaveCompletePhase();
                        break;
                }
            }

            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    GAME OVER                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
            
            // DO NOT save game over state - only playable states are saved
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // ====================================================================
        // MAIN MENU - Single Save Slot (Anti-Save-Scum)
        // ====================================================================

        private void ShowMainMenu()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        SPACE GUN DEFENSE SIMULATOR - MAIN MENU            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            bool autoSaveExists = GameState.AutoSaveExists();

            if (autoSaveExists)
            {
                Console.WriteLine("RESUME GAME");
                Console.WriteLine($"Auto-save found (last saved: {GameState.GetAutoSaveTimestamp()})\n");
            }

            Console.WriteLine("[1] Start New Game");
            
            if (autoSaveExists)
            {
                Console.WriteLine("[2] Resume Game");
            }
            
            Console.WriteLine("[3] Exit");

            bool validChoice = false;
            while (!validChoice)
            {
                Console.Write("\nSelect option: ");
                string input = Console.ReadLine() ?? "0";

                switch (input)
                {
                    case "1":
                        Console.WriteLine("\nStarting new game...\n");
                        System.Threading.Thread.Sleep(800);
                        validChoice = true;
                        break;

                    case "2":
                        if (autoSaveExists)
                        {
                            if (engine.LoadAutoSave())
                            {
                                Console.WriteLine("\n✓ Game resumed from auto-save.\n");
                                System.Threading.Thread.Sleep(1200);
                                validChoice = true;
                            }
                            else
                            {
                                Console.WriteLine("\n✗ Failed to load auto-save. Starting new game...\n");
                                System.Threading.Thread.Sleep(1200);
                                validChoice = true;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid choice. Please try again.\n");
                        }
                        break;

                    case "3":
                        Environment.Exit(0);
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.\n");
                        break;
                }
            }
        }

        // ====================================================================
        // GAME PHASES
        // ====================================================================

        private void RunDetectionPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           SPACE GUN DEFENSE SIMULATOR                     ║");
            Console.WriteLine($"║           Wave {engine.CurrentWaveNumber} of {GameConstants.TotalWaves}".PadRight(57) + "║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var detectionResult = engine.ExecuteDetectionPhase();

            Console.WriteLine("=== DETECTION PHASE ===\n");
            Console.WriteLine(detectionResult.Message);

            if (!detectionResult.WaveDetected)
            {
                Console.WriteLine("\n✗ MISSION FAILED");
                engine.IsGameOver = true;
                return;
            }

            // ===== ARCHETYPE THREAT PROFILE =====
            var archetype = detectionResult.Wave.Archetype;
            Console.WriteLine($"\n=== THREAT ARCHETYPE ===");
            Console.WriteLine($"Class: {archetype.Name}");
            Console.WriteLine($"Description: {archetype.Description}");
            Console.WriteLine();

            // ===== BALLISTIC REQUIREMENTS =====
            Console.WriteLine($"=== BALLISTIC REQUIREMENTS ===");
            Console.WriteLine($"Enemy Mass Range: {archetype.MassRange.Min:N0} - {archetype.MassRange.Max:N0} metric tons");
            Console.WriteLine($"Required Fracture Energy Range: {archetype.FractureEnergyRange.Min:N0} - {archetype.FractureEnergyRange.Max:N0} MJ");
            Console.WriteLine($"Difficulty: {BallisticsCalculator.GetDifficultyDescription(archetype.BaseDifficultyRating)}");
            Console.WriteLine();

            Console.WriteLine($"=== ENEMY PROFILE ===");
            Console.WriteLine($"Type: {detectionResult.Wave.Targets[0].Name}");
            Console.WriteLine($"Detection Distance: {GameConstants.FormatDistance(detectionResult.Wave.CurrentDistance)}");
            Console.WriteLine($"Velocity: {GameConstants.FormatVelocity(detectionResult.Wave.AverageVelocity)}");
            Console.WriteLine($"Radar Cross-Section: {detectionResult.Wave.AverageRadarCrossSection:F1} m²");
            Console.WriteLine($"Evasiveness: {detectionResult.Wave.AverageEvasiveness * 100:F0}%");
            
            Console.WriteLine($"\n=== TIME BUDGET ===");
            Console.WriteLine($"Years Available: {(long)detectionResult.AvailableYears} years");

            Console.WriteLine($"\n=== CURRENT RESOURCES ===");
            Console.WriteLine($"Budget: {engine.Resources.Budget:F0}");
            Console.WriteLine($"Steel: {engine.Resources.Steel:F0} tons");
            Console.WriteLine($"Exotic Materials: {engine.Resources.ExoticMaterials:F1} units");

            Console.WriteLine("\nPress any key to proceed to Resource Allocation phase...");
            Console.ReadKey();
            
            // Auto-save after detection phase
            engine.AutoSaveGame();
        }

        private void RunResourceAllocationPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           RESOURCE ALLOCATION PHASE                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Total Available Time: {(long)engine.AvailableYears} years\n");

            // Display resources in a cleaner format
            Console.WriteLine("=== RESOURCE PRODUCTION RATES (per year) ===");
            Console.WriteLine("Base Materials:");
            Console.WriteLine($"  Steel:                  {GameConstants.SteelProductionPerYear:F0} tons/year");
            Console.WriteLine($"  Budget:                 {GameConstants.BudgetProductionPerYear:F0} currency/year");
            Console.WriteLine("\nSpecialized Resources:");
            Console.WriteLine($"  Specialized Alloys:     {GameConstants.SpecializedAlloysProductionPerYear:F0} tons/year");
            Console.WriteLine($"  Rare Earth Elements:    {GameConstants.RareEarthElementsProductionPerYear:F0} units/year");
            Console.WriteLine("\nAdvanced Systems:");
            Console.WriteLine($"  Power Cells:            {GameConstants.PowerCellsProductionPerYear:F0} units/year");
            Console.WriteLine($"  Exotic Materials:       {GameConstants.ExoticProductionPerYear:F0} units/year\n");

            Console.WriteLine("Enter years for each resource. Type 'u' to undo the last input.\n");

            // Track allocation for this phase - 6 resources now
            long[] yearsAllocated = new long[6];
            string[] resourceNames = { "Steel", "Budget", "Specialized Alloys", "Rare Earth Elements", "Power Cells", "Exotic Materials" };
            double[] productionRates = {
                GameConstants.SteelProductionPerYear,
                GameConstants.BudgetProductionPerYear,
                GameConstants.SpecializedAlloysProductionPerYear,
                GameConstants.RareEarthElementsProductionPerYear,
                GameConstants.PowerCellsProductionPerYear,
                GameConstants.ExoticProductionPerYear
            };

            int currentStep = 0;

            while (currentStep < 6)
            {
                string resourceName = resourceNames[currentStep];
                double productionRate = productionRates[currentStep];

                Console.Write($"{currentStep + 1}/6 - Years for {resourceName:,-25} (remaining: {engine.RemainingYears}): ");
                string input = Console.ReadLine() ?? "0";

                // Check for undo
                if (input.ToLower() == "u")
                {
                    if (currentStep == 0)
                    {
                        Console.WriteLine("✗ Nothing to undo.\n");
                        continue;
                    }

                    // Restore the years from previous allocation
                    engine.RemainingYears += yearsAllocated[currentStep - 1];
                    yearsAllocated[currentStep - 1] = 0;

                    Console.WriteLine($"✓ Undid {resourceNames[currentStep - 1]} allocation. Restored {yearsAllocated[currentStep - 1]} years.\n");
                    currentStep--;
                    continue;
                }

                if (!long.TryParse(input, out long years) || years < 0)
                    years = 0;

                if (years > engine.RemainingYears)
                {
                    Console.WriteLine($"✗ Cannot allocate {years} years. Only {engine.RemainingYears} remaining.\n");
                    continue;
                }

                // Deduct from remaining years
                engine.RemainingYears -= years;
                yearsAllocated[currentStep] = years;

                // Display confirmation with production details
                double resourceGathered = years * productionRate;
                Console.WriteLine($"  ✓ Allocated {years} years → {resourceGathered:F0} units gathered");
                Console.WriteLine($"  → Remaining time: {engine.RemainingYears} years\n");

                currentStep++;
            }

            // Update accumulated resources with final totals
            engine.AccumulatedResources["Steel"] += yearsAllocated[0] * GameConstants.SteelProductionPerYear;
            engine.AccumulatedResources["Budget"] += yearsAllocated[1] * GameConstants.BudgetProductionPerYear;
            engine.AccumulatedResources["SpecializedAlloys"] += yearsAllocated[2] * GameConstants.SpecializedAlloysProductionPerYear;
            engine.AccumulatedResources["RareEarthElements"] += yearsAllocated[3] * GameConstants.RareEarthElementsProductionPerYear;
            engine.AccumulatedResources["PowerCells"] += yearsAllocated[4] * GameConstants.PowerCellsProductionPerYear;
            engine.AccumulatedResources["Exotic"] += yearsAllocated[5] * GameConstants.ExoticProductionPerYear;

            // Display final allocation summary
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              ALLOCATION COMPLETE                          ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Accumulated Resources (this wave):");
            Console.WriteLine("  Base Materials:");
            Console.WriteLine($"    Steel:                {engine.AccumulatedResources["Steel"]:F0} tons");
            Console.WriteLine($"    Budget:               {engine.AccumulatedResources["Budget"]:F0} currency");
            Console.WriteLine("  Specialized Resources:");
            Console.WriteLine($"    Specialized Alloys:   {engine.AccumulatedResources["SpecializedAlloys"]:F0} tons");
            Console.WriteLine($"    Rare Earth Elements:  {engine.AccumulatedResources["RareEarthElements"]:F0} units");
            Console.WriteLine("  Advanced Systems:");
            Console.WriteLine($"    Power Cells:          {engine.AccumulatedResources["PowerCells"]:F0} units");
            Console.WriteLine($"    Exotic Materials:     {engine.AccumulatedResources["Exotic"]:F1} units");
            Console.WriteLine($"\n  Time Remaining: {(long)engine.RemainingYears} years");

            Console.WriteLine("\nPress any key to proceed to Development phase...");
            Console.ReadKey();
            
            // CRITICAL: Set phase to Development before saving
            engine.CurrentPhase = GameState.GamePhase.Development;
            
            // Auto-save after allocation phase
            engine.AutoSaveGame();
        }

        private void RunDevelopmentPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              DEVELOPMENT & UPGRADES PHASE                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Accumulated Resources (this wave):");
            Console.WriteLine($"  Steel: {engine.AccumulatedResources["Steel"]:F0} tons");
            Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units");
            Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0} currency\n");

            // ===== DISPLAY TARGET REQUIREMENT =====
            if (engine.CurrentWave?.Archetype != null)
            {
                var archetype = engine.CurrentWave.Archetype;
                Console.WriteLine($"=== TARGET REQUIREMENT ===");
                Console.WriteLine($"Archetype: {archetype.Name}");
                Console.WriteLine($"Fracture Energy Needed: {archetype.FractureEnergyRange.Min:N0} - {archetype.FractureEnergyRange.Max:N0} MJ");
                Console.WriteLine();
            }

            Console.WriteLine("=== AVAILABLE GUN/PROJECTILE COMBINATIONS ===\n");

            // Convert accumulated resources to ResourceCost for affordability check
            var availableResources = new ResourceCost(
                budget: engine.AccumulatedResources["Budget"],
                steel: engine.AccumulatedResources["Steel"],
                exotic: engine.AccumulatedResources["Exotic"]
            );

            // Get affordable options
            var affordableSpecs = GunProjectileSpec.GetAffordable(availableResources);

            if (affordableSpecs.Count == 0)
            {
                Console.WriteLine("✗ No affordable gun/projectile combinations with current resources.");
                Console.WriteLine("\nPress any key to proceed to Firing Solution phase...");
                Console.ReadKey();
                engine.CurrentPhase = GameState.GamePhase.Firing;
                engine.AutoSaveGame();
                return;
            }

            // Display all options (affordable and not affordable)
            for (int i = 0; i < GunProjectileSpec.All.Length; i++)
            {
                var spec = GunProjectileSpec.All[i];
                bool isAffordable = affordableSpecs.Contains(spec);
                string affordabilityMark = isAffordable ? "✓" : "✗";

                Console.WriteLine($"{affordabilityMark} [{i + 1}] {spec.Name}");
                Console.WriteLine($"    Mass: {spec.ProjectileMassKg}kg @ {spec.MuzzleVelocityMs:N0} m/s");
                Console.WriteLine($"    Kinetic Energy: {spec.ResultingKE_MJ:N0} MJ");
                Console.WriteLine($"    Cost: {spec.Cost.Budget:F0} Budget, {spec.Cost.Steel:F0} Steel, {spec.Cost.ExoticMaterials:F1} Exotic");
                
                // Show if this meets the requirement
                if (engine.CurrentWave?.Archetype != null)
                {
                    bool meetsRequirement = BallisticsCalculator.CanDestroyTarget(spec.ResultingKE_MJ, engine.CurrentWave.Targets[0]);
                    string requirement = meetsRequirement ? "✓ MEETS REQUIREMENT" : "✗ Insufficient energy";
                    Console.WriteLine($"    {requirement}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Select a gun/projectile spec (1-5), or 0 to skip: ");
            string input = Console.ReadLine() ?? "0";

            if (!int.TryParse(input, out int choice) || choice < 0 || choice > GunProjectileSpec.All.Length)
            {
                Console.WriteLine("Invalid selection.");
                engine.CurrentPhase = GameState.GamePhase.Firing;
                Console.WriteLine("Press any key to proceed to Firing Solution phase...");
                Console.ReadKey();
                engine.AutoSaveGame();
                return;
            }

            if (choice == 0)
            {
                Console.WriteLine("Proceeding to Firing Solution phase without selecting a spec...");
                engine.CurrentPhase = GameState.GamePhase.Firing;
                Console.WriteLine("Press any key to proceed...");
                Console.ReadKey();
                engine.AutoSaveGame();
                return;
            }

            var selectedSpec = GunProjectileSpec.All[choice - 1];

            // Check if affordable
            if (!affordableSpecs.Contains(selectedSpec))
            {
                Console.WriteLine($"\n✗ Cannot afford {selectedSpec.Name}.");
                Console.WriteLine($"Required: {selectedSpec.Cost.Budget:F0} Budget, {selectedSpec.Cost.Steel:F0} Steel, {selectedSpec.Cost.ExoticMaterials:F1} Exotic");
                Console.WriteLine($"Available: {availableResources.Budget:F0} Budget, {availableResources.Steel:F0} Steel, {availableResources.ExoticMaterials:F1} Exotic");
                Console.WriteLine("Press any key to select a different spec...");
                Console.ReadKey();
                RunDevelopmentPhase(); // Loop back
                return;
            }

            // Apply the selection
            Console.WriteLine($"\n✓ Selected: {selectedSpec.Name}");
            Console.WriteLine($"Deducting resources...");
            
            engine.AccumulatedResources["Budget"] -= selectedSpec.Cost.Budget;
            engine.AccumulatedResources["Steel"] -= selectedSpec.Cost.Steel;
            engine.AccumulatedResources["Exotic"] -= selectedSpec.Cost.ExoticMaterials;

            // Store the selected spec for firing phase
            engine.SelectedGunProjectileSpec = selectedSpec;

            Console.WriteLine($"\nRemaining Resources:");
            Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0}");
            Console.WriteLine($"  Steel: {engine.AccumulatedResources["Steel"]:F0}");
            Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1}");

            engine.CurrentPhase = GameState.GamePhase.Firing;
            Console.WriteLine("\nPress any key to proceed to Firing Solution phase...");
            Console.ReadKey();
            
            // Auto-save after development phase
            engine.AutoSaveGame();
        }

        private void RunFiringPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            FIRING SOLUTION & ENGAGEMENT PHASE             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            var firingResult = engine.ExecuteFiringPhase();

            Console.WriteLine("=== TARGET INFORMATION ===");
            Console.WriteLine($"Target: {engine.CurrentWave?.Targets[0].Name ?? "Unknown"}");
            Console.WriteLine($"Distance: {GameConstants.FormatDistance(firingResult.TargetDistance)}");
            Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(firingResult.GunRange)}\n");

            if (!firingResult.CanReachTarget)
            {
                Console.WriteLine("✗ " + firingResult.Message);
                Console.WriteLine("\nTarget is beyond effective gun range. Mission failed.");
                engine.IsGameOver = true;
                return;
            }

            // ===== 3D BALLISTIC FIRING SOLUTION =====
            Console.WriteLine("=== CALCULATE 3D BALLISTIC FIRING SOLUTION ===\n");
            Console.WriteLine("You must calculate a precise 3D intercept trajectory.");
            Console.WriteLine("Enter four critical parameters:\n");

            var target = engine.CurrentWave?.Targets[0];
            if (target == null)
            {
                Console.WriteLine("✗ No valid target found!");
                engine.IsGameOver = true;
                return;
            }

            // Get weapon/projectile info from SELECTED SPEC, not default
            double muzzleVelocity = engine.SelectedGunProjectileSpec != null
                ? engine.SelectedGunProjectileSpec.MuzzleVelocityMs
                : BallisticsCalculator.CalculateMuzzleVelocity(engine.Gun, engine.Gun.DefaultProjectile);
            
            double projectileMass = engine.SelectedGunProjectileSpec != null
                ? engine.SelectedGunProjectileSpec.ProjectileMassKg
                : engine.Gun.DefaultProjectile.Mass;

            Console.WriteLine($"=== YOUR WEAPON ===");
            Console.WriteLine($"Projectile Mass: {projectileMass:F1} kg");
            Console.WriteLine($"Max Muzzle Velocity: {muzzleVelocity:F0} m/s");
            Console.WriteLine($"Has Guidance System: {(engine.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}\n");
            
            // Get enemy approach angles (generated during wave creation)
            float enemyCurrentElevation = engine.CurrentWave?.ApproachElevation ?? 45.0f;
            float enemyCurrentAzimuth = engine.CurrentWave?.ApproachAzimuth ?? 0.0f;
            float enemyDistance = (float)firingResult.TargetDistance;

            // Convert to 3D Cartesian coordinates
            Vector3 enemyPosition = FiringSolution.AnglesToCartesian(enemyCurrentElevation, enemyCurrentAzimuth, enemyDistance);

            // Enemy velocity: moving toward gun along approach vector
            // Decompose the approach direction into velocity components
            float approachElRad = enemyCurrentElevation * (float)Math.PI / 180f;
            float approachAzRad = enemyCurrentAzimuth * (float)Math.PI / 180f;
            
            float horizontalComponent = -(float)target.Velocity * (float)Math.Cos(approachElRad);
            float verticalComponent = -(float)target.Velocity * (float)Math.Sin(approachElRad);
            
            float vx = horizontalComponent * (float)Math.Sin(approachAzRad);
            float vy = horizontalComponent * (float)Math.Cos(approachAzRad);
            float vz = verticalComponent;
            
            Vector3 enemyVelocity = new Vector3(vx, vy, vz);

            // Display target data for player calculations
            Console.WriteLine("=== TARGET DATA FOR CALCULATIONS ===");
            Console.WriteLine($"Enemy Approach Vector:");
            Console.WriteLine($"  Elevation: {enemyCurrentElevation:F1}° (in sky)");
            Console.WriteLine($"  Azimuth: {enemyCurrentAzimuth:F1}° (bearing)");
            Console.WriteLine($"  Distance: {GameConstants.FormatDistance(enemyDistance)}");
            Console.WriteLine($"  Cartesian Position: {enemyPosition}");
            Console.WriteLine($"Enemy Velocity Vector: ({vx:F1}, {vy:F1}, {vz:F1}) m/s");
            Console.WriteLine($"Approach Speed: {(float)target.Velocity:F0} m/s");
            Console.WriteLine($"Fracture Energy Required: {target.FractureEnergy:F0} MJ\n");

            // Create firing solution calculator
            var calculator = new FiringSolution((float)projectileMass, (float)target.FractureEnergy);

            // Calculate constraints
            float minVelocity = calculator.CalculateRequiredVelocity();
            Console.WriteLine($"=== BALLISTIC CONSTRAINTS ===");
            Console.WriteLine($"Minimum velocity to destroy: {minVelocity:F0} m/s");
            Console.WriteLine($"Maximum velocity available: {muzzleVelocity:F0} m/s\n");

            // Get player's four inputs
            Console.WriteLine("=== ENTER FIRING PARAMETERS ===\n");
            float playerInterceptTime = GetPlayerTimeInput("Intercept time (seconds): ");
            float playerTargetElevation = GetPlayerElevationInput("Target elevation angle (0-90 degrees): ");
            float playerTargetAzimuth = GetPlayerAzimuthInput("Target azimuth bearing (0-360 degrees, 0=North): ");
            float playerLaunchVelocity = GetPlayerVelocityInput($"Launch velocity ({minVelocity:F0}-{muzzleVelocity:F0} m/s): ");

            Console.WriteLine();

            // Calculate firing solution
            var solution = calculator.CalculateSolution(
                enemyPosition,
                enemyVelocity,
                playerInterceptTime,
                playerTargetElevation,
                playerTargetAzimuth,
                playerLaunchVelocity,
                (float)muzzleVelocity,
                engine.CurrentWaveNumber);  // Add wave number for tier-based constraints

            // Display solution analysis
            Console.WriteLine("=== FIRING SOLUTION ANALYSIS ===");
            Console.WriteLine($"Your Input Parameters:");
            Console.WriteLine($"  Intercept Time: {playerInterceptTime:F2} seconds");
            Console.WriteLine($"  Target Elevation: {playerTargetElevation:F1}°");
            Console.WriteLine($"  Target Azimuth: {playerTargetAzimuth:F1}°");
            Console.WriteLine($"  Launch Velocity: {playerLaunchVelocity:F0} m/s\n");

            Console.WriteLine($"Ballistic Results:");
            Console.WriteLine($"  Kinetic Energy: {solution.KineticEnergyMJ:F1} MJ (Need: {solution.FractureEnergyRequired:F0} MJ)");
            Console.WriteLine($"  Can Destroy: {(solution.CanDestroy ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"  Can Intercept: {(solution.CanHit ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"  Miss Distance: {solution.InterceptDeviation:F0} meters\n");

            if (solution.CanHit && solution.EnemyInterceptPoint.HasValue)
            {
                Console.WriteLine($"Intercept Point: {solution.EnemyInterceptPoint.Value}");
                Console.WriteLine($"Time to Impact: {solution.InterceptTime:F2} seconds\n");
            }

            Console.WriteLine($"Solution Status: {solution.Message}\n");

            // Determine hit based on solution validity
            double hitProbability = 0.0;
            if (solution.SolutionValid)
            {
                // Calculate theoretical max probability
                double theoreticalMax = BallisticsCalculator.GetTheoreticalMaxProbability(
                    engine.Gun,
                    engine.Gun.DefaultProjectile,
                    target);

                // Perfect solution = full theoretical probability
                hitProbability = theoreticalMax;

                Console.WriteLine($"Hit Probability: {hitProbability * 100:F1}%\n");
            }
            else
            {
                Console.WriteLine($"Hit Probability: 0% (Invalid solution)\n");
            }

            Console.WriteLine("Firing...\n");
            System.Threading.Thread.Sleep(1000);

            // Determine hit
            bool hit = engine.rng.NextDouble() < hitProbability;

            // ===== DEBUG MATH SECTION =====
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              [DEBUG] FIRING CALCULATION MATH              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("=== ENERGY CALCULATION ===");
            Console.WriteLine($"Formula: KE = 0.5 × mass × velocity²");
            Console.WriteLine($"  Mass: {projectileMass:F1} kg");
            Console.WriteLine($"  Velocity: {playerLaunchVelocity:F0} m/s");
            
            // FIX: Use double arithmetic for display calculation too
            double displayVel = playerLaunchVelocity;
            double displayMass = projectileMass;
            double displayVelSquared = displayVel * displayVel;
            double displayEnergyJoules = 0.5 * displayMass * displayVelSquared;
            double displayEnergyMJ = displayEnergyJoules / 1_000_000.0;
            
            Console.WriteLine($"  Calculation: 0.5 × {displayMass:F1} × ({displayVel:F0})²");
            Console.WriteLine($"  = 0.5 × {displayMass:F1} × {displayVelSquared:F0}");
            Console.WriteLine($"  = {displayEnergyMJ:F1} MJ");
            Console.WriteLine($"Required: {solution.FractureEnergyRequired:F0} MJ");
            Console.WriteLine($"✓ Energy Check: {(solution.CanDestroy ? "PASS" : "FAIL")} ({solution.KineticEnergyMJ:F1} MJ vs {solution.FractureEnergyRequired:F0} MJ threshold)\n");

            Console.WriteLine("=== INTERCEPT CALCULATION ===");
            Console.WriteLine($"Enemy at intercept time t={playerInterceptTime:F2}s:");
            if (solution.EnemyInterceptPoint.HasValue)
            {
                Vector3 enemyAtT = solution.EnemyInterceptPoint.Value;
                Console.WriteLine($"  Position: {enemyAtT}");
                
                // Show projectile position calculation
                Console.WriteLine($"\nProjectile trajectory at t={playerInterceptTime:F2}s:");
                Console.WriteLine($"  Elevation angle: {playerTargetElevation:F1}°");
                Console.WriteLine($"  Azimuth bearing: {playerTargetAzimuth:F1}°");
                Console.WriteLine($"  Launch velocity: {playerLaunchVelocity:F0} m/s");
                
                // Horizontal and vertical components
                float elevRad = playerTargetElevation * (float)Math.PI / 180f;
                float azRad = playerTargetAzimuth * (float)Math.PI / 180f;
                float vzComponent = playerLaunchVelocity * (float)Math.Sin(elevRad);
                float vHorizontal = playerLaunchVelocity * (float)Math.Cos(elevRad);
                float vxComponent = vHorizontal * (float)Math.Sin(azRad);
                float vyComponent = vHorizontal * (float)Math.Cos(azRad);
                
                Console.WriteLine($"  Velocity components: Vx={vxComponent:F1} m/s, Vy={vyComponent:F1} m/s, Vz={vzComponent:F1} m/s");
                Console.WriteLine($"  Position deviation: {solution.InterceptDeviation:F0} meters");
                Console.WriteLine($"✓ Intercept Check: {(solution.CanHit ? "PASS" : "FAIL")} (deviation {solution.InterceptDeviation:F0}m, tolerance 1m)\n");
            }
            else
            {
                Console.WriteLine($"  ERROR: No intercept point calculated");
                Console.WriteLine($"✗ Intercept Check: FAIL (no valid intercept)\n");
            }

            Console.WriteLine("=== HIT PROBABILITY ===");
            Console.WriteLine($"Base weapon accuracy: {BallisticsCalculator.GetBaseWeaponAccuracy(engine.Gun):P1}");
            Console.WriteLine($"Theoretical max hit probability: {hitProbability:P1}");
            double randomRoll = engine.rng.NextDouble();  // ← Store it ONCE
            Console.WriteLine($"Random roll generated: {randomRoll:F4}");
            Console.WriteLine($"Hit threshold: {hitProbability:F4}");
            Console.WriteLine($"✓ Probability Check: Hit rolled as {(hit ? "TRUE" : "FALSE")}\n");

            Console.WriteLine("=== OVERALL SOLUTION VALIDITY ===");
            Console.WriteLine($"Energy sufficient: {(solution.CanDestroy ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Intercept valid: {(solution.CanHit ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Solution valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Hit roll result: {(hit ? "✓ HIT" : "✗ MISS")}\n");

            // ===== END DEBUG SECTION =====

            if (hit)
            {
                Console.WriteLine("✓ DIRECT HIT! Enemy destroyed!");
                if (firingResult.Reward != null)
                {
                    Console.WriteLine("\n=== VICTORY REWARDS ===");
                    Console.WriteLine($"  +{firingResult.Reward.Budget:F0} Budget");
                    Console.WriteLine($"  +{firingResult.Reward.Steel:F0} Steel");
                    Console.WriteLine($"  +{firingResult.Reward.ExoticMaterials:F0} Exotic Materials");
                }

                if (firingResult.GameOver)
                {
                    Console.WriteLine("\n" + firingResult.Message);
                    engine.IsGameOver = true;
                    return;
                }

                engine.AutoSaveGame();
            }
            else
            {
                Console.WriteLine("✗ MISS! The intercept solution was invalid or the projectile lacked sufficient energy.");
                engine.IsGameOver = true;
                return;
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        /// <summary>
        /// Get player input for intercept time in seconds.
        /// </summary>
        private float GetPlayerTimeInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float time) && time > 0)
                {
                    return time;
                }

                Console.WriteLine("Invalid input. Please enter a positive time value in seconds.\n");
            }
        }

        /// <summary>
        /// Get player input for launch elevation angle (-90 to 90 degrees).
        /// Negative angles represent aiming below the horizon (at descending targets).
        /// </summary>
        private float GetPlayerElevationInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float angle) && angle >= -90 && angle <= 90)
                {
                    return angle;
                }

                Console.WriteLine("Invalid input. Please enter an angle between -90 and 90 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for target azimuth bearing (0-360 degrees).
        /// </summary>
        private float GetPlayerAzimuthInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float bearing) && bearing >= 0 && bearing < 360)
                {
                    return bearing;
                }

                Console.WriteLine("Invalid input. Please enter a bearing between 0 and 360 degrees.\n");
            }
        }

        /// <summary>
        /// Get player input for projectile launch velocity in m/s.
        /// </summary>
        private float GetPlayerVelocityInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float velocity) && velocity > 0)
                {
                    return velocity;
                }

                Console.WriteLine("Invalid input. Please enter a positive velocity value in m/s.\n");
            }
        }

        private void RunWaveCompletePhase()
        {
            if (engine.IsGameOver)
                return;

            engine.AdvanceToNextWave();
        }
    }
}