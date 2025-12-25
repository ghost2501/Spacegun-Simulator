using System;
using System.IO;
using System.Text;
using Spacegun_Simulator;
using Spacegun_Simulator.FireControlTools;
using Spacegun_Simulator.UI;
using Spacegun_Simulator.UI.Screen;
using PageBuffer = Spacegun_Simulator.UI.Screen.PageBuffer;

namespace Spacegun_Simulator
{
    public class ConsoleUI
    {
        private readonly GameState engine;
        private const string SaveDirectory = "Saves";

        private readonly ScreenLayout screenLayout;
        private readonly TextWriter? originalConsoleOut;
        private readonly IndentTextWriter indentWriter;

        // Buffer used during a BeginBufferedPage/EndBufferedPage sequence.
        private global::Spacegun_Simulator.UI.Screen.PageBuffer? _pageBuffer;

        public ConsoleUI(GameState engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            EnsureSaveDirectory();

            // Keep original Console.Out so we can write raw (no global indent) when rendering full-width art.
            originalConsoleOut = Console.Out;

            // Create and install the global indent writer once.
            indentWriter = new IndentTextWriter(originalConsoleOut, indentSpaces: 30);
            Console.SetOut(indentWriter);

            // Use the same left indent as the global IndentTextWriter so centering math aligns.
            screenLayout = new ScreenLayout(offset: indentWriter.IndentLength, frameWidth: 60);

            // Try to load optional side art files (place your art in Assets\ascii-art\SideLeft.txt / SideRight.txt)
            try
            {
                string baseAssets = Path.Combine(AppContext.BaseDirectory, "Assets", "ascii-art");
                string leftPath = Path.Combine(baseAssets, "SideLeft.txt");
                string rightPath = Path.Combine(baseAssets, "SideRight.txt");
                screenLayout.LoadSideArt(File.Exists(leftPath) ? leftPath : null, File.Exists(rightPath) ? rightPath : null);
            }
            catch
            {
                // Non-fatal - side art is optional
            }

            // One-time diagnostic: write to Console.Error (not captured by IndentTextWriter) and to layout_debug.txt
            try
            {
                int winW = 0, winH = 0;
                try { winW = Console.WindowWidth; winH = Console.WindowHeight; } catch { }

                int leftW = 0; foreach (var s in screenLayout.LeftArt) if (!string.IsNullOrEmpty(s) && s.Length > leftW) leftW = s.Length;
                int rightW = 0; foreach (var s in screenLayout.RightArt) if (!string.IsNullOrEmpty(s) && s.Length > rightW) rightW = s.Length;
                int frameW = screenLayout.FrameWidth;
                int offset = screenLayout.Offset;
                int totalWidth = leftW + frameW + rightW;
                int padLeftNoOffset = Math.Max(0, (winW - totalWidth) / 2);
                int padLeftWithOffset = padLeftNoOffset + offset;

                string msg = $"[LayoutDebug] Window={winW}x{winH} | Indent={indentWriter.IndentLength} | ScreenLayout.Offset={offset} | FrameWidth={frameW} | LeftW={leftW} | RightW={rightW} | TotalWidth={totalWidth} | PadNoOffset={padLeftNoOffset} | PadWithOffset={padLeftWithOffset}";

                try { originalConsoleOut?.WriteLine(msg); } catch { }
                try { Console.Error.WriteLine(msg); } catch { }

                try
                {
                    var f = Path.Combine(AppContext.BaseDirectory, "layout_debug.txt");
                    File.AppendAllText(f, DateTime.UtcNow.ToString("s") + " " + msg + Environment.NewLine);
                }
                catch { }

                try
                {
                    string f2 = Path.Combine(Directory.GetCurrentDirectory(), "layout_debug.txt");
                    File.AppendAllText(f2, DateTime.UtcNow.ToString("s") + " " + msg + Environment.NewLine);
                }
                catch { }
            }
            catch { /* non-fatal */ }
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
            // Single session: assumes engine has already been initialized (new game or autosave loaded).
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

                    default:
                        // Safety fallback
                        engine.IsGameOver = true;
                        break;
                }
            }

