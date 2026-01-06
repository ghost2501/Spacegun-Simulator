using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Economy;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Development.Technology;
using Spacegun_Simulator.Development.Weapons;
using Spacegun_Simulator.Detection;
using Spacegun_Simulator.Events;
using System.Security.Cryptography;

namespace Spacegun_Simulator.Core
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

        // Accumulated resources during allocation phase (time spent as tokens)
        public Dictionary<string, double> AccumulatedResources { get; private set; } = new();

        internal Random rng;

        /// <summary>
        /// Base seed for deterministic generation. Used to derive per-wave seeds so save/load is stable.
        /// </summary>
        public int BaseSeed { get; private set; }

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
        /// High-level mode selection (economy/dev loop vs pure detection+fire).
        /// </summary>
        public GameModeId SelectedMode { get; set; } = GameModeId.Economy_KineticDronesVsRobotAsteroids;

        public GameModeDefinition Mode => GameModeCatalog.Get(SelectedMode);

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
            SelectedDifficulty = difficulty;
            SelectedMode = GameModeCatalog.GetDefaultForDifficulty(difficulty);

            // Determinism foundation:
            // - Full modes default to a random seed, but that seed is persisted so save/load is stable.
            // - Pure modes default to a random seed too, unless an explicit deterministic seed is configured.
            BaseSeed = seed ?? ChooseDefaultSeedForMode(Mode);

            rng = new Random(BaseSeed);
            TechTree = new TechTree();  // Initialize at level 1 in all trees

            InitializeResourceAccumulation();

            // NEW: Generate campaign-wide enemy type at game start
            CampaignEnemyType = EnemyType.GenerateForCampaign(CreateDeterministicRng("CampaignEnemyType"));
        }

        public GameState(int? seed = null, GameModeId mode = GameModeId.Economy_KineticDronesVsRobotAsteroids)
            : this(seed: seed, difficulty: GameModeCatalog.Get(mode).Difficulty)
        {
            SelectedMode = mode;
            SelectedDifficulty = GameModeCatalog.Get(mode).Difficulty;

            // Re-apply seed defaults now that mode is known.
            if (!seed.HasValue)
            {
                BaseSeed = ChooseDefaultSeedForMode(Mode);
                rng = new Random(BaseSeed);
            }
        }

        private static int ChooseDefaultSeedForMode(GameModeDefinition mode)
        {
            if (GameModeTuning.IsPureMode(mode))
            {
                int configured = GameModeTuning.Current.PureDeterministicSeed;
                if (configured >= 0)
                    return configured;
            }

            return RandomNumberGenerator.GetInt32(int.MaxValue);
        }

        public void SetBaseSeed(int seed)
        {
            BaseSeed = seed;
            rng = new Random(seed);
        }

        private Random CreateDeterministicRng(string purpose, int waveNumber = 0)
        {
            int seed = DeriveSeed(purpose, waveNumber);
            return new Random(seed);
        }

        private int DeriveSeed(string purpose, int waveNumber)
        {
            unchecked
            {
                uint hash = 2166136261;

                void Add(string s)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        hash ^= s[i];
                        hash *= 16777619;
                    }
                }

                Add(BaseSeed.ToString());
                Add("|");
                Add(((int)SelectedMode).ToString());
                Add("|");
                Add(((int)SelectedDifficulty).ToString());
                Add("|");
                Add(purpose);
                Add("|");
                Add(waveNumber.ToString());
                return (int)hash;
            }
        }

        private Random CreateWaveRng(int waveNumber) => CreateDeterministicRng("Wave", waveNumber);
        private Random CreateEventRng(int waveNumber) => CreateDeterministicRng("Event", waveNumber);

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
            var modeTuning = GameModeTuning.Current;
            var enemyRuleset = GameModeTuning.IsPureMode(Mode) ? EnemyGenerationRuleset.Pure : EnemyGenerationRuleset.Full;

            ApplyRadarTechToDetectionSystem();

            // ===== TUTORIAL MODE: Use simplified beachball waves =====
            if (diffConfig.IsTutorialMode)
            {
                CurrentWave = EnemyWave.GenerateTutorialWave(CurrentWaveNumber, CreateWaveRng(CurrentWaveNumber));
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
                GamePhaseTransitionRules.Apply(
                    this,
                    (!Mode.UsesEconomyAndDevelopment || diffConfig.SkipResourcePhases)
                        ? GamePhaseTransitionRules.PhaseEvent.DetectionResolvedSkipResourcePhases
                        : GamePhaseTransitionRules.PhaseEvent.DetectionResolvedProceedToResourceAllocation);

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
                CurrentWave = EnemyWave.GenerateWave(CurrentWaveNumber, CreateWaveRng(CurrentWaveNumber), enemyRuleset, CampaignEnemyType);
                CurrentWave.Targets = CurrentWave.Targets.Take(1).ToList();

                // Apply pre-generated trajectory data to ensure consistency
                CurrentWave.CachedEnemyPosition = preGenProblem.EnemyPosition;
                CurrentWave.CachedEnemyVelocity = preGenProblem.EnemyVelocity;
                CurrentWave.AverageVelocity = preGenProblem.EnemyVelocity.Magnitude;
                if (CurrentWave.Targets.Count > 0)
                    CurrentWave.Targets[0].Velocity = CurrentWave.AverageVelocity;
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
                CurrentWave = EnemyWave.GenerateWave(CurrentWaveNumber, CreateWaveRng(CurrentWaveNumber), enemyRuleset, CampaignEnemyType);
                CurrentWave.Targets = CurrentWave.Targets.Take(1).ToList();
            }

            result.Wave = CurrentWave;

            // Apply mode tuning before detection calculations (e.g., RCS scaling).
            if (CurrentWave != null && !diffConfig.IsTutorialMode)
            {
                double rcsMult = modeTuning.GetDetectionRcsMultiplier(Mode);
                if (Math.Abs(rcsMult - 1.0) > 0.0000001)
                {
                    CurrentWave.AverageRadarCrossSection *= rcsMult;
                    if (CurrentWave.Targets.Count > 0)
                        CurrentWave.Targets[0].CrossSection *= rcsMult;
                }
            }

            // Get detection status
            CurrentDetectionStatus = Detection.GetDetectionStatus(CurrentWave!);
            result.DetectionStatus = CurrentDetectionStatus;

            if (!CurrentDetectionStatus.IsDetected)
            {
                IsGameOver = true;
                result.WaveDetected = false;

                try
                {
                    var threat = EarthImpactThreat.Compute(CurrentWave!);
                    string verdict = threat.ExceedsThreshold ? "YES" : "NO";
                    result.Message =
                        "Wave not detected until impact. GLOBAL DESTRUCTION." +
                        $"\nImpact KE: {threat.ImpactEnergyMJ:F0} MJ" +
                        $" (×coupling={DevelopmentTuning.EarthThreat.EnemyEarthThreatCoupling:F2} → {threat.CoupledImpactEnergyMJ:F0} MJ)" +
                        $" | Threshold: {threat.ThresholdMJ:F0} MJ" +
                        $" | Earth-cracking: {verdict}";
                }
                catch
                {
                    result.Message = "Wave not detected until impact. GLOBAL DESTRUCTION.";
                }
                return result;
            }

            // Calculate time available to reach gun range
            // This is: (InitialDistance - GunRange) / Velocity
            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            double effectiveGunRange = GetCurrentEffectiveGunRangeMeters();
            double distanceToGunRange = CurrentWave!.InitialDistance - effectiveGunRange;

            // Store in BOTH seconds and years for consistency
            double secondsUntilGunRange = distanceToGunRange / CurrentWave.AverageVelocity;

            double timeMult = modeTuning.GetTimeBudgetMultiplier(Mode);
            if (Math.Abs(timeMult - 1.0) > 0.0000001)
                secondsUntilGunRange *= timeMult;

            // Persist exact seconds for save/load.
            availableSecondsForGunRange = secondsUntilGunRange;

            // Round to whole years, minimum 1 year
            AvailableYears = Math.Max(1, (long)Math.Round(secondsUntilGunRange / GameConstants.SECONDS_PER_YEAR));
            RemainingYears = AvailableYears;
            InitializeResourceAccumulation();

            result.WaveDetected = true;
            result.AvailableYears = AvailableYears;
            var intelRng = CreateDeterministicRng("Intel", CurrentWaveNumber);
            string intelSummary = Detection.GenerateNoisyIntelSummary(CurrentWave, intelRng);
            result.Message = $"Enemy detected at {GameConstants.FormatDistance(CurrentWave.InitialDistance)}! {GameConstants.FormatTime(secondsUntilGunRange)} until target enters gun range.\n{intelSummary}";

            GamePhaseTransitionRules.Apply(
                this,
                Mode.UsesEconomyAndDevelopment
                    ? GamePhaseTransitionRules.PhaseEvent.DetectionResolvedProceedToResourceAllocation
                    : GamePhaseTransitionRules.PhaseEvent.DetectionResolvedSkipResourcePhases);
            return result;
        }

        private void ApplyRadarTechToDetectionSystem()
        {
            int radarLevel = 1;
            if (TechTree?.CurrentLevel != null && TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Radar, out int lvl))
                radarLevel = Math.Max(1, lvl);

            Detection.DetectionRangeMultiplier = radarLevel switch
            {
                1 => 1.0,
                2 => 1.15,
                3 => 1.30,
                _ => 1.30
            };

            Detection.MaxSimultaneousTargets = radarLevel switch
            {
                1 => 5,
                2 => 8,
                3 => 12,
                _ => 12
            };

            Detection.StealthPenetration = radarLevel switch
            {
                1 => 0.0,
                2 => 0.40,
                3 => 0.75,
                _ => 0.75
            };

            Detection.IntelResolution = radarLevel switch
            {
                1 => 0.20,
                2 => 0.50,
                3 => 0.80,
                _ => 0.80
            };

            // Keep existing space-based flag (some modes may set it elsewhere).
        }

        public double GetCurrentEffectiveGunRangeMeters()
        {
            var tier = GameConstants.GetTierForWave(CurrentWaveNumber);
            double range = tier.MaxEffectiveGunRange;

            // Gun upgrades: barrel length directly scales gun range.
            if (Gun != null)
                range *= Gun.RangeMultiplierFromBarrelLength;

            // Stealth applies the same percent debuff to gun range as to detection.
            if (CurrentWave != null)
                range *= Detection.GetStealthRangeMultiplier(CurrentWave);

            return Math.Max(0.0, range);
        }

        public ResolvedShotStats ResolveShotStats(EnemyTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (Gun == null) throw new InvalidOperationException("GameState.Gun is null.");

            // Tutorial mode is fully predetermined and does not use modular projectile configuration.
            if (DifficultyConfig.IsTutorialMode)
            {
                return new ResolvedShotStats(
                    ProjectileMassKg: DifficultyConfig.TutorialPotatoCannon.ProjectileMassKg,
                    MaxLaunchVelocityMs: DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs,
                    EffectiveFractureEnergyMJ: Math.Max(0.0, target.FractureEnergy),
                    Penetration: 1.0,
                    AdditionalHitToleranceMultiplier: 1.0,
                    PropulsionDeltaVCapacityMs: 0.0,
                    PropulsionBurnDurationSeconds: 1.0,
                    PropulsionReferenceMassKg: 10.0,
                    ProjectileDefenseRating: 0.0
                );
            }

            int weaponsTechLevel = 1;
            if (TechTree?.CurrentLevel != null && TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Weapons, out int w))
                weaponsTechLevel = Math.Max(1, w);

            // Keep GunConfiguration synchronized with Weapons tech.
            Gun.UpdateBaseMuzzleVelocity(weaponsTechLevel);

            EnsureCraftedProjectileInitialized();

            double projectileMassKg =
                CraftedProjectile?.MassKg
                ?? Gun.DefaultProjectile?.Mass
                ?? DevelopmentTuning.ProjectileDefaults.Mass;

            var (minMassKg, maxMassKg) = Gun.GetSupportedProjectileMassRangeKg();
            if (projectileMassKg < minMassKg || projectileMassKg > maxMassKg)
            {
                throw new InvalidOperationException(
                    $"Projectile mass {projectileMassKg:N2} kg is incompatible with bore {Gun.BoreDiameter:N2} m " +
                    $"(supported {minMassKg:N2}..{maxMassKg:N2} kg)."
                );
            }

            double penetration = CraftedProjectile?.Enhancement?.Penetration ?? 1.0;
            penetration = Math.Max(0.1, penetration);

            double baseImpactCoupling = DevelopmentTuning.ProjectileDefaults.ImpactCoupling;
            double enhancementImpactCoupling = CraftedProjectile?.Enhancement?.ImpactCoupling ?? 1.0;

            double couplingReferenceMassKg = Math.Max(0.01, DevelopmentTuning.ProjectileDefaults.ImpactCouplingReferenceMassKg);
            double couplingMassExponent = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingMassExponent);
            double couplingMassScale = couplingMassExponent > 0.0
                ? Math.Pow(couplingReferenceMassKg / Math.Max(0.01, projectileMassKg), couplingMassExponent)
                : 1.0;

            double couplingTechPerLevel = Math.Max(0.0, DevelopmentTuning.ProjectileDefaults.ImpactCouplingTechMultiplierPerWeaponsLevel);
            double couplingTechScale = couplingTechPerLevel != 1.0
                ? Math.Pow(couplingTechPerLevel, Math.Max(0, weaponsTechLevel - 1))
                : 1.0;

            double impactCoupling = Math.Clamp(
                baseImpactCoupling * couplingMassScale * couplingTechScale * enhancementImpactCoupling,
                0.0001,
                100.0);

            double defense01 = Math.Clamp(target.Defense, 0.0, 1.0);
            double defenseScale = Math.Max(0.0, GameModeTuning.Current.FractureEnergyDefenseScale);
            double armoredFractureEnergyMJ = Math.Max(0.0, target.FractureEnergy * (1.0 + defenseScale * defense01));
            // Penetration and impact coupling both reduce the required kinetic energy to achieve the same damage.
            // Coupling represents the fraction of KE that actually couples into destructive internal work.
            double effectiveFractureEnergyMJ = Math.Max(0.0, armoredFractureEnergyMJ / (penetration * impactCoupling));

            double additionalHitToleranceMultiplier = CraftedProjectile?.Enhancement?.HitToleranceBonus ?? 1.0;
            additionalHitToleranceMultiplier = Math.Max(0.1, additionalHitToleranceMultiplier);

            double deltaVCapacity = CraftedProjectile?.Propulsion?.DeltaVCapacityMs ?? 0.0;
            double burnDuration = CraftedProjectile?.Propulsion?.BurnDurationSeconds ?? 1.0;
            double referenceMass = CraftedProjectile?.Propulsion?.ReferenceMassKg ?? 10.0;

            double projectileDefense = CraftedProjectile?.DefenseRating ?? 0.0;

            // Compute maximum launch velocity from gun physics, then cap by tech base velocity.
            var projectileCfg = new ProjectileConfiguration { Mass = projectileMassKg };
            double energyBasedMax = BallisticsCalculator.CalculateMuzzleVelocity(Gun, projectileCfg);

            double barrelEfficiency = Math.Min(1.0, Gun.BarrelLength / 200.0);
            double barrelMultiplier = (0.5 + 0.5 * barrelEfficiency);
            double techBaseMax = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel) * barrelMultiplier * Gun.BarrelIntegrity;

            double maxLaunchVelocity = Math.Max(1.0, Math.Min(techBaseMax, energyBasedMax));

            return new ResolvedShotStats(
                ProjectileMassKg: projectileMassKg,
                MaxLaunchVelocityMs: maxLaunchVelocity,
                EffectiveFractureEnergyMJ: effectiveFractureEnergyMJ,
                Penetration: penetration,
                AdditionalHitToleranceMultiplier: additionalHitToleranceMultiplier,
                PropulsionDeltaVCapacityMs: Math.Max(0.0, deltaVCapacity),
                PropulsionBurnDurationSeconds: Math.Max(0.1, burnDuration),
                PropulsionReferenceMassKg: Math.Max(0.01, referenceMass),
                ProjectileDefenseRating: Math.Clamp(projectileDefense, 0.0, 1.0)
            );
        }

        private void EnsureCraftedProjectileInitialized()
        {
            if (CraftedProjectile != null)
                return;

            int projectilesTechLevel = 1;
            if (TechTree?.CurrentLevel != null && TechTree.CurrentLevel.TryGetValue(TechTree.TechType.Projectiles, out int p))
                projectilesTechLevel = Math.Max(1, p);

            var core = ProjectileCore.All
                .FirstOrDefault(c => c is not null && c.RequiredTechLevel <= projectilesTechLevel)
                ?? ProjectileCore.All.FirstOrDefault();

            if (core == null)
                return;

            CraftedProjectile = new CraftedProjectile(
                core: core,
                propulsion: PropulsionSystem.None,
                enhancement: ProjectileEnhancement.None,
                gunBaseMuzzleVelocityMs: Gun.BaseMuzzleVelocityMs);
        }

        internal Random CreateFiringRng(string purpose) => CreateDeterministicRng($"Firing|{purpose}", CurrentWaveNumber);

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
            // Don't overwrite a restored event.
            if (CurrentWaveEvent != null)
                return;

            // Pure runs have no RNG side-effects from events.
            if (GameModeTuning.IsPureMode(Mode) && GameModeTuning.Current.DisableRandomEventsInPure)
            {
                CurrentWaveEvent = null;
                return;
            }

            if (RandomEvent.ShouldHaveEvent(CurrentWaveNumber))
            {
                CurrentWaveEvent = RandomEvent.GenerateEvent(CurrentWaveNumber, CreateEventRng(CurrentWaveNumber));
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
        /// Filters by what can be researched (&lt; level 3) and what player can afford.
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

            // Legacy behavior: allocating resources ends the resource phase.
            GamePhaseTransitionRules.Apply(this, GamePhaseTransitionRules.PhaseEvent.ResourcePhaseCompleted);
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

                // Legacy behavior: applying an upgrade completes development and proceeds to firing.
                GamePhaseTransitionRules.Apply(this, GamePhaseTransitionRules.PhaseEvent.DevelopmentCompleted);
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

            // ===== CRITICAL: Only generate if NOT already generated =====
            // If CurrentFiringProblem already exists, reuse it (from a prior call or load)
            double effectiveRangeForWave = diffConfig.IsTutorialMode
                ? DifficultyConfig.TutorialPotatoCannon.EffectiveRangeMeters
                : GetCurrentEffectiveGunRangeMeters();

            if (CurrentFiringProblem != null && !diffConfig.IsTutorialMode)
            {
                // If gun range changed (e.g., upgrades or stealth mitigation), regenerate.
                if (Math.Abs(CurrentFiringProblem.EngagementDistance - (float)effectiveRangeForWave) > 0.5f)
                {
                    CurrentFiringProblem = null;
                }
            }

            if (CurrentFiringProblem == null)
            {
                // For tutorial mode, use the tutorial firing problem generator
                if (diffConfig.IsTutorialMode)
                {
                    CurrentFiringProblem = GenerateTutorialFiringProblem(CurrentWave);
                }
                else
                {
                    var resolved = ResolveShotStats(target);
                    var solver = new FiringSolution(
                        (float)resolved.ProjectileMassKg,
                        (float)resolved.EffectiveFractureEnergyMJ,
                        target.Mass,
                        enemyCrossSectionM2: target.CrossSection);
                    solver.ConfigureProjectileModifiers(resolved);

                    FiringProblem firingProblem = null!;
                    try
                    {
                        // Pass gun's effective range to constrain engagement
                        firingProblem = solver.GenerateFiringProblem(
                            CurrentWave,
                            (float)resolved.MaxLaunchVelocityMs,
                            (float)effectiveRangeForWave,
                            rng);
                    }
                    catch (InvalidOperationException exception)
                    {
                        System.Console.WriteLine($"Warning: Failed to generate firing problem: {exception.Message}");
                        return new FiringPhaseResult
                        {
                            CanReachTarget = false,
                            TargetDistance = 1_100_000,
                            GunRange = effectiveRangeForWave,
                            Message = $"Gun is insufficient for this target: {exception.Message}",
                            Reward = null,
                            GameOver = false
                        };
                    }

                    // Store for use by the firing phase
                    CurrentFiringProblem = firingProblem;
                }
            }

            return new FiringPhaseResult
            {
                CanReachTarget = true,
                TargetDistance = CurrentFiringProblem.EngagementDistance,
                GunRange = effectiveRangeForWave,
                Message = diffConfig.IsTutorialMode
                    ? $"🎯 Beachball at {CurrentFiringProblem.EngagementDistance:F0} meters - Ready to fire!"
                    : $"Target at engagement distance {GameConstants.FormatDistance(CurrentFiringProblem.EngagementDistance)}",
                Reward = null,
                GameOver = false
            };
        }

        // Stores the firing problem for the firing phase
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
        private static string GetAutoSavePath() => UserDataPaths.GetAutoSavePath();

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
            var enemyRuleset = GameModeTuning.IsPureMode(Mode) ? EnemyGenerationRuleset.Pure : EnemyGenerationRuleset.Full;

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
                        var wave = EnemyWave.GenerateTutorialWave(waveNumber, CreateWaveRng(waveNumber));
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
                    var wave = EnemyWave.GenerateWave(waveNumber, CreateWaveRng(waveNumber), enemyRuleset, CampaignEnemyType);
                    wave.Targets = wave.Targets.Take(1).ToList();

                    var tier = GameConstants.GetTierForWave(waveNumber);
                    int tierIndex = tier.TierIndex;

                    // No tier-based player velocity cap. Use the gun's current base muzzle velocity
                    // (augmented by barrel length + integrity effects) as the campaign reference.
                    double barrelEfficiency = Math.Min(1.0, Gun.BarrelLength / 200.0);
                    double barrelMultiplier = (0.5 + 0.5 * barrelEfficiency);
                    float campaignReferenceVelocity = (float)(Gun.BaseMuzzleVelocityMs * barrelMultiplier * Gun.BarrelIntegrity);

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
                        CreateWaveRng(waveNumber));

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

        // Add this property to the GameState class:
        public CraftedProjectile? CraftedProjectile { get; set; }
    }
}