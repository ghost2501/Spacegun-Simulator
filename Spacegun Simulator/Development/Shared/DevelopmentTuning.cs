using Spacegun_Simulator.Development.Shared;

namespace Spacegun_Simulator.Core
{
    /// <summary>
    /// Config-backed tuning values for systems defined under Development/.
    /// Defaults define the baseline gameplay values.
    /// </summary>
    public static class DevelopmentTuning
    {
        public static TierTargetMaterialValues TierTargetMaterial { get; private set; } = TierTargetMaterialValues.CreateDefaults();
        public static TierVelocityValues TierVelocity { get; private set; } = TierVelocityValues.CreateDefaults();
        public static TechTreeTuningValues TechTree { get; private set; } = TechTreeTuningValues.CreateDefaults();
        public static ProjectileDefaultsValues ProjectileDefaults { get; private set; } = ProjectileDefaultsValues.CreateDefaults();
        public static EarthThreatValues EarthThreat { get; private set; } = EarthThreatValues.CreateDefaults();

        public static void Apply(DevelopmentTuningConfig cfg)
        {
            if (cfg.TierTargetMaterialTuning is not null)
                TierTargetMaterial = TierTargetMaterial.Apply(cfg.TierTargetMaterialTuning);

            if (cfg.TierVelocityTuning is not null)
                TierVelocity = TierVelocity.Apply(cfg.TierVelocityTuning);

            if (cfg.TechTreeTuning is not null)
                TechTree = TechTree.Apply(cfg.TechTreeTuning);

            if (cfg.ProjectileDefaults is not null)
                ProjectileDefaults = ProjectileDefaults.Apply(cfg.ProjectileDefaults);

            if (cfg.EarthThreatTuning is not null)
                EarthThreat = EarthThreat.Apply(cfg.EarthThreatTuning);
        }

        public sealed record EarthThreatValues(double EnemyEarthThreatCoupling, double EarthDestructionThresholdMJ)
        {
            public static EarthThreatValues CreateDefaults() => new(
                EnemyEarthThreatCoupling: 1.0,
                // Narrative default: any enemy impact ends the world.
                // Keeping this at 0.0 ensures the threat report always reads "Earth-cracking: YES"
                // across all tiers without affecting combat balance.
                EarthDestructionThresholdMJ: 0.0
            );

            public EarthThreatValues Apply(EarthThreatTuningConfig c) => this with
            {
                EnemyEarthThreatCoupling = c.EnemyEarthThreatCoupling ?? EnemyEarthThreatCoupling,
                EarthDestructionThresholdMJ = c.EarthDestructionThresholdMJ ?? EarthDestructionThresholdMJ,
            };
        }

        public sealed record TierVelocityValues(double[] TierEnemyMinVelocity, double[] TierEnemyMaxVelocity)
        {
            public static TierVelocityValues CreateDefaults() => new(
                // Defaults used for diagnostics/test-scenario sampling.
                // These values are intentionally config-overridable and are not required to match WaveTiers.
                TierEnemyMinVelocity: [25_000.0, 45_000.0, 80_000.0, 140_000.0, 220_000.0],
                TierEnemyMaxVelocity: [55_000.0, 90_000.0, 150_000.0, 230_000.0, 280_000.0]
            );

            public TierVelocityValues Apply(TierVelocityTuningConfig c) => this with
            {
                TierEnemyMinVelocity = c.TierEnemyMinVelocity ?? TierEnemyMinVelocity,
                TierEnemyMaxVelocity = c.TierEnemyMaxVelocity ?? TierEnemyMaxVelocity,
            };
        }

        public sealed record TierTargetMaterialValues(
            double[] TierEnemyMassTonsMin,
            double[] TierEnemyMassTonsMax,
            double[] TierEnemyDensityKgM3Min,
            double[] TierEnemyDensityKgM3Max,
            double[] TierEnemyBulkModulusGpaMin,
            double[] TierEnemyBulkModulusGpaMax,
            double FractureStrain)
        {
            public static TierTargetMaterialValues CreateDefaults() => new(
                // Defaults are chosen to preserve the existing fracture-energy (kill difficulty) curve
                // while moving to a denser/harder material regime.
                //
                // With the current derived model, fracture energy scales roughly as:
                //   E ~ (K * eps^2) * (m / rho)
                // so when rho and K increase, we scale mass roughly by (rho/K) to keep E comparable.
                TierEnemyMassTonsMin: [27_000.0, 75_000.0, 120_000.0, 200_000.0, 240_000.0],
                TierEnemyMassTonsMax: [110_000.0, 170_000.0, 250_000.0, 390_000.0, 430_000.0],

                // "Effective density" used for geometry scaling (sphere approximation).
                TierEnemyDensityKgM3Min: [5_000.0, 7_000.0, 9_000.0, 10_500.0, 15_500.0],
                TierEnemyDensityKgM3Max: [6_000.0, 8_000.0, 10_000.0, 15_000.0, 20_000.0],

                // Bulk modulus range drives the derived fracture energy model.
                // NOTE: tuned to make unmodded shots fall off earlier (Tier 1+),
                // while keeping Tier 0 reliably solvable.
                TierEnemyBulkModulusGpaMin: [200.0, 650.0, 1600.0, 1750.0, 1600.0],
                TierEnemyBulkModulusGpaMax: [220.0, 760.0, 1800.0, 2000.0, 1850.0],

                // Fracture strain used in E ~= 1/2 K eps^2 V.
                // Raised so KE gating remains meaningful at high muzzle velocities.
                FractureStrain: 0.02
            );

            public TierTargetMaterialValues Apply(TierTargetMaterialTuningConfig c) => this with
            {
                TierEnemyMassTonsMin = c.TierEnemyMassTonsMin ?? TierEnemyMassTonsMin,
                TierEnemyMassTonsMax = c.TierEnemyMassTonsMax ?? TierEnemyMassTonsMax,
                TierEnemyDensityKgM3Min = c.TierEnemyDensityKgM3Min ?? TierEnemyDensityKgM3Min,
                TierEnemyDensityKgM3Max = c.TierEnemyDensityKgM3Max ?? TierEnemyDensityKgM3Max,
                TierEnemyBulkModulusGpaMin = c.TierEnemyBulkModulusGpaMin ?? TierEnemyBulkModulusGpaMin,
                TierEnemyBulkModulusGpaMax = c.TierEnemyBulkModulusGpaMax ?? TierEnemyBulkModulusGpaMax,
                FractureStrain = c.FractureStrain ?? FractureStrain,
            };
        }

        public sealed record TechTreeTuningValues(
            ResourceCost ResearchCostToLevel2,
            ResourceCost ResearchCostToLevel3,
            double[] ProductionBonusByLevel)
        {
            public static TechTreeTuningValues CreateDefaults() => new(
                ResearchCostToLevel2: new ResourceCost(budget: 350, steel: 250, exotic: 0),
                ResearchCostToLevel3: new ResourceCost(budget: 450, steel: 350, exotic: 0),
                ProductionBonusByLevel: [1.0, 1.2, 1.8]
            );

            public TechTreeTuningValues Apply(TechTreeTuningConfig c) => this with
            {
                ResearchCostToLevel2 = c.ResearchCostToLevel2?.ToResourceCost() ?? ResearchCostToLevel2,
                ResearchCostToLevel3 = c.ResearchCostToLevel3?.ToResourceCost() ?? ResearchCostToLevel3,
                ProductionBonusByLevel = c.ProductionBonusByLevel ?? ProductionBonusByLevel,
            };
        }

        public sealed record ProjectileDefaultsValues(
            double Mass,
            double Length,
            bool HasGuidance,
            double GuidanceAccuracy,
            double ImpactCoupling,
            double ImpactCouplingReferenceMassKg,
            double ImpactCouplingMassExponent,
            double ImpactCouplingTechMultiplierPerWeaponsLevel)
        {
            public static ProjectileDefaultsValues CreateDefaults() => new(
                Mass: 5000.0,
                Length: 0.5,
                HasGuidance: false,
                GuidanceAccuracy: 0.0,
                // Keep the established tier curve by reducing coupled (effective) energy
                // so a 5t slug behaves similarly to the prior small-projectile baseline at the same velocity.
                ImpactCoupling: 0.002,
                // Mass-scaling coupling keeps KE meaningful when projectile mass increases.
                ImpactCouplingReferenceMassKg: 5000.0,
                ImpactCouplingMassExponent: 1.0,
                ImpactCouplingTechMultiplierPerWeaponsLevel: 1.0
            );

            public ProjectileDefaultsValues Apply(ProjectileDefaultsConfig c) => this with
            {
                Mass = c.Mass ?? Mass,
                Length = c.Length ?? Length,
                HasGuidance = c.HasGuidance ?? HasGuidance,
                GuidanceAccuracy = c.GuidanceAccuracy ?? GuidanceAccuracy,
                ImpactCoupling = c.ImpactCoupling ?? ImpactCoupling,
                ImpactCouplingReferenceMassKg = c.ImpactCouplingReferenceMassKg ?? ImpactCouplingReferenceMassKg,
                ImpactCouplingMassExponent = c.ImpactCouplingMassExponent ?? ImpactCouplingMassExponent,
                ImpactCouplingTechMultiplierPerWeaponsLevel = c.ImpactCouplingTechMultiplierPerWeaponsLevel ?? ImpactCouplingTechMultiplierPerWeaponsLevel,
            };
        }

        public readonly record struct Range(double Min, double Max);
    }
}
