using Spacegun_Simulator.Ballistics;
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

        public static readonly ProjectileCore[] All =
        [
            // Light Core: 10kg - entry level
            new("light", "Light Core", "10kg tungsten dart - entry-level penetrator", 
                massKg: 10, requiredTechLevel: 1, 
                cost: new ResourceCost(budget: 50, steel: 30, exotic: 0)),

            // Standard Core: 15kg - balanced
            new("standard", "Standard Core", "15kg dense penetrator - reliable workhorse",
                massKg: 15, requiredTechLevel: 1,
                cost: new ResourceCost(budget: 100, steel: 80, exotic: 10)),

            // Heavy Core: 25kg - high mass
            new("heavy", "Heavy Core", "25kg armored slug - high mass",
                massKg: 25, requiredTechLevel: 2,
                cost: new ResourceCost(budget: 200, steel: 180, exotic: 30)),

            // Ultra-Dense Core: 50kg - endgame
            new("ultra", "Ultra-Dense Core", "50kg exotic alloy - devastating impact",
                massKg: 50, requiredTechLevel: 3,
                cost: new ResourceCost(budget: 400, steel: 300, exotic: 80))
        ];
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
        public static readonly PropulsionSystem None = new(
            "none", "No Propulsion", "Unpowered projectile - uses gun velocity only",
            deltaVCapacityMs: 0, burnDurationSeconds: 1, referenceMassKg: 10,
            requiredTechLevel: 1, cost: ResourceCost.None);

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
        public static readonly PropulsionSystem[] All =
        [
            None,

            // Solid Rocket Booster: Quick burn, moderate Delta-V
            // Good for close targets (full burn completes quickly)
            new("solid_rocket", "Solid Rocket Booster", "Quick-burn solid fuel - +20 km/s over 2 seconds",
                deltaVCapacityMs: 20_000, burnDurationSeconds: 2.0, referenceMassKg: 15,
                requiredTechLevel: 2,
                cost: new ResourceCost(budget: 80, steel: 40, exotic: 10)),

            // Liquid Fuel Sustainer: Slow burn, high Delta-V
            // Good for distant targets (more time to accumulate velocity)
            new("liquid_sustainer", "Liquid Fuel Sustainer", "Extended burn liquid fuel - +40 km/s over 8 seconds",
                deltaVCapacityMs: 40_000, burnDurationSeconds: 8.0, referenceMassKg: 20,
                requiredTechLevel: 2,
                cost: new ResourceCost(budget: 150, steel: 80, exotic: 30)),

            // Ion Drive: Very slow burn, massive Delta-V
            // Best for long-range engagements
            new("ion_drive", "Ion Thruster Array", "Continuous ion propulsion - +80 km/s over 20 seconds",
                deltaVCapacityMs: 80_000, burnDurationSeconds: 20.0, referenceMassKg: 10,
                requiredTechLevel: 3,
                cost: new ResourceCost(budget: 300, steel: 100, exotic: 80)),

            // Plasma Accelerator: Fast burn, extreme Delta-V
            // Endgame option for maximum impact velocity
            new("plasma_accel", "Plasma Accelerator", "High-energy plasma burst - +120 km/s over 3 seconds",
                deltaVCapacityMs: 120_000, burnDurationSeconds: 3.0, referenceMassKg: 25,
                requiredTechLevel: 3,
                cost: new ResourceCost(budget: 400, steel: 150, exotic: 100))
        ];
    }

    /// <summary>
    /// Optional enhancement modules for projectiles.
    /// </summary>
    public class ProjectileEnhancement
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public double HitToleranceBonus { get; }      // Multiplier to hit tolerance
        public double EnergyEfficiencyBonus { get; }  // Multiplier to effective KE
        public int RequiredTechLevel { get; }         // TechTree.Projectiles level required
        public ResourceCost Cost { get; }

        public ProjectileEnhancement(string id, string name, string description, 
            double hitToleranceBonus, double energyEfficiencyBonus, 
            int requiredTechLevel, ResourceCost cost)
        {
            Id = id;
            Name = name;
            Description = description;
            HitToleranceBonus = hitToleranceBonus;
            EnergyEfficiencyBonus = energyEfficiencyBonus;
            RequiredTechLevel = requiredTechLevel;
            Cost = cost;
        }

        public static readonly ProjectileEnhancement None = new(
            "none", "No Enhancement", "Standard projectile without modifications",
            hitToleranceBonus: 1.0, energyEfficiencyBonus: 1.0,
            requiredTechLevel: 1, cost: ResourceCost.None);

        public static readonly ProjectileEnhancement[] All =
        [
            None,

            new("guidance", "Guidance Package", "Terminal guidance for improved accuracy",
                hitToleranceBonus: 2.0, energyEfficiencyBonus: 1.0,
                requiredTechLevel: 3,
                cost: new ResourceCost(budget: 200, steel: 50, exotic: 50)),

            new("shaped", "Shaped Charge", "Focused energy on impact - 25% more effective damage",
                hitToleranceBonus: 1.0, energyEfficiencyBonus: 1.25,
                requiredTechLevel: 2,
                cost: new ResourceCost(budget: 150, steel: 80, exotic: 30)),

            new("armor_piercing", "Armor Piercing Tip", "Hardened tip for dense targets - 15% damage boost",
                hitToleranceBonus: 1.0, energyEfficiencyBonus: 1.15,
                requiredTechLevel: 1,
                cost: new ResourceCost(budget: 80, steel: 60, exotic: 10)),

            new("fragmentation", "Fragmentation Shell", "Larger hit tolerance, slight damage penalty",
                hitToleranceBonus: 1.75, energyEfficiencyBonus: 0.9,
                requiredTechLevel: 2,
                cost: new ResourceCost(budget: 120, steel: 70, exotic: 20))
        ];
    }

    /// <summary>
    /// A crafted projectile configuration combining components.
    /// Velocity is calculated from gun base velocity + propulsion Delta-V.
    /// </summary>
    public class CraftedProjectile
    {
        public ProjectileCore Core { get; }
        public PropulsionSystem Propulsion { get; }
        public ProjectileEnhancement Enhancement { get; }

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
            return BallisticsCalculator.CalculateKineticEnergyMJ(MassKg, impactVelocity) * Enhancement.EnergyEfficiencyBonus;
        }

        /// <summary>
        /// Effective kinetic energy after enhancement bonus (at max velocity).
        /// </summary>
        public double EffectiveKineticEnergyMJ => RawKineticEnergyMJ * Enhancement.EnergyEfficiencyBonus;

        /// <summary>
        /// Hit tolerance multiplier from enhancement.
        /// </summary>
        public double HitToleranceMultiplier => Enhancement.HitToleranceBonus;

        /// <summary>
        /// Total cost to build this projectile.
        /// </summary>
        public ResourceCost TotalCost => new(
            budget: Core.Cost.Budget + Propulsion.Cost.Budget + Enhancement.Cost.Budget,
            steel: Core.Cost.Steel + Propulsion.Cost.Steel + Enhancement.Cost.Steel,
            exotic: Core.Cost.ExoticMaterials + Propulsion.Cost.ExoticMaterials + Enhancement.Cost.ExoticMaterials
        );

        public string DisplayName
        {
            get
            {
                var parts = new List<string> { Core.Name };
                if (Propulsion.Id != "none")
                    parts.Add(Propulsion.Name);
                if (Enhancement.Id != "none")
                    parts.Add(Enhancement.Name);
                return string.Join(" + ", parts);
            }
        }

        public CraftedProjectile(ProjectileCore core, PropulsionSystem propulsion, ProjectileEnhancement? enhancement, double gunBaseMuzzleVelocityMs)
        {
            Core = core;
            Propulsion = propulsion ?? PropulsionSystem.None;
            Enhancement = enhancement ?? ProjectileEnhancement.None;
            GunBaseMuzzleVelocityMs = gunBaseMuzzleVelocityMs;
        }

        // Legacy constructor for backward compatibility (assumes 80 km/s base)
        public CraftedProjectile(ProjectileCore core, PropulsionSystem propulsion, ProjectileEnhancement? enhancement = null)
            : this(core, propulsion, enhancement, 80_000)
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
        /// Get all enhancements unlocked at the current tech level.
        /// </summary>
        public static List<ProjectileEnhancement> GetUnlockedEnhancements(TechTree techTree)
        {
            return ProjectileEnhancement.All
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