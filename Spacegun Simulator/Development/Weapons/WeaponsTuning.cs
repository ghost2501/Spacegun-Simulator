using Spacegun_Simulator.Development;

namespace Spacegun_Simulator.Core
{
    /// <summary>
    /// Config-backed tuning values for Development/Weapons.
    /// Defaults match the prior hard-coded values so gameplay math stays identical.
    /// </summary>
    public static class WeaponsTuning
    {
        public readonly record struct WeaponsTechTuning(
            int TechLevel,
            string Name,
            PropulsionType PropulsionSystem,
            double MuzzleVelocityMultiplier,
            double BarrelWearMultiplier = 1.0,
            double FireControlQualityMultiplier = 1.0,
            double ProjectileMassMultiplier = 1.0,
            double PenetrationMultiplier = 1.0
        );

        // Canonical weapon wear + base velocity are still mutable (config can override).
        public static double DefaultBarrelWearPerShot { get; set; } = 0.0005;
        public static int BaseMuzzleVelocityMs { get; set; } = 80_000;

        public static WeaponsTechTuning[] WeaponsTechLevels { get; private set; } = CreateDefaultTechLevels();
        public static double[] WeaponsTechVelocityMultipliers { get; private set; } = CreateVelocityMultipliers(CreateDefaultTechLevels());

        public static GunTuningValues Gun { get; private set; } = GunTuningValues.CreateDefaults();

        public static void Apply(WeaponsTuningConfig cfg)
        {
            if (cfg.BaseMuzzleVelocityMs.HasValue)
                BaseMuzzleVelocityMs = Math.Max(1, cfg.BaseMuzzleVelocityMs.Value);

            if (cfg.DefaultBarrelWearPerShot.HasValue)
                DefaultBarrelWearPerShot = Math.Max(0.0, cfg.DefaultBarrelWearPerShot.Value);

            if (cfg.WeaponsTechLevels is { Length: > 0 })
            {
                var levels = new WeaponsTechTuning[cfg.WeaponsTechLevels.Length];
                for (int i = 0; i < levels.Length; i++)
                {
                    var row = cfg.WeaponsTechLevels[i];

                    var propulsion = PropulsionType.Chemical;
                    if (!string.IsNullOrWhiteSpace(row.PropulsionSystem)
                        && Enum.TryParse(row.PropulsionSystem, ignoreCase: true, out PropulsionType parsed))
                    {
                        propulsion = parsed;
                    }

                    levels[i] = new WeaponsTechTuning(
                        TechLevel: Math.Max(1, row.TechLevel),
                        Name: row.Name ?? "",
                        PropulsionSystem: propulsion,
                        MuzzleVelocityMultiplier: row.MuzzleVelocityMultiplier,
                        BarrelWearMultiplier: row.BarrelWearMultiplier,
                        FireControlQualityMultiplier: row.FireControlQualityMultiplier,
                        ProjectileMassMultiplier: row.ProjectileMassMultiplier,
                        PenetrationMultiplier: row.PenetrationMultiplier
                    );
                }

                WeaponsTechLevels = levels;
                WeaponsTechVelocityMultipliers = CreateVelocityMultipliers(levels);
            }

            if (cfg.GunTuning is not null)
                Gun = Gun.Apply(cfg.GunTuning);
        }

        public static PropulsionType GetPropulsionSystemForTechLevel(int weaponsTechLevel)
        {
            if (weaponsTechLevel <= 0) weaponsTechLevel = 1;
            int index = weaponsTechLevel - 1;
            if (index < 0) index = 0;
            if (WeaponsTechLevels.Length == 0) return PropulsionType.Chemical;
            if (index >= WeaponsTechLevels.Length) index = WeaponsTechLevels.Length - 1;
            return WeaponsTechLevels[index].PropulsionSystem;
        }

        public static double GetBaseMuzzleVelocityForTechLevel(int weaponsTechLevel)
        {
            if (weaponsTechLevel <= 0) weaponsTechLevel = 1;

            int index = weaponsTechLevel - 1;
            if (index < 0) index = 0;
            if (index >= WeaponsTechVelocityMultipliers.Length)
                index = WeaponsTechVelocityMultipliers.Length - 1;

            return BaseMuzzleVelocityMs * WeaponsTechVelocityMultipliers[index];
        }

        private static WeaponsTechTuning[] CreateDefaultTechLevels() =>
        [
            new WeaponsTechTuning(
                TechLevel: 1,
                Name: "Chemical",
                PropulsionSystem: PropulsionType.Chemical,
                MuzzleVelocityMultiplier: 1.0
            ),
            new WeaponsTechTuning(
                TechLevel: 2,
                Name: "Railgun",
                PropulsionSystem: PropulsionType.Railgun,
                MuzzleVelocityMultiplier: 2.0
            ),
            new WeaponsTechTuning(
                TechLevel: 3,
                Name: "Hybrid",
                PropulsionSystem: PropulsionType.Hybrid,
                MuzzleVelocityMultiplier: 4.0
            )
        ];

