using Spacegun_Simulator.Development;
using Spacegun_Simulator.Development.Weapons;

namespace Spacegun_Simulator.Core
{
    public sealed class WeaponsTuningConfig
    {
        public int Version { get; set; } = 1;

        public int? BaseMuzzleVelocityMs { get; set; }
        public double? DefaultBarrelWearPerShot { get; set; }

        public WeaponsTechLevelConfig[]? WeaponsTechLevels { get; set; }
        public GunTuningConfig? GunTuning { get; set; }
    }

    public sealed class WeaponsTechLevelConfig
    {
        public int TechLevel { get; set; }
        public string? Name { get; set; }
        public string? PropulsionSystem { get; set; }
        public double MuzzleVelocityMultiplier { get; set; } = 1.0;
        public double BarrelWearMultiplier { get; set; } = 1.0;
        public double FireControlQualityMultiplier { get; set; } = 1.0;
        public double ProjectileMassMultiplier { get; set; } = 1.0;
        public double PenetrationMultiplier { get; set; } = 1.0;
    }

    public sealed class GunTuningConfig
    {
        public double? DefaultBarrelLength { get; set; }
        public double? DefaultBoreDiameter { get; set; }
        public string? DefaultBarrelMaterial { get; set; }
        public double? DefaultBarrelIntegrity { get; set; }
        public double? DefaultFireControlQuality { get; set; }
        public PropulsionType? DefaultPropulsionSystem { get; set; }
        public double? DefaultPropellantMass { get; set; }
        public double? DefaultPropellantEnergyDensity { get; set; }
        public double? DefaultPowerCapacity { get; set; }
        public double? DefaultCapacitorEfficiency { get; set; }
        public CoolingSystem? DefaultCoolingSystem { get; set; }
        public double? DefaultCoolingCapacity { get; set; }
        public int? DefaultAmmunitionCount { get; set; }

        public double? IntegrityFailureThreshold { get; set; }

        public double? RangeReferenceBarrelLength { get; set; }
        public double? RangeMultiplierMin { get; set; }
        public double? RangeMultiplierMax { get; set; }

        public double? WearHeatCoolingCapacityMin { get; set; }
        public double? WearHeatFactorMin { get; set; }
        public double? WearPressureFactorMin { get; set; }
        public double? WearPerShotClampMin { get; set; }
        public double? WearPerShotClampMax { get; set; }

        public double? UpgradeWearModifierMin { get; set; }
        public Dictionary<string, double>? WearModifiersByUpgradeId { get; set; }

        public Dictionary<string, double>? MaxPressureByBarrelMaterial { get; set; }

        public double? SteelSafePropellantEnergyDensityCap { get; set; }
        public Dictionary<string, double>? PropellantEnergyDensityCapMultiplierByBarrelMaterial { get; set; }

        public Dictionary<string, double>? HeatGenerationCoefficientByPropulsion { get; set; }
        public Dictionary<string, double>? HeatGenerationPowerCoefficientByPropulsion { get; set; }

        public double? ReloadBaseTimeSeconds { get; set; }
        public Dictionary<string, double>? ReloadCoolingModifierByCoolingSystem { get; set; }
        public double? ReloadHeatRatioThreshold { get; set; }
    }
}
