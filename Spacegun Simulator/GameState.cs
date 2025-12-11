namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME STATE - 4-TURN SEQUENCE ARCHITECTURE
    // ============================================================================
    // Turn 1: Detection     → Identify threat, calculate available time
    // Turn 2: Allocation    → Spend years on resource gathering
    // Turn 3: Development   → Spend resources on gun upgrades
    // Turn 4: Firing        → Single shot engagement (hit = victory, miss = defeat)
    // ============================================================================

    public class GameState
    {
        public GunConfiguration Gun { get; set; }
        public DetectionSystem Detection { get; set; }
        public ResourcePool Resources { get; set; }
        public int CurrentWaveNumber { get; set; }
        public List<EnemyWave> CompletedWaves { get; set; }
        public bool IsGameOver { get; set; }
        public int WavesDefeated { get; set; }  // CHANGED: Unified - one wave = one enemy

        // 4-Turn sequence state
        public enum GamePhase
        {
            Detection,
            ResourceAllocation,
            Development,
            Firing,
            WaveComplete
        }

        public GamePhase CurrentPhase { get; set; }
        public EnemyWave? CurrentWave { get; private set; }
        public DetectionStatus? CurrentDetectionStatus { get; private set; }

        // Available time budget for current wave (in WHOLE years only)
        public long AvailableYears { get; private set; }
        public long RemainingYears { get; set; }

        // Store the actual seconds available for precise calculation
        private double availableSecondsForGunRange = 0;

        // ===== NEW: Selected gun/projectile spec for this wave =====
        public GunProjectileSpec? SelectedGunProjectileSpec { get; set; }

        // Accumulated resources during allocation phase (time spent as tokens)
        public Dictionary<string, double> AccumulatedResources { get; private set; } = new();

        internal readonly Random rng;

        /// <summary>
        /// The single enemy type for this entire campaign.
        /// Selected at game start and persists through all 25 waves.
        /// All enemies will be procedurally generated variations of this type.
        /// </summary>
        public EnemyType? CampaignEnemyType { get; set; }

        // ===== NEW: Difficulty Settings =====
        /// <summary>
        /// The selected difficulty level for this game session.
        /// Affects how hit tolerance is calculated in firing phase.
        /// Set during game initialization, cannot be changed mid-campaign.
        /// </summary>
        public GameDifficulty SelectedDifficulty { get; set; } = GameDifficulty.CometsAndAsteroids;

        /// <summary>
        /// Gets the difficulty configuration for the currently selected difficulty.
        /// </summary>
        public DifficultyConfig DifficultyConfig => DifficultyConfig.GetConfig(SelectedDifficulty);

        // ===== NEW: Tech Tree System =====
        public TechTree TechTree { get; set; }
        public RandomEvent? CurrentWaveEvent { get; set; }

        public GameState(int? seed = null, GameDifficulty difficulty = GameDifficulty.CometsAndAsteroids)
        {
            Gun = new GunConfiguration();
            Detection = new DetectionSystem();
            Resources = new ResourcePool();
            CurrentWaveNumber = 1;
            CompletedWaves = new();
            IsGameOver = false;
            WavesDefeated = 0;  // Unified: represents both waves and enemies destroyed
            CurrentPhase = GamePhase.Detection;
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
            SelectedDifficulty = difficulty;
            TechTree = new TechTree();  // Initialize at level 1 in all trees

            InitializeResourceAccumulation();

            // NEW: Generate campaign-wide enemy type at game start
            CampaignEnemyType = EnemyType.GenerateForCampaign(rng);
        }

        private void InitializeResourceAccumulation()
        {
            AccumulatedResources.Clear();
            AccumulatedResources["Steel"] = 0;
            AccumulatedResources["Budget"] = 0;
            AccumulatedResources["SpecializedAlloys"] = 0;
            AccumulatedResources["RareEarthElements"] = 0;
            AccumulatedResources["PowerCells"] = 0;
            AccumulatedResources["Exotic"] = 0;
        }

        // ====================================================================
        // PHASE 1: DETECTION
        // ====================================================================

        public class DetectionPhaseResult
        {
            public EnemyWave Wave { get; set; } = null!;
            public DetectionStatus DetectionStatus { get; set; } = null!;
            public long AvailableYears { get; set; }
            public bool WaveDetected { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public DetectionPhaseResult ExecuteDetectionPhase()
        {
            var result = new DetectionPhaseResult();
            var diffConfig = DifficultyConfig.GetConfig(SelectedDifficulty);

            // ===== TUTORIAL MODE: Use simplified beachball waves =====
            if (diffConfig.IsTutorialMode)
            {
                CurrentWave = EnemyWave.GenerateTutorialWave(CurrentWaveNumber, rng);
                result.Wave = CurrentWave;

                // Tutorial waves are always detected - use existing Detection system
                CurrentDetectionStatus = Detection.GetDetectionStatus(CurrentWave);
                // Override to ensure detection for tutorial
                if (!CurrentDetectionStatus.IsDetected)
                {
                    // Force detection for tutorial mode
                    CurrentDetectionStatus = new DetectionStatus { IsDetected = true };
                }
                result.DetectionStatus = CurrentDetectionStatus;

                // Tutorial uses simple time (seconds, not years)
                AvailableYears = 1;  // Minimal - tutorial skips resource phase anyway
                RemainingYears = AvailableYears;
                InitializeResourceAccumulation();

                result.WaveDetected = true;
                result.AvailableYears = AvailableYears;
                result.Message = $"🎯 Tutorial Wave {CurrentWaveNumber}: {CurrentWave.Archetype.Name}\n" +
                                 $"Target at {CurrentWave.InitialDistance:F0} meters, approaching at {CurrentWave.AverageVelocity:F0} m/s";

                // Tutorial skips resource phases - go directly to firing
                CurrentPhase = diffConfig.SkipResourcePhases ? GamePhase.Firing : GamePhase.ResourceAllocation;

                // Pre-generate the firing problem for tutorial
                if (CurrentFiringProblem == null)
                {
                    CurrentFiringProblem = GenerateTutorialFiringProblem(CurrentWave);
                }

                return result;
            }

            // ===== STANDARD MODE: Use pre-generated wave if available =====
            if (PreGeneratedWaves.Count > 0 && CurrentWaveIndex < PreGeneratedWaves.Count)
            {
                // Retrieve the pre-generated firing problem for this wave
                var preGenProblem = PreGeneratedWaves[CurrentWaveIndex];
                
                // Generate wave data for display (detection stats), but use cached trajectory
                CurrentWave = EnemyWave.GenerateWave(CurrentWaveNumber, rng, CampaignEnemyType);
                CurrentWave.Targets = CurrentWave.Targets.Take(1).ToList();
                
                // Apply pre-generated trajectory data to ensure consistency
                CurrentWave.CachedEnemyPosition = preGenProblem.EnemyPosition;
                CurrentWave.CachedEnemyVelocity = preGenProblem.EnemyVelocity;
                CurrentWave.ApproachElevation = preGenProblem.ApproachElevation;
                CurrentWave.ApproachAzimuth = preGenProblem.ApproachAzimuth;
                CurrentWave.CachedCorrectLaunchDelayTime = preGenProblem.CorrectLaunchDelayTime;
                CurrentWave.CachedCorrectElevation = preGenProblem.CorrectElevation;
                CurrentWave.CachedCorrectAzimuth = preGenProblem.CorrectAzimuth;
                CurrentWave.CachedCorrectVelocity = preGenProblem.CorrectVelocity;
                
                // Store the pre-generated firing problem for use in firing phase
                CurrentFiringProblem = preGenProblem;
            }
            else
            {
                // Fallback: Generate wave fresh (for testing or if pre-generation was skipped)
                CurrentWave = EnemyWave.GenerateWave(CurrentWaveNumber, rng, CampaignEnemyType);
                CurrentWave.Targets = CurrentWave.Targets.Take(1).ToList();
            }

            result.Wave = CurrentWave;

            // Get detection status
            CurrentDetectionStatus = Detection.GetDetectionStatus(CurrentWave);
            result.DetectionStatus = CurrentDetectionStatus;

            if (!CurrentDetectionStatus.IsDetected)
            {
                IsGameOver = true;
                result.WaveDetected = false;
                result.Message = "Wave not detected until impact. GLOBAL DESTRUCTION.";
                return result;
            }

            // Calculate time available to reach gun range
            // This is: (InitialDistance - GunRange) / Velocity
            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            double distanceToGunRange = CurrentWave.InitialDistance - tier.MaxEffectiveGunRange;

            // Store in BOTH seconds and years for consistency
            double availableSecondsForGunRange = distanceToGunRange / CurrentWave.AverageVelocity;

            // Round to whole years, minimum 1 year
            AvailableYears = Math.Max(1, (long)Math.Round(availableSecondsForGunRange / GameConstants.SecondsPerYear));
            RemainingYears = AvailableYears;
            InitializeResourceAccumulation();

            result.WaveDetected = true;
            result.AvailableYears = AvailableYears;
            result.Message = $"Enemy detected at {GameConstants.FormatDistance(CurrentWave.InitialDistance)}! {GameConstants.FormatTime(availableSecondsForGunRange)} until target enters gun range.";

            CurrentPhase = GamePhase.ResourceAllocation;
            return result;
        }

        /// <summary>
        /// Generate a firing problem for tutorial mode.
        /// Uses the pre-calculated vectors from the tutorial wave.
        /// </summary>
        private FiringProblem GenerateTutorialFiringProblem(EnemyWave wave)
        {
            var target = wave.Targets[0];

            // Use cached vectors from tutorial wave generation (Vector3 uses double internally)
            Vector3 enemyPosition = wave.CachedEnemyPosition ?? new Vector3(wave.InitialDistance, 0.0, 0.0);
            Vector3 enemyVelocity = wave.CachedEnemyVelocity ?? Vector3.Zero;

            // Calculate correct solution for tutorial
            // Simple case: fire directly at where the target will be
            double muzzleVelocity = DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs;
            double flightTimeDouble = wave.InitialDistance / muzzleVelocity;

            // For stationary targets, aim directly at them
            // For moving targets, lead the target
            Vector3 interceptPoint = enemyPosition + (enemyVelocity * flightTimeDouble);

            // Calculate angles to intercept point
            double interceptDistance = interceptPoint.Magnitude;
            
            // Handle edge case where intercept distance is zero (stationary target at origin)
            float elevation = 0f;
            float azimuth = 0f;
            if (interceptDistance > 0.001)
            {
                elevation = (float)(Math.Asin(interceptPoint.Z / interceptDistance) * 180.0 / Math.PI);
                azimuth = (float)(Math.Atan2(interceptPoint.X, interceptPoint.Y) * 180.0 / Math.PI);
                if (azimuth < 0f) azimuth += 360f;
            }

            return new FiringProblem
            {
                EnemyPosition = enemyPosition,
                EnemyVelocity = enemyVelocity,
                ApproachElevation = wave.ApproachElevation,
                ApproachAzimuth = wave.ApproachAzimuth,
                ApproachSpeed = (float)wave.AverageVelocity,
                EngagementDistance = (float)wave.InitialDistance,
                FractureEnergyRequired = (float)target.FractureEnergy,
                CorrectLaunchDelayTime = 0f,  // Fire immediately in tutorial
                CorrectElevation = elevation,
                CorrectAzimuth = azimuth,
                CorrectVelocity = (float)muzzleVelocity
            };
        }

        /// <summary>
        /// Check for random events at the start of resource allocation.
        /// Events occur every 3rd wave and affect production rates.
        /// </summary>
        public void GenerateWaveEvent()
        {
            if (RandomEvent.ShouldHaveEvent(CurrentWaveNumber))
            {
                CurrentWaveEvent = RandomEvent.GenerateEvent(CurrentWaveNumber, rng);
            }
            else
            {
                CurrentWaveEvent = null;
            }
        }

        /// <summary>
        /// Research a tech using accumulated resources.
        /// Returns true if research was successful.
        /// </summary>
        public bool ResearchTech(TechUnlock tech)
        {
            if (tech == null)
                return false;

            // Create resource cost from accumulated resources
            var availableResources = new Dictionary<string, double>(AccumulatedResources);

            // Try to research using TechUnlock helper
            if (!TechUnlock.ResearchTech(tech, TechTree, AccumulatedResources))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get all available tech researches for the current state.
        /// Filters by what can be researched (< level 3) and what player can afford.
        /// </summary>
        public List<TechUnlock> GetAvailableTechs()
        {
            var available = TechUnlock.GetAvailableUnlocks(TechTree);
            
            // Filter by affordability
            return available.Where(tech => 
            {
                double budget = AccumulatedResources.ContainsKey("Budget") ? AccumulatedResources["Budget"] : 0;
                double steel = AccumulatedResources.ContainsKey("Steel") ? AccumulatedResources["Steel"] : 0;
                double exotic = AccumulatedResources.ContainsKey("Exotic") ? AccumulatedResources["Exotic"] : 0;

                return budget >= tech.ResearchCost.Budget &&
                       steel >= tech.ResearchCost.Steel &&
                       exotic >= tech.ResearchCost.ExoticMaterials;
            }).ToList();
        }

        // ====================================================================
        // PHASE 2: RESOURCE ALLOCATION
        // ====================================================================

        public class ResourceAllocationResult
        {
            public double SteelGathered { get; set; }
            public double ExoticGathered { get; set; }
            public double BudgetGathered { get; set; }
            public long YearsSpent { get; set; }
            public long RemainingYears { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// Allocate available years to resource gathering.
        /// steelYears, exoticYears, budgetYears are year tokens to spend.
        /// Returns gathered resources.
        /// </summary>
        public ResourceAllocationResult AllocateResources(double steelYears, double exoticYears, double budgetYears)
        {
            var result = new ResourceAllocationResult();

            // Round input years to whole numbers
            long steelYearsWhole = (long)Math.Round(steelYears);
            long exoticYearsWhole = (long)Math.Round(exoticYears);
            long budgetYearsWhole = (long)Math.Round(budgetYears);

            long totalYears = steelYearsWhole + exoticYearsWhole + budgetYearsWhole;
            if (totalYears > RemainingYears)
                throw new InvalidOperationException($"Cannot allocate {totalYears} years, only {RemainingYears} available.");

            // Get event multiplier (applies to ALL resources uniformly)
            double eventMultiplier = CurrentWaveEvent?.ProductionMultiplier ?? 1.0;

            // Convert years to resources (1 year = 1 production token)
            // Apply event multiplier to all gathered resources
            double steelGathered = steelYearsWhole * GameConstants.SteelProductionPerYear * eventMultiplier;
            double exoticGathered = exoticYearsWhole * GameConstants.ExoticProductionPerYear * eventMultiplier;
            double budgetGathered = budgetYearsWhole * GameConstants.BudgetProductionPerYear * eventMultiplier;

            // Add to accumulated
            AccumulatedResources["Steel"] += steelGathered;
            AccumulatedResources["Exotic"] += exoticGathered;
            AccumulatedResources["Budget"] += budgetGathered;

            RemainingYears -= totalYears;

            result.SteelGathered = steelGathered;
            result.ExoticGathered = exoticGathered;
            result.BudgetGathered = budgetGathered;
            result.YearsSpent = totalYears;
            result.RemainingYears = RemainingYears;
            result.Message = $"Gathered {steelGathered:F0} steel, {exoticGathered:F0} exotic, {budgetGathered:F0} budget. {RemainingYears} years remaining.";

            // Move to development if all time allocated or player is done
            CurrentPhase = GamePhase.Development;
            return result;
        }

        // ====================================================================
        // PHASE 3: DEVELOPMENT
        // ====================================================================

        public class DevelopmentResult
        {
            public bool UpgradeApplied { get; set; }
            public string UpgradeName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public double ResourcesRemaining { get; set; }
        }

        /// <summary>
        /// Apply accumulated resources to gun upgrades.
        /// Returns result of upgrade application.
        /// </summary>
        public DevelopmentResult ApplyUpgrade(UpgradeSystem upgrade)
        {
            var result = new DevelopmentResult();

            if (upgrade is null)
            {
                result.Message = "No upgrade specified.";
                return result;
            }

            // Convert accumulated resources to ResourceCost for checking
            var availableCost = new ResourceCost(
                budget: AccumulatedResources["Budget"],
                steel: AccumulatedResources["Steel"],
                exotic: AccumulatedResources["Exotic"]
            );

            // Check if we can afford it
            if (!CanAffordUpgrade(upgrade.Cost, availableCost))
            {
                result.Message = $"Insufficient accumulated resources for {upgrade.Name}.";
                return result;
            }

            // Apply upgrade
            try
            {
                upgrade.Apply(Gun, new ResourcePool
                {
                    Budget = AccumulatedResources["Budget"],
                    Steel = AccumulatedResources["Steel"],
                    ExoticMaterials = AccumulatedResources["Exotic"],
                    PowerCapacity = Gun.PowerCapacity,
                    ResearchPoints = 0
                });

                // Deduct from accumulated
                AccumulatedResources["Budget"] -= upgrade.Cost.Budget;
                AccumulatedResources["Steel"] -= upgrade.Cost.Steel;
                AccumulatedResources["Exotic"] -= upgrade.Cost.ExoticMaterials;

                result.UpgradeApplied = true;
                result.UpgradeName = upgrade.Name;
                result.Message = $"Applied upgrade: {upgrade.Name}";
                result.ResourcesRemaining = AccumulatedResources["Budget"] + AccumulatedResources["Steel"] + AccumulatedResources["Exotic"];

                CurrentPhase = GamePhase.Firing;
            }
            catch (Exception ex)
            {
                result.Message = $"Failed to apply upgrade: {ex.Message}";
            }

            return result;
        }

        private bool CanAffordUpgrade(ResourceCost cost, ResourceCost available)
        {
            if (cost is null) return true;
            if (cost.Budget > available.Budget) return false;
            if (cost.Steel > available.Steel) return false;
            if (cost.ExoticMaterials > available.ExoticMaterials) return false;
            return true;
        }

        // ====================================================================
        // PHASE 4: FIRING SOLUTION
        // ====================================================================

        public class FiringPhaseResult
        {
            public bool CanReachTarget { get; set; }
            public double GunRange { get; set; }
            public double TargetDistance { get; set; }
            public bool Hit { get; set; }
            public double HitProbability { get; set; }
            public bool WaveDefeated { get; set; }
            public bool GameOver { get; set; }
            public string Message { get; set; } = string.Empty;
            public ResourceCost? Reward { get; set; }
        }

        public FiringPhaseResult ExecuteFiringPhase()
        {
            if (CurrentWave == null)
                throw new InvalidOperationException("No active wave for firing phase");

            var target = CurrentWave.Targets[0];
            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            var diffConfig = DifficultyConfig.GetConfig(SelectedDifficulty);

            // ===== TUTORIAL MODE: Use potato cannon specs =====
            if (diffConfig.IsTutorialMode && SelectedGunProjectileSpec == null)
            {
                SelectedGunProjectileSpec = GunProjectileSpec.PotatoCannon;
            }

            // ===== CRITICAL: Only generate if NOT already generated =====
            // If CurrentFiringProblem already exists, reuse it (from a prior call or load)
            if (CurrentFiringProblem == null)
            {
                // For tutorial mode, use the tutorial firing problem generator
                if (diffConfig.IsTutorialMode)
                {
                    CurrentFiringProblem = GenerateTutorialFiringProblem(CurrentWave);
                }
                else
                {
                    var solver = new FiringSolution(
                        (float)(SelectedGunProjectileSpec?.ProjectileMassKg ?? Gun.DefaultProjectile.Mass),
                        (float)target.FractureEnergy,
                        target.Mass);

                    FiringProblem firingProblem = null!;
                    try
                    {
                        // Pass gun's effective range to constrain engagement
                        firingProblem = solver.GenerateFiringProblem(
                            CurrentWave,
                            (float)(SelectedGunProjectileSpec?.MuzzleVelocityMs ??
                                    BallisticsCalculator.CalculateMuzzleVelocity(Gun, Gun.DefaultProjectile)),
                            (float)tier.MaxEffectiveGunRange,
                            rng);
                    }
                    catch (InvalidOperationException exception)
                    {
                        System.Console.WriteLine($"Warning: Failed to generate firing problem: {exception.Message}");
                        return new FiringPhaseResult
                        {
                            CanReachTarget = false,
                            TargetDistance = 1_100_000,
                            GunRange = tier.MaxEffectiveGunRange,
                            Message = $"Gun is insufficient for this target: {exception.Message}",
                            Reward = null,
                            GameOver = false
                        };
                    }

                    // Store for use in ConsoleUI firing phase
                    CurrentFiringProblem = firingProblem;
                }
            }

            // For tutorial, use tutorial effective range
            double effectiveRange = diffConfig.IsTutorialMode 
                ? DifficultyConfig.TutorialPotatoCannon.EffectiveRangeMeters 
                : tier.MaxEffectiveGunRange;

            return new FiringPhaseResult
            {
                CanReachTarget = true,
                TargetDistance = CurrentFiringProblem.EngagementDistance,
                GunRange = effectiveRange,
                Message = diffConfig.IsTutorialMode
                    ? $"🎯 Beachball at {CurrentFiringProblem.EngagementDistance:F0} meters - Ready to fire!"
                    : $"Target at engagement distance {GameConstants.FormatDistance(CurrentFiringProblem.EngagementDistance)}",
                Reward = null,
                GameOver = false
            };
        }

        // Add this property to store firing problem for ConsoleUI
        public FiringProblem? CurrentFiringProblem { get; set; }

        // ====================================================================
        // HELPER: Advance to next wave
        // ====================================================================

        public void AdvanceToNextWave()
        {
            CurrentWave = null;
            CurrentDetectionStatus = null;
            CurrentFiringProblem = null;
            CurrentWaveNumber++;  // ADD THIS LINE
            CurrentPhase = GamePhase.Detection;
        }

        // ====================================================================
        // TIME BUDGET ACCESSORS (for save/load)
        // ====================================================================

        /// <summary>
        /// Get the available seconds for gun range calculation (used for serialization).
        /// </summary>
        public double GetAvailableSecondsForGunRange()
        {
            return availableSecondsForGunRange;
        }

        /// <summary>
        /// Set the time budget state from saved data.
        /// Restores AvailableYears, RemainingYears, and availableSecondsForGunRange.
        /// </summary>
        public void SetTimebudget(long availableYears, long remainingYears, double availableSecondsForGunRange)
        {
            AvailableYears = availableYears;
            RemainingYears = remainingYears;
            this.availableSecondsForGunRange = availableSecondsForGunRange;
        }

        /// <summary>
        /// Restore the current wave state from saved data.
        /// </summary>
        public void RestoreCurrentWave(EnemyWave wave)
        {
            CurrentWave = wave;
        }

        // ====================================================================
        // RESOURCE ALLOCATION UNDO SUPPORT
        // ====================================================================

        private Dictionary<string, double> resourceAllocationCheckpoint = new();

        /// <summary>
        /// Create a checkpoint of current accumulated resources for undo functionality.
        /// </summary>
        public void CreateResourceAllocationCheckpoint()
        {
            resourceAllocationCheckpoint.Clear();
            foreach (var kvp in AccumulatedResources)
            {
                resourceAllocationCheckpoint[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Rollback to the last checkpoint and restore remaining years.
        /// </summary>
        public bool UndoLastResourceAllocation(long yearsToRestore)
        {
            if (resourceAllocationCheckpoint.Count == 0)
                return false;

            foreach (var kvp in resourceAllocationCheckpoint)
            {
                AccumulatedResources[kvp.Key] = kvp.Value;
            }

            RemainingYears += yearsToRestore;
            return true;
        }

        // ====================================================================
        // SAVE/LOAD SYSTEM - Single Auto-Save Slot (Anti-Save-Scum)
        // ====================================================================
        // Only ONE save slot exists. Saves happen automatically after each major phase.
        // This prevents save-scumming by allowing continuation but not retry capability.

        /// <summary>
        /// Auto-save the current game state to the single save slot.
        /// Overwrites any previous save. Called automatically after phases.
        /// </summary>
        public void AutoSaveGame()
        {
            string savePath = GetAutoSavePath();
            try
            {
                // ===== CRITICAL FIX: Ensure firing problem is generated before saving =====
                // If we're in the Firing phase and haven't generated the firing problem yet, do it now
                if (CurrentPhase == GamePhase.Firing && CurrentFiringProblem == null && CurrentWave != null)
                {
                    ExecuteFiringPhase();
                }
                
                var data = GameStateData.FromGameState(this);
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(savePath, json);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Warning: Auto-save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load game from the single auto-save slot.
        /// </summary>
        public bool LoadAutoSave()
        {
            string savePath = GetAutoSavePath();
            
            try
            {
                if (!System.IO.File.Exists(savePath))
                    return false;

                var json = System.IO.File.ReadAllText(savePath);
                var data = System.Text.Json.JsonSerializer.Deserialize<GameStateData>(json);

                if (data is null)
                    return false;

                data.ApplyToGameState(this);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading auto-save: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if an auto-save exists.
        /// </summary>
        public static bool AutoSaveExists()
        {
            return System.IO.File.Exists(GetAutoSavePath());
        }

        /// <summary>
        /// Get the single auto-save file path.
        /// </summary>
        private static string GetAutoSavePath() => "Saves/AutoSave.json";

        /// <summary>
        /// Get auto-save timestamp for display.
        /// </summary>
        public static string GetAutoSaveTimestamp()
        {
            string savePath = GetAutoSavePath();
            if (!System.IO.File.Exists(savePath))
                return "No save found";

            try
            {
                var fileInfo = new System.IO.FileInfo(savePath);
                return fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "Unknown";
            }
        }

        // Add public method to increment waves/enemies defeated
        public void IncrementWavesDefeated()
        {
            WavesDefeated++;
        }

        // ====================================================================
        // WAVE PRE-GENERATION (OPTION A: Pre-generate All Waves at Game Start)
        // ====================================================================
        // All waves are generated once at startup, ensuring consistency and
        // eliminating the velocity normalization problem. Each wave has an immutable
        // velocity derived from its ballistic geometry.

        /// <summary>
        /// Pre-generated firing problems for all campaign waves.
        /// Populated during game initialization, used throughout the game.
        /// Guarantees: Each wave is solvable and has immutable velocity.
        /// </summary>
        public List<FiringProblem> PreGeneratedWaves { get; private set; } = new();

        /// <summary>
        /// Current index in the pre-generated waves list.
        /// Incremented as player progresses through waves.
        /// </summary>
        public int CurrentWaveIndex { get; private set; } = 0;

        /// <summary>
        /// Pre-generate all campaign waves at startup.
        /// For tutorial mode, generates simplified beachball scenarios.
        /// For standard mode, generates full ballistic challenges.
        /// </summary>
        public void GenerateAllCampaignWaves(int campaignLength)
        {
            var diffConfig = DifficultyConfig.GetConfig(SelectedDifficulty);

            // ===== TUTORIAL MODE: Generate simple beachball scenarios =====
            if (diffConfig.IsTutorialMode)
            {
                Console.WriteLine($"[TUTORIAL] Generating {campaignLength} beachball scenarios...");
                PreGeneratedWaves.Clear();
                CurrentWaveIndex = 0;

                for (int waveNumber = 1; waveNumber <= campaignLength; waveNumber++)
                {
                    try
                    {
                        var wave = EnemyWave.GenerateTutorialWave(waveNumber, rng);
                        var problem = GenerateTutorialFiringProblem(wave);
                        PreGeneratedWaves.Add(problem);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error generating tutorial wave {waveNumber}: {ex.Message}");
                    }
                }

                Console.WriteLine($"✓ Tutorial scenarios ready!");
                return;
            }

            // ===== STANDARD MODE: Pre-generate complex firing problems =====
            Console.WriteLine($"[CAMPAIGN GENERATION] Pre-generating {campaignLength} firing problems...");
            PreGeneratedWaves.Clear();
            CurrentWaveIndex = 0;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int successCount = 0;
            int failureCount = 0;

            for (int waveNumber = 1; waveNumber <= campaignLength; waveNumber++)
            {
                try
                {
                    var wave = EnemyWave.GenerateWave(waveNumber, rng, CampaignEnemyType);
                    wave.Targets = wave.Targets.Take(1).ToList();

                    var tier = GameConstants.GetTierForWave(waveNumber);
                    int tierIndex = tier.TierIndex;

                    var (_, _, playerMin, playerMax) = GameConstants.GetTierVelocityConstraints(tierIndex);
                    float campaignReferenceVelocity = (float)playerMax;

                    // FIX: Use actual target data from generated wave instead of hardcoded values
                    var target = wave.Targets[0];
                    var firingSolution = new FiringSolution(
                        (float)Gun.DefaultProjectile.Mass,
                        (float)target.FractureEnergy,
                        target.Mass);

                    var problem = firingSolution.GenerateFiringProblem(
                        wave,
                        campaignReferenceVelocity,
                        (float)tier.MaxEffectiveGunRange,
                        rng);

                    PreGeneratedWaves.Add(problem);
                    successCount++;

                    // Minimal progress - just dots
                    if (waveNumber % 5 == 0)
                        Console.Write(".");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error generating wave {waveNumber}: {ex.Message}");
                    failureCount++;
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"\n✓ Generated {successCount}/{campaignLength} waves in {stopwatch.ElapsedMilliseconds}ms");
            
            if (failureCount > 0)
                Console.WriteLine($"  ({failureCount} waves had generation issues)");
        }

        /// <summary>
        /// Get the current wave's pre-generated firing problem.
        /// Safe to call after pre-generation is complete.
        /// Returns null if wave index is out of range.
        /// </summary>
        public FiringProblem? GetCurrentWaveProblem()
        {
            if (CurrentWaveIndex < 0 || CurrentWaveIndex >= PreGeneratedWaves.Count)
                return null;

            return PreGeneratedWaves[CurrentWaveIndex];
        }

        /// <summary>
        /// Advance to the next wave in the campaign.
        /// Increments CurrentWaveIndex and checks for campaign completion.
        /// Returns false if campaign is complete.
        /// </summary>
        public bool AdvanceToNextWaveIndex()
        {
            CurrentWaveIndex++;
            return CurrentWaveIndex < PreGeneratedWaves.Count;
        }
    }
}