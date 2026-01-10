using System.Text.Json.Serialization;
using Range = Spacegun_Simulator.Core.DevelopmentTuning.Range;

namespace Spacegun_Simulator.Core
{
    public sealed class DevelopmentTuningConfig
    {
        public int Version { get; set; } = 1;
        public TierTargetMaterialTuningConfig? TierTargetMaterialTuning { get; set; }
        public TierVelocityTuningConfig? TierVelocityTuning { get; set; }
        public TechTreeTuningConfig? TechTreeTuning { get; set; }
        public ProjectileDefaultsConfig? ProjectileDefaults { get; set; }
        public EarthThreatTuningConfig? EarthThreatTuning { get; set; }
    }

    public sealed class EarthThreatTuningConfig
    {
        /// <summary>
        /// Multiplier applied to computed enemy kinetic energy at Earth impact.
        /// Intended as a narrative/uncertainty knob that does not affect combat balance.
        /// </summary>
        public double? EnemyEarthThreatCoupling { get; set; }

        /// <summary>
        /// Threshold in megajoules (MJ) for "Earth destruction" / "Earth cracking" narrative checks.
        /// </summary>
        public double? EarthDestructionThresholdMJ { get; set; }
    }

    public sealed class TierTargetMaterialTuningConfig
    {
        /// <summary>
        /// Length should equal TierCount.
        /// Units: metric tons.
        /// </summary>
        public double[]? TierEnemyMassTonsMin { get; set; }

        /// <summary>
        /// Length should equal TierCount.
        /// Units: metric tons.
        /// </summary>
        public double[]? TierEnemyMassTonsMax { get; set; }

        /// <summary>
        /// Length should equal TierCount.
        /// Units: kg/m^3.
        /// </summary>
        public double[]? TierEnemyDensityKgM3Min { get; set; }

        /// <summary>
        /// Length should equal TierCount.
        /// Units: kg/m^3.
        /// </summary>
        public double[]? TierEnemyDensityKgM3Max { get; set; }

        /// <summary>
        /// Length should equal TierCount.
        /// Units: GPa.
        /// </summary>
        public double[]? TierEnemyBulkModulusGpaMin { get; set; }

        /// <summary>
        /// Length should equal TierCount.
        /// Units: GPa.
        /// </summary>
        public double[]? TierEnemyBulkModulusGpaMax { get; set; }

        /// <summary>
        /// Scalar fracture strain used by the derived fracture energy model.
        /// Typical values: 0.002..0.02.
        /// </summary>
        public double? FractureStrain { get; set; }
    }

    public sealed class TierVelocityTuningConfig
    {
        public double[]? TierEnemyMinVelocity { get; set; }
        public double[]? TierEnemyMaxVelocity { get; set; }
    }

    public sealed class TechTreeTuningConfig
    {
        public ResourceCostConfig? ResearchCostToLevel2 { get; set; }
        public ResourceCostConfig? ResearchCostToLevel3 { get; set; }

        /// <summary>
        /// Length should be 3 and correspond to levels 1..3.
        /// </summary>
        public double[]? ProductionBonusByLevel { get; set; }
    }

    public sealed class ProjectileDefaultsConfig
    {
        public double? Mass { get; set; }
        public double? Length { get; set; }
        public bool? HasGuidance { get; set; }
        public double? GuidanceAccuracy { get; set; }
        public double? ImpactCoupling { get; set; }
        public double? ImpactCouplingReferenceMassKg { get; set; }
        public double? ImpactCouplingMassExponent { get; set; }
        public double? ImpactCouplingTechMultiplierPerWeaponsLevel { get; set; }
    }

    public sealed class ResourceCostConfig
    {
        public int Budget { get; set; }
        public int Steel { get; set; }
        public int Exotic { get; set; }

        [JsonConstructor]
        public ResourceCostConfig(int budget, int steel, int exotic)
        {
            Budget = budget;
            Steel = steel;
            Exotic = exotic;
        }

        public Spacegun_Simulator.Development.Shared.ResourceCost ToResourceCost() => new(Budget, Steel, Exotic);
    }
}
