using Spacegun_Simulator.Enemies;
using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Development;
using Spacegun_Simulator.Development.Technology;
using Spacegun_Simulator.Events;
using Spacegun_Simulator.Core.Stats;

namespace Spacegun_Simulator.Core
{
    // ============================================================================
    // GAME STATE DATA - Serializable snapshot of game progress
    // ============================================================================
    // This class is designed for JSON serialization via System.Text.Json
    // Used for save/load functionality.
    // NOW INCLUDES: Tech Tree state and Random Event state

    [Serializable]
    public partial class GameStateData
    {
        // ===== BASELINE TUNING SNAPSHOTS (NEW) =====
        // Backward compatible: older saves won't have this field.
        public ProjectileDefaultsBaselineData? ProjectileDefaultsBaseline { get; set; }

        // Backward compatible: older saves won't have this field.
        public WeaponsTuningConfig? WeaponsTuningBaseline { get; set; }

        public sealed class ProjectileDefaultsBaselineData
        {
            public double Mass { get; set; }
            public double Length { get; set; }
            public bool HasGuidance { get; set; }
            public double GuidanceAccuracy { get; set; }
            public double ImpactCoupling { get; set; }
            public double ImpactCouplingReferenceMassKg { get; set; }
            public double ImpactCouplingMassExponent { get; set; }
            public double ImpactCouplingTechMultiplierPerWeaponsLevel { get; set; }

            public static ProjectileDefaultsBaselineData From(DevelopmentTuning.ProjectileDefaultsValues v) => new()
            {
                Mass = v.Mass,
                Length = v.Length,
                HasGuidance = v.HasGuidance,
                GuidanceAccuracy = v.GuidanceAccuracy,
                ImpactCoupling = v.ImpactCoupling,
                ImpactCouplingReferenceMassKg = v.ImpactCouplingReferenceMassKg,
                ImpactCouplingMassExponent = v.ImpactCouplingMassExponent,
                ImpactCouplingTechMultiplierPerWeaponsLevel = v.ImpactCouplingTechMultiplierPerWeaponsLevel,
            };

            public DevelopmentTuning.ProjectileDefaultsValues ToValue() => new(
                Mass: Mass,
                Length: Length,
                HasGuidance: HasGuidance,
                GuidanceAccuracy: GuidanceAccuracy,
                ImpactCoupling: ImpactCoupling,
                ImpactCouplingReferenceMassKg: ImpactCouplingReferenceMassKg,
                ImpactCouplingMassExponent: ImpactCouplingMassExponent,
                ImpactCouplingTechMultiplierPerWeaponsLevel: ImpactCouplingTechMultiplierPerWeaponsLevel
            );
        }

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
        public double FireControlQuality { get; set; }
        public string PropulsionSystem { get; set; } = string.Empty;
        public double PropellantMass { get; set; }
        public double PropellantEnergyDensity { get; set; }
        public double PowerCapacityGun { get; set; }
        public double CapacitorEfficiency { get; set; }
        public string CoolingSystem { get; set; } = string.Empty;
        public double CoolingCapacity { get; set; }
        public int AmmunitionCount { get; set; }
        public List<string> InstalledUpgrades { get; set; } = new();

        // Persistent installed stat modifiers (applied during ResolveWeaponStats).
        public List<SavedStatModifier> InstalledStatModifiers { get; set; } = new();

        // Projectile configuration
        public double ProjectileMass { get; set; }
        public double ProjectileLength { get; set; }
        public string ProjectileType { get; set; } = string.Empty;
        public bool ProjectileHasGuidance { get; set; }
        public double ProjectileGuidanceAccuracy { get; set; }
        public string ProjectilePenetrationType { get; set; } = string.Empty;

        // ===== PROJECTILE MOD SHOP (NEW) =====
        // Offers refresh once per wave and are persisted so save/load doesn't re-roll.
        public int ProjectileModShopOffersWaveNumber { get; set; } = 0;
        public List<string> ProjectileOwnedCoreIds { get; set; } = new();
        public List<string> ProjectileOwnedPropulsionIds { get; set; } = new();
        public List<string> ProjectileShopCoreOfferIds { get; set; } = new();
        public List<string> ProjectileShopPropulsionOfferIds { get; set; } = new();
        public List<string> ProjectileOwnedGuidanceModuleIds { get; set; } = new();
        public List<string> ProjectileOwnedPayloadModuleIds { get; set; } = new();
        public List<string> ProjectileOwnedArmorModuleIds { get; set; } = new();

