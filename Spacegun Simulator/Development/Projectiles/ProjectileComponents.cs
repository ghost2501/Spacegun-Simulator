using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Core;
using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Development.Technology;

namespace Spacegun_Simulator.Development.Projectiles
{
    // ============================================================================
    // PROJECTILE COMPONENT SYSTEM
    // ============================================================================
    // Players craft projectiles by combining components:
    //   1. Core (determines base mass) - Available at Projectiles Tech 1
    //   2. Propulsion (provides Delta-V boost) - Unlocked at Projectiles Tech 2
    //   3. Enhancement (optional bonuses) - Various tech requirements
    //
    // VELOCITY MODEL:
    //   - Gun provides BASE muzzle velocity (from Weapons Tech)
    //   - Propulsion provides DELTA-V that accumulates during flight
    //   - Final impact velocity = Base + EffectiveDeltaV
    //   - EffectiveDeltaV = min(flightTime × burnRate, maxDeltaV) × massEfficiency
    //
    // KE = 0.5 × mass × (baseVelocity + effectiveDeltaV)²

    /// <summary>
    /// Projectile core component - determines projectile mass.
    /// Available at Projectiles Tech Level 1.
    /// </summary>
    public class ProjectileCore
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public double MassKg { get; }
        public int RequiredTechLevel { get; }  // TechTree.Projectiles level required
        public ResourceCost Cost { get; }

        public ProjectileCore(string id, string name, string description, double massKg, int requiredTechLevel, ResourceCost cost)
        {
            Id = id;
            Name = name;
            Description = description;
            MassKg = massKg;
            RequiredTechLevel = requiredTechLevel;
            Cost = cost;
        }

