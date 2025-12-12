using Spacegun_Simulator.FireControlTools;

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
            while (true)
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

                // Game Over - show end state and return to menu
                DisplayGameOverScreen();
            }
        }

        /// <summary>
        /// Display the game over screen with final stats, then wait for input to return to menu.
        /// Deletes the auto-save to prevent resuming completed games.
        /// </summary>
        private void DisplayGameOverScreen()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    GAME OVER                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            if (engine.WavesDefeated >= GameConstants.TotalWaves)
            {
                Console.WriteLine("✓ VICTORY! Campaign Complete!\n");
                Console.WriteLine($"Waves Defeated: {engine.WavesDefeated}/{GameConstants.TotalWaves}");
            }
            else
            {
                Console.WriteLine("✗ DEFEAT! Mission Failed\n");
                Console.WriteLine($"Waves Defeated: {engine.WavesDefeated}/{GameConstants.TotalWaves}");
            }

            Console.WriteLine("\nPress any key to return to main menu...");
            Console.ReadKey();

            // Delete auto-save - force players to start fresh after game over
            DeleteAutoSave();

            // Reset game state for next game
            engine.IsGameOver = false;
        }

        /// <summary>
        /// Delete the auto-save file to prevent resuming completed games.
        /// Called after game over (victory or defeat).
        /// </summary>
        private void DeleteAutoSave()
        {
            try
            {
                string savePath = Path.Combine(SaveDirectory, "AutoSave.json");
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
            }
            catch (Exception ex)
            {
                // Silently fail - save deletion is not critical to gameplay
                System.Console.WriteLine($"Note: Could not delete save file: {ex.Message}");
            }
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
                Console.WriteLine("[3] Test Mode (Debug Tools)");
                Console.WriteLine("[4] Exit");
            }
            else
            {
                Console.WriteLine("[2] Test Mode (Debug Tools)");
                Console.WriteLine("[3] Exit");
            }

            bool validChoice = false;
            while (!validChoice)
            {
                Console.Write("\nSelect option: ");
                string input = Console.ReadLine() ?? "0";

                switch (input)
                {
                    case "1":
                        // NEW GAME: Show difficulty selection
                        Console.WriteLine("\nInitializing new game...\n");
                        System.Threading.Thread.Sleep(800);

                        // CRITICAL: Call difficulty selection before starting game
                        GameDifficulty selectedDifficulty = ShowDifficultySelection();

                        // Set difficulty and reset game state for new game
                        engine.SelectedDifficulty = selectedDifficulty;
                        engine.CurrentWaveNumber = 1;
                        engine.IsGameOver = false;
                        engine.WavesDefeated = 0;
                        engine.CurrentPhase = GameState.GamePhase.Detection;

                        var diffConfig = DifficultyConfig.GetConfig(selectedDifficulty);
                        Console.WriteLine($"\nDifficulty: {diffConfig.DisplayName}");
                        
                        // ===== NEW: PRE-GENERATE ALL CAMPAIGN WAVES =====
                        Console.WriteLine("\n[INITIALIZATION] Pre-generating campaign waves...");
                        Console.WriteLine("This may take a few seconds on first run.\n");
                        
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        engine.GenerateAllCampaignWaves(GameConstants.TotalWaves);
                        stopwatch.Stop();
                        
                        Console.WriteLine($"\n✓ Campaign initialization complete in {stopwatch.ElapsedMilliseconds}ms");
                        Console.WriteLine("Press any key to begin...\n");
                        Console.ReadKey();

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
                            // No auto-save, so option 2 is Test Mode
                            RunTestModeMenu();
                            validChoice = true;
                            ShowMainMenu();
                            return;
                        }
                        break;

                    case "3":
                        if (autoSaveExists)
                        {
                            // Auto-save exists, so option 3 is Test Mode
                            RunTestModeMenu();
                            validChoice = true;
                            ShowMainMenu();
                            return;
                        }
                        else
                        {
                            // No auto-save, so option 3 is Exit
                            Environment.Exit(0);
                            break;
                        }

                    case "4":
                        if (autoSaveExists)
                        {
                            // Exit with auto-save
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("Invalid choice. Please try again.\n");
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.\n");
                        break;
                }
            }
        }

        /// <summary>
        /// Display test mode menu with debug tools.
        /// Includes Firing Challenge and automated test harness.
        /// </summary>
        private void RunTestModeMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              TEST MODE - DEBUG TOOLS                     ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                Console.WriteLine("[1] Firing Challenge (Quick Firing Test)");
                Console.WriteLine("[2] Test Harness (Automated Validation)");
                Console.WriteLine("[3] Return to Main Menu");

                Console.Write("\nSelect option: ");
                string input = Console.ReadLine() ?? "0";

                switch (input)
                {
                    case "1":
                        RunFiringChallenge();
                        break;

                    case "2":
                        RunTestMode();
                        break;

                    case "3":
                        return;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.\n");
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }
        }

        /// <summary>
        /// Run a quick firing challenge mode, skipping resource and development phases.
        /// Useful for debugging firing solution mechanics without full game setup.
        /// Uses actual gameplay firing system, not a separate test iteration.
        /// </summary>
        private void RunFiringChallenge()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            FIRING CHALLENGE MODE (DEBUG)                  ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            // Select difficulty
            GameDifficulty difficulty = ShowDifficultySelection();
            engine.SelectedDifficulty = difficulty;

            // Initialize engine for new challenge
            engine.CurrentWaveNumber = 1;
            engine.IsGameOver = false;
            engine.WavesDefeated = 0;
            engine.CurrentPhase = GameState.GamePhase.Detection;

            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            Console.WriteLine($"\nDifficulty: {diffConfig.DisplayName}");

            // Generate campaign enemy type
            if (engine.CampaignEnemyType == null)
            {
                engine.CampaignEnemyType = EnemyType.GenerateForCampaign(engine.rng ?? new Random());
            }

            // Pre-generate first wave only (faster for quick testing)
            Console.WriteLine("\n[INITIALIZATION] Generating firing challenge wave...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Use the standard detection phase to properly initialize wave
                var detectionResult = engine.ExecuteDetectionPhase();

                if (!detectionResult.WaveDetected)
                {
                    Console.WriteLine("\n✗ Wave detection failed. Challenge cancelled.");
                    System.Threading.Thread.Sleep(2000);
                    return;
                }

                stopwatch.Stop();
                Console.WriteLine($"\n✓ Challenge generated in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine("\nPress any key to skip directly to firing phase...\n");
                Console.ReadKey();

                // Skip to firing phase directly (no resource allocation or development)
                engine.CurrentPhase = GameState.GamePhase.Firing;
                RunFiringPhase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Failed to generate firing challenge: {ex.Message}");
                System.Threading.Thread.Sleep(2000);
            }
        }

        /// <summary>
        /// Run automated firing solution test harness.
        /// Tests all scenarios and validates mechanics without affecting game state.
        /// </summary>
        private void RunTestMode()
        {
            using (Spacegun_Simulator.Tests.FireSimulatorTestHarness harness = new())
            {
                harness.RunAllTests();
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

            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);

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

            Console.WriteLine($"\n=== TIME BUDGET ===");
            Console.WriteLine($"Years Available: {(long)detectionResult.AvailableYears} years");

            Console.WriteLine($"\n=== CURRENT RESOURCES ===");
            Console.WriteLine($"Budget: {engine.Resources.Budget:F0}");
            Console.WriteLine($"Steel: {engine.Resources.Steel:F0} tons");
            Console.WriteLine($"Exotic Materials: {engine.Resources.ExoticMaterials:F1} units");

            // ===== TUTORIAL MODE: Skip resource phases =====
            if (diffConfig.SkipResourcePhases)
            {
                Console.WriteLine("\n────────────────────────────────────────────────────────────");
                Console.WriteLine("📚 TUTORIAL MODE: Skipping resource and development phases.");
                Console.WriteLine("   Proceeding directly to firing solution...");
                Console.WriteLine("────────────────────────────────────────────────────────────");
                Console.WriteLine("\nPress any key to proceed to Firing Solution phase...");
                Console.ReadKey();

                // Skip directly to firing phase
                engine.CurrentPhase = GameState.GamePhase.Firing;
            }
            else
            {
                Console.WriteLine("\nPress any key to proceed to Resource Allocation phase...");
                Console.ReadKey();
            }

            // Auto-save after detection phase
            engine.AutoSaveGame();
        }

        private void RunResourceAllocationPhase()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         PREPARATION PHASE - RESOURCES & RESEARCH           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            // ===== NEW: Display wave event if present =====
            engine.GenerateWaveEvent();
            if (engine.CurrentWaveEvent != null)
            {
                Console.WriteLine("=== RANDOM EVENT ===");
                Console.WriteLine($"⚡ {engine.CurrentWaveEvent.Title}");
                Console.WriteLine($"   {engine.CurrentWaveEvent.Description}");
                if (engine.CurrentWaveEvent.ProductionMultiplier != 1.0)
                {
                    string modifier = engine.CurrentWaveEvent.ProductionMultiplier > 1.0 ? "+" : "";
                    Console.WriteLine($"   Production: {modifier}{(engine.CurrentWaveEvent.ProductionMultiplier - 1) * 100:F0}%\n");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Total Available Time: {(long)engine.AvailableYears} years\n");

            // ===== NEW: Calculate effective production rates with tech & difficulty scaling =====
            Console.WriteLine("=== RESOURCE PRODUCTION RATES (per year, with tech & difficulty) ===");
            
            double eventMultiplier = engine.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;
            
            Dictionary<ResourceType, double> effectiveRates = new();
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                double rate = ResourceGathering.GetEffectiveProductionRate(
                    resource, 
                    engine.TechTree, 
                    engine.SelectedDifficulty,
                    eventMultiplier);
                
                if (rate > 0)
                {
                    effectiveRates[resource] = rate;
                }
            }

            Console.WriteLine("Base Materials:");
            if (effectiveRates.ContainsKey(ResourceType.Steel))
                Console.WriteLine($"  Steel:                  {effectiveRates[ResourceType.Steel]:F0} tons/year");
            if (effectiveRates.ContainsKey(ResourceType.Budget))
                Console.WriteLine($"  Budget:                 {effectiveRates[ResourceType.Budget]:F0} currency/year");

            Console.WriteLine("\nTier 2 Resources (Mining II+):");
            if (effectiveRates.ContainsKey(ResourceType.SpecializedAlloys))
                Console.WriteLine($"  Specialized Alloys:     {effectiveRates[ResourceType.SpecializedAlloys]:F0} tons/year");
            if (effectiveRates.ContainsKey(ResourceType.RareEarthElements))
                Console.WriteLine($"  Rare Earth Elements:    {effectiveRates[ResourceType.RareEarthElements]:F0} units/year");

            Console.WriteLine("\nTier 3 Resources (Mining III+):");
            if (effectiveRates.ContainsKey(ResourceType.AdvancedOre))
                Console.WriteLine($"  Advanced Ore:           {effectiveRates[ResourceType.AdvancedOre]:F0} units/year");
            if (effectiveRates.ContainsKey(ResourceType.ExoticMaterials))
                Console.WriteLine($"  Exotic Materials:       {effectiveRates[ResourceType.ExoticMaterials]:F0} units/year");

            Console.WriteLine("\nOther Systems:");
            if (effectiveRates.ContainsKey(ResourceType.PowerCells))
                Console.WriteLine($"  Power Cells:            {effectiveRates[ResourceType.PowerCells]:F0} units/year");

            // ===== FLEXIBLE WORKFLOW: Loop until player is ready for development =====
            bool readyForDevelopment = false;

            while (!readyForDevelopment)
            {
                Console.WriteLine("\n=== PREPARATION OPTIONS ===");
                Console.WriteLine("[R] Allocate Resources for Gathering");
                Console.WriteLine("[T] Research New Technology");
                Console.WriteLine("[S] Show Current Status");
                Console.WriteLine("[D] Done - Proceed to Development\n");

                Console.Write("Select action (R/T/S/D): ");
                string action = Console.ReadLine()?.ToUpper() ?? "D";

                switch (action)
                {
                    case "R":
                        AllocateResourcesInteractive(effectiveRates);
                        break;

                    case "T":
                        ResearchTechInteractive();
                        break;

                    case "S":
                        DisplayPreparationStatus(effectiveRates);
                        break;

                    case "D":
                        readyForDevelopment = true;
                        break;

                    default:
                        Console.WriteLine("Invalid action.\n");
                        break;
                }
            }

            // Final summary
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           PREPARATION COMPLETE - SUMMARY                  ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("Accumulated Resources:");
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

            engine.CurrentPhase = GameState.GamePhase.Development;
            engine.AutoSaveGame();
        }

        private void AllocateResourcesInteractive(Dictionary<ResourceType, double> effectiveRates)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              RESOURCE ALLOCATION MENU                     ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine($"Time Remaining: {engine.RemainingYears} years\n");
            Console.WriteLine("Enter years for each resource. Type 'u' to undo the last input.\n");

            long[] yearsAllocated = new long[6];
            string[] resourceNames = { "Steel", "Budget", "Specialized Alloys", "Rare Earth Elements", "Power Cells", "Exotic Materials" };
            double[] productionRates = {
                effectiveRates.ContainsKey(ResourceType.Steel) ? effectiveRates[ResourceType.Steel] : 0,
                effectiveRates.ContainsKey(ResourceType.Budget) ? effectiveRates[ResourceType.Budget] : 0,
                effectiveRates.ContainsKey(ResourceType.SpecializedAlloys) ? effectiveRates[ResourceType.SpecializedAlloys] : 0,
                effectiveRates.ContainsKey(ResourceType.RareEarthElements) ? effectiveRates[ResourceType.RareEarthElements] : 0,
                effectiveRates.ContainsKey(ResourceType.PowerCells) ? effectiveRates[ResourceType.PowerCells] : 0,
                effectiveRates.ContainsKey(ResourceType.ExoticMaterials) ? effectiveRates[ResourceType.ExoticMaterials] : 0
            };

            int currentStep = 0;

            while (currentStep < 6)
            {
                string resourceName = resourceNames[currentStep];
                double productionRate = productionRates[currentStep];

                if (productionRate == 0)
                {
                    Console.WriteLine($"{currentStep + 1}/6 - {resourceName:,-25} (LOCKED - Tech not researched)\n");
                    currentStep++;
                    continue;
                }

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
            engine.AccumulatedResources["Steel"] += yearsAllocated[0] * productionRates[0];
            engine.AccumulatedResources["Budget"] += yearsAllocated[1] * productionRates[1];
            engine.AccumulatedResources["SpecializedAlloys"] += yearsAllocated[2] * productionRates[2];
            engine.AccumulatedResources["RareEarthElements"] += yearsAllocated[3] * productionRates[3];
            engine.AccumulatedResources["PowerCells"] += yearsAllocated[4] * productionRates[4];
            engine.AccumulatedResources["Exotic"] += yearsAllocated[5] * productionRates[5];

            Console.WriteLine("✓ Resource allocation complete.\n");
            System.Threading.Thread.Sleep(1000);
        }

        private void ResearchTechInteractive()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               TECHNOLOGY RESEARCH MENU                    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            TechUnlock.DisplayAvailableTechs(engine.TechTree, engine.AccumulatedResources);

            Console.Write("Research tech number (or 0 to skip): ");
            if (int.TryParse(Console.ReadLine() ?? "0", out int techChoice) && techChoice > 0)
            {
                var availableTechs = TechUnlock.GetAvailableUnlocks(engine.TechTree);
                if (techChoice <= availableTechs.Count)
                {
                    var selectedTech = availableTechs[techChoice - 1];
                    if (engine.ResearchTech(selectedTech))
                    {
                        Console.WriteLine($"\n✓ Tech research complete: {selectedTech.TechType} → Level {selectedTech.ToLevel}\n");
                        System.Threading.Thread.Sleep(1500);
                    }
                    else
                    {
                        Console.WriteLine($"\n✗ Cannot afford this research.\n");
                        System.Threading.Thread.Sleep(1000);
                    }
                }
            }
        }

        private void DisplayPreparationStatus(Dictionary<ResourceType, double> effectiveRates)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               PREPARATION PHASE STATUS                    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("=== ACCUMULATED RESOURCES ===");
            Console.WriteLine($"Steel:                {engine.AccumulatedResources["Steel"]:F0} tons");
            Console.WriteLine($"Budget:               {engine.AccumulatedResources["Budget"]:F0} currency");
            Console.WriteLine($"Specialized Alloys:   {engine.AccumulatedResources["SpecializedAlloys"]:F0} tons");
            Console.WriteLine($"Rare Earth Elements:  {engine.AccumulatedResources["RareEarthElements"]:F0} units");
            Console.WriteLine($"Power Cells:          {engine.AccumulatedResources["PowerCells"]:F0} units");
            Console.WriteLine($"Exotic Materials:     {engine.AccumulatedResources["Exotic"]:F1} units");

            Console.WriteLine($"\n=== TIME ===");
            Console.WriteLine($"Years Remaining: {engine.RemainingYears} / {engine.AvailableYears}");

            Console.WriteLine($"\n=== TECH TREE ===");
            Console.WriteLine($"Radar:       Level {engine.TechTree.CurrentLevel[TechTree.TechType.Radar]}");
            Console.WriteLine($"Mining:      Level {engine.TechTree.CurrentLevel[TechTree.TechType.Mining]}");
            Console.WriteLine($"Production:  Level {engine.TechTree.CurrentLevel[TechTree.TechType.Production]}");
            Console.WriteLine($"Weapons:     Level {engine.TechTree.CurrentLevel[TechTree.TechType.Weapons]}");
            Console.WriteLine($"Projectiles: Level {engine.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}");

            Console.WriteLine("\nPress any key to return to Preparation menu...");
            Console.ReadKey();
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

            if (!firingResult.CanReachTarget)
            {
                Console.WriteLine("✗ " + firingResult.Message);
                Console.WriteLine("\nTarget is beyond effective gun range. Mission failed.");
                engine.IsGameOver = true;
                return;
            }

            var target = engine.CurrentWave?.Targets[0];
            if (target == null)
            {
                Console.WriteLine("✗ No valid target found!");
                engine.IsGameOver = true;
                return;
            }

            if (engine.CurrentWave == null)
            {
                Console.WriteLine("✗ Critical error: Wave data lost during firing phase!");
                engine.IsGameOver = true;
                return;
            }

            // ===== CRITICAL: Use the FiringProblem generated by ExecuteFiringPhase() =====
            // Do NOT call GenerateFiringProblem() again - it will regenerate!
            if (engine.CurrentFiringProblem == null)
            {
                Console.WriteLine("✗ Critical error: Firing problem not initialized!");
                engine.IsGameOver = true;
                return;
            }

            var firingProblem = engine.CurrentFiringProblem;
            Vector3 enemyPosition = firingProblem.EnemyPosition;
            Vector3 enemyVelocity = firingProblem.EnemyVelocity;

            var tier = GameConstants.GetTierForWave(engine.CurrentWaveNumber);

            double muzzleVelocity = engine.SelectedGunProjectileSpec != null
                ? engine.SelectedGunProjectileSpec.MuzzleVelocityMs
                : BallisticsCalculator.CalculateMuzzleVelocity(engine.Gun, engine.Gun.DefaultProjectile);

            double projectileMass = engine.SelectedGunProjectileSpec != null
                ? engine.SelectedGunProjectileSpec.ProjectileMassKg
                : engine.Gun.DefaultProjectile.Mass;

            // ===== DECLARE MISSING VARIABLES =====
            var calculator = new FiringSolution(
                (float)projectileMass,
                (float)target.FractureEnergy,
                target.Mass);

            float minVelocity = calculator.CalculateRequiredVelocity();
            double targetRadarCrossSection = target.CrossSection;

            Console.WriteLine($"=== YOUR WEAPON ===");
            Console.WriteLine($"Projectile Mass: {FiringPhaseFormatter.FormatMass(projectileMass, engine.SelectedDifficulty)} kg");
            Console.WriteLine($"Max Muzzle Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Has Guidance System: {(engine.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}");
            Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange)}\n");

            // ===== TARGET DATA FOR CALCULATIONS =====
            Console.WriteLine("=== TARGET DATA FOR CALCULATIONS ===");
            Console.WriteLine($"Designation: {engine.CurrentWave?.Targets[0].Name ?? "Unknown"}");
            Console.WriteLine($"Enemy Approach Vector:");
            Console.WriteLine($"  Elevation: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachElevation, engine.SelectedDifficulty)}° (in sky)");
            Console.WriteLine($"  Azimuth: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachAzimuth, engine.SelectedDifficulty)}° (bearing)");
            Console.WriteLine($"  Distance: {GameConstants.FormatDistance((double)firingProblem.EngagementDistance)}");
            Console.WriteLine($"  Cartesian Position: {FiringPhaseFormatter.FormatVector3(enemyPosition, engine.SelectedDifficulty)}");
            Console.WriteLine($"Enemy Velocity Vector: ({FiringPhaseFormatter.FormatVelocity(enemyVelocity.X, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(enemyVelocity.Y, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(enemyVelocity.Z, engine.SelectedDifficulty)}) m/s");
            Console.WriteLine($"Approach Speed: {FiringPhaseFormatter.FormatVelocity(firingProblem.ApproachSpeed, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Fracture Energy Required: {FiringPhaseFormatter.FormatEnergy(firingProblem.FractureEnergyRequired, engine.SelectedDifficulty)}");
            
            // Display hit tolerance instead of RCS for tutorial mode
            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);
            if (diffConfig.IsTutorialMode)
            {
                double hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;
                Console.WriteLine($"Hit Tolerance: {hitTolerance:F1} m (beachball radius)\n");
            }
            else
            {
                Console.WriteLine($"Target Radar Cross-Section: {FiringPhaseFormatter.FormatRadarCrossSection(targetRadarCrossSection, engine.SelectedDifficulty)} m²\n");
            }

            bool workflowComplete = false;

            while (!workflowComplete)
            {
                Console.WriteLine("=== FIRING SOLUTION WORKFLOW ===\n");
                Console.WriteLine("[1] PREDICT TARGET POSITION (Target Motion Calculator)");
                Console.WriteLine("[2] CALCULATE REQUIREMENTS (Ballistic Tables)");
                Console.WriteLine("[3] PLAN TRAJECTORY (Trajectory Plotter)");
                Console.WriteLine("[4] TEST SOLUTION (Fire Simulator)");
                Console.WriteLine("[5] ENTER FINAL SOLUTION (Commit & Fire)");
                Console.WriteLine("[0] SKIP WORKFLOW (Direct Entry)\n");

                Console.Write("Select step (0-5): ");
                string choice = Console.ReadLine() ?? "0";

                switch (choice)
                {
                    case "1":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     STEP 1: PREDICT TARGET POSITION                       ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                        TargetMotionComputer.ShowMotionComputerTool(enemyPosition, enemyVelocity, engine.SelectedDifficulty);
                        Console.WriteLine("\n✓ Step 1 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocity, muzzleVelocity, targetRadarCrossSection, enemyPosition, enemyVelocity);
                        break;

                    case "2":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     STEP 2: CALCULATE REQUIREMENTS                       ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                        BallisticsTablesReference.ShowReferencesMenu(currentTierIndex: engine.CurrentWaveNumber > 0 ? GameConstants.GetTierForWave(engine.CurrentWaveNumber).TierIndex : 0, currentDifficulty: engine.SelectedDifficulty);
                        Console.WriteLine("\n✓ Step 2 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocity, muzzleVelocity, targetRadarCrossSection, enemyPosition, enemyVelocity);
                        break;

                    case "3":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     STEP 3: PLAN TRAJECTORY                              ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                        TrajectoryPlotter.ShowTrajectoryPlotterTool(engine.SelectedDifficulty);
                        Console.WriteLine("\n✓ Step 3 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocity, muzzleVelocity, targetRadarCrossSection, enemyPosition, enemyVelocity);
                        break;

                    case "4":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     STEP 4: TEST SOLUTION (TEST MODE)                    ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
                        FireSimulator.ShowSimulatorTool(
                            enemyPosition,
                            enemyVelocity,
                            (float)projectileMass,
                            (float)muzzleVelocity,
                            engine.SelectedDifficulty);  // Pass difficulty
                        Console.WriteLine("\n✓ Step 4 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocity, muzzleVelocity, targetRadarCrossSection, enemyPosition, enemyVelocity);
                        break;

                    case "5":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     STEP 5: ENTER FINAL SOLUTION                         ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                        Console.WriteLine("=== ENTER YOUR FIRING PARAMETERS ===\n");
                        float playerLaunchDelayTime = GetPlayerTimeInput("Launch delay time (seconds): ");
                        float playerTargetElevation = GetPlayerElevationInput("Target elevation angle (-90 to 90 degrees): ");
                        float playerTargetAzimuth = GetPlayerAzimuthInput("Target azimuth bearing (0-360 degrees, 0=North): ");
                        float playerLaunchVelocity = GetPlayerVelocityInput($"Launch velocity ({minVelocity:F0}-{muzzleVelocity:F0} m/s): ");

                        Console.WriteLine();

                        var solution = calculator.CalculateSolution(
                            enemyPosition,
                            enemyVelocity,
                            playerLaunchDelayTime,
                            playerTargetElevation,
                            playerTargetAzimuth,
                            playerLaunchVelocity,
                            (float)muzzleVelocity,
                            (float)tier.MaxEffectiveGunRange,
                            engine.CurrentWaveNumber,
                            target.Mass,
                            engine.SelectedDifficulty);

                        DisplayFiringAnalysis(solution, playerLaunchDelayTime, playerTargetElevation, playerTargetAzimuth,
                            playerLaunchVelocity);

                        Console.WriteLine("Firing...\n");
                        System.Threading.Thread.Sleep(1000);

                        bool hitResult = solution.CanDestroy && solution.CanHit;

                        DisplayDebugCalculations(solution, projectileMass, playerLaunchVelocity, targetRadarCrossSection);

                        if (hitResult)
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
                            }
                            else
                            {
                                engine.WavesDefeated++;
                                engine.CurrentPhase = GameState.GamePhase.WaveComplete;
                                engine.AutoSaveGame();
                            }
                        }
                        else
                        {
                            Console.WriteLine("✗ MISS! Your ballistic solution was inaccurate or lacked sufficient energy.");
                            engine.IsGameOver = true;
                        }

                        workflowComplete = true;
                        break;

                    case "0":
                        Console.Clear();
                        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║     DIRECT FIRING SOLUTION ENTRY (SKIP WORKFLOW)         ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                        Console.WriteLine("=== ENTER FIRING PARAMETERS ===\n");
                        float directDelayTime = GetPlayerTimeInput("Launch delay time (seconds): ");
                        float directElevation = GetPlayerElevationInput("Target elevation angle (-90 to 90 degrees): ");
                        float directAzimuth = GetPlayerAzimuthInput("Target azimuth bearing (0-360 degrees, 0=North): ");
                        float directVelocity = GetPlayerVelocityInput($"Launch velocity ({minVelocity:F0}-{muzzleVelocity:F0} m/s): ");

                        Console.WriteLine();

                        var directSolution = calculator.CalculateSolution(
                            enemyPosition,
                            enemyVelocity,
                            directDelayTime,
                            directElevation,
                            directAzimuth,
                            directVelocity,
                            (float)muzzleVelocity,
                            (float)tier.MaxEffectiveGunRange,
                            engine.CurrentWaveNumber,
                            target.Mass,
                            engine.SelectedDifficulty);

                        DisplayFiringAnalysis(directSolution, directDelayTime, directElevation, directAzimuth, directVelocity);

                        Console.WriteLine("Firing...\n");
                        System.Threading.Thread.Sleep(1000);

                        bool directHitResult = directSolution.CanDestroy && directSolution.CanHit;

                        DisplayDebugCalculations(directSolution, projectileMass, directVelocity, targetRadarCrossSection);

                        if (directHitResult)
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
                            }
                            else
                            {
                                engine.WavesDefeated++;
                                engine.CurrentPhase = GameState.GamePhase.WaveComplete;
                                engine.AutoSaveGame();
                            }
                        }
                        else
                        {
                            Console.WriteLine("✗ MISS! Your ballistic solution was inaccurate or lacked sufficient energy.");
                            engine.IsGameOver = true;
                        }

                        workflowComplete = true;
                        break;

                    default:
                        Console.WriteLine("Invalid selection. Please try again.\n");
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        // ====================================================================
        // HELPER METHODS FOR FIRING ANALYSIS
        // ====================================================================

        private void DisplayWorkflowContext(FiringProblem firingProblem, EnemyTarget target, float minVelocity,
            double muzzleVelocity, double targetRadarCrossSection, Vector3 enemyPosition, Vector3 enemyVelocity)
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            FIRING SOLUTION & ENGAGEMENT PHASE             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("=== YOUR WEAPON ===");
            Console.WriteLine($"Projectile Mass: {(engine.SelectedGunProjectileSpec?.ProjectileMassKg ?? engine.Gun.DefaultProjectile.Mass):F1} kg");
            Console.WriteLine($"Max Muzzle Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Has Guidance System: {(engine.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}");
            Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange)}\n");

            Console.WriteLine("=== TARGET DATA FOR CALCULATIONS ===");
            Console.WriteLine($"Designation: {target.Name}");
            Console.WriteLine($"Enemy Approach Vector:");
            Console.WriteLine($"  Elevation: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachElevation, engine.SelectedDifficulty)}° (in sky)");
            Console.WriteLine($"  Azimuth: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachAzimuth, engine.SelectedDifficulty)}° (bearing)");
            Console.WriteLine($"  Distance: {GameConstants.FormatDistance((double)firingProblem.EngagementDistance)}");
            Console.WriteLine($"  Cartesian Position: {FiringPhaseFormatter.FormatVector3(enemyPosition, engine.SelectedDifficulty)}");
            Console.WriteLine($"Enemy Velocity Vector: ({FiringPhaseFormatter.FormatVelocity(enemyVelocity.X, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(enemyVelocity.Y, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(enemyVelocity.Z, engine.SelectedDifficulty)}) m/s");
            Console.WriteLine($"Approach Speed: {FiringPhaseFormatter.FormatVelocity(firingProblem.ApproachSpeed, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Fracture Energy Required: {FiringPhaseFormatter.FormatEnergy(firingProblem.FractureEnergyRequired, engine.SelectedDifficulty)}");
            Console.WriteLine($"Target Radar Cross-Section: {FiringPhaseFormatter.FormatRadarCrossSection(targetRadarCrossSection, engine.SelectedDifficulty)} m²\n");
        }

        private void DisplayFiringAnalysis(FiringSolutionResult solution, float delayTime, float elevation,
            float azimuth, float velocity)
        {
            // Analysis display (can be expanded if needed)
        }

        private void DisplayDebugCalculations(FiringSolutionResult solution, double mass, float velocity, double targetRCS)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              RESULTS                                      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("=== ENERGY CALCULATION ===");
            Console.WriteLine($"Formula: KE = 0.5 × mass × velocity²");
            Console.WriteLine($"  Mass: {mass:F1} kg");
            Console.WriteLine($"  Velocity: {velocity:F0} m/s");

            double displayVelSq = velocity * velocity;
            double displayEnergyJ = 0.5 * mass * displayVelSq;
            double displayEnergyMJ = displayEnergyJ / 1_000_000.0;

            Console.WriteLine($"  Calculation: 0.5 × {mass:F1} × ({velocity:F0})²");
            Console.WriteLine($"  = 0.5 × {mass:F1} × {displayVelSq:F0}");
            Console.WriteLine($"  = {displayEnergyMJ:F1} MJ");
            Console.WriteLine($"Required: {solution.FractureEnergyRequired:F0} MJ");
            Console.WriteLine($"✓ Energy Check: {(solution.CanDestroy ? "PASS" : "FAIL")} ({displayEnergyMJ:F1} MJ vs {solution.FractureEnergyRequired:F0} MJ threshold)\n");

            Console.WriteLine("=== INTERCEPT ACCURACY ===");
            if (solution.EnemyInterceptPoint.HasValue)
            {
                Vector3 enemyAtT = solution.EnemyInterceptPoint.Value;
                Console.WriteLine($"Enemy at intercept: {enemyAtT}");
                Console.WriteLine($"  Position deviation: {solution.InterceptDeviation:F1} meters");
                
                // Calculate and display actual hit tolerance used
                var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);
                double hitTolerance;
                if (diffConfig.IsTutorialMode)
                {
                    hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;  // 1.0m
                    Console.WriteLine($"  Hit tolerance: {hitTolerance:F1} m (beachball radius)");
                }
                else
                {
                    // For non-tutorial modes, estimate hit tolerance from RCS
                    // This is approximate - the real calculation is in FiringSolution
                    double diameterFromRCS = 2.0 * Math.Sqrt(targetRCS / Math.PI);
                    hitTolerance = diameterFromRCS * 0.5 * diffConfig.HitToleranceMultiplier;
                    Console.WriteLine($"  Hit tolerance: {hitTolerance:F1} m (from {targetRCS:F1} m² RCS)");
                }
                
                Console.WriteLine($"✓ Accuracy Check: {(solution.CanHit ? "PASS" : "FAIL")} ({solution.InterceptDeviation:F1}m deviation vs {hitTolerance:F1}m tolerance)");
            }
            else
            {
                Console.WriteLine($"  ERROR: No intercept point calculated");
                Console.WriteLine($"✗ Accuracy Check: FAIL\n");
            }

            Console.WriteLine("\n=== OVERALL SOLUTION VALIDITY ===");
            Console.WriteLine($"Energy sufficient: {(solution.CanDestroy ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Accuracy valid: {(solution.CanHit ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Solution valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Result: {(solution.CanDestroy && solution.CanHit ? "✓ HIT" : "✗ MISS")}\n");
        }

        private float GetPlayerTimeInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine() ?? "0";

                if (float.TryParse(input, out float time) && time >= 0)
                {
                    return time;
                }

                Console.WriteLine("Invalid input. Please enter a non-negative time value in seconds.\n");
            }
        }

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

        // ADD THIS METHOD before the closing brace of the class:

        private void RunWaveCompletePhase()
        {
            if (engine.IsGameOver)
                return;

            engine.AdvanceToNextWave();
        }

        /// <summary>
        /// Display difficulty selection menu for new game.
        /// Returns selected difficulty.
        /// </summary>
        public static GameDifficulty ShowDifficultySelection()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
                Console.WriteLine("║             SELECT YOUR SCENARIO                          ║");
                Console.WriteLine("║         How will you defend against the threat?           ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

                var configs = DifficultyConfig.GetAllConfigs();

                for (int i = 0; i < configs.Count; i++)
                {
                    Console.WriteLine($"\n[{i + 1}] {configs[i].DisplayName}");
                    Console.WriteLine("────────────────────────────────────────────────────────────");
                    Console.WriteLine(configs[i].NarrativeDescription);
                    Console.WriteLine();
                }

                Console.WriteLine("\n[Q] Quit\n");
                Console.Write($"Select scenario (1-{configs.Count} or Q): ");

                string input = Console.ReadLine()?.Trim() ?? "";

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    return GameDifficulty.RealSpacegunSimulator;
                }

                if (int.TryParse(input, out int choice) && choice >= 1 && choice <= configs.Count)
                {
                    return configs[choice - 1].Difficulty;
                }

                Console.WriteLine("\nInvalid selection. Please try again.");
                System.Threading.Thread.Sleep(1500);
            }
        }
    }
}