        private static double[] CreateVelocityMultipliers(WeaponsTechTuning[] techLevels)
        {
            var arr = new double[Math.Max(1, techLevels.Length)];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = i < techLevels.Length
                    ? Math.Max(0.0, techLevels[i].MuzzleVelocityMultiplier)
                    : 1.0;
            }
            return arr;
        }

        public sealed record GunTuningValues(
            double DefaultBarrelLength,
            double DefaultBoreDiameter,
            string DefaultBarrelMaterial,
            double DefaultBarrelIntegrity,
            double DefaultFireControlQuality,
            PropulsionType DefaultPropulsionSystem,
            double DefaultPropellantMass,
            double DefaultPropellantEnergyDensity,
            double DefaultPowerCapacity,
            double DefaultCapacitorEfficiency,
            CoolingSystem DefaultCoolingSystem,
            double DefaultCoolingCapacity,
            int DefaultAmmunitionCount,
            double IntegrityFailureThreshold,
            double RangeReferenceBarrelLength,
            double RangeMultiplierMin,
            double RangeMultiplierMax,
            double WearHeatCoolingCapacityMin,
            double WearHeatFactorMin,
            double WearPressureFactorMin,
            double WearPerShotClampMin,
            double WearPerShotClampMax,
            double UpgradeWearModifierMin,
            Dictionary<string, double> WearModifiersByUpgradeId,
            Dictionary<string, double> MaxPressureByBarrelMaterial,
            double SteelSafePropellantEnergyDensityCap,
            Dictionary<string, double> PropellantEnergyDensityCapMultiplierByBarrelMaterial,
            Dictionary<string, double> HeatGenerationCoefficientByPropulsion,
            Dictionary<string, double> HeatGenerationPowerCoefficientByPropulsion,
            double ReloadBaseTimeSeconds,
            Dictionary<string, double> ReloadCoolingModifierByCoolingSystem,
            double ReloadHeatRatioThreshold)
        {
            public static GunTuningValues CreateDefaults() => new(
                DefaultBarrelLength: 100.0,
                DefaultBoreDiameter: 0.5,
                DefaultBarrelMaterial: "Steel",
                DefaultBarrelIntegrity: 1.0,
                DefaultFireControlQuality: 1.0,
                DefaultPropulsionSystem: PropulsionType.Chemical,
                DefaultPropellantMass: 50.0,
                DefaultPropellantEnergyDensity: 5.0,
                DefaultPowerCapacity: 100.0,
                DefaultCapacitorEfficiency: 0.7,
                DefaultCoolingSystem: CoolingSystem.Passive,
                DefaultCoolingCapacity: 10.0,
                DefaultAmmunitionCount: 10,
                IntegrityFailureThreshold: 0.05,
                RangeReferenceBarrelLength: 100.0,
                RangeMultiplierMin: 0.5,
                RangeMultiplierMax: 2.0,
                WearHeatCoolingCapacityMin: 1.0,
                WearHeatFactorMin: 0.1,
                WearPressureFactorMin: 0.5,
                WearPerShotClampMin: 1e-6,
                WearPerShotClampMax: 0.2,
                UpgradeWearModifierMin: 0.1,
                WearModifiersByUpgradeId: new Dictionary<string, double>
                {
                    ["ReinforcedBarrel"] = 0.6,
                    ["HighTempCoating"] = 0.75,
                    ["RapidFire"] = 1.5,
                    ["CeramicLiner"] = 0.8,
                },
                MaxPressureByBarrelMaterial: new Dictionary<string, double>
                {
                    ["Steel"] = 500.0,
                    ["Titanium"] = 700.0,
                    ["Composite"] = 900.0,
                    ["Exotic"] = 1200.0,
                },
                SteelSafePropellantEnergyDensityCap: 5.0,
                PropellantEnergyDensityCapMultiplierByBarrelMaterial: new Dictionary<string, double>
                {
                    ["Steel"] = 1.0,
                    ["Titanium"] = 1.2,
                    ["Composite"] = 1.5,
                    ["Exotic"] = 2.0,
                },
                HeatGenerationCoefficientByPropulsion: new Dictionary<string, double>
                {
                    [nameof(PropulsionType.Chemical)] = 0.3,
                    [nameof(PropulsionType.Hybrid)] = 0.2,
                },
                HeatGenerationPowerCoefficientByPropulsion: new Dictionary<string, double>
                {
                    [nameof(PropulsionType.Railgun)] = 0.5,
                    [nameof(PropulsionType.Coilgun)] = 0.4,
                    [nameof(PropulsionType.Hybrid)] = 0.3,
                },
                ReloadBaseTimeSeconds: 30.0,
                ReloadCoolingModifierByCoolingSystem: new Dictionary<string, double>
                {
                    [nameof(CoolingSystem.Passive)] = 1.0,
                    [nameof(CoolingSystem.ActiveAir)] = 0.8,
                    [nameof(CoolingSystem.Liquid)] = 0.6,
                    [nameof(CoolingSystem.Cryogenic)] = 0.4,
                },
                ReloadHeatRatioThreshold: 1.0
            );

            public GunTuningValues Apply(GunTuningConfig c) => this with
            {
                DefaultBarrelLength = c.DefaultBarrelLength ?? DefaultBarrelLength,
                DefaultBoreDiameter = c.DefaultBoreDiameter ?? DefaultBoreDiameter,
                DefaultBarrelMaterial = c.DefaultBarrelMaterial ?? DefaultBarrelMaterial,
                DefaultBarrelIntegrity = c.DefaultBarrelIntegrity ?? DefaultBarrelIntegrity,
                DefaultFireControlQuality = c.DefaultFireControlQuality ?? DefaultFireControlQuality,
                DefaultPropulsionSystem = c.DefaultPropulsionSystem ?? DefaultPropulsionSystem,
                DefaultPropellantMass = c.DefaultPropellantMass ?? DefaultPropellantMass,
                DefaultPropellantEnergyDensity = c.DefaultPropellantEnergyDensity ?? DefaultPropellantEnergyDensity,
                DefaultPowerCapacity = c.DefaultPowerCapacity ?? DefaultPowerCapacity,
                DefaultCapacitorEfficiency = c.DefaultCapacitorEfficiency ?? DefaultCapacitorEfficiency,
                DefaultCoolingSystem = c.DefaultCoolingSystem ?? DefaultCoolingSystem,
                DefaultCoolingCapacity = c.DefaultCoolingCapacity ?? DefaultCoolingCapacity,
                DefaultAmmunitionCount = c.DefaultAmmunitionCount ?? DefaultAmmunitionCount,
                IntegrityFailureThreshold = c.IntegrityFailureThreshold ?? IntegrityFailureThreshold,
                RangeReferenceBarrelLength = c.RangeReferenceBarrelLength ?? RangeReferenceBarrelLength,
                RangeMultiplierMin = c.RangeMultiplierMin ?? RangeMultiplierMin,
                RangeMultiplierMax = c.RangeMultiplierMax ?? RangeMultiplierMax,
                WearHeatCoolingCapacityMin = c.WearHeatCoolingCapacityMin ?? WearHeatCoolingCapacityMin,
                WearHeatFactorMin = c.WearHeatFactorMin ?? WearHeatFactorMin,
                WearPressureFactorMin = c.WearPressureFactorMin ?? WearPressureFactorMin,
                WearPerShotClampMin = c.WearPerShotClampMin ?? WearPerShotClampMin,
                WearPerShotClampMax = c.WearPerShotClampMax ?? WearPerShotClampMax,
                UpgradeWearModifierMin = c.UpgradeWearModifierMin ?? UpgradeWearModifierMin,
                WearModifiersByUpgradeId = c.WearModifiersByUpgradeId ?? WearModifiersByUpgradeId,
                MaxPressureByBarrelMaterial = c.MaxPressureByBarrelMaterial ?? MaxPressureByBarrelMaterial,
                SteelSafePropellantEnergyDensityCap = c.SteelSafePropellantEnergyDensityCap ?? SteelSafePropellantEnergyDensityCap,
                PropellantEnergyDensityCapMultiplierByBarrelMaterial = c.PropellantEnergyDensityCapMultiplierByBarrelMaterial ?? PropellantEnergyDensityCapMultiplierByBarrelMaterial,
                HeatGenerationCoefficientByPropulsion = c.HeatGenerationCoefficientByPropulsion ?? HeatGenerationCoefficientByPropulsion,
                HeatGenerationPowerCoefficientByPropulsion = c.HeatGenerationPowerCoefficientByPropulsion ?? HeatGenerationPowerCoefficientByPropulsion,
                ReloadBaseTimeSeconds = c.ReloadBaseTimeSeconds ?? ReloadBaseTimeSeconds,
                ReloadCoolingModifierByCoolingSystem = c.ReloadCoolingModifierByCoolingSystem ?? ReloadCoolingModifierByCoolingSystem,
                ReloadHeatRatioThreshold = c.ReloadHeatRatioThreshold ?? ReloadHeatRatioThreshold,
            };
        }
    }
}
