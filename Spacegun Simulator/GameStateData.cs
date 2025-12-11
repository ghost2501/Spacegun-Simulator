namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME STATE DATA - Serializable snapshot of game progress
    // ============================================================================
    // This class is designed for JSON serialization via System.Text.Json
    // Used for save/load functionality.
    // NOW INCLUDES: Tech Tree state and Random Event state

    [Serializable]
    public class GameStateData
    {
        // Core game state
        public int CurrentWaveNumber { get; set; }
        public int WavesDefeated { get; set; }  // UNIFIED: Single property for waves/enemies
        public int TotalEnemiesDestroyed { get; set; }
        public bool IsGameOver { get; set; }
        public string CurrentPhase { get; set; } = string.Empty;

        // Resources
        public double BudgetResources { get; set; }
        public double SteelResources { get; set; }
        public double ExoticResources { get; set; }
        public double PowerCapacity { get; set; }
        public double ResearchPoints { get; set; }

        // Gun state
        public double BarrelLength { get; set; }
        public string BarrelMaterial { get; set; } = string.Empty;
        public double BarrelIntegrity { get; set; }
        public string PropulsionSystem { get; set; } = string.Empty;
        public double PropellantMass { get; set; }
        public double PropellantEnergyDensity { get; set; }
        public double PowerCapacityGun { get; set; }
        public double CapacitorEfficiency { get; set; }
        public string CoolingSystem { get; set; } = string.Empty;
        public double CoolingCapacity { get; set; }
        public double StructuralReinforcement { get; set; }
        public int AmmunitionCount { get; set; }
        public List<string> InstalledUpgrades { get; set; } = new();

        // Projectile configuration
        public double ProjectileMass { get; set; }
        public double ProjectileLength { get; set; }
        public string ProjectileType { get; set; } = string.Empty;
        public double ProjectileDragCoefficient { get; set; }
        public bool ProjectileHasGuidance { get; set; }
        public double ProjectileGuidanceAccuracy { get; set; }
        public string ProjectilePenetrationType { get; set; } = string.Empty;

        // Current wave accumulated resources
        public Dictionary<string, double> AccumulatedResources { get; set; } = new();

        // ===== TIME BUDGET STATE =====
        public long AvailableYears { get; set; }
        public long RemainingYears { get; set; }
        public double AvailableSecondsForGunRange { get; set; }

        // ===== CURRENT WAVE STATE =====
        public int CurrentWaveNumber_Wave { get; set; }
        public double CurrentWaveInitialDistance { get; set; }
        public double CurrentWaveCurrentDistance { get; set; }
        public double CurrentWaveAverageVelocity { get; set; }
        public double CurrentWaveAverageRadarCrossSection { get; set; }
        public double CurrentWaveAverageEvasiveness { get; set; }
        public bool CurrentWaveHasStealthCoating { get; set; }
        public string CurrentWaveArchetypeId { get; set; } = string.Empty;
        public string CurrentWaveArchetypeName { get; set; } = string.Empty;
        public string CurrentWaveArchetypeDescription { get; set; } = string.Empty;
        public double CurrentWaveArchetypeVelocityMultiplier { get; set; }
        public double CurrentWaveArchetypeMass { get; set; }
        public double CurrentWaveArchetypeFractureEnergy { get; set; }
        public int CurrentWaveArchetypeDifficultyRating { get; set; }
        public string CurrentWaveTargetName { get; set; } = string.Empty;
        public double CurrentWaveTargetAltitude { get; set; }
        public double CurrentWaveTargetVelocity { get; set; }
        public double CurrentWaveTargetCrossSection { get; set; }
        public double CurrentWaveTargetEvasiveness { get; set; }
        public double CurrentWaveTargetMass { get; set; }
        public double CurrentWaveTargetFractureEnergy { get; set; }

        // ===== SELECTED GUN/PROJECTILE SPEC =====
        public string SelectedGunProjectileSpecId { get; set; } = string.Empty;

        // Timestamp
        public string SaveTimestamp { get; set; } = string.Empty;

        // ===== CAMPAIGN ENEMY TYPE =====
        public string CampaignEnemyTypeId { get; set; } = string.Empty;
        public string CampaignEnemyTypeArchetypeId { get; set; } = string.Empty;
        public string CampaignEnemyTypeCustomName { get; set; } = string.Empty;
        public string CampaignEnemyTypeDescription { get; set; } = string.Empty;
        public float EnemyApproachElevation { get; set; }
        public float EnemyApproachAzimuth { get; set; }

        // ===== CACHED CARTESIAN VECTORS (From Wave) =====
        public double CachedEnemyPositionX { get; set; }
        public double CachedEnemyPositionY { get; set; }
        public double CachedEnemyPositionZ { get; set; }
        public double CachedEnemyVelocityX { get; set; }
        public double CachedEnemyVelocityY { get; set; }
        public double CachedEnemyVelocityZ { get; set; }
        public bool HasCachedVectors { get; set; } = false;

        // ===== FIRING PROBLEM STATE (Critical for preventing regeneration on load) =====
        public double FiringProblemEnemyPositionX { get; set; }
        public double FiringProblemEnemyPositionY { get; set; }
        public double FiringProblemEnemyPositionZ { get; set; }
        public double FiringProblemEnemyVelocityX { get; set; }
        public double FiringProblemEnemyVelocityY { get; set; }
        public double FiringProblemEnemyVelocityZ { get; set; }
        public float FiringProblemApproachElevation { get; set; }
        public float FiringProblemApproachAzimuth { get; set; }
        public float FiringProblemEngagementDistance { get; set; }
        public float FiringProblemApproachSpeed { get; set; }
        public double FiringProblemFractureEnergyRequired { get; set; }
        public float FiringProblemCorrectLaunchDelayTime { get; set; }
        public float FiringProblemCorrectElevation { get; set; }
        public float FiringProblemCorrectAzimuth { get; set; }
        public float FiringProblemCorrectVelocity { get; set; }
        public bool HasFiringProblem { get; set; } = false;

        // ===== DIFFICULTY SETTING =====
        public string SelectedDifficulty { get; set; } = string.Empty;

        // ===== TECH TREE STATE (NEW) =====
        public Dictionary<string, int> TechTreeLevels { get; set; } = new();

        // ===== CURRENT WAVE EVENT (NEW) =====
        public bool HasCurrentWaveEvent { get; set; } = false;
        public string CurrentWaveEventTitle { get; set; } = string.Empty;
        public string CurrentWaveEventDescription { get; set; } = string.Empty;
        public string CurrentWaveEventType { get; set; } = string.Empty;
        public double CurrentWaveEventProductionMultiplier { get; set; } = 1.0;

        // ===== CACHED CORRECT FIRING SOLUTION (From Wave) =====
        public float CachedCorrectLaunchDelayTime { get; set; }
        public float CachedCorrectElevation { get; set; }
        public float CachedCorrectAzimuth { get; set; }
        public float CachedCorrectVelocity { get; set; }

        public static GameStateData FromGameState(GameState gameState)
        {
            // ===== Check if we have valid cached vectors to save =====
            bool shouldSaveVectors = gameState.CurrentWave?.CachedEnemyPosition.HasValue == true &&
                                     gameState.CurrentWave?.CachedEnemyVelocity.HasValue == true;

            // ===== Check if we have a firing problem to save =====
            bool shouldSaveFiringProblem = gameState.CurrentFiringProblem != null;

            // ===== Build tech tree levels dictionary =====
            var techTreeLevels = new Dictionary<string, int>();
            foreach (TechTree.TechType tech in System.Enum.GetValues(typeof(TechTree.TechType)))
            {
                if (gameState.TechTree?.CurrentLevel.ContainsKey(tech) == true)
                {
                    techTreeLevels[tech.ToString()] = gameState.TechTree.CurrentLevel[tech];
                }
            }

            // ===== Prepare random event data =====
            bool hasEvent = gameState.CurrentWaveEvent != null;

            var data = new GameStateData
            {
                CurrentWaveNumber = gameState.CurrentWaveNumber,
                WavesDefeated = gameState.WavesDefeated,
                IsGameOver = gameState.IsGameOver,
                CurrentPhase = gameState.CurrentPhase.ToString(),

                BudgetResources = gameState.Resources.Budget,
                SteelResources = gameState.Resources.Steel,
                ExoticResources = gameState.Resources.ExoticMaterials,
                PowerCapacity = gameState.Resources.PowerCapacity,
                ResearchPoints = gameState.Resources.ResearchPoints,

                BarrelLength = gameState.Gun.BarrelLength,
                BarrelMaterial = gameState.Gun.BarrelMaterial,
                BarrelIntegrity = gameState.Gun.BarrelIntegrity,
                PropulsionSystem = gameState.Gun.PropulsionSystem.ToString(),
                PropellantMass = gameState.Gun.PropellantMass,
                PropellantEnergyDensity = gameState.Gun.PropellantEnergyDensity,
                PowerCapacityGun = gameState.Gun.PowerCapacity,
                CapacitorEfficiency = gameState.Gun.CapacitorEfficiency,
                CoolingSystem = gameState.Gun.CoolingSystem.ToString(),
                CoolingCapacity = gameState.Gun.CoolingCapacity,
                StructuralReinforcement = gameState.Gun.StructuralReinforcement,
                AmmunitionCount = gameState.Gun.AmmunitionCount,
                InstalledUpgrades = new List<string>(gameState.Gun.InstalledUpgrades),

                ProjectileMass = gameState.Gun.DefaultProjectile.Mass,
                ProjectileLength = gameState.Gun.DefaultProjectile.Length,
                ProjectileType = gameState.Gun.DefaultProjectile.Type.ToString(),
                ProjectileDragCoefficient = gameState.Gun.DefaultProjectile.DragCoefficient,
                ProjectileHasGuidance = gameState.Gun.DefaultProjectile.HasGuidance,
                ProjectileGuidanceAccuracy = gameState.Gun.DefaultProjectile.GuidanceAccuracy,
                ProjectilePenetrationType = gameState.Gun.DefaultProjectile.PenetrationType.ToString(),

                AccumulatedResources = new Dictionary<string, double>(gameState.AccumulatedResources),

                AvailableYears = gameState.AvailableYears,
                RemainingYears = gameState.RemainingYears,
                AvailableSecondsForGunRange = gameState.GetAvailableSecondsForGunRange(),

                CurrentWaveNumber_Wave = gameState.CurrentWave?.WaveNumber ?? 0,
                CurrentWaveInitialDistance = gameState.CurrentWave?.InitialDistance ?? 0,
                CurrentWaveCurrentDistance = gameState.CurrentWave?.CurrentDistance ?? 0,
                CurrentWaveAverageVelocity = gameState.CurrentWave?.AverageVelocity ?? 0,
                CurrentWaveAverageRadarCrossSection = gameState.CurrentWave?.AverageRadarCrossSection ?? 0,
                CurrentWaveAverageEvasiveness = gameState.CurrentWave?.AverageEvasiveness ?? 0,
                CurrentWaveHasStealthCoating = gameState.CurrentWave?.HasStealthCoating ?? false,
                CurrentWaveArchetypeId = gameState.CurrentWave?.Archetype?.Id ?? string.Empty,
                CurrentWaveArchetypeName = gameState.CurrentWave?.Archetype?.Name ?? string.Empty,
                CurrentWaveArchetypeDescription = gameState.CurrentWave?.Archetype?.Description ?? string.Empty,
                CurrentWaveArchetypeVelocityMultiplier = gameState.CurrentWave?.Archetype?.VelocityMultiplier ?? 0,
                CurrentWaveArchetypeDifficultyRating = gameState.CurrentWave?.Archetype?.BaseDifficultyRating ?? 0,
                CurrentWaveTargetName = gameState.CurrentWave?.Targets?[0]?.Name ?? string.Empty,
                CurrentWaveTargetAltitude = gameState.CurrentWave?.Targets?[0]?.Altitude ?? 0,
                CurrentWaveTargetVelocity = gameState.CurrentWave?.Targets?[0]?.Velocity ?? 0,
                CurrentWaveTargetCrossSection = gameState.CurrentWave?.Targets?[0]?.CrossSection ?? 0,
                CurrentWaveTargetEvasiveness = gameState.CurrentWave?.Targets?[0]?.Evasiveness ?? 0,
                CurrentWaveTargetMass = gameState.CurrentWave?.Targets?[0]?.Mass ?? 0,
                CurrentWaveTargetFractureEnergy = gameState.CurrentWave?.Targets?[0]?.FractureEnergy ?? 0,

                SelectedGunProjectileSpecId = gameState.SelectedGunProjectileSpec?.Id ?? string.Empty,

                CampaignEnemyTypeId = gameState.CampaignEnemyType?.Id ?? string.Empty,
                CampaignEnemyTypeArchetypeId = gameState.CampaignEnemyType?.Archetype?.Id ?? string.Empty,
                CampaignEnemyTypeCustomName = gameState.CampaignEnemyType?.CustomName ?? string.Empty,
                CampaignEnemyTypeDescription = gameState.CampaignEnemyType?.Description ?? string.Empty,

                EnemyApproachElevation = gameState.CurrentWave?.ApproachElevation ?? 45f,
                EnemyApproachAzimuth = gameState.CurrentWave?.ApproachAzimuth ?? 0f,

                // ===== Save cached wave vectors if they exist =====
                CachedEnemyPositionX = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyPosition != null ? gameState.CurrentWave.CachedEnemyPosition.Value.X : 0,
                CachedEnemyPositionY = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyPosition != null ? gameState.CurrentWave.CachedEnemyPosition.Value.Y : 0,
                CachedEnemyPositionZ = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyPosition != null ? gameState.CurrentWave.CachedEnemyPosition.Value.Z : 0,
                CachedEnemyVelocityX = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyVelocity != null ? gameState.CurrentWave.CachedEnemyVelocity.Value.X : 0,
                CachedEnemyVelocityY = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyVelocity != null ? gameState.CurrentWave.CachedEnemyVelocity.Value.Y : 0,
                CachedEnemyVelocityZ = shouldSaveVectors && gameState.CurrentWave?.CachedEnemyVelocity != null ? gameState.CurrentWave.CachedEnemyVelocity.Value.Z : 0,
                CachedCorrectLaunchDelayTime = shouldSaveVectors ? gameState.CurrentWave?.CachedCorrectLaunchDelayTime ?? 0f : 0f,
                CachedCorrectElevation = shouldSaveVectors ? gameState.CurrentWave?.CachedCorrectElevation ?? 0f : 0f,
                CachedCorrectAzimuth = shouldSaveVectors ? gameState.CurrentWave?.CachedCorrectAzimuth ?? 0f : 0f,
                CachedCorrectVelocity = shouldSaveVectors ? gameState.CurrentWave?.CachedCorrectVelocity ?? 0f : 0f,
                HasCachedVectors = shouldSaveVectors,

                // ===== Save firing problem if it exists =====
                FiringProblemEnemyPositionX = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyPosition.X : 0,
                FiringProblemEnemyPositionY = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyPosition.Y : 0,
                FiringProblemEnemyPositionZ = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyPosition.Z : 0,
                FiringProblemEnemyVelocityX = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyVelocity.X : 0,
                FiringProblemEnemyVelocityY = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyVelocity.Y : 0,
                FiringProblemEnemyVelocityZ = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EnemyVelocity.Z : 0,
                FiringProblemApproachElevation = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.ApproachElevation : 0f,
                FiringProblemApproachAzimuth = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.ApproachAzimuth : 0f,
                FiringProblemEngagementDistance = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.EngagementDistance : 0f,
                FiringProblemApproachSpeed = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.ApproachSpeed : 0f,
                FiringProblemFractureEnergyRequired = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.FractureEnergyRequired : 0,
                FiringProblemCorrectLaunchDelayTime = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.CorrectLaunchDelayTime : 0f,
                FiringProblemCorrectElevation = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.CorrectElevation : 0f,
                FiringProblemCorrectAzimuth = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.CorrectAzimuth : 0f,
                FiringProblemCorrectVelocity = shouldSaveFiringProblem ? gameState.CurrentFiringProblem!.CorrectVelocity : 0f,
                HasFiringProblem = shouldSaveFiringProblem,

                SelectedDifficulty = gameState.SelectedDifficulty.ToString(),

                // ===== Save tech tree levels =====
                TechTreeLevels = techTreeLevels,

                // ===== Save random event state =====
                HasCurrentWaveEvent = hasEvent,
                CurrentWaveEventTitle = hasEvent ? gameState.CurrentWaveEvent!.Title : string.Empty,
                CurrentWaveEventDescription = hasEvent ? gameState.CurrentWaveEvent!.Description : string.Empty,
                CurrentWaveEventType = hasEvent ? gameState.CurrentWaveEvent!.Type.ToString() : string.Empty,
                CurrentWaveEventProductionMultiplier = hasEvent ? gameState.CurrentWaveEvent!.ProductionMultiplier : 1.0,

                SaveTimestamp = DateTime.UtcNow.ToString("O")
            };

            return data;
        }

        public void ApplyToGameState(GameState gameState)
        {
            gameState.CurrentWaveNumber = CurrentWaveNumber;
            gameState.WavesDefeated = WavesDefeated;
            gameState.IsGameOver = IsGameOver;

            gameState.Resources.Budget = BudgetResources;
            gameState.Resources.Steel = SteelResources;
            gameState.Resources.ExoticMaterials = ExoticResources;
            gameState.Resources.PowerCapacity = PowerCapacity;
            gameState.Resources.ResearchPoints = ResearchPoints;

            gameState.Gun.BarrelLength = BarrelLength;
            gameState.Gun.BarrelMaterial = BarrelMaterial;
            gameState.Gun.BarrelIntegrity = BarrelIntegrity;
            if (Enum.TryParse<PropulsionType>(PropulsionSystem, out var propulsion))
                gameState.Gun.PropulsionSystem = propulsion;
            gameState.Gun.PropellantMass = PropellantMass;
            gameState.Gun.PropellantEnergyDensity = PropellantEnergyDensity;
            gameState.Gun.PowerCapacity = PowerCapacityGun;
            gameState.Gun.CapacitorEfficiency = CapacitorEfficiency;
            if (Enum.TryParse<CoolingSystem>(CoolingSystem, out var cooling))
                gameState.Gun.CoolingSystem = cooling;
            gameState.Gun.CoolingCapacity = CoolingCapacity;
            gameState.Gun.StructuralReinforcement = StructuralReinforcement;
            gameState.Gun.AmmunitionCount = AmmunitionCount;
            gameState.Gun.InstalledUpgrades.Clear();
            gameState.Gun.InstalledUpgrades.AddRange(InstalledUpgrades);

            gameState.Gun.DefaultProjectile.Mass = ProjectileMass;
            gameState.Gun.DefaultProjectile.Length = ProjectileLength;
            if (Enum.TryParse<ProjectileType>(ProjectileType, out var projType))
                gameState.Gun.DefaultProjectile.Type = projType;
            gameState.Gun.DefaultProjectile.DragCoefficient = ProjectileDragCoefficient;
            gameState.Gun.DefaultProjectile.HasGuidance = ProjectileHasGuidance;
            gameState.Gun.DefaultProjectile.GuidanceAccuracy = ProjectileGuidanceAccuracy;
            if (Enum.TryParse<ArmorPenetrationType>(ProjectilePenetrationType, out var penType))
                gameState.Gun.DefaultProjectile.PenetrationType = penType;

            gameState.AccumulatedResources.Clear();
            foreach (var kvp in AccumulatedResources)
            {
                gameState.AccumulatedResources[kvp.Key] = kvp.Value;
            }

            gameState.SetTimebudget(AvailableYears, RemainingYears, AvailableSecondsForGunRange);

            // ===== Restore tech tree levels =====
            if (gameState.TechTree != null && TechTreeLevels.Count > 0)
            {
                gameState.TechTree.CurrentLevel.Clear();
                foreach (var kvp in TechTreeLevels)
                {
                    if (Enum.TryParse<TechTree.TechType>(kvp.Key, out var techType))
                    {
                        gameState.TechTree.CurrentLevel[techType] = kvp.Value;
                    }
                }
            }

            // ===== Restore random event state =====
            if (HasCurrentWaveEvent)
            {
                if (Enum.TryParse<RandomEvent.EventType>(CurrentWaveEventType, out var eventType))
                {
                    gameState.CurrentWaveEvent = new RandomEvent
                    {
                        Title = CurrentWaveEventTitle,
                        Description = CurrentWaveEventDescription,
                        Type = eventType,
                        ProductionMultiplier = CurrentWaveEventProductionMultiplier
                    };
                }
            }
            else
            {
                gameState.CurrentWaveEvent = null;
            }

            // Restore current wave state
            if (CurrentWaveNumber_Wave > 0)
            {
                var archetype = gameState.CampaignEnemyType?.Archetype;

                if (archetype == null)
                {
                    archetype = EnemyArchetype.All.FirstOrDefault(a => a.Id == CurrentWaveArchetypeId);
                }

                if (archetype == null)
                {
                    archetype = new EnemyArchetype(
                        CurrentWaveArchetypeId,
                        CurrentWaveArchetypeName,
                        CurrentWaveArchetypeDescription,
                        CurrentWaveArchetypeVelocityMultiplier,
                        (0, 50_000),
                        (0, 100_000),
                        1
                    );
                }

                var target = new EnemyTarget
                {
                    Name = CurrentWaveTargetName,
                    Altitude = CurrentWaveTargetAltitude,
                    Velocity = CurrentWaveTargetVelocity,
                    CrossSection = CurrentWaveTargetCrossSection,
                    Evasiveness = CurrentWaveTargetEvasiveness,
                    Mass = CurrentWaveTargetMass,
                    FractureEnergy = CurrentWaveTargetFractureEnergy
                };

                var restoredWave = new EnemyWave(CurrentWaveNumber_Wave)
                {
                    WaveNumber = CurrentWaveNumber_Wave,
                    InitialDistance = CurrentWaveInitialDistance,
                    CurrentDistance = CurrentWaveCurrentDistance,
                    AverageVelocity = CurrentWaveAverageVelocity,
                    AverageRadarCrossSection = CurrentWaveAverageRadarCrossSection,
                    AverageEvasiveness = CurrentWaveAverageEvasiveness,
                    HasStealthCoating = CurrentWaveHasStealthCoating,
                    Archetype = archetype,
                    ApproachElevation = EnemyApproachElevation,
                    ApproachAzimuth = EnemyApproachAzimuth,
                    CachedEnemyPosition = HasCachedVectors ? new Vector3(CachedEnemyPositionX, CachedEnemyPositionY, CachedEnemyPositionZ) : null,
                    CachedEnemyVelocity = HasCachedVectors ? new Vector3(CachedEnemyVelocityX, CachedEnemyVelocityY, CachedEnemyVelocityZ) : null,
                    CachedCorrectLaunchDelayTime = CachedCorrectLaunchDelayTime,
                    CachedCorrectElevation = CachedCorrectElevation,
                    CachedCorrectAzimuth = CachedCorrectAzimuth,
                    CachedCorrectVelocity = CachedCorrectVelocity,
                    IsRestoredFromSave = HasCachedVectors,
                    Targets = new List<EnemyTarget> { target }
                };

                gameState.RestoreCurrentWave(restoredWave);
            }

            // ===== Restore firing problem if it exists =====
            if (HasFiringProblem)
            {
                gameState.CurrentFiringProblem = new FiringProblem
                {
                    EnemyPosition = new Vector3(FiringProblemEnemyPositionX, FiringProblemEnemyPositionY, FiringProblemEnemyPositionZ),
                    EnemyVelocity = new Vector3(FiringProblemEnemyVelocityX, FiringProblemEnemyVelocityY, FiringProblemEnemyVelocityZ),
                    ApproachElevation = FiringProblemApproachElevation,
                    ApproachAzimuth = FiringProblemApproachAzimuth,
                    EngagementDistance = FiringProblemEngagementDistance,
                    ApproachSpeed = FiringProblemApproachSpeed,
                    FractureEnergyRequired = FiringProblemFractureEnergyRequired,
                    CorrectLaunchDelayTime = FiringProblemCorrectLaunchDelayTime,
                    CorrectElevation = FiringProblemCorrectElevation,
                    CorrectAzimuth = FiringProblemCorrectAzimuth,
                    CorrectVelocity = FiringProblemCorrectVelocity
                };
            }

            if (!string.IsNullOrEmpty(SelectedGunProjectileSpecId))
            {
                gameState.SelectedGunProjectileSpec = GunProjectileSpec.All.FirstOrDefault(s => s.Id == SelectedGunProjectileSpecId);
            }

            if (!string.IsNullOrEmpty(CampaignEnemyTypeId))
            {
                var archetype = EnemyArchetype.All.FirstOrDefault(a => a.Id == CampaignEnemyTypeArchetypeId);
                if (archetype != null)
                {
                    gameState.CampaignEnemyType = new EnemyType(
                        CampaignEnemyTypeId,
                        archetype,
                        CampaignEnemyTypeCustomName,
                        CampaignEnemyTypeDescription
                    );
                }
            }

            if (!string.IsNullOrEmpty(SelectedDifficulty) && Enum.TryParse<GameDifficulty>(SelectedDifficulty, out var difficulty))
            {
                gameState.SelectedDifficulty = difficulty;
            }

            if (!IsGameOver && Enum.TryParse<GameState.GamePhase>(CurrentPhase, out var phase))
            {
                gameState.CurrentPhase = phase;
            }
        }
    }
}