        public List<string> ProjectileShopGuidanceOfferModuleIds { get; set; } = new();
        public List<string> ProjectileShopPayloadOfferModuleIds { get; set; } = new();
        public List<string> ProjectileShopArmorOfferModuleIds { get; set; } = new();

        // Current wave accumulated resources
        public Dictionary<string, double> AccumulatedResources { get; set; } = new();

        // ===== TIME BUDGET STATE =====
        public long AvailableYears { get; set; }
        public long RemainingYears { get; set; }
        public double AvailableSecondsForGunRange { get; set; }

        // Timestamp
        public string SaveTimestamp { get; set; } = string.Empty;

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

        // ===== GAME MODE (NEW) =====
        // Backward compatible: older saves won't have this field.
        public string SelectedMode { get; set; } = string.Empty;

        // ===== DETERMINISM (NEW) =====
        // Base seed used to derive per-wave random streams.
        // Nullable for backward compatibility with older saves.
        public int? BaseSeed { get; set; }

        // ===== TECH TREE STATE (NEW) =====
        public Dictionary<string, int> TechTreeLevels { get; set; } = new();

        // ===== CURRENT WAVE EVENT (NEW) =====
        public bool HasCurrentWaveEvent { get; set; } = false;
        public string CurrentWaveEventTitle { get; set; } = string.Empty;
        public string CurrentWaveEventDescription { get; set; } = string.Empty;
        public string CurrentWaveEventType { get; set; } = string.Empty;
        public double CurrentWaveEventProductionMultiplier { get; set; } = 1.0;

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
                ProjectileDefaultsBaseline = ProjectileDefaultsBaselineData.From(gameState.ProjectileDefaultsBaseline),
                WeaponsTuningBaseline = gameState.WeaponsTuningBaseline,

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
                FireControlQuality = gameState.Gun.FireControlQuality,
                PropulsionSystem = gameState.Gun.PropulsionSystem.ToString(),
                PropellantMass = gameState.Gun.PropellantMass,
                PropellantEnergyDensity = gameState.Gun.PropellantEnergyDensity,
                PowerCapacityGun = gameState.Gun.PowerCapacity,
                CapacitorEfficiency = gameState.Gun.CapacitorEfficiency,
                CoolingSystem = gameState.Gun.CoolingSystem.ToString(),
                CoolingCapacity = gameState.Gun.CoolingCapacity,
                AmmunitionCount = gameState.Gun.AmmunitionCount,
                InstalledUpgrades = new List<string>(gameState.Gun.InstalledUpgrades),
                InstalledStatModifiers = (gameState.Gun.InstalledStatModifiers is null || gameState.Gun.InstalledStatModifiers.Count == 0)
                    ? new List<SavedStatModifier>()
                    : gameState.Gun.InstalledStatModifiers
                        .Where(m => m is not null)
                        .Select(m => new SavedStatModifier { Key = m.Key, Op = m.Op.ToString(), Value = m.Value })
                        .ToList(),

                ProjectileMass = gameState.Gun.DefaultProjectile.Mass,
                ProjectileLength = gameState.Gun.DefaultProjectile.Length,
                ProjectileType = gameState.Gun.DefaultProjectile.Type.ToString(),
                ProjectileHasGuidance = gameState.Gun.DefaultProjectile.HasGuidance,
                ProjectileGuidanceAccuracy = gameState.Gun.DefaultProjectile.GuidanceAccuracy,
                ProjectilePenetrationType = gameState.Gun.DefaultProjectile.PenetrationType.ToString(),

                ProjectileModShopOffersWaveNumber = gameState.ProjectileModShop?.OffersWaveNumber ?? 0,
                ProjectileOwnedCoreIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.OwnedCoreIds),
                ProjectileOwnedPropulsionIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.OwnedPropulsionIds),
                ProjectileShopCoreOfferIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.CoreOfferIds),
                ProjectileShopPropulsionOfferIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.PropulsionOfferIds),
                ProjectileOwnedGuidanceModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.OwnedGuidanceModuleIds),
                ProjectileOwnedPayloadModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.OwnedPayloadModuleIds),
                ProjectileOwnedArmorModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.OwnedArmorModuleIds),
                ProjectileShopGuidanceOfferModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.GuidanceOfferModuleIds),
                ProjectileShopPayloadOfferModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.PayloadOfferModuleIds),
                ProjectileShopArmorOfferModuleIds = gameState.ProjectileModShop is null
                    ? new List<string>()
                    : new List<string>(gameState.ProjectileModShop.ArmorOfferModuleIds),

                AccumulatedResources = new Dictionary<string, double>(gameState.AccumulatedResources),

                AvailableYears = gameState.AvailableYears,
                RemainingYears = gameState.RemainingYears,
                AvailableSecondsForGunRange = gameState.GetAvailableSecondsForGunRange(),

                CurrentWaveNumber_Wave = gameState.CurrentWave?.WaveNumber ?? 0,
                CurrentWaveInitialDistance = gameState.CurrentWave?.InitialDistance ?? 0,
                CurrentWaveCurrentDistance = gameState.CurrentWave?.CurrentDistance ?? 0,
                CurrentWaveAverageVelocity = gameState.CurrentWave?.AverageVelocity ?? 0,
                CurrentWaveAverageRadarCrossSection = gameState.CurrentWave?.AverageRadarCrossSection ?? 0,
                CurrentWaveHasStealthCoating = gameState.CurrentWave?.HasStealthCoating ?? false,
                CurrentWaveThreatCount = gameState.CurrentWave?.ThreatCount ?? 1,
                CurrentWaveArchetypeId = gameState.CurrentWave?.Archetype?.Id ?? string.Empty,
                CurrentWaveArchetypeName = gameState.CurrentWave?.Archetype?.Name ?? string.Empty,
                CurrentWaveArchetypeDescription = gameState.CurrentWave?.Archetype?.Description ?? string.Empty,
                CurrentWaveArchetypeVelocityMultiplier = gameState.CurrentWave?.Archetype?.VelocityMultiplier ?? 0,
                CurrentWaveArchetypeDifficultyRating = gameState.CurrentWave?.Archetype?.BaseDifficultyRating ?? 0,
                CurrentWaveDoctrine = gameState.CurrentWave?.Doctrine.ToString() ?? string.Empty,
                CurrentWaveDoctrineSource = gameState.CurrentWave?.DoctrineSource.ToString() ?? string.Empty,
                CurrentWaveTargetName = gameState.CurrentWave?.Targets?[0]?.Name ?? string.Empty,
                CurrentWaveTargetAltitude = gameState.CurrentWave?.Targets?[0]?.Altitude ?? 0,
                CurrentWaveTargetVelocity = gameState.CurrentWave?.Targets?[0]?.Velocity ?? 0,
                CurrentWaveTargetCrossSection = gameState.CurrentWave?.Targets?[0]?.CrossSection ?? 0,
                CurrentWaveTargetAcceleration = gameState.CurrentWave?.Targets?[0]?.Acceleration ?? 0,
                CurrentWaveTargetManeuverability = gameState.CurrentWave?.Targets?[0]?.Maneuverability ?? 0,
                CurrentWaveTargetDefense = gameState.CurrentWave?.Targets?[0]?.Defense ?? 0,
                CurrentWaveTargetOffense = gameState.CurrentWave?.Targets?[0]?.Offense ?? 0,
                CurrentWaveTargetMass = gameState.CurrentWave?.Targets?[0]?.Mass ?? 0,
                CurrentWaveTargetFractureEnergy = gameState.CurrentWave?.Targets?[0]?.FractureEnergy ?? 0,

                CampaignEnemyTypeId = gameState.CampaignEnemyType?.Id ?? string.Empty,
                CampaignEnemyTypeArchetypeId = gameState.CampaignEnemyType?.Archetype?.Id ?? string.Empty,
                CampaignEnemyTypeSecondaryArchetypeId = gameState.CampaignEnemyType?.SecondaryArchetype?.Id ?? string.Empty,
                CampaignEnemyTypeCustomName = gameState.CampaignEnemyType?.CustomName ?? string.Empty,
                CampaignEnemyTypeDescription = gameState.CampaignEnemyType?.Description ?? string.Empty,
                CampaignEnemyTypePrimaryDoctrine = gameState.CampaignEnemyType?.PrimaryDoctrine.ToString() ?? string.Empty,

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
                SelectedMode = gameState.SelectedMode.ToString(),
                BaseSeed = gameState.BaseSeed,

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
            // Restore (or migrate) baseline tuning snapshots.
            // If missing (older saves), snapshot the current config baseline at first load.
            gameState.ProjectileDefaultsBaseline = ProjectileDefaultsBaseline?.ToValue() ?? DevelopmentTuning.ProjectileDefaults;

            // Restore (or migrate) weapon tuning baseline.
            // Apply it so all code paths that reference WeaponsTuning.* directly remain grandfathered.
            var weaponsBaseline = WeaponsTuningBaseline ?? WeaponsTuning.SnapshotCurrentToConfig();
            WeaponsTuning.Apply(weaponsBaseline);
            gameState.WeaponsTuningBaseline = weaponsBaseline;

            // Ensure gun-level baseline tunables are refreshed from the (possibly migrated) baseline.
            // These are not currently persisted as part of the gun state.
            gameState.Gun.BaseWearPerShot = WeaponsTuning.DefaultBarrelWearPerShot;
            gameState.Gun.IntegrityFailureThreshold = WeaponsTuning.Gun.IntegrityFailureThreshold;

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
            gameState.Gun.FireControlQuality = FireControlQuality > 0 ? FireControlQuality : 1.0;
            if (Enum.TryParse<PropulsionType>(PropulsionSystem, out var propulsion))
                gameState.Gun.PropulsionSystem = propulsion;
            gameState.Gun.PropellantMass = PropellantMass;
            gameState.Gun.PropellantEnergyDensity = PropellantEnergyDensity;
            gameState.Gun.PowerCapacity = PowerCapacityGun;
            gameState.Gun.CapacitorEfficiency = CapacitorEfficiency;
            if (Enum.TryParse<CoolingSystem>(CoolingSystem, out var cooling))
                gameState.Gun.CoolingSystem = cooling;
            gameState.Gun.CoolingCapacity = CoolingCapacity;
            gameState.Gun.AmmunitionCount = AmmunitionCount;
            gameState.Gun.InstalledUpgrades.Clear();
            gameState.Gun.InstalledUpgrades.AddRange(InstalledUpgrades);

            gameState.Gun.InstalledStatModifiers.Clear();
            if (InstalledStatModifiers is not null && InstalledStatModifiers.Count > 0)
            {
                foreach (var m in InstalledStatModifiers)
                {
                    if (m is null) continue;
                    if (string.IsNullOrWhiteSpace(m.Key)) continue;
                    if (!Enum.TryParse<StatModifierOp>(m.Op, ignoreCase: true, out var op))
                        continue;
                    gameState.Gun.InstalledStatModifiers.Add(new StatModifier(m.Key, op, m.Value));
                }
            }

            gameState.Gun.DefaultProjectile.Mass = ProjectileMass;
            gameState.Gun.DefaultProjectile.Length = ProjectileLength;
            if (Enum.TryParse<ProjectileType>(ProjectileType, out var projType))
                gameState.Gun.DefaultProjectile.Type = projType;
            gameState.Gun.DefaultProjectile.HasGuidance = ProjectileHasGuidance;
            gameState.Gun.DefaultProjectile.GuidanceAccuracy = ProjectileGuidanceAccuracy;
            if (Enum.TryParse<ArmorPenetrationType>(ProjectilePenetrationType, out var penType))
                gameState.Gun.DefaultProjectile.PenetrationType = penType;

            // ===== Restore projectile mod shop =====
            if (gameState.ProjectileModShop != null)
            {
                gameState.ProjectileModShop.OwnedCoreIds.Clear();
                gameState.ProjectileModShop.OwnedPropulsionIds.Clear();
                gameState.ProjectileModShop.OwnedGuidanceModuleIds.Clear();
                gameState.ProjectileModShop.OwnedPayloadModuleIds.Clear();
                gameState.ProjectileModShop.OwnedArmorModuleIds.Clear();

                if (ProjectileOwnedCoreIds is not null)
                    foreach (var id in ProjectileOwnedCoreIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.OwnedCoreIds.Add(id);

                if (ProjectileOwnedPropulsionIds is not null)
                    foreach (var id in ProjectileOwnedPropulsionIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.OwnedPropulsionIds.Add(id);

                if (ProjectileOwnedGuidanceModuleIds is not null)
                    foreach (var id in ProjectileOwnedGuidanceModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.OwnedGuidanceModuleIds.Add(id);

                if (ProjectileOwnedPayloadModuleIds is not null)
                    foreach (var id in ProjectileOwnedPayloadModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.OwnedPayloadModuleIds.Add(id);

                if (ProjectileOwnedArmorModuleIds is not null)
                    foreach (var id in ProjectileOwnedArmorModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.OwnedArmorModuleIds.Add(id);

                gameState.ProjectileModShop.OffersWaveNumber = ProjectileModShopOffersWaveNumber;
                gameState.ProjectileModShop.CoreOfferIds.Clear();
                gameState.ProjectileModShop.PropulsionOfferIds.Clear();
                gameState.ProjectileModShop.GuidanceOfferModuleIds.Clear();
                gameState.ProjectileModShop.PayloadOfferModuleIds.Clear();
                gameState.ProjectileModShop.ArmorOfferModuleIds.Clear();

                if (ProjectileShopCoreOfferIds is not null)
                    foreach (var id in ProjectileShopCoreOfferIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.CoreOfferIds.Add(id);

                if (ProjectileShopPropulsionOfferIds is not null)
                    foreach (var id in ProjectileShopPropulsionOfferIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.PropulsionOfferIds.Add(id);

                if (ProjectileShopGuidanceOfferModuleIds is not null)
                    foreach (var id in ProjectileShopGuidanceOfferModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.GuidanceOfferModuleIds.Add(id);

                if (ProjectileShopPayloadOfferModuleIds is not null)
                    foreach (var id in ProjectileShopPayloadOfferModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.PayloadOfferModuleIds.Add(id);

                if (ProjectileShopArmorOfferModuleIds is not null)
                    foreach (var id in ProjectileShopArmorOfferModuleIds)
                        if (!string.IsNullOrWhiteSpace(id)) gameState.ProjectileModShop.ArmorOfferModuleIds.Add(id);
            }

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
                var snapshot = new EnemySaveRestore.EnemyWaveRestoreSnapshot(
                    WaveNumber: CurrentWaveNumber_Wave,
                    ArchetypeId: CurrentWaveArchetypeId,
                    ArchetypeName: CurrentWaveArchetypeName,
                    ArchetypeDescription: CurrentWaveArchetypeDescription,
                    ArchetypeVelocityMultiplier: CurrentWaveArchetypeVelocityMultiplier,
                    Doctrine: CurrentWaveDoctrine,
                    DoctrineSource: CurrentWaveDoctrineSource,
                    TargetName: CurrentWaveTargetName,
                    TargetAltitude: CurrentWaveTargetAltitude,
                    TargetVelocity: CurrentWaveTargetVelocity,
                    TargetCrossSection: CurrentWaveTargetCrossSection,
                    TargetAcceleration: CurrentWaveTargetAcceleration,
                    TargetManeuverability: CurrentWaveTargetManeuverability,
                    TargetDefense: CurrentWaveTargetDefense,
                    TargetOffense: CurrentWaveTargetOffense,
                    TargetMass: CurrentWaveTargetMass,
                    TargetFractureEnergy: CurrentWaveTargetFractureEnergy,
                    InitialDistance: CurrentWaveInitialDistance,
                    CurrentDistance: CurrentWaveCurrentDistance,
                    AverageVelocity: CurrentWaveAverageVelocity,
                    AverageRadarCrossSection: CurrentWaveAverageRadarCrossSection,
                    HasStealthCoating: CurrentWaveHasStealthCoating,
                    ThreatCount: CurrentWaveThreatCount,
                    ApproachElevation: EnemyApproachElevation,
                    ApproachAzimuth: EnemyApproachAzimuth,
                    HasCachedVectors: HasCachedVectors,
                    CachedEnemyPositionX: CachedEnemyPositionX,
                    CachedEnemyPositionY: CachedEnemyPositionY,
                    CachedEnemyPositionZ: CachedEnemyPositionZ,
                    CachedEnemyVelocityX: CachedEnemyVelocityX,
                    CachedEnemyVelocityY: CachedEnemyVelocityY,
                    CachedEnemyVelocityZ: CachedEnemyVelocityZ,
                    CachedCorrectLaunchDelayTime: CachedCorrectLaunchDelayTime,
                    CachedCorrectElevation: CachedCorrectElevation,
                    CachedCorrectAzimuth: CachedCorrectAzimuth,
                    CachedCorrectVelocity: CachedCorrectVelocity
                );

                var restoredWave = EnemySaveRestore.CreateWaveForRestore(snapshot, gameState.CampaignEnemyType?.Archetype);
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

            if (!string.IsNullOrEmpty(CampaignEnemyTypeId))
            {
                var snapshot = new EnemySaveRestore.CampaignEnemyTypeSnapshot(
                    Id: CampaignEnemyTypeId,
                    ArchetypeId: CampaignEnemyTypeArchetypeId,
                    SecondaryArchetypeId: CampaignEnemyTypeSecondaryArchetypeId,
                    CustomName: CampaignEnemyTypeCustomName,
                    Description: CampaignEnemyTypeDescription,
                    PrimaryDoctrine: CampaignEnemyTypePrimaryDoctrine
                );

                gameState.CampaignEnemyType = EnemySaveRestore.TryCreateCampaignEnemyType(snapshot);
            }

            if (!string.IsNullOrEmpty(SelectedDifficulty) && Enum.TryParse<GameDifficulty>(SelectedDifficulty, out var difficulty))
            {
                gameState.SelectedDifficulty = difficulty;
            }

            // Prefer new mode if present; otherwise derive a sensible default from legacy difficulty.
            if (!string.IsNullOrEmpty(SelectedMode) && Enum.TryParse<GameModeId>(SelectedMode, out var mode))
            {
                gameState.SelectedMode = mode;
                gameState.SelectedDifficulty = GameModeCatalog.Get(mode).Difficulty;
            }
            else
            {
                gameState.SelectedMode = GameModeCatalog.GetDefaultForDifficulty(gameState.SelectedDifficulty);
            }

            // Restore base seed (or derive a stable fallback for legacy saves).
            int seedToApply = BaseSeed ?? DeriveLegacySeedFallback();
            gameState.SetBaseSeed(seedToApply);

            if (!IsGameOver && Enum.TryParse<GameState.GamePhase>(CurrentPhase, out var phase))
            {
                gameState.CurrentPhase = phase;
            }
        }

        public sealed class SavedStatModifier
        {
            public string Key { get; set; } = string.Empty;
            public string Op { get; set; } = string.Empty;
            public double Value { get; set; }
        }

        private int DeriveLegacySeedFallback()
        {
            unchecked
            {
                // Stable FNV-1a hash across platforms/runs.
                uint hash = 2166136261;

                void Add(string s)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        hash ^= s[i];
                        hash *= 16777619;
                    }
                }

                Add(SaveTimestamp ?? string.Empty);
                Add("|");
                Add(CampaignEnemyTypeId ?? string.Empty);
                Add("|");
                Add(SelectedDifficulty ?? string.Empty);
                Add("|");
                Add(SelectedMode ?? string.Empty);
                Add("|");
                Add(CurrentWaveNumber.ToString());

                // Avoid returning 0 too often for legacy saves.
                return (int)(hash == 0 ? 1u : hash);
            }
        }
    }
}