            DisplayGameOverScreen();

        }

        /// <summary>
        /// Display the game over screen with final stats, then wait for input to return to menu.
        /// Deletes the auto-save to prevent resuming completed games.
        /// </summary>
        private void DisplayGameOverScreen()
        {
            var header = new System.Collections.Generic.List<string>
            {
                //"▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                //"░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                //"                                                             ",
                //"                     GAME OVER                               ",
                "",
                "",
                "",
                "      (                         ( /(                     ",
                "      )\\ )      )    )     (    )\\())  )     (  (        ",
                "     (()/(   ( /(   (     ))\\  ((_)\\  /((   ))\\ )(       ",
                "      /(_))_ )(_))  )\\  '/((_)   ((_)(_))\\ /((_|()\\      ",
                "     (_)) __((_)_ _((_))(_))    / _ \\_)((_|_))  ((_)     ",
                "       | (_ / _` | '  \\() -_)  | (_) \\ V // -_)| '_|     ",
                "        \\___\\__,_|_|_|_|\\___|   \\___/ \\_/ \\___||_|       ",                                                    

                string.Empty
            };

            Console.Clear();
            RenderBufferedPage("GameOver", header, () =>
            {
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
            });

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
                // Non-critical
                originalConsoleOut?.WriteLine($"Note: Could not delete save file: {ex.Message}");
            }
        }



        /// <summary>
        /// Centralised cursor-placement used by MainMenu, RenderPageFrame and RenderBufferedPage.
        /// Place the input cursor inside the center frame using no-offset coordinates returned by ScreenLayout.
        /// contentLeftNoOffset and promptRowNoOffset are coordinates in the raw (no-indent) console space.
        /// This method restores the global indent writer and maps the no-offset column to the indented column.
        /// It never writes padding lines — only moves the cursor.
        /// </summary>
        private void PositionPromptCursor_NoOffset(int contentLeftNoOffset, int promptRowNoOffset)
        {
            try
            {
                // Restore indented writer for subsequent input/output.
                Console.SetOut(indentWriter);

                int col = Math.Max(0, contentLeftNoOffset + indentWriter.IndentLength);

                // Place prompts in a dedicated area BELOW the main frame to avoid overwriting
                // any buffered page content. Use a bottom area (reserve 2 lines) inside the visible window.
                int targetRow;
                try
                {
                    int winH = Console.WindowHeight;
                    // Reserve two lines at bottom for status/prompt. If console very small, this clamps to 0.
                    int bottomPromptRow = Math.Max(0, winH - 3);
                    // Allow callers' promptRowNoOffset only if it is already below the header area,
                    // otherwise prefer the bottom prompt row to keep prompt external to central window.
                    targetRow = Math.Max(promptRowNoOffset, bottomPromptRow);

                    // Clamp to buffer height when possible.
                    if (targetRow >= Console.BufferHeight) targetRow = Console.BufferHeight - 1;
                    if (targetRow < 0) targetRow = 0;
                }
                catch
                {
                    // Fallback when console metrics not available
                    targetRow = Math.Max(promptRowNoOffset, 0);
                }

                // Some consoles disallow SetCursorPosition to rows ahead of current cursor.
                // Try SetCursorPosition, otherwise write newlines until we reach the target row.
                try
                {
                    Console.SetCursorPosition(col, targetRow);
                }
                catch
                {
                    try
                    {
                        // Best-effort: move to desired column at current row, then advance by writing blank lines.
                        Console.SetCursorPosition(col, Console.CursorTop);
                    }
                    catch { /* ignore */ }

                    int current = 0;
                    try { current = Console.CursorTop; } catch { current = 0; }
                    while (current < targetRow)
                    {
                        Console.WriteLine();
                        current++;
                    }

                    try { Console.SetCursorPosition(col, Math.Min(targetRow, Console.CursorTop)); } catch { /* ignore */ }
                }
            }
            catch
            {
                // Ensure Console.Out remains the indent writer on failure.
                try { Console.SetOut(indentWriter); } catch { }
            }
        }

        private void RunTestModeMenu()
        {
            while (true)
            {
                var header = new System.Collections.Generic.List<string>
                {
                    "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                    "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                    "                                                             ",
                    "               TEST MODE - DEBUG TOOLS                       ",

                    string.Empty
                };

                Console.Clear();
                RenderPageFrame(header);

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

        private void RunFiringChallenge()
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               FIRING CHALLENGE MODE (DEBUG)                 ",

                string.Empty
            };

            Console.Clear();
            RenderPageFrame(header);

            GameDifficulty difficulty = ShowDifficultySelection();
            engine.SelectedDifficulty = difficulty;

            engine.CurrentWaveNumber = 1;
            engine.IsGameOver = false;
            engine.WavesDefeated = 0;
            engine.CurrentPhase = GameState.GamePhase.Detection;

            var diffConfig = DifficultyConfig.GetConfig(difficulty);
            Console.WriteLine($"\nDifficulty: {diffConfig.DisplayName}");

            if (engine.CampaignEnemyType == null)
            {
                engine.CampaignEnemyType = EnemyType.GenerateForCampaign(engine.rng ?? new Random());
            }

            Console.WriteLine("\n[INITIALIZATION] Generating firing challenge wave...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var detectionResult = engine.ExecuteDetectionPhase();

                if (!detectionResult.WaveDetected)
                {
                    Console.WriteLine("\n✗ Wave detection failed. Challenge canceled.");
                    System.Threading.Thread.Sleep(2000);
                    return;
                }

                stopwatch.Stop();
                Console.WriteLine($"\n✓ Challenge generated in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine("\nPress any key to skip directly to firing phase...\n");
                Console.ReadKey();

                engine.CurrentPhase = GameState.GamePhase.Firing;
                RunFiringPhase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Failed to generate firing challenge: {ex.Message}");
                System.Threading.Thread.Sleep(2000);
            }
        }

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
            // Build the framed center block
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               THREAT DETECTED!                              ",
                $"               Wave {engine.CurrentWaveNumber} of {GameConstants.TotalWaves}".PadRight(57) + " ",

                string.Empty
            };

            // Create a page renderer that composes ScreenLayout and the existing writers.
            var renderer = new global::Spacegun_Simulator.PageRenderer(screenLayout, originalConsoleOut, indentWriter, indentWriter.IndentLength);

            // Compute detection (do this before rendering so contentWriter can use the result
            // and the method can make control-flow decisions afterwards).
            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);
            var detectionResult = engine.ExecuteDetectionPhase();

            // Use the centralized buffered renderer so flush coordinates match the side-art rendering.
            RenderBufferedPage("Detection", header, () =>
            {
                // This lambda runs with Console.Out redirected to the page buffer.
                Console.WriteLine("=== DETECTION PHASE ===\n");
                Console.WriteLine(detectionResult.Message);

                if (!detectionResult.WaveDetected)
                {
                    Console.WriteLine("\n✗ MISSION FAILED");
                    // Return from the content writer; ScreenLayout will flush/restore.
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
                Console.WriteLine($"Difficulty: {DifficultyText.DescribeStars(archetype.BaseDifficultyRating)}");
                Console.WriteLine();

                Console.WriteLine($"=== ENEMY PROFILE ===");
                Console.WriteLine($"Type: {detectionResult.Wave.Targets[0].Name}");
                Console.WriteLine($"Detection Distance: {GameConstants.FormatDistance(detectionResult.Wave.CurrentDistance)}");
                Console.WriteLine($"Velocity: {GameConstants.FormatVelocity(detectionResult.Wave.AverageVelocity)}");

                // For tutorial mode, use fixed beachball RCS; otherwise apply multiplier
                if (diffConfig.IsTutorialMode)
                {
                    Console.WriteLine($"Radar Cross-Section: {DifficultyConfig.TutorialBeachball.CrossSectionM2:F2} m² (beachball)");
                }
                else
                {
                    double displayRCS = detectionResult.Wave.AverageRadarCrossSection * diffConfig.TargetRcsMultiplier;
                    Console.WriteLine($"Radar Cross-Section: {displayRCS:F1} m²");
                }

                Console.WriteLine($"\n=== TIME BUDGET ===");
                Console.WriteLine($"Years Available: {(long)detectionResult.AvailableYears} years");

                Console.WriteLine($"\n=== CURRENT RESOURCES ===");
                Console.WriteLine($"Budget: {engine.Resources.Budget:F0}");
                Console.WriteLine($"Steel: {engine.Resources.Steel:F0} tons");
                Console.WriteLine($"Exotic Materials: {engine.Resources.ExoticMaterials:F1} units");
            });

            // After rendering, inspect the detection result and continue control flow.
            if (!detectionResult.WaveDetected)
            {
                // Detection failed — game over
                engine.IsGameOver = true;
                return;
            }

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
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               RESOURCES & RESEARCH                          ",

                string.Empty
            };

            // Compute wave event and effective rates BEFORE rendering so the displayed
            // content and the interactive loop share the same data scope.
            engine.GenerateWaveEvent();
            var eventMultiplier = engine.CurrentWaveEvent?.ProductionMultiplier ?? 1.0;

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

            // Use RenderBufferedPage to show the initial statistics block (reads engine.CurrentWaveEvent & effectiveRates)
            RenderBufferedPage("ResourceAllocation", header, () =>
            {
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

                Console.WriteLine("=== RESOURCE PRODUCTION RATES (per year, with tech & difficulty) ===");

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
            });

            // ===== FLEXIBLE WORKFLOW: Loop until player is ready for development =====
            bool readyForDevelopment = false;

            while (!readyForDevelopment)
            {
                // For each iteration, render the action menu inside its own buffered page to keep layout stable
                var optionsHeader = new System.Collections.Generic.List<string>
                {
                    "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                    "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                    "                                                             ",
                    "               RESOURCES & RESEARCH                          ",

                    string.Empty
                };

                RenderBufferedPage("ResourceOptions", optionsHeader, () =>
                {
                    Console.WriteLine("\n=== OPTIONS ===");
                    Console.WriteLine("[R] Spend Time on Resources");
                    Console.WriteLine("[T] Spend Time on Research");
                    Console.WriteLine("[S] Show Current Status");
                    Console.WriteLine("[D] Proceed to Development\n");
                });

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
                        Thread.Sleep(800);
                        break;
                }
            }

            // Final summary
            var summaryHeader = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               RESOURCES  - SUMMARY                          ",

                string.Empty
            };

            RenderBufferedPage("PreparationSummary", summaryHeader, () =>
            {
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
            });

            Console.WriteLine("\nPress any key to proceed to Development phase...");
            Console.ReadKey();

            // Transition to development and auto-save
            engine.CurrentPhase = GameState.GamePhase.Development;
            engine.AutoSaveGame();
        }

        private void AllocateResourcesInteractive(Dictionary<ResourceType, double> effectiveRates)
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               RESOURCE ALLOCATION                           ",

                string.Empty
            };

            Console.Clear();
            RenderPageFrame(header);

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
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               RESEARCH TECHNOLOGY                           ",

                string.Empty
            };

            Console.Clear();
            // Buffer the header so it stays aligned with side art, then flush before prompting
            RenderBufferedPage("ResearchMenu", header, () =>
            {
                TechUnlock.DisplayAvailableTechs(engine.TechTree, engine.AccumulatedResources);
                Console.WriteLine();
            });

            Console.Write("Research tech number (or 0 to skip): ");
            // Read input after the header has been rendered
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
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               PREPARATION PHASE STATUS                      ",

                string.Empty
            };

            Console.Clear();
            RenderBufferedPage("PreparationStatus", header, () =>
            {
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
            });

            Console.WriteLine("\nPress any key to return to Preparation menu...");
            Console.ReadKey();
        }

        private void RunDevelopmentPhase()
        {
            bool developmentComplete = false;

            while (!developmentComplete)
            {
                var header = new System.Collections.Generic.List<string>
                {
                    "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                    "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                    "                                                             ",
                    "               WEAPON DEVELOPMENT                            ",

                    string.Empty
                };

                Console.Clear();
                // Buffer static display, prompt after
                RenderBufferedPage("WeaponDevelopment", header, () =>
                {
                    // ===== RESOURCES SUMMARY =====
                    Console.WriteLine("=== AVAILABLE RESOURCES ===");
                    Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0}");
                    Console.WriteLine($"  Steel:  {engine.AccumulatedResources["Steel"]:F0} tons");
                    Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units\n");

                    // ===== TARGET REQUIREMENT =====
                    if (engine.CurrentWave?.Archetype != null)
                    {
                        var archetype = engine.CurrentWave.Archetype;
                        Console.WriteLine("=== TARGET REQUIREMENT ===");
                        Console.WriteLine($"  Archetype: {archetype.Name}");
                        Console.WriteLine($"  Fracture Energy Needed: {archetype.FractureEnergyRange.Min:N0} - {archetype.FractureEnergyRange.Max:N0} MJ");
                        Console.WriteLine($"  Mass: {archetype.MassRange.Min:N0} - {archetype.MassRange.Max:N0} metric tons");
                        Console.WriteLine($"  Difficulty: {BallisticsCalculator.GetDifficultyDescription(archetype.BaseDifficultyRating)}");
                        Console.WriteLine();
                    }

                    // ===== CURRENT WEAPON TECH STATUS =====
                    Console.WriteLine("=== CURRENT WEAPON TECHNOLOGY ===");
                    Console.WriteLine($"  Weapons Tech:     Level {engine.TechTree.CurrentLevel[TechTree.TechType.Weapons]} - {TechTree.GetTechDescription(TechTree.TechType.Weapons, engine.TechTree.CurrentLevel[TechTree.TechType.Weapons])}");
                    Console.WriteLine($"  Projectiles Tech: Level {engine.TechTree.CurrentLevel[TechTree.TechType.Projectiles]} - {TechTree.GetTechDescription(TechTree.TechType.Projectiles, engine.TechTree.CurrentLevel[TechTree.TechType.Projectiles])}");

                    // ===== CURRENT WEAPON CONFIGURATION =====
                    Console.WriteLine("\n=== CURRENT WEAPON CONFIGURATION ===");
                    if (engine.CraftedProjectile != null)
                    {
                        var proj = engine.CraftedProjectile;
                        Console.WriteLine($"  Projectile: {proj.DisplayName}");
                        Console.WriteLine($"  Mass: {proj.MassKg} kg | Velocity: {proj.MaxVelocityMs:N0} m/s");
                        Console.WriteLine($"  Kinetic Energy: {proj.EffectiveKineticEnergyMJ:N0} MJ");
                        if (proj.HitToleranceMultiplier != 1.0)
                            Console.WriteLine($"  Hit Tolerance Bonus: {(proj.HitToleranceMultiplier - 1) * 100:+0}%");
                    }
                    else
                    {
                        Console.WriteLine("  Projectile: [NOT CONFIGURED]");
                        Console.WriteLine("  ⚠ You must develop a projectile before firing!");
                    }

                    Console.WriteLine($"\n  Gun Configuration:");
                    Console.WriteLine($"    Barrel Integrity: {engine.Gun.BarrelIntegrity:P0}");
                    Console.WriteLine($"    Power Capacity: {engine.Gun.PowerCapacity:F0} MW");
                    Console.WriteLine($"    Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange)}");

                    // ===== DEVELOPMENT OPTIONS =====
                    Console.WriteLine("\n=== OPTIONS ===");
                    Console.WriteLine("[P] Projectile Development - Craft a new projectile");
                    Console.WriteLine("[G] Gun Development - Upgrade gun systems");
                    Console.WriteLine("[S] Show Detailed Status");
                    if (engine.CraftedProjectile != null)
                        Console.WriteLine("[D] Done - Proceed to Firing Phase");
                    else
                        Console.WriteLine("[D] Done - (Requires projectile configuration)");
                });

                // Interaction loop unchanged (prompts & choices occur after header flush)
                Console.Write("\nSelect action (P/G/S/D): ");
                string action = Console.ReadLine()?.ToUpper() ?? "";

                switch (action)
                {
                    case "P":
                        RunProjectileDevelopment();
                        break;
                    case "G":
                        RunGunDevelopment();
                        break;
                    case "S":
                        DisplayDetailedWeaponStatus();
                        break;
                    case "D":
                        if (engine.CraftedProjectile != null)
                            developmentComplete = true;
                        else
                        {
                            Console.WriteLine("\n✗ You must configure a projectile before proceeding!");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey();
                        }
                        break;
                    default:
                        Console.WriteLine("\nInvalid action.");
                        Thread.Sleep(1000);
                        break;
                }
            }

            // Transition to firing phase
            engine.CurrentPhase = GameState.GamePhase.Firing;
            Console.WriteLine("\n✓ Weapon development complete. Proceeding to firing phase...");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        /// <summary>
        /// Projectile Development submenu - craft a projectile from components.
        /// Velocity comes from the gun; propulsion provides Delta-V boost.
        /// </summary>
        private void RunProjectileDevelopment()
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               PROJECTILE DEVELOPMENT                        ",

                string.Empty
            };

            // Prepare data before rendering so interaction after the buffered header can reference it.
            int weaponsTechLevel = engine.TechTree.CurrentLevel[TechTree.TechType.Weapons];
            double gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);

            // Ensure we have the difficulty config available for later tutorial messaging/logic.
            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);

            var unlockedCores = CraftedProjectile.GetUnlockedCores(engine.TechTree);
            var unlockedPropulsion = CraftedProjectile.GetUnlockedPropulsion(engine.TechTree);
            var unlockedEnhancements = CraftedProjectile.GetUnlockedEnhancements(engine.TechTree);

            ProjectileCore? selectedCore = null;

            // Buffer the configuration process (we flush before user prompts)
            RenderBufferedPage("ProjectileDevelopment", header, () =>
            {
                Console.WriteLine("=== AVAILABLE RESOURCES ===");
                Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0}");
                Console.WriteLine($"  Steel:  {engine.AccumulatedResources["Steel"]:F0} tons");
                Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units\n");

                // Display gun base velocity
                Console.WriteLine("=== GUN SPECIFICATIONS ===");
                Console.WriteLine($"  Weapons Tech Level: {weaponsTechLevel}");
                Console.WriteLine($"  Base Muzzle Velocity: {gunBaseVelocity:N0} m/s ({gunBaseVelocity / 1000:N0} km/s)");
                Console.WriteLine($"  Barrel Integrity: {engine.Gun.BarrelIntegrity:P2}\n");

                Console.WriteLine("=== STEP 1: SELECT PROJECTILE CORE ===");
                Console.WriteLine("(Determines projectile mass)\n");

                for (int i = 0; i < unlockedCores.Count; i++)
                {
                    var core = unlockedCores[i];
                    double baseKE = BallisticsCalculator.CalculateKineticEnergyMJ(core.MassKg, gunBaseVelocity);
                    Console.WriteLine($"[{i + 1}] {core.Name}");
                    Console.WriteLine($"    Mass: {core.MassKg} kg");
                    Console.WriteLine($"    Base KE (gun only): {baseKE:N0} MJ");
                    Console.WriteLine($"    Cost: {core.Cost.Budget:F0} Budget, {core.Cost.Steel:F0} Steel, {core.Cost.ExoticMaterials:F0} Exotic");
                    Console.WriteLine($"    {core.Description}\n");
                }
            });

            // Now prompt the user using the precomputed lists
            while (selectedCore == null)
            {
                Console.Write("Select core (1-" + unlockedCores.Count + "): ");
                if (int.TryParse(Console.ReadLine(), out int coreChoice) && coreChoice >= 1 && coreChoice <= unlockedCores.Count)
                {
                    selectedCore = unlockedCores[coreChoice - 1];
                }
                else
                {
                    Console.WriteLine("Invalid selection.\n");
                }
            }
            Console.WriteLine($"\n✓ Selected: {selectedCore.Name}\n");

            // Step 2: Select Propulsion (optional - provides Delta-V)
            Console.WriteLine("=== STEP 2: SELECT PROPULSION SYSTEM (OPTIONAL) ===");
            Console.WriteLine("(Provides Delta-V boost during flight - unlocked at Projectiles Tech 2)\n");

            bool hasPropulsionOptions = unlockedPropulsion.Count > 1;  // More than just "None"

            for (int i = 0; i < unlockedPropulsion.Count; i++)
            {
                var prop = unlockedPropulsion[i];

                if (prop.Id == "none")
                {
                    double baseKE = BallisticsCalculator.CalculateKineticEnergyMJ(selectedCore.MassKg, gunBaseVelocity);
                    Console.WriteLine($"[{i + 1}] {prop.Name} (no boost)");
                    Console.WriteLine($"    Velocity: {gunBaseVelocity:N0} m/s (gun only)");
                    Console.WriteLine($"    KE: {baseKE:N0} MJ");
                    Console.WriteLine($"    Cost: FREE\n");
                }
                else
                {
                    // Calculate max velocity with full Delta-V
                    double maxDeltaV = prop.CalculateEffectiveDeltaV(selectedCore.MassKg, prop.BurnDurationSeconds);
                    double maxVelocity = gunBaseVelocity + maxDeltaV;
                    double maxKE = BallisticsCalculator.CalculateKineticEnergyMJ(selectedCore.MassKg, maxVelocity);

                    Console.WriteLine($"[{i + 1}] {prop.Name}");
                    Console.WriteLine($"    Delta-V: +{prop.DeltaVCapacityMs:N0} m/s over {prop.BurnDurationSeconds:F1}s burn");
                    Console.WriteLine($"    Effective Delta-V (for {selectedCore.MassKg}kg): +{maxDeltaV:N0} m/s");
                    Console.WriteLine($"    Max Velocity: {maxVelocity:N0} m/s ({maxVelocity / 1000:N0} km/s)");
                    Console.WriteLine($"    Max KE: {maxKE:N0} MJ");
                    Console.WriteLine($"    Cost: {prop.Cost.Budget:F0} Budget, {prop.Cost.Steel:F0} Steel, {prop.Cost.ExoticMaterials:F0} Exotic");
                    Console.WriteLine($"    {prop.Description}\n");
                }
            }

            PropulsionSystem selectedPropulsion = PropulsionSystem.None;
            Console.Write($"Select propulsion (1-{unlockedPropulsion.Count}, or Enter for none): ");
            string propInput = Console.ReadLine() ?? "";
            if (int.TryParse(propInput, out int propChoice) && propChoice >= 1 && propChoice <= unlockedPropulsion.Count)
            {
                selectedPropulsion = unlockedPropulsion[propChoice - 1];
                Console.WriteLine($"\n✓ Selected: {selectedPropulsion.Name}\n");
            }
            else
            {
                Console.WriteLine("\n✓ No propulsion selected (using gun velocity only).\n");
            }

            // Step 3: Select Enhancement (optional)
            Console.WriteLine("=== STEP 3: SELECT ENHANCEMENT (OPTIONAL) ===");
            Console.WriteLine("(Modifies accuracy or damage)\n");

            for (int i = 0; i < unlockedEnhancements.Count; i++)
            {
                var enh = unlockedEnhancements[i];
                string bonusText = "";
                if (enh.HitToleranceBonus != 1.0)
                    bonusText += $"Hit Tolerance: {(enh.HitToleranceBonus - 1) * 100:+0;-0}%  ";
                if (enh.EnergyEfficiencyBonus != 1.0)
                    bonusText += $"Damage: {(enh.EnergyEfficiencyBonus - 1) * 100:+0;-0}%";

                Console.WriteLine($"[{i + 1}] {enh.Name}");
                if (!string.IsNullOrEmpty(bonusText))
                    Console.WriteLine($"    Bonuses: {bonusText}");
                if (enh.Id != "none")
                    Console.WriteLine($"    Cost: {enh.Cost.Budget:F0} Budget, {enh.Cost.Steel:F0} Steel, {enh.Cost.ExoticMaterials:F0} Exotic");
                Console.WriteLine($"    {enh.Description}\n");
            }

            ProjectileEnhancement selectedEnhancement = ProjectileEnhancement.None;
            Console.Write("Select enhancement (1-" + unlockedEnhancements.Count + ", or Enter to skip): ");
            string enhInput = Console.ReadLine() ?? "";
            if (int.TryParse(enhInput, out int enhChoice) && enhChoice >= 1 && enhChoice <= unlockedEnhancements.Count)
            {
                selectedEnhancement = unlockedEnhancements[enhChoice - 1];
                Console.WriteLine($"\n✓ Selected: {selectedEnhancement.Name}\n");
            }
            else
            {
                Console.WriteLine("\n✓ No enhancement selected.\n");
            }

            // Create the crafted projectile with gun base velocity
            var craftedProjectile = new CraftedProjectile(selectedCore, selectedPropulsion, selectedEnhancement, gunBaseVelocity);

            // Display final configuration (buffered summary)
            var configHeader = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "                PROJECTILE CONFIGURATION - SUMMARY           ",

                string.Empty
            };

            Console.Clear();
            RenderBufferedPage("ProjectileConfigSummary", configHeader, () =>
            {
                Console.WriteLine($"  Configuration: {craftedProjectile.DisplayName}");
                Console.WriteLine($"  Projectile Mass: {craftedProjectile.MassKg} kg");
                Console.WriteLine($"  Gun Base Velocity: {craftedProjectile.GunBaseMuzzleVelocityMs:N0} m/s");

                if (selectedPropulsion.Id != "none")
                {
                    double maxDeltaV = selectedPropulsion.CalculateEffectiveDeltaV(craftedProjectile.MassKg, selectedPropulsion.BurnDurationSeconds);
                    Console.WriteLine($"  Propulsion Delta-V: +{maxDeltaV:N0} m/s");
                    Console.WriteLine($"  Max Velocity: {craftedProjectile.MaxVelocityMs:N0} m/s");
                }

                Console.WriteLine($"  Max KE: {craftedProjectile.RawKineticEnergyMJ:N0} MJ");
                Console.WriteLine($"  Effective Kinetic Energy: {craftedProjectile.EffectiveKineticEnergyMJ:N0} MJ");
                if (craftedProjectile.HitToleranceMultiplier != 1.0)
                    Console.WriteLine($"  Hit Tolerance: {(craftedProjectile.HitToleranceMultiplier - 1) * 100:+0}%");

                Console.WriteLine($"\n  TOTAL COST:");
                Console.WriteLine($"    Budget: {craftedProjectile.TotalCost.Budget:F0}");
                Console.WriteLine($"    Steel:  {craftedProjectile.TotalCost.Steel:F0}");
                Console.WriteLine($"    Exotic: {craftedProjectile.TotalCost.ExoticMaterials:F0}");
            });

            // Check if meets requirement (using max KE as upper bound)
            if (engine.CurrentWave?.Archetype != null)
            {
                bool meetsRequirement = craftedProjectile.EffectiveKineticEnergyMJ >= engine.CurrentWave.Archetype.FractureEnergyRange.Min;
                Console.WriteLine($"\n  Target Requirement: {(meetsRequirement ? "✓ MEETS REQUIREMENT" : "✗ INSUFFICIENT ENERGY")}");

                // Special message for tutorial mode (friendly beachball target)
                if (diffConfig.IsTutorialMode)
                {
                    Console.WriteLine("  Note: Tutorial mode uses a fixed beachball target with known RCS.");
                }
            }

            // Check affordability
            bool canAfford = CraftedProjectile.CanAfford(craftedProjectile, engine.AccumulatedResources);
            Console.WriteLine($"  Affordability: {(canAfford ? "✓ CAN AFFORD" : "✗ INSUFFICIENT RESOURCES")}");

            if (!canAfford)
            {
                Console.WriteLine("\n✗ Cannot afford this configuration. Please select different components.");
                Console.WriteLine("Press any key to return to Weapon Development...");
                Console.ReadKey();
                return;
            }

            Console.Write("\nConfirm build? (Y/N): ");
            string confirm = Console.ReadLine()?.ToUpper() ?? "N";

            if (confirm != "Y")
            {
                Console.WriteLine("Build cancelled.");
                Console.WriteLine("Press any key to return to Weapon Development...");
                Console.ReadKey();
                return;
            }

            // Deduct resources
            engine.AccumulatedResources["Budget"] -= craftedProjectile.TotalCost.Budget;
            engine.AccumulatedResources["Steel"] -= craftedProjectile.TotalCost.Steel;
            engine.AccumulatedResources["Exotic"] -= craftedProjectile.TotalCost.ExoticMaterials;

            // Store crafted projectile
            engine.CraftedProjectile = craftedProjectile;

            Console.WriteLine("\n✓ Projectile built successfully!");
            Console.WriteLine($"\nRemaining Resources:");
            Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0}");
            Console.WriteLine($"  Steel:  {engine.AccumulatedResources["Steel"]:F0}");
            Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1}");

            Console.WriteLine("Press any key to return to Weapon Development...");
            Console.ReadKey();
        }

        private void RunGunDevelopment()
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "                GUN DEVELOPMENT                              ",

                string.Empty
            };

            Console.Clear();
            // Buffer static display, prompt after
            RenderBufferedPage("GunDevelopment", header, () =>
            {
                Console.WriteLine("=== AVAILABLE RESOURCES ===");
                Console.WriteLine($"  Budget: {engine.AccumulatedResources["Budget"]:F0}");
                Console.WriteLine($"  Steel:  {engine.AccumulatedResources["Steel"]:F0} tons");
                Console.WriteLine($"  Exotic: {engine.AccumulatedResources["Exotic"]:F1} units\n");

                Console.WriteLine("=== CURRENT GUN STATUS ===");
                Console.WriteLine($"  Barrel Integrity: {engine.Gun.BarrelIntegrity:P0}");
                Console.WriteLine($"  Power Capacity: {engine.Gun.PowerCapacity:F0} MW");
                Console.WriteLine($"  Weapons Tech Level: {engine.TechTree.CurrentLevel[TechTree.TechType.Weapons]}\n");

                Console.WriteLine("=== AVAILABLE UPGRADES ===\n");
            });

            // Define available gun upgrades
            var upgrades = new List<(string Name, string Description, ResourceCost Cost, Action Apply)>(
                new (string, string, ResourceCost, Action)[] {
                    ("Barrel Repair", "Restore barrel integrity to 100%",
                        new ResourceCost(budget: 100, steel: 50, exotic: 0),
                        () => engine.Gun.BarrelIntegrity = 1.0),

                    ("Power Capacitor Upgrade", "Increase power capacity by 20%",
                        new ResourceCost(budget: 150, steel: 80, exotic: 20),
                        () => engine.Gun.PowerCapacity *= 1.2),

                    ("Reinforced Barrel", "Reduce barrel degradation per shot by 50%",
                        new ResourceCost(budget: 200, steel: 120, exotic: 40),
                        () => { /* Future: implement barrel reinforcement tracking */ })
                });

            // Display upgrade options and costs
            for (int i = 0; i < upgrades.Count; i++)
            {
                var (name, description, cost, _) = upgrades[i];
                bool canAfford = engine.AccumulatedResources["Budget"] >= cost.Budget &&
                                 engine.AccumulatedResources["Steel"] >= cost.Steel &&
                                 engine.AccumulatedResources["Exotic"] >= cost.ExoticMaterials;

                string affordMark = canAfford ? "✓" : "✗";
                Console.WriteLine($"[{i + 1}] {affordMark} {name}");
                Console.WriteLine($"    {description}");
                Console.WriteLine($"    Cost: {cost.Budget:F0} Budget, {cost.Steel:F0} Steel, {cost.ExoticMaterials:F0} Exotic\n");
            }

            Console.WriteLine("[0] Cancel - Return to Weapon Development\n");

            Console.Write($"Select upgrade (0-{upgrades.Count}): ");
            if (int.TryParse(Console.ReadLine(), out int choice))
            {
                if (choice == 0) return;

                if (choice >= 1 && choice <= upgrades.Count)
                {
                    var (name, _, cost, apply) = upgrades[choice - 1];

                    bool canAfford = engine.AccumulatedResources["Budget"] >= cost.Budget &&
                                     engine.AccumulatedResources["Steel"] >= cost.Steel &&
                                     engine.AccumulatedResources["Exotic"] >= cost.ExoticMaterials;

                    if (!canAfford)
                    {
                        Console.WriteLine("\n✗ Cannot afford this upgrade.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        return;
                    }

                    Console.Write($"\nApply {name}? (Y/N): ");
                    if (Console.ReadLine()?.ToUpper() == "Y")
                    {
                        engine.AccumulatedResources["Budget"] -= cost.Budget;
                        engine.AccumulatedResources["Steel"] -= cost.Steel;
                        engine.AccumulatedResources["Exotic"] -= cost.ExoticMaterials;

                        apply();

                        Console.WriteLine($"\n✓ {name} applied successfully!");
                    }
                    else
                    {
                        Console.WriteLine("\nUpgrade cancelled.");
                    }
                }
            }

            Console.WriteLine("\nPress any key to return to Weapon Development...");
            Console.ReadKey();
        }

        private void RunFiringPhase()
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               FIRING SOLUTION                               ",

                string.Empty
            };

            // Compute firing result and dependent state first.
            var firingResult = engine.ExecuteFiringPhase();
            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);
            var firingProblem = engine.CurrentFiringProblem;
            var target = engine.CurrentWave?.Targets.Count > 0 ? engine.CurrentWave.Targets[0] : null;

            // Render and buffer the header + summary using the PageRenderer helper.
            RenderBufferedPage("Firing", header, () =>
            {
                if (!firingResult.CanReachTarget)
                {
                    Console.WriteLine("✗ " + firingResult.Message);
                    Console.WriteLine("\nTarget is beyond effective gun range. Mission failed.");
                    return;
                }

                if (target == null)
                {
                    Console.WriteLine("✗ No valid target found!");
                    return;
                }

                if (engine.CurrentWave == null)
                {
                    Console.WriteLine("✗ Critical error: Wave data lost during firing phase!");
                    return;
                }

                if (firingProblem == null)
                {
                    Console.WriteLine("✗ Critical error: Firing problem not initialized!");
                    return;
                }

                if (engine.SelectedGunProjectileSpec == null)
                {
                    Console.WriteLine("✗ Critical error: No weapon selected!");
                    return;
                }

                double muzzleVelocity = engine.SelectedGunProjectileSpec.MuzzleVelocityMs;
                double projectileMass = engine.SelectedGunProjectileSpec.ProjectileMassKg;

                var calculator = new FiringSolution(
                    (float)projectileMass,
                    (float)target.FractureEnergy,
                    target.Mass);

                float minVelocity = calculator.CalculateRequiredVelocity();
                float maxVelocity = (float)muzzleVelocity;
                double targetRadarCrossSection = target.CrossSection;
                double displayRCS = targetRadarCrossSection * diffConfig.TargetRcsMultiplier;

                Console.WriteLine($"=== YOUR WEAPON ===");
                Console.WriteLine($"Projectile Mass: {FiringPhaseFormatter.FormatMass(projectileMass, engine.SelectedDifficulty)} kg");
                Console.WriteLine($"Max Muzzle Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, engine.SelectedDifficulty)} m/s");
                Console.WriteLine($"Barrel Integrity: {engine.Gun.BarrelIntegrity:P2}");
                Console.WriteLine($"Has Guidance System: {(engine.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}");
                Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange)}\n");

                Console.WriteLine("=== TARGET DATA FOR CALCULATIONS ===");
                Console.WriteLine($"Designation: {target.Name}");
                Console.WriteLine($"Enemy Approach Vector:");
                Console.WriteLine($"  Elevation: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachElevation, engine.SelectedDifficulty)}° (in sky)");
                Console.WriteLine($"  Azimuth: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachAzimuth, engine.SelectedDifficulty)}° (bearing)");
                Console.WriteLine($"  Distance: {GameConstants.FormatDistance((double)firingProblem.EngagementDistance)}");
                Console.WriteLine($"  Cartesian Position: {FiringPhaseFormatter.FormatVector3(firingProblem.EnemyPosition, engine.SelectedDifficulty)}");
                Console.WriteLine($"Enemy Velocity Vector: ({FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.X, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Y, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Z, engine.SelectedDifficulty)}) m/s");
                Console.WriteLine($"Approach Speed: {FiringPhaseFormatter.FormatVelocity(firingProblem.ApproachSpeed, engine.SelectedDifficulty)} m/s");
                Console.WriteLine($"Fracture Energy Required: {FiringPhaseFormatter.FormatEnergy(firingProblem.FractureEnergyRequired, engine.SelectedDifficulty)}");

                if (diffConfig.IsTutorialMode)
                {
                    double hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;
                    Console.WriteLine($"Hit Tolerance: {hitTolerance:F1} m (beachball radius)\n");
                }
                else
                {
                    Console.WriteLine($"Target Radar Cross-Section: {FiringPhaseFormatter.FormatRadarCrossSection(displayRCS, engine.SelectedDifficulty)} m²\n");
                }
            });

            // Handle early failure cases (match previous behavior)
            if (!firingResult.CanReachTarget)
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                engine.IsGameOver = true;
                return;
            }

            if (target == null || engine.CurrentFiringProblem == null || engine.SelectedGunProjectileSpec == null)
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                engine.IsGameOver = true;
                return;
            }

            // Interactive workflow continues as before...
            bool workflowComplete = false;

            // Recompute commonly used locals for the interactive loop
            var calculatorForLoop = new FiringSolution(
                (float)engine.SelectedGunProjectileSpec.ProjectileMassKg,
                (float)target.FractureEnergy,
                target.Mass);

            float minVelocityLoop = calculatorForLoop.CalculateRequiredVelocity();
            float maxVelocityLoop = (float)engine.SelectedGunProjectileSpec.MuzzleVelocityMs;
            double displayRcsLoop = target.CrossSection * diffConfig.TargetRcsMultiplier;

            while (!workflowComplete)
            {
                Console.WriteLine("=== FIRING SOLUTION & FIRE ASSIST TOOLS ===\n");
                Console.WriteLine("[1] PREDICT TARGET POSITION (Target Motion Calculator)");
                Console.WriteLine("[2] CALCULATE REQUIREMENTS (Ballistic Tables)");
                Console.WriteLine("[3] PLAN TRAJECTORY (Projectile Trajectory Plotter)");
                Console.WriteLine("[4] TEST SOLUTION (Fire Simulator)");
                Console.WriteLine("[5] ENTER FINAL SOLUTION (Commit & Fire)");
                Console.WriteLine("[0] SKIP WORKFLOW (Direct Entry)\n");

                Console.Write("Select step (0-5): ");
                string choice = Console.ReadLine() ?? "0";

                switch (choice)
                {
                    case "1":
                        Console.Clear();
                        var step1Header = new System.Collections.Generic.List<string>
                        {
                            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "                                                             ",
                            "              PREDICT TARGET POSITION                        ",

                            string.Empty
                        };
                        // Wrap tool in buffered page so header alignment is preserved
                        RenderBufferedPage("MotionComputer", step1Header, () =>
                        {
                            TargetMotionComputer.ShowMotionComputerTool(
                                firingProblem.EnemyPosition,
                                firingProblem.EnemyVelocity,
                                engine.SelectedDifficulty,
                                screenLayout,
                                originalConsoleOut,
                                indentWriter,
                                indentWriter.IndentLength);
                        });
                        Console.WriteLine("\n✓ Step 1 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocityLoop, engine.SelectedGunProjectileSpec.MuzzleVelocityMs, target.CrossSection, firingProblem.EnemyPosition, firingProblem.EnemyVelocity);
                        break;

                    case "2":
                        Console.Clear();
                        var step2Header = new System.Collections.Generic.List<string>
                        {
                            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "                                                             ",
                            "               CALCULATE REQUIREMENTS                        ",

                            string.Empty
                        };

                        // Render header raw and then call the ballistics reference tool (non-buffered).
                        RenderPageFrame(step2Header);
                        BallisticsTablesReference.ShowReferencesMenu(currentTierIndex: engine.CurrentWaveNumber > 0 ? GameConstants.GetTierForWave(engine.CurrentWaveNumber).TierIndex : 0, currentDifficulty: engine.SelectedDifficulty);

                        Console.WriteLine("\n✓ Step 2 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocityLoop, engine.SelectedGunProjectileSpec.MuzzleVelocityMs, target.CrossSection, firingProblem.EnemyPosition, firingProblem.EnemyVelocity);
                        break;

                    case "3":
                        Console.Clear();
                        var step3Header = new System.Collections.Generic.List<string>
                        {
                            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "                                                             ",
                            "               PLAN TRAJECTORY                               ",

                            string.Empty
                        };
                        RenderBufferedPage("TrajectoryPlotter", step3Header, () =>
                        {
                            TrajectoryPlotter.ShowTrajectoryPlotterTool(
                                engine.SelectedDifficulty,
                                screenLayout,
                                originalConsoleOut,
                                indentWriter,
                                indentWriter.IndentLength);
                        });
                        Console.WriteLine("\n✓ Step 3 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocityLoop, engine.SelectedGunProjectileSpec.MuzzleVelocityMs, target.CrossSection, firingProblem.EnemyPosition, firingProblem.EnemyVelocity);
                        break;

                    case "4":
                        Console.Clear();
                        var step4Header = new System.Collections.Generic.List<string>
                        {
                            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "                                                             ",
                            "               SIMULATION (TEST MODE)                        ",

                            string.Empty
                        };
                        RenderBufferedPage("FireSimulator", step4Header, () =>
                        {
                            FireSimulator.ShowSimulatorTool(
                                firingProblem.EnemyPosition,
                                firingProblem.EnemyVelocity,
                                (float)engine.SelectedGunProjectileSpec.ProjectileMassKg,
                                (float)engine.SelectedGunProjectileSpec.MuzzleVelocityMs,
                                engine.SelectedDifficulty);
                        });
                        Console.WriteLine("\n✓ Step 4 complete.\n");
                        System.Threading.Thread.Sleep(1000);
                        DisplayWorkflowContext(firingProblem, target, minVelocityLoop, engine.SelectedGunProjectileSpec.MuzzleVelocityMs, target.CrossSection, firingProblem.EnemyPosition, firingProblem.EnemyVelocity);
                        break;

                    case "5":
                        Console.Clear();
                        var step5Header = new System.Collections.Generic.List<string>
                        {
                            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "                                                             ",
                            "               COMMIT FIRING SOLUTION                        ",

                            string.Empty
                        };
                        // Buffer header + static label, prompts happen after the buffered header
                        RenderBufferedPage("EnterFiringParameters", step5Header, () =>
                        {
                            Console.WriteLine("=== ENTER YOUR FIRING PARAMETERS ===\n");
                        });

                        float playerLaunchDelayTime = GetPlayerTimeInput("Launch delay (seconds): ");
                        float playerTargetElevation = GetPlayerElevationInput("Target elevation (-90 to 90 degrees): ");
                        float playerTargetAzimuth = GetPlayerAzimuthInput("Target azimuth (0-360 degrees, 0=North): ");

                        float playerLaunchVelocity = GetPlayerVelocityInput(
                            $"Launch velocity ({0:N0}-{maxVelocityLoop:N0} m/s): ");

                        Console.WriteLine();

                        var solution = calculatorForLoop.CalculateSolution(
                            firingProblem.EnemyPosition,
                            firingProblem.EnemyVelocity,
                            playerLaunchDelayTime,
                            playerTargetElevation,
                            playerTargetAzimuth,
                            playerLaunchVelocity,
                            (float)engine.SelectedGunProjectileSpec.MuzzleVelocityMs,
                            (float)GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange,
                            engine.CurrentWaveNumber,
                            target.Mass,
                            engine.SelectedDifficulty);

                        DisplayFiringAnalysis(solution, playerLaunchDelayTime, playerTargetElevation, playerTargetAzimuth,
                            playerLaunchVelocity);

                        Console.WriteLine("Firing...\n");
                        System.Threading.Thread.Sleep(1000);

                        bool hitResult = solution.CanDestroy && solution.CanHit;

                        // Show animated visualization
                        double animFlightTime = firingProblem.EnemyPosition.Magnitude / Math.Max(1.0, playerLaunchVelocity) * 1.5;
                        DisplayAnimatedShot(
                            firingProblem.EnemyPosition,
                            firingProblem.EnemyVelocity,
                            playerLaunchDelayTime,
                            playerTargetElevation,
                            playerTargetAzimuth,
                            playerLaunchVelocity,
                            Math.Min(animFlightTime, 10.0),
                            hitResult);

                        DisplayDebugCalculations(solution, engine.SelectedGunProjectileSpec.ProjectileMassKg, playerLaunchVelocity, displayRcsLoop);

                        // ===== APPLY BARREL DEGRADATION (GAMEPLAY ONLY) =====
                        if (engine.Gun != null)
                        {
                            bool barrelStillOk = engine.Gun.RegisterShot();
                            Console.WriteLine($"\nBarrel Integrity (post-shot): {engine.Gun.BarrelIntegrity:P2}");
                            if (!barrelStillOk)
                            {
                                Console.WriteLine("\n✗ Barrel integrity failed after shot. The gun is unusable until repaired.");
                                engine.IsGameOver = true;
                            }
                        }

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

                    default:
                        Console.WriteLine("Invalid selection. Please try again.\n");
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

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
            PageMusicSystem.PlayForPage("Difficulties");

            while (true)
            {
                var header = new System.Collections.Generic.List<string>
        {
            "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
            "                                                             ",
            "                         DIFFICULTY                          ",
            string.Empty
        };

                Console.Clear();

                // Get page art overrides
                var (left, right) = PageArtOverrides.Get("Difficulties");

                // Render with sides (you'll need to get the screenLayout instance here)
                // If this is a static method and you can't access instance members easily,
                // you can keep the simple rendering but at least make it consistent:

                foreach (var line in header)
                {
                    Console.WriteLine(line);
                }

                var configs = DifficultyConfig.GetAllConfigs();

                for (int i = 0; i < configs.Count; i++)
                {
                    Console.WriteLine($"[{i + 1}] {configs[i].DisplayName}");
                    Console.WriteLine("────────────────────────────────────────────────────────────");
                    Console.WriteLine(configs[i].NarrativeDescription);
                    Console.WriteLine();
                }

                Console.WriteLine("[Q] Quit\n");
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

        private void RenderBufferedPage(string pageKey, System.Collections.Generic.IList<string> headerLines, Action contentWriter)
        {
            // Start music for this page (only changes if different track)
            PageMusicSystem.PlayForPage(pageKey);

            // Get overrides from the centralized PageArtOverrides class
            var (left, right) = PageArtOverrides.Get(pageKey);

            // Use ScreenLayout's BeginBufferedFrame / EndBufferedFrame directly to ensure
            // the buffered content is flushed at the exact coordinates used for the header/art.
            // BeginBufferedFrame installs the PageBuffer as Console.Out for us.
            try
            {
                // NOTE: BeginBufferedFrame now returns raw (no-indent) coordinates.
                (int contentLeftNoOffset, int contentTop) = screenLayout.BeginBufferedFrame(
                    headerLines,
                    originalConsoleOut,
                    indentWriter,
                    left,
                    right);
                try
                {
                    // Run the content writer while the page buffer is active (Console.Out -> PageBuffer).
                    contentWriter();
                }
                finally
                {
                    // Flush buffer into the raw console and restore indented writer.
                    // EndBufferedFrame now returns the first row after the flushed content.
                    int promptRowAfterContent = screenLayout.EndBufferedFrame(contentLeftNoOffset, contentTop);
                    // Position input cursor inside the center frame using the centralized helper.
                    // Pass the first free row so prompts render below all buffered game text.
                    PositionPromptCursor_NoOffset(contentLeftNoOffset, promptRowAfterContent);
                }
            }
            catch
            {
                // On any failure, ensure Console.Out is the indented writer so UI remains usable.
                try { Console.SetOut(indentWriter); } catch { }
            }
        }

        private int RenderPageFrame(System.Collections.Generic.IList<string> centerLines, string? leftOverride = null, string? rightOverride = null, string? pageKey = null)
        {
            if (!string.IsNullOrEmpty(pageKey))
            {
                var art = PageArtOverrides.Get(pageKey);
                leftOverride = leftOverride ?? art.Left;
                rightOverride = rightOverride ?? art.Right;
            }

            // Capture start row so we place the prompt relative to the frame top.
            int startRowNoOffset = 0;
            try { startRowNoOffset = Console.CursorTop; } catch { startRowNoOffset = 0; }

            try
            {
                Console.SetOut(originalConsoleOut ?? Console.Out);
                screenLayout.RenderWithSides_NoOffset(centerLines, leftOverride, rightOverride);

                int contentLeftNoOffset = screenLayout.CalculateContentLeft_NoOffset(centerLines, leftOverride, rightOverride);

                // Compute the desired prompt row: directly after the center frame content.
                int promptRowNoOffset = startRowNoOffset + centerLines.Count;

                // Use central helper to position cursor (maps no-offset to indented coordinates).
                PositionPromptCursor_NoOffset(contentLeftNoOffset, promptRowNoOffset);

                return contentLeftNoOffset + indentWriter.IndentLength;
            }
            catch
            {
                // On failure, fall back to normal rendering path (indented)
                try { Console.SetOut(indentWriter); } catch { }
                screenLayout.RenderWithSides(centerLines, leftOverride, rightOverride);
                int contentLeft = screenLayout.CalculateContentLeft(centerLines, leftOverride, rightOverride);
                try { Console.SetCursorPosition(Math.Max(0, contentLeft), Console.CursorTop); } catch { }
                return contentLeft;
            }
        }

        // ====================================================================
        // HELPER METHODS FOR FIRING ANALYSIS
        // ====================================================================

        private void DisplayWorkflowContext(FiringProblem firingProblem, EnemyTarget target, float minVelocity,
            double muzzleVelocity, double targetRadarCrossSection, Vector3 enemyPosition, Vector3 enemyVelocity)
        {
            // Apply RCS multiplier for display
            var diffConfig = DifficultyConfig.GetConfig(engine.SelectedDifficulty);
            double displayRCS = targetRadarCrossSection * diffConfig.TargetRcsMultiplier;

            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               FIRING SOLUTION             ",

                string.Empty
            };

            Console.Clear();
            RenderPageFrame(header);

            // Determine projectile mass from selected spec, crafted projectile, or gun default.
            double projectileMass = engine.SelectedGunProjectileSpec?.ProjectileMassKg
                                    ?? engine.CraftedProjectile?.MassKg
                                    ?? engine.Gun.DefaultProjectile.Mass;

            Console.WriteLine("=== YOUR WEAPON ===");
            Console.WriteLine($"Projectile Mass: {FiringPhaseFormatter.FormatMass(projectileMass, engine.SelectedDifficulty)} kg");
            Console.WriteLine($"Max Muzzle Velocity: {FiringPhaseFormatter.FormatVelocity(muzzleVelocity, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Barrel Integrity: {engine.Gun.BarrelIntegrity:P2}");
            Console.WriteLine($"Has Guidance System: {(engine.Gun.DefaultProjectile.HasGuidance ? "Yes" : "No")}");
            Console.WriteLine($"Gun Effective Range: {GameConstants.FormatDistance(GameConstants.GetTierForWave(engine.CurrentWaveNumber).MaxEffectiveGunRange)}\n");

            Console.WriteLine("=== TARGET DATA FOR CALCULATIONS ===");
            Console.WriteLine($"Designation: {target.Name}");
            Console.WriteLine($"Enemy Approach Vector:");
            Console.WriteLine($"  Elevation: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachElevation, engine.SelectedDifficulty)}° (in sky)");
            Console.WriteLine($"  Azimuth: {FiringPhaseFormatter.FormatAngle(firingProblem.ApproachAzimuth, engine.SelectedDifficulty)}° (bearing)");
            Console.WriteLine($"  Distance: {GameConstants.FormatDistance((double)firingProblem.EngagementDistance)}");
            Console.WriteLine($"  Cartesian Position: {FiringPhaseFormatter.FormatVector3(firingProblem.EnemyPosition, engine.SelectedDifficulty)}");
            Console.WriteLine($"Enemy Velocity Vector: ({FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.X, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Y, engine.SelectedDifficulty)}, {FiringPhaseFormatter.FormatVelocity(firingProblem.EnemyVelocity.Z, engine.SelectedDifficulty)}) m/s");
            Console.WriteLine($"Approach Speed: {FiringPhaseFormatter.FormatVelocity(firingProblem.ApproachSpeed, engine.SelectedDifficulty)} m/s");
            Console.WriteLine($"Fracture Energy Required: {FiringPhaseFormatter.FormatEnergy(firingProblem.FractureEnergyRequired, engine.SelectedDifficulty)}");

            if (diffConfig.IsTutorialMode)
            {
                double hitTolerance = DifficultyConfig.TutorialBeachball.RadiusMeters;
                Console.WriteLine($"Hit Tolerance: {hitTolerance:F1} m (beachball radius)\n");
            }
            else
            {
                Console.WriteLine($"Target Radar Cross-Section: {FiringPhaseFormatter.FormatRadarCrossSection(displayRCS, engine.SelectedDifficulty)} m²\n");
            }
        }

        private void DisplayFiringAnalysis(FiringSolutionResult solution, float delayTime, float elevation,
            float azimuth, float velocity)
        {
            // Keep lightweight; the    debug view is in DisplayDebugCalculations.
            Console.WriteLine("\n=== FIRING ANALYSIS SUMMARY ===");
            Console.WriteLine($"  Launch Delay: {delayTime:F2}s");
            Console.WriteLine($"  Elevation: {elevation:F2}°   Azimuth: {azimuth:F2}°");
            Console.WriteLine($"  Launch Velocity: {velocity:F0} m/s");
            Console.WriteLine($"  Energy Needed: {solution.FractureEnergyRequired:F0} MJ");
            Console.WriteLine($"  Can Destroy: {(solution.CanDestroy ? "Yes" : "No")}");

            // Show canHit only if solution is valid (else it will always be "Yes" for untried solutions)
            Console.WriteLine($"  Can Hit: {(solution.SolutionValid ? (solution.CanHit ? "Yes" : "No") : "N/A")}");

            // Indicate if solution is valid
            Console.WriteLine($"  Solution Valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}\n");
        }

        private void DisplayDebugCalculations(FiringSolutionResult solution, double mass, float velocity, double targetRCS)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("               RESULTS                                       ");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            Console.WriteLine("=== ENERGY CALCULATION ===");
            Console.WriteLine($"Formula: KE = 0.5 × mass × velocity²");
            Console.WriteLine($"  Mass: {mass:F1} kg");
            Console.WriteLine($"  Velocity: {velocity:F0} m/s");

            // Delegate KE computation to canonical BallisticsCalculator
            double displayEnergyMJ = BallisticsCalculator.CalculateKineticEnergyMJ(mass, velocity);

            Console.WriteLine($"  Calculation: 0.5 × {mass:F1} × ({velocity:F0})²");
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

            // Show canHit only if solution is valid (else it will always be "Yes" for untried solutions)
            Console.WriteLine($"Accuracy valid: {(solution.SolutionValid ? (solution.CanHit ? "✓ Yes" : "✗ No") : "N/A")}");
            Console.WriteLine($"Solution valid: {(solution.SolutionValid ? "✓ Yes" : "✗ No")}");
            Console.WriteLine($"Result: {(solution.CanDestroy && solution.CanHit ? "✓ HIT" : "✗ MISS")}\n");
        }

        // Also updated animated projectile calculation to use BallisticsCalculator
        private void DisplayAnimatedShot(
            Vector3 enemyPosition,
            Vector3 enemyVelocity,
            double launchDelayTime,
            double elevation,
            double azimuth,
            double velocity,
            double maxFlightTime,
            bool isHit)
        {
            const int WIDTH = 60;   // Console width for animation
            const int HEIGHT = 20;  // Console height for animation
            const double FRAME_DELAY_MS = 50;  // Animation speed

            // Calculate scale factors to fit trajectory in view
            double maxDistance = Math.Max(enemyPosition.Magnitude * 1.2, 100);
            double scaleX = (WIDTH - 10) / maxDistance;
            double scaleY = (HEIGHT - 4) / (maxDistance * 0.5);  // Vertical is typically smaller

            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               FIRING VISUALIZATION                          ",

                string.Empty
            };



            RenderPageFrame(header);

            // Pre-calculate positions for smooth animation
            double timeStep = maxFlightTime / 100.0;  // 100 frames max
            var projectilePositions = new List<(double time, double x, double y)>();
            var targetPositions = new List<(double time, double x, double y)>();

            for (double t = 0; t <= maxFlightTime; t += timeStep)
            {
                // Use canonical trajectory math
                var projVec = BallisticsCalculator.CalculateProjectilePositionStatic(t, velocity, elevation, azimuth);
                double projX = projVec.Magnitude; // horizontal projection used below via horizontal distance
                double projZ = projVec.Z;

                projectilePositions.Add((t, projX, projZ));

                // Calculate target position at this time
                double totalTime = launchDelayTime + t;
                Vector3 targetPos = enemyPosition + (enemyVelocity * totalTime);
                double targetHorizontalDist = Math.Sqrt(targetPos.X * targetPos.X + targetPos.Y * targetPos.Y);
                targetPositions.Add((t, targetHorizontalDist, targetPos.Z));
            }

            // Animate frame by frame
            int frameCount = projectilePositions.Count;
            char[,] buffer = new char[HEIGHT, WIDTH];

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Clear buffer
                for (int row = 0; row < HEIGHT; row++)
                    for (int col = 0; col < WIDTH; col++)
                        buffer[row, col] = ' ';

                // Draw ground line
                for (int col = 0; col < WIDTH; col++)
                    buffer[HEIGHT - 1, col] = '─';

                // Draw axis labels
                buffer[HEIGHT - 1, 0] = '└';
                buffer[0, 0] = '│';
                for (int row = 1; row < HEIGHT - 1; row++)
                    buffer[row, 0] = '│';

                // Draw target (all frames up to current)
                for (int i = 0; i <= frame; i++)
                {
                    var (_, tx, ty) = targetPositions[i];
                    int targetCol = (int)(tx * scaleX) + 2;
                    int targetRow = HEIGHT - 2 - (int)(ty * scaleY);

                    if (targetCol >= 0 && targetCol < WIDTH && targetRow >= 0 && targetRow < HEIGHT - 1)
                    {
                        if (i == frame)
                            buffer[targetRow, targetCol] = '●'; // Current target position
                        else if (i % 5 == 0)
                            buffer[targetRow, targetCol] = '·';  // Target trail
                    }
                }

                // Draw projectile trail
                for (int i = Math.Max(0, frame - 10); i <= frame; i++)
                {
                    var (_, px, py) = projectilePositions[i];
                    int projCol = (int)(px * scaleX) + 2;
                    int projRow = HEIGHT - 2 - (int)(py * scaleY);

                    if (projCol >= 0 && projCol < WIDTH && projRow >= 0 && projRow < HEIGHT - 1)
                    {
                        if (i == frame)
                            buffer[projRow, projCol] = '◆';  // Current projectile position
                        else
                            buffer[projRow, projCol] = '·';  // Trail
                    }
                }

                // Draw gun position
                buffer[HEIGHT - 2, 2] = '▲';

                // Render buffer to console
                const int LEFT_MARGIN_WIDTH = 30;

                // Render buffer to console
                try { Console.SetCursorPosition(LEFT_MARGIN_WIDTH, 4); } catch { }  // Start at column 30
                for (int row = 0; row < HEIGHT; row++)
                {
                    try { Console.SetCursorPosition(LEFT_MARGIN_WIDTH, 4 + row); } catch { }  // Reset to column 30 each row
                    for (int col = 0; col < WIDTH; col++)
                        Console.Write(buffer[row, col]);
                    // Remove Console.WriteLine() - we're manually positioning instead
                }

                // Display current stats
                // Display current stats
                var (time, projDist, projAlt) = projectilePositions[frame];
                var (_, tgtDist, tgtAlt) = targetPositions[frame];
                double separation = Math.Sqrt(Math.Pow(projDist - tgtDist, 2) + Math.Pow(projAlt - tgtAlt, 2));

                try { Console.SetCursorPosition(LEFT_MARGIN_WIDTH, 4 + HEIGHT + 1); } catch { }
                Console.Write($"  Time: {time:F2}s  |  Projectile: {projDist:F0}m @ {projAlt:F1}m alt");

                try { Console.SetCursorPosition(LEFT_MARGIN_WIDTH, 4 + HEIGHT + 2); } catch { }
                Console.Write($"  Target: {tgtDist:F0}m @ {tgtAlt:F1}m alt  |  Separation: {separation:F1}m");

                try { Console.SetCursorPosition(LEFT_MARGIN_WIDTH, 4 + HEIGHT + 4); } catch { }
                Console.Write($"  ◆ = Projectile   ● = Target   ▲ = Gun");

                Thread.Sleep((int)FRAME_DELAY_MS);
            }

            // Final result
            Console.WriteLine();
            if (isHit)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ╔═══════════════════════════════════════╗");
                Console.WriteLine("              ★ DIRECT HIT! ★              ");
                Console.WriteLine("  ╚═══════════════════════════════════════╝");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ╔═══════════════════════════════════════╗");
                Console.WriteLine("                 ✗ MISS ✗                  ");
                Console.WriteLine("  ╚═══════════════════════════════════════╝");
                Console.ResetColor();
            }

            Console.WriteLine("\n  Press any key to continue...");
            Console.ReadKey();
        }

        private void DisplayDetailedWeaponStatus()
        {
            var header = new System.Collections.Generic.List<string>
            {
                "▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓",
                "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                "                                                             ",
                "               DETAILED WEAPON STATUS                        ",

                string.Empty
            };

            Console.Clear();
            RenderBufferedPage("DetailedWeaponStatus", header, () =>
            {
                // Tech Levels
                Console.WriteLine("=== TECHNOLOGY LEVELS ===");
                Console.WriteLine($"  Weapons:     Level {engine.TechTree.CurrentLevel[TechTree.TechType.Weapons]}");
                Console.WriteLine($"               {TechTree.GetTechDescription(TechTree.TechType.Weapons, engine.TechTree.CurrentLevel[TechTree.TechType.Weapons])}");
                Console.WriteLine($"  Projectiles: Level {engine.TechTree.CurrentLevel[TechTree.TechType.Projectiles]}");
                Console.WriteLine($"               {TechTree.GetTechDescription(TechTree.TechType.Projectiles, engine.TechTree.CurrentLevel[TechTree.TechType.Projectiles])}");

                // Gun Base Velocity
                int weaponsTechLevel = engine.TechTree.CurrentLevel[TechTree.TechType.Weapons];
                double gunBaseVelocity = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);
                Console.WriteLine($"\n=== GUN BASE VELOCITY ===");
                Console.WriteLine($"  Base Muzzle Velocity: {gunBaseVelocity:N0} m/s ({gunBaseVelocity / 1000:N0} km/s)");

                // Unlocked Components
                Console.WriteLine("\n=== UNLOCKED COMPONENTS ===");

                var cores = CraftedProjectile.GetUnlockedCores(engine.TechTree);
                Console.WriteLine($"  Cores ({cores.Count} available):");
                foreach (var core in cores)
                    Console.WriteLine($"    - {core.Name} ({core.MassKg} kg)");

                var propulsion = CraftedProjectile.GetUnlockedPropulsion(engine.TechTree);
                Console.WriteLine($"\n  Propulsion ({propulsion.Count} available):");
                foreach (var prop in propulsion)
                {
                    if (prop.Id == "none")
                        Console.WriteLine($"    - {prop.Name} (no boost)");
                    else
                        Console.WriteLine($"    - {prop.Name} (+{prop.DeltaVCapacityMs / 1000:N0} km/s Delta-V over {prop.BurnDurationSeconds:F1}s)");
                }

                var enhancements = CraftedProjectile.GetUnlockedEnhancements(engine.TechTree);
                Console.WriteLine($"\n  Enhancements ({enhancements.Count} available):");
                foreach (var enh in enhancements)
                    Console.WriteLine($"    - {enh.Name}");

                // Gun Status
                Console.WriteLine("\n=== GUN CONFIGURATION ===");
                Console.WriteLine($"  Barrel Integrity: {engine.Gun.BarrelIntegrity:P0}");
                Console.WriteLine($"  Power Capacity: {engine.Gun.PowerCapacity:F0} MW");
                Console.WriteLine($"  Weapons Tech Level: {engine.TechTree.CurrentLevel[TechTree.TechType.Weapons]}\n");

                // Current Projectile
                Console.WriteLine("\n=== CURRENT PROJECTILE ===");
                if (engine.CraftedProjectile != null)
                {
                    var proj = engine.CraftedProjectile;
                    Console.WriteLine($"  Configuration: {proj.DisplayName}");
                    Console.WriteLine($"  Mass: {proj.MassKg} kg");
                    Console.WriteLine($"  Gun Base Velocity: {proj.GunBaseMuzzleVelocityMs:N0} m/s");

                    if (proj.Propulsion.Id != "none")
                    {
                        double maxDeltaV = proj.Propulsion.CalculateEffectiveDeltaV(proj.MassKg, proj.Propulsion.BurnDurationSeconds);
                        Console.WriteLine($"  Propulsion Delta-V: +{maxDeltaV:N0} m/s");
                        Console.WriteLine($"  Max Velocity: {proj.MaxVelocityMs:N0} m/s");
                    }

                    Console.WriteLine($"  Max KE: {proj.RawKineticEnergyMJ:N0} MJ");
                    Console.WriteLine($"  Effective KE: {proj.EffectiveKineticEnergyMJ:N0} MJ");
                    if (proj.HitToleranceMultiplier != 1.0)
                        Console.WriteLine($"  Hit Tolerance: {(proj.HitToleranceMultiplier - 1) * 100:+0}%");
                }
                else
                {
                    Console.WriteLine("  [NOT CONFIGURED]");
                }
            });

            Console.WriteLine("\nPress any key to return to Weapon Development...");
            Console.ReadKey();
        }

        // IndentTextWriter class - handles global console indentation.
        private sealed class IndentTextWriter : TextWriter
        {
            private readonly TextWriter _inner;
            private readonly string _indent;
            private bool _beginLine = true;
            private readonly object _lock = new();

            public IndentTextWriter(TextWriter inner, int indentSpaces)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _indent = new string(' ', Math.Max(0, indentSpaces));
            }

            public int IndentLength => _indent.Length;

            public override Encoding Encoding => _inner.Encoding;

            private bool ShouldIndent()
            {
                if (!_beginLine) return false;
                try
                {
                    return Console.CursorLeft == 0;
                }
                catch
                {
                    return true;
                }
            }

            public override void Write(char value)
            {
                lock (_lock)
                {
                    if (_beginLine && ShouldIndent())
                    {
                        _inner.Write(_indent);
                        _beginLine = false;
                    }

                    _inner.Write(value);
                    _beginLine = value == '\n';
                }
            }

            public override void Write(string? value)
            {
                if (value == null) return;

                lock (_lock)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        char c = value[i];
                        if (_beginLine && ShouldIndent())
                        {
                            _inner.Write(_indent);
                            _beginLine = false;
                        }

                        _inner.Write(c);

                        if (c == '\n')
                            _beginLine = true;
                    }
                }
            }

            public override void Write(char[] buffer, int index, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (index < 0 || count < 0 || index + count > buffer.Length) throw new ArgumentOutOfRangeException();

                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        char c = buffer[index + i];
                        if (_beginLine && ShouldIndent())
                        {
                            _inner.Write(_indent);
                            _beginLine = false;
                        }

                        _inner.Write(c);
                        if (c == '\n') _beginLine = true;
                    }
                }
            }

            public override void WriteLine()
            {
                lock (_lock)
                {
                    if (_beginLine && ShouldIndent())
                    {
                        _inner.Write(_indent);
                        _beginLine = false;
                    }
                    _inner.WriteLine();
                    _beginLine = true;
                }
            }

            public override void WriteLine(string? value)
            {
                lock (_lock)
                {
                    Write(value);
                    _inner.WriteLine();
                    _beginLine = true;
                }
            }

            public override void Flush() => _inner.Flush();

            protected override void Dispose(bool disposing)
            {
                // Intentionally don't dispose the inner writer (Console.Out)
                base.Dispose(disposing);
            }
        }

        private float GetPlayerTimeInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return 0f;

                if (float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value >= 0f)
                    return value;

                Console.WriteLine("Invalid time. Enter a non-negative number (seconds).");
            }
        }

        private float GetPlayerElevationInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return 0f;

                if (float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value >= -90f && value <= 90f)
                    return value;

                Console.WriteLine("Invalid elevation. Enter a value between -90 and 90 degrees.");
            }
        }

        private float GetPlayerAzimuthInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return 0f;

                if (float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                {
                    value %= 360f;
                    if (value < 0f) value += 360f;
                    return value;
                }

                Console.WriteLine("Invalid azimuth. Enter a number (0-360).");
            }
        }

        private float GetPlayerVelocityInput(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return 0f;

                if (float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value >= 0f)
                    return value;

                Console.WriteLine("Invalid velocity. Enter a non-negative number (m/s).");
            }
        }
    }
}