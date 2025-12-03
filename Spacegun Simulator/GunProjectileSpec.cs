namespace Spacegun_Simulator
{
    // ============================================================================
    // GUN & PROJECTILE SPECS
    // ============================================================================
    // Predefined combinations of guns and projectiles that player can select.
    // Each spec represents a viable strategy: fast-small, slow-heavy, balanced, etc.
    
    public class GunProjectileSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Projectile mass in kilograms.
        /// </summary>
        public double ProjectileMassKg { get; set; }

        /// <summary>
        /// Muzzle velocity this gun produces in m/s.
        /// </summary>
        public double MuzzleVelocityMs { get; set; }

        /// <summary>
        /// Resulting kinetic energy in megajoules.
        /// Pre-calculated for display: KE = 0.5 * mass * velocity²
        /// </summary>
        public double ResultingKE_MJ { get; set; }

        /// <summary>
        /// Cost to build this gun/projectile combination.
        /// </summary>
        public ResourceCost Cost { get; set; } = ResourceCost.None;

        public GunProjectileSpec(
            string id,
            string name,
            string description,
            double projectileMassKg,
            double muzzleVelocityMs,
            ResourceCost cost)
        {
            Id = id;
            Name = name;
            Description = description;
            ProjectileMassKg = projectileMassKg;
            MuzzleVelocityMs = muzzleVelocityMs;
            Cost = cost;
            
            // Pre-calculate KE using consolidated BallisticsCalculator
            ResultingKE_MJ = BallisticsCalculator.CalculateKineticEnergyMJ(projectileMassKg, muzzleVelocityMs);
        }

        /// <summary>
        /// All predefined gun/projectile combinations.
        /// These are tuned to be viable across all enemy archetypes.
        /// Velocity tiers: Early 75-90 km/s, Mid 150-200 km/s, End 300-400 km/s
        /// </summary>
        public static readonly GunProjectileSpec[] All =
        {
            // SPEC 1: Needle Strike - Early game, light and fast
            new(
                id: "needle",
                name: "Needle Strike",
                description: "10kg projectile @ 80,000 m/s. Early game fast strike, lower energy.",
                projectileMassKg: 10,
                muzzleVelocityMs: 80_000,
                cost: new ResourceCost(budget: 200, steel: 150, exotic: 50)
            ),

            // SPEC 2: Armor Piercer - Mid game, balanced standard
            new(
                id: "piercer",
                name: "Armor Piercer",
                description: "25kg projectile @ 160,000 m/s. Mid-game well-balanced approach.",
                projectileMassKg: 25,
                muzzleVelocityMs: 160_000,
                cost: new ResourceCost(budget: 300, steel: 250, exotic: 80)
            ),

            // SPEC 3: Heavy Slugger - Early game, brute force
            new(
                id: "slugger",
                name: "Heavy Slugger",
                description: "50kg projectile @ 75,000 m/s. Early game maximum impact force.",
                projectileMassKg: 50,
                muzzleVelocityMs: 75_000,
                cost: new ResourceCost(budget: 400, steel: 400, exotic: 100)
            ),

            // SPEC 4: Hypersonic Rail - Late game, ultra-high velocity
            new(
                id: "rail",
                name: "Hypersonic Rail",
                description: "15kg projectile @ 350,000 m/s. Late-game extreme speed delivery.",
                projectileMassKg: 15,
                muzzleVelocityMs: 350_000,
                cost: new ResourceCost(budget: 350, steel: 200, exotic: 120)
            ),

            // SPEC 5: Titan Breaker - Late game, maximum energy
            new(
                id: "titan",
                name: "Titan Breaker",
                description: "100kg projectile @ 320,000 m/s. Late-game maximum kinetic energy output.",
                projectileMassKg: 100,
                muzzleVelocityMs: 320_000,
                cost: new ResourceCost(budget: 500, steel: 600, exotic: 150)
            )
        };

        /// <summary>
        /// Get all specs that are affordable with given resources.
        /// </summary>
        public static List<GunProjectileSpec> GetAffordable(ResourceCost available)
        {
            var affordable = new List<GunProjectileSpec>();
            foreach (var spec in All)
            {
                if (CanAfford(available, spec.Cost))
                {
                    affordable.Add(spec);
                }
            }
            return affordable;
        }

        private static bool CanAfford(ResourceCost available, ResourceCost cost)
        {
            if (cost is null) return true;
            if (cost.Budget > available.Budget) return false;
            if (cost.Steel > available.Steel) return false;
            if (cost.ExoticMaterials > available.ExoticMaterials) return false;
            return true;
        }
    }
}