        public static ProjectileCore[] All => ProjectilesCatalog.Cores;
    }

    /// <summary>
    /// Propulsion system component - provides Delta-V boost during flight.
    /// UNLOCKED AT PROJECTILES TECH LEVEL 2.
    /// 
    /// Delta-V is applied proportionally to flight time:
    ///   - burnRate = DeltaVCapacity / BurnDurationSeconds
    ///   - effectiveDeltaV = min(flightTime × burnRate, DeltaVCapacity)
    ///   - massEfficiency = ReferenceMass / (ReferenceMass + coreMass)
    ///   - finalDeltaV = effectiveDeltaV × massEfficiency
    /// </summary>
    public class PropulsionSystem
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        
        /// <summary>
        /// Maximum Delta-V this propulsion system can provide (m/s).
        /// This is the total velocity change if the full burn completes.
        /// </summary>
        public double DeltaVCapacityMs { get; }
        
        /// <summary>
        /// How long the propulsion system burns (seconds).
        /// Longer burn = more gradual acceleration over distance.
        /// </summary>
        public double BurnDurationSeconds { get; }
        
        /// <summary>
        /// Reference mass for efficiency calculation (kg).
        /// Heavier projectiles get less effective Delta-V.
        /// </summary>
        public double ReferenceMassKg { get; }
        
        public int RequiredTechLevel { get; }  // TechTree.Projectiles level required (minimum 2)
        public ResourceCost Cost { get; }

        /// <summary>
        /// Burn rate in m/s per second of flight.
        /// </summary>
        public double BurnRateMsPerSecond => DeltaVCapacityMs / BurnDurationSeconds;

        public PropulsionSystem(string id, string name, string description, 
            double deltaVCapacityMs, double burnDurationSeconds, double referenceMassKg,
            int requiredTechLevel, ResourceCost cost)
        {
            Id = id;
            Name = name;
            Description = description;
            DeltaVCapacityMs = deltaVCapacityMs;
            BurnDurationSeconds = burnDurationSeconds;
            ReferenceMassKg = referenceMassKg;
            RequiredTechLevel = requiredTechLevel;
            Cost = cost;
        }

        /// <summary>
        /// No propulsion - projectile uses gun velocity only.
        /// </summary>
        public static PropulsionSystem None => ProjectilesCatalog.PropulsionNone;

        /// <summary>
        /// Calculate effective Delta-V for a given projectile mass and flight time.
        /// </summary>
        public double CalculateEffectiveDeltaV(double projectileMassKg, double flightTimeSeconds)
        {
            if (DeltaVCapacityMs <= 0) return 0;

            // Calculate burn-limited Delta-V
            double burnLimitedDeltaV = Math.Min(flightTimeSeconds * BurnRateMsPerSecond, DeltaVCapacityMs);

            // Apply mass efficiency (heavier = less efficient)
            double massEfficiency = ReferenceMassKg / (ReferenceMassKg + projectileMassKg);

            return burnLimitedDeltaV * massEfficiency;
        }

        // All propulsion systems - None is always available, others require Tech 2+
        public static PropulsionSystem[] All => ProjectilesCatalog.PropulsionSystems;
    }

    /// <summary>
    /// Optional enhancement modules for projectiles.
    /// </summary>
    public enum ProjectileEnhancementSlot
    {
        Guidance,
        Payload,
        Armor,
    }

    public class ProjectileEnhancement
    {
        public string Id { get; }
        public ProjectileEnhancementSlot Slot { get; }
        public string Name { get; }
        public string Description { get; }
        public double HitToleranceBonus { get; }      // Multiplier to hit tolerance
        public double Penetration { get; }           // Multiplier to penetration (higher => less energy required)
        public double ImpactCoupling { get; }        // Multiplier to effective energy delivered on impact
        public double DefenseBonus { get; }           // 0..1 additive defense rating
        public int RequiredTechLevel { get; }         // TechTree.Projectiles level required
        public ResourceCost Cost { get; }

        public ProjectileEnhancement(string id, string name, string description, 
            ProjectileEnhancementSlot slot,
            double hitToleranceBonus, double penetration, double impactCoupling,
            double defenseBonus,
            int requiredTechLevel, ResourceCost cost)
        {
            Id = id;
            Slot = slot;
            Name = name;
            Description = description;
            HitToleranceBonus = hitToleranceBonus;
            Penetration = penetration;
            ImpactCoupling = impactCoupling;
            DefenseBonus = defenseBonus;
            RequiredTechLevel = requiredTechLevel;
            Cost = cost;
        }

        public bool IsNone => Id.StartsWith("none_", StringComparison.OrdinalIgnoreCase);

        public static ProjectileEnhancement[] All => ProjectilesCatalog.Enhancements;
    }

    /// <summary>
    /// A crafted projectile configuration combining components.
    /// Velocity is calculated from gun base velocity + propulsion Delta-V.
    /// </summary>
    public class CraftedProjectile
    {
        public ProjectileCore Core { get; }
        public PropulsionSystem Propulsion { get; }
        public ProjectileEnhancement GuidanceModule { get; }
        public ProjectileEnhancement PayloadModule { get; }
        public ProjectileEnhancement ArmorModule { get; }

        public IEnumerable<ProjectileEnhancement> Modules
        {
            get
            {
                yield return GuidanceModule;
                yield return PayloadModule;
                yield return ArmorModule;
            }
        }

        /// <summary>
        /// The gun's base muzzle velocity (set when projectile is crafted).
        /// </summary>
        public double GunBaseMuzzleVelocityMs { get; }

        public double MassKg => Core.MassKg;

        /// <summary>
        /// Maximum possible velocity (gun base + full propulsion Delta-V at reference mass).
        /// Used for display purposes. Actual impact velocity depends on flight time.
        /// </summary>
        public double MaxVelocityMs => GunBaseMuzzleVelocityMs + Propulsion.DeltaVCapacityMs;

        /// <summary>
        /// Calculate the actual impact velocity for a given flight time.
        /// This accounts for propulsion burn duration and mass efficiency.
        /// </summary>
        public double CalculateImpactVelocity(double flightTimeSeconds)
        {
            double effectiveDeltaV = Propulsion.CalculateEffectiveDeltaV(MassKg, flightTimeSeconds);
            return GunBaseMuzzleVelocityMs + effectiveDeltaV;
        }

        /// <summary>
        /// Raw kinetic energy in megajoules at maximum velocity.
        /// For display purposes - actual KE depends on flight time.
        /// </summary>
        public double RawKineticEnergyMJ => BallisticsCalculator.CalculateKineticEnergyMJ(MassKg, MaxVelocityMs);

        /// <summary>
        /// Calculate actual kinetic energy for a given flight time.
        /// </summary>
        public double CalculateKineticEnergyMJ(double flightTimeSeconds)
        {
            double impactVelocity = CalculateImpactVelocity(flightTimeSeconds);
            return BallisticsCalculator.CalculateKineticEnergyMJ(MassKg, impactVelocity);
        }

        /// <summary>
        /// Hit tolerance multiplier from modules.
        /// </summary>
        public double HitToleranceMultiplier => GuidanceModule.HitToleranceBonus * PayloadModule.HitToleranceBonus * ArmorModule.HitToleranceBonus;

        public double PenetrationMultiplier => GuidanceModule.Penetration * PayloadModule.Penetration * ArmorModule.Penetration;

        public double ImpactCouplingMultiplier => GuidanceModule.ImpactCoupling * PayloadModule.ImpactCoupling * ArmorModule.ImpactCoupling;

        public bool HasGuidance => GuidanceModule.Id is "guidance" or "bird_brain";

        /// <summary>
        /// Projectile defensive rating (0.0..1.0) used against enemy Offense.
        /// </summary>
        public double DefenseRating => Math.Clamp(GuidanceModule.DefenseBonus + PayloadModule.DefenseBonus + ArmorModule.DefenseBonus, 0.0, 1.0);

        /// <summary>
        /// Total cost to build this projectile.
        /// </summary>
        public ResourceCost TotalCost => new(
            budget: Core.Cost.Budget + Propulsion.Cost.Budget + GuidanceModule.Cost.Budget + PayloadModule.Cost.Budget + ArmorModule.Cost.Budget,
            steel: Core.Cost.Steel + Propulsion.Cost.Steel + GuidanceModule.Cost.Steel + PayloadModule.Cost.Steel + ArmorModule.Cost.Steel,
            exotic: Core.Cost.ExoticMaterials + Propulsion.Cost.ExoticMaterials + GuidanceModule.Cost.ExoticMaterials + PayloadModule.Cost.ExoticMaterials + ArmorModule.Cost.ExoticMaterials
        );

        public string DisplayName
        {
            get
            {
                var parts = new List<string> { Core.Name };
                if (Propulsion.Id != "none")
                    parts.Add(Propulsion.Name);

                foreach (var module in Modules)
                {
                    if (!module.IsNone)
                        parts.Add(module.Name);
                }

                return string.Join(" + ", parts);
            }
        }

        public CraftedProjectile(
            ProjectileCore core,
            PropulsionSystem propulsion,
            ProjectileEnhancement? guidanceModule,
            ProjectileEnhancement? payloadModule,
            ProjectileEnhancement? armorModule,
            double gunBaseMuzzleVelocityMs)
        {
            Core = core;
            Propulsion = propulsion ?? PropulsionSystem.None;
            GuidanceModule = guidanceModule ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Guidance);
            PayloadModule = payloadModule ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Payload);
            ArmorModule = armorModule ?? ProjectilesCatalog.GetNoneModule(ProjectileEnhancementSlot.Armor);
            GunBaseMuzzleVelocityMs = gunBaseMuzzleVelocityMs;
        }

        // Convenience constructor (assumes 80 km/s base).
        public CraftedProjectile(
            ProjectileCore core,
            PropulsionSystem propulsion,
            ProjectileEnhancement? guidanceModule = null,
            ProjectileEnhancement? payloadModule = null,
            ProjectileEnhancement? armorModule = null)
            : this(core, propulsion, guidanceModule, payloadModule, armorModule, 80_000)
        {
        }

        /// <summary>
        /// Check if a component is unlocked based on tech tree.
        /// </summary>
        public static bool IsComponentUnlocked(int requiredLevel, TechTree techTree, TechTree.TechType techType)
        {
            return techTree.CurrentLevel.TryGetValue(techType, out int currentLevel) && currentLevel >= requiredLevel;
        }

        /// <summary>
        /// Get all cores unlocked at the current tech level.
        /// </summary>
        public static List<ProjectileCore> GetUnlockedCores(TechTree techTree)
        {
            return ProjectileCore.All
                .Where(c => IsComponentUnlocked(c.RequiredTechLevel, techTree, TechTree.TechType.Projectiles))
                .ToList();
        }

        /// <summary>
        /// Get all propulsion systems unlocked at the current tech level.
        /// Propulsion requires Projectiles Tech (not Weapons Tech).
        /// </summary>
        public static List<PropulsionSystem> GetUnlockedPropulsion(TechTree techTree)
        {
            return PropulsionSystem.All
                .Where(p => IsComponentUnlocked(p.RequiredTechLevel, techTree, TechTree.TechType.Projectiles))
                .ToList();
        }

        /// <summary>
        /// Get all modules (for the given slot) unlocked at the current tech level.
        /// </summary>
        public static List<ProjectileEnhancement> GetUnlockedModules(TechTree techTree, ProjectileEnhancementSlot slot)
        {
            return ProjectileEnhancement.All
            .Where(e => e.Slot == slot)
            .Where(e => IsComponentUnlocked(e.RequiredTechLevel, techTree, TechTree.TechType.Projectiles))
                .ToList();
        }

        /// <summary>
        /// Check if player can afford this projectile.
        /// </summary>
        public static bool CanAfford(CraftedProjectile projectile, Dictionary<string, double> accumulatedResources)
        {
            var cost = projectile.TotalCost;
            return accumulatedResources.GetValueOrDefault("Budget", 0) >= cost.Budget &&
                   accumulatedResources.GetValueOrDefault("Steel", 0) >= cost.Steel &&
                   accumulatedResources.GetValueOrDefault("Exotic", 0) >= cost.ExoticMaterials;
        }
    }
}