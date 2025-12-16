namespace Spacegun_Simulator
{
    // ============================================================================
    // GUN & PROJECTILE SPECS (legacy presets + tutorial)
    // ============================================================================
    // This file preserves the tutorial Potato Cannon and restores a small set of
    // legacy predefined specs so existing code that indexes into `All[...]`
    // continues to compile. New code should prefer `CreateDefaultForTier(...)`
    // or the crafted projectile system (ProjectileComponents/CraftedProjectile).
    // ============================================================================
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
        /// Legacy/static specs. Indexing order kept for backward compatibility:
        /// 0 = potato (tutorial), 1 = needle, 2 = piercer, 3 = slugger, 4 = rail, 5 = titan
        /// New systems should NOT add more entries here.
        /// </summary>
        public static readonly GunProjectileSpec[] All = new[]
        {
            // SPEC 0: Potato Cannon - Tutorial mode only
            new GunProjectileSpec(
                id: "potato",
                name: "Potato Cannon",
                description: "0.3kg potato @ 50 m/s. Tutorial mode - learn the basics!",
                projectileMassKg: DifficultyConfig.TutorialPotatoCannon.ProjectileMassKg,
                muzzleVelocityMs: DifficultyConfig.TutorialPotatoCannon.MuzzleVelocityMs,
                cost: ResourceCost.None
            ),

            // SPEC 1: Needle Strike - Early game, light and fast (legacy preset)
            new GunProjectileSpec(
                id: "needle",
                name: "Needle Strike",
                description: "10kg projectile @ tier1 base velocity.",
                projectileMassKg: 10.0,
                muzzleVelocityMs: GameConstants.WeaponsTechBaseVelocity.Length > 0 ? GameConstants.WeaponsTechBaseVelocity[0] : 80_000,
                cost: new ResourceCost(budget: 200, steel: 150, exotic: 50)
            ),

            // SPEC 2: Armor Piercer - Mid game, balanced (legacy preset)
            new GunProjectileSpec(
                id: "piercer",
                name: "Armor Piercer",
                description: "25kg projectile @ tier2 base velocity.",
                projectileMassKg: 25.0,
                muzzleVelocityMs: GameConstants.WeaponsTechBaseVelocity.Length > 1 ? GameConstants.WeaponsTechBaseVelocity[1] : 160_000,
                cost: new ResourceCost(budget: 300, steel: 250, exotic: 80)
            ),

            // SPEC 3: Heavy Slugger - Legacy heavy preset
            new GunProjectileSpec(
                id: "slugger",
                name: "Heavy Slugger",
                description: "50kg projectile @ 75,000 m/s (legacy heavy preset).",
                projectileMassKg: 50.0,
                muzzleVelocityMs: 75_000,
                cost: new ResourceCost(budget: 400, steel: 400, exotic: 100)
            ),

            // SPEC 4: Hypersonic Rail - Late game, ultra-high velocity (legacy preset)
            new GunProjectileSpec(
                id: "rail",
                name: "Hypersonic Rail",
                description: "15kg projectile @ tier3 base velocity.",
                projectileMassKg: 15.0,
                muzzleVelocityMs: GameConstants.WeaponsTechBaseVelocity.Length > 2 ? GameConstants.WeaponsTechBaseVelocity[2] : 350_000,
                cost: new ResourceCost(budget: 350, steel: 200, exotic: 120)
            ),

            // SPEC 5: Titan Breaker - Legacy heavy-end preset
            new GunProjectileSpec(
                id: "titan",
                name: "Titan Breaker",
                description: "100kg projectile @ 320,000 m/s (legacy heavy-end preset).",
                projectileMassKg: 100.0,
                muzzleVelocityMs: 320_000,
                cost: new ResourceCost(budget: 500, steel: 600, exotic: 150)
            )
        };

        /// <summary>
        /// Accessor to the tutorial potato cannon.
        /// </summary>
        public static GunProjectileSpec PotatoCannon => All[0];

        /// <summary>
        /// Create a sensible default GunProjectileSpec for a given tier.
        /// Prefer this for new code instead of indexing `All`.
        /// </summary>
        public static GunProjectileSpec CreateDefaultForTier(int tierIndex)
        {
            // Prefer mapping to the legacy presets where it makes sense so migration is safe.
            return tierIndex switch
            {
                <= 0 => All[1], // Needle Strike as light starter
                1 => All[2],    // Armor Piercer as mid starter
                _ => All[4],    // Hypersonic Rail for late tiers
            };
        }

        /// <summary>
        /// Get all specs that are affordable with given resources.
        /// Returns the static legacy list (mostly tutorial + legacy presets).
        /// Crafted projectiles should be evaluated separately.
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