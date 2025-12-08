namespace Spacegun_Simulator
{
    // ============================================================================
    // GAME STATE DATA - Serializable snapshot of game progress
    // ============================================================================
    // This class is designed for JSON serialization via System.Text.Json
    // Used for save/load functionality.

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

        // Add to property section:
        public string CampaignEnemyTypeId { get; set; } = string.Empty;
        public string CampaignEnemyTypeArchetypeId { get; set; } = string.Empty;
        public string CampaignEnemyTypeCustomName { get; set; } = string.Empty;
        public string CampaignEnemyTypeDescription { get; set; } = string.Empty;
        public float EnemyApproachElevation { get; set; }
        public float EnemyApproachAzimuth { get; set; }

        // ===== NEW: Cached Cartesian vectors =====
        public double CachedEnemyPositionX { get; set; }
        public double CachedEnemyPositionY { get; set; }
        public double CachedEnemyPositionZ { get; set; }
        public double CachedEnemyVelocityX { get; set; }
        public double CachedEnemyVelocityY { get; set; }
        public double CachedEnemyVelocityZ { get; set; }

        // Save difficulty setting
        public string SelectedDifficulty { get; set; } = string.Empty;

        public static GameStateData FromGameState(GameState gameState)
        {
            var data = new GameStateData
            {
                CurrentWaveNumber = gameState.CurrentWaveNumber,
                WavesDefeated = gameState.WavesDefeated,  // UNIFIED: Single property for waves/enemies
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

                // Save projectile configuration
                ProjectileMass = gameState.Gun.DefaultProjectile.Mass,
                ProjectileLength = gameState.Gun.DefaultProjectile.Length,
                ProjectileType = gameState.Gun.DefaultProjectile.Type.ToString(),
                ProjectileDragCoefficient = gameState.Gun.DefaultProjectile.DragCoefficient,
                ProjectileHasGuidance = gameState.Gun.DefaultProjectile.HasGuidance,
                ProjectileGuidanceAccuracy = gameState.Gun.DefaultProjectile.GuidanceAccuracy,
                ProjectilePenetrationType = gameState.Gun.DefaultProjectile.PenetrationType.ToString(),

                AccumulatedResources = new Dictionary<string, double>(gameState.AccumulatedResources),

                // Save time budget
                AvailableYears = gameState.AvailableYears,
                RemainingYears = gameState.RemainingYears,
                AvailableSecondsForGunRange = gameState.GetAvailableSecondsForGunRange(),

                // Save current wave state
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

                // Save selected gun/projectile spec
                SelectedGunProjectileSpecId = gameState.SelectedGunProjectileSpec?.Id ?? string.Empty,

                // Save campaign enemy type
                CampaignEnemyTypeId = gameState.CampaignEnemyType?.Id ?? string.Empty,
                CampaignEnemyTypeArchetypeId = gameState.CampaignEnemyType?.Archetype?.Id ?? string.Empty,
                CampaignEnemyTypeCustomName = gameState.CampaignEnemyType?.CustomName ?? string.Empty,
                CampaignEnemyTypeDescription = gameState.CampaignEnemyType?.Description ?? string.Empty,

                // Save enemy approach parameters
                EnemyApproachElevation = gameState.CurrentWave?.ApproachElevation ?? 45f,
                EnemyApproachAzimuth = gameState.CurrentWave?.ApproachAzimuth ?? 0f,

                // ===== NEW: Save cached Cartesian vectors =====
                CachedEnemyPositionX = gameState.CurrentWave?.CachedEnemyPosition?.X ?? 0,
                CachedEnemyPositionY = gameState.CurrentWave?.CachedEnemyPosition?.Y ?? 0,
                CachedEnemyPositionZ = gameState.CurrentWave?.CachedEnemyPosition?.Z ?? 0,
                CachedEnemyVelocityX = gameState.CurrentWave?.CachedEnemyVelocity?.X ?? 0,
                CachedEnemyVelocityY = gameState.CurrentWave?.CachedEnemyVelocity?.Y ?? 0,
                CachedEnemyVelocityZ = gameState.CurrentWave?.CachedEnemyVelocity?.Z ?? 0,

                // Save difficulty setting
                SelectedDifficulty = gameState.SelectedDifficulty.ToString(),

                SaveTimestamp = DateTime.UtcNow.ToString("O")
            };

            return data;
        }

        public void ApplyToGameState(GameState gameState)
        {
            gameState.CurrentWaveNumber = CurrentWaveNumber;
            gameState.WavesDefeated = WavesDefeated;  // UNIFIED: Restore single property
            gameState.IsGameOver = IsGameOver;

            // Restore resources
            gameState.Resources.Budget = BudgetResources;
            gameState.Resources.Steel = SteelResources;
            gameState.Resources.ExoticMaterials = ExoticResources;
            gameState.Resources.PowerCapacity = PowerCapacity;
            gameState.Resources.ResearchPoints = ResearchPoints;

            // Restore gun state
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

            // Restore projectile configuration
            gameState.Gun.DefaultProjectile.Mass = ProjectileMass;
            gameState.Gun.DefaultProjectile.Length = ProjectileLength;
            if (Enum.TryParse<ProjectileType>(ProjectileType, out var projType))
                gameState.Gun.DefaultProjectile.Type = projType;
            gameState.Gun.DefaultProjectile.DragCoefficient = ProjectileDragCoefficient;
            gameState.Gun.DefaultProjectile.HasGuidance = ProjectileHasGuidance;
            gameState.Gun.DefaultProjectile.GuidanceAccuracy = ProjectileGuidanceAccuracy;
            if (Enum.TryParse<ArmorPenetrationType>(ProjectilePenetrationType, out var penType))
                gameState.Gun.DefaultProjectile.PenetrationType = penType;

            // Restore accumulated resources
            gameState.AccumulatedResources.Clear();
            foreach (var kvp in AccumulatedResources)
            {
                gameState.AccumulatedResources[kvp.Key] = kvp.Value;
            }

            // Restore time budget
            gameState.SetTimebudget(AvailableYears, RemainingYears, AvailableSecondsForGunRange);

            // Restore current wave state
            if (CurrentWaveNumber_Wave > 0)
            {
                // Find the archetype from the campaign enemy type
                var archetype = gameState.CampaignEnemyType?.Archetype;

                // If no campaign enemy type, look it up from All by ID
                if (archetype == null)
                {
                    archetype = EnemyArchetype.All.FirstOrDefault(a => a.Id == CurrentWaveArchetypeId);
                }

                // If still null, create a minimal archetype (shouldn't happen but safety net)
                if (archetype == null)
                {
                    archetype = new EnemyArchetype(
                        CurrentWaveArchetypeId,
                        CurrentWaveArchetypeName,
                        CurrentWaveArchetypeDescription,
                        CurrentWaveArchetypeVelocityMultiplier,
                        (0, 50_000),  // Default mass range
                        (0, 100_000),  // Default fracture energy range
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

                gameState.RestoreCurrentWave(new EnemyWave(CurrentWaveNumber_Wave)
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
                    CachedEnemyPosition = new Vector3(CachedEnemyPositionX, CachedEnemyPositionY, CachedEnemyPositionZ),
                    CachedEnemyVelocity = new Vector3(CachedEnemyVelocityX, CachedEnemyVelocityY, CachedEnemyVelocityZ),
                    Targets = new List<EnemyTarget> { target }
                });
            }

            // Restore selected gun/projectile spec
            if (!string.IsNullOrEmpty(SelectedGunProjectileSpecId))
            {
                gameState.SelectedGunProjectileSpec = GunProjectileSpec.All.FirstOrDefault(s => s.Id == SelectedGunProjectileSpecId);
            }

            // Restore campaign enemy type
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

            // Restore difficulty setting
            if (!string.IsNullOrEmpty(SelectedDifficulty) && Enum.TryParse<GameDifficulty>(SelectedDifficulty, out var difficulty))
            {
                gameState.SelectedDifficulty = difficulty;
            }

            // Restore phase if not game over
            if (!IsGameOver && Enum.TryParse<GameState.GamePhase>(CurrentPhase, out var phase))
            {
                gameState.CurrentPhase = phase;
            }
        }
    }
}