using Spacegun_Simulator.Development.Projectiles;

namespace Spacegun_Simulator.Core
{
    /// <summary>
    /// Config-backed catalog of projectile technology items (cores/propulsion/enhancements).
    /// Defaults define baseline projectile tech items.
    /// </summary>
    public static class ProjectilesCatalog
    {
        public static ProjectileCore[] Cores { get; private set; } = CreateDefaultCores();
        public static PropulsionSystem[] PropulsionSystems { get; private set; } = CreateDefaultPropulsion();
        public static ProjectileEnhancement[] Enhancements { get; private set; } = CreateDefaultEnhancements();

        public static PropulsionSystem PropulsionNone { get; private set; } = CreateDefaultPropulsion()[0];
        public static ProjectileEnhancement GuidanceNone { get; private set; } = CreateDefaultEnhancements()[0];
        public static ProjectileEnhancement PayloadNone { get; private set; } = CreateDefaultEnhancements()[1];
        public static ProjectileEnhancement ArmorNone { get; private set; } = CreateDefaultEnhancements()[2];

        public static void Apply(ProjectilesCatalogConfig cfg)
        {
            if (cfg.Cores is { Length: > 0 })
                Cores = cfg.Cores;

            if (cfg.PropulsionSystems is { Length: > 0 })
                PropulsionSystems = cfg.PropulsionSystems;

            if (cfg.Enhancements is { Length: > 0 })
                Enhancements = cfg.Enhancements;

            // Ensure "none" entries are always available for UX + safety.
            PropulsionNone = FindPropulsionNone(PropulsionSystems) ?? CreateDefaultPropulsion()[0];
            GuidanceNone = FindNoneForSlot(Enhancements, ProjectileEnhancementSlot.Guidance) ?? CreateDefaultEnhancements()[0];
            PayloadNone = FindNoneForSlot(Enhancements, ProjectileEnhancementSlot.Payload) ?? CreateDefaultEnhancements()[1];
            ArmorNone = FindNoneForSlot(Enhancements, ProjectileEnhancementSlot.Armor) ?? CreateDefaultEnhancements()[2];

            // If config omitted the none item, prepend it.
            if (FindPropulsionNone(PropulsionSystems) is null)
            {
                var withNone = new PropulsionSystem[PropulsionSystems.Length + 1];
                withNone[0] = PropulsionNone;
                Array.Copy(PropulsionSystems, 0, withNone, 1, PropulsionSystems.Length);
                PropulsionSystems = withNone;
            }

            EnsureNoneForSlot(ProjectileEnhancementSlot.Guidance, GuidanceNone);
            EnsureNoneForSlot(ProjectileEnhancementSlot.Payload, PayloadNone);
            EnsureNoneForSlot(ProjectileEnhancementSlot.Armor, ArmorNone);
        }

        public static ProjectileEnhancement GetNoneModule(ProjectileEnhancementSlot slot) => slot switch
        {
            ProjectileEnhancementSlot.Guidance => GuidanceNone,
            ProjectileEnhancementSlot.Payload => PayloadNone,
            ProjectileEnhancementSlot.Armor => ArmorNone,
            _ => PayloadNone,
        };

        public static IReadOnlyList<ProjectileEnhancement> GetModulesForSlot(ProjectileEnhancementSlot slot)
        {
            return Enhancements
                .Where(e => e is not null && e.Slot == slot)
                .OrderByDescending(e => e.IsNone)
                .ThenBy(e => e.RequiredTechLevel)
                .ThenBy(e => e.Name)
                .ToList();
        }

        public static bool TryGetEnhancementById(string? id, out ProjectileEnhancement enhancement)
        {
            enhancement = null!;

            if (string.IsNullOrWhiteSpace(id))
                return false;

            foreach (var e in Enhancements)
            {
                if (e is null)
                    continue;

                if (string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    enhancement = e;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetCoreById(string? id, out ProjectileCore core)
        {
            core = null!;

            if (string.IsNullOrWhiteSpace(id))
                return false;

            foreach (var c in Cores)
            {
                if (c is null)
                    continue;

                if (string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    core = c;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetPropulsionById(string? id, out PropulsionSystem propulsion)
        {
            propulsion = null!;

            if (string.IsNullOrWhiteSpace(id))
                return false;

            foreach (var p in PropulsionSystems)
            {
                if (p is null)
                    continue;

                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    propulsion = p;
                    return true;
                }
            }

            return false;
        }

        private static PropulsionSystem? FindPropulsionNone(PropulsionSystem[] list)
        {
            foreach (var p in list)
            {
                if (p is not null
                    && string.Equals(p.Id, "none", StringComparison.OrdinalIgnoreCase)
                    && p.RequiredTechLevel <= 1)
                    return p;
            }
            return null;
        }

        private static ProjectileEnhancement? FindNoneForSlot(ProjectileEnhancement[] list, ProjectileEnhancementSlot slot)
        {
            foreach (var e in list)
            {
                if (e is not null
                    && e.Slot == slot
                    && e.IsNone
                    && e.RequiredTechLevel <= 1)
                    return e;
            }
            return null;
        }

        private static void EnsureNoneForSlot(ProjectileEnhancementSlot slot, ProjectileEnhancement fallbackNone)
        {
            if (FindNoneForSlot(Enhancements, slot) is not null)
                return;

            var withNone = new ProjectileEnhancement[Enhancements.Length + 1];
            withNone[0] = fallbackNone;
            Array.Copy(Enhancements, 0, withNone, 1, Enhancements.Length);
            Enhancements = withNone;
        }

        private static ProjectileCore[] CreateDefaultCores() =>
        [
            new ProjectileCore(
                id: "light",
                name: "Light Core",
                description: "5t penetrator slug - baseline core",
                massKg: 5_000,
                requiredTechLevel: 1,
                cost: new Development.Shared.ResourceCost(budget: 50, steel: 30, exotic: 0)
            ),
            new ProjectileCore(
                id: "standard",
                name: "Standard Core",
                description: "6.5t dense slug - reliable workhorse",
                massKg: 6_500,
                requiredTechLevel: 1,
                cost: new Development.Shared.ResourceCost(budget: 100, steel: 80, exotic: 10)
            ),
            new ProjectileCore(
                id: "heavy",
                name: "Heavy Core",
                description: "8t armored slug - high mass",
                massKg: 8_000,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 200, steel: 180, exotic: 30)
            ),
            new ProjectileCore(
                id: "ultra",
                name: "Ultra-Dense Core",
                description: "10t exotic alloy - maximum mass core",
                massKg: 10_000,
                requiredTechLevel: 3,
                cost: new Development.Shared.ResourceCost(budget: 400, steel: 300, exotic: 80)
            ),
        ];

        private static PropulsionSystem[] CreateDefaultPropulsion() =>
        [
            new PropulsionSystem(
                id: "none",
                name: "No Propulsion",
                description: "Unpowered projectile - uses gun velocity only",
                deltaVCapacityMs: 0,
                burnDurationSeconds: 1,
                referenceMassKg: 5000,
                requiredTechLevel: 1,
                cost: Development.Shared.ResourceCost.None
            ),
            new PropulsionSystem(
                id: "solid_rocket",
                name: "Solid Rocket Booster",
                description: "Quick-burn solid fuel - +20 km/s over 2 seconds",
                deltaVCapacityMs: 20_000,
                burnDurationSeconds: 2.0,
                referenceMassKg: 5_000,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 80, steel: 40, exotic: 10)
            ),
            new PropulsionSystem(
                id: "liquid_sustainer",
                name: "Liquid Fuel Sustainer",
                description: "Extended burn liquid fuel - +40 km/s over 8 seconds",
                deltaVCapacityMs: 40_000,
                burnDurationSeconds: 8.0,
                referenceMassKg: 5_000,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 150, steel: 80, exotic: 30)
            ),
            new PropulsionSystem(
                id: "ion_drive",
                name: "Ion Thruster Array",
                description: "Continuous ion propulsion - +80 km/s over 20 seconds",
                deltaVCapacityMs: 80_000,
                burnDurationSeconds: 20.0,
                referenceMassKg: 5_000,
                requiredTechLevel: 3,
                cost: new Development.Shared.ResourceCost(budget: 300, steel: 100, exotic: 80)
            ),
            new PropulsionSystem(
                id: "plasma_accel",
                name: "Plasma Accelerator",
                description: "High-energy plasma burst - +120 km/s over 3 seconds",
                deltaVCapacityMs: 120_000,
                burnDurationSeconds: 3.0,
                referenceMassKg: 5_000,
                requiredTechLevel: 3,
                cost: new Development.Shared.ResourceCost(budget: 400, steel: 150, exotic: 100)
            ),
        ];

        private static ProjectileEnhancement[] CreateDefaultEnhancements() =>
        [
            new ProjectileEnhancement(
                id: "none_guidance",
                name: "No Guidance",
                description: "No guidance module installed",
                slot: ProjectileEnhancementSlot.Guidance,
                hitToleranceBonus: 1.0,
                penetration: 1.0,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 1,
                cost: Development.Shared.ResourceCost.None
            ),
            new ProjectileEnhancement(
                id: "none_payload",
                name: "No Payload Mod",
                description: "No payload modifier installed",
                slot: ProjectileEnhancementSlot.Payload,
                hitToleranceBonus: 1.0,
                penetration: 1.0,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 1,
                cost: Development.Shared.ResourceCost.None
            ),
            new ProjectileEnhancement(
                id: "none_armor",
                name: "No Armor",
                description: "No armor/survivability module installed",
                slot: ProjectileEnhancementSlot.Armor,
                hitToleranceBonus: 1.0,
                penetration: 1.0,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 1,
                cost: Development.Shared.ResourceCost.None
            ),
            new ProjectileEnhancement(
                id: "guidance",
                name: "Guidance Package",
                description: "Terminal guidance for improved accuracy",
                slot: ProjectileEnhancementSlot.Guidance,
                hitToleranceBonus: 2.0,
                penetration: 1.0,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 3,
                cost: new Development.Shared.ResourceCost(budget: 200, steel: 50, exotic: 50)
            ),
            new ProjectileEnhancement(
                id: "shaped",
                name: "Shaped Charge",
                description: "Focused energy on impact - 25% more effective damage",
                slot: ProjectileEnhancementSlot.Payload,
                hitToleranceBonus: 1.0,
                penetration: 1.25,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 150, steel: 80, exotic: 30)
            ),
            new ProjectileEnhancement(
                id: "armor_piercing",
                name: "Armor Piercing Tip",
                description: "Hardened tip for dense targets - 15% damage boost",
                slot: ProjectileEnhancementSlot.Payload,
                hitToleranceBonus: 1.0,
                penetration: 1.15,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 1,
                cost: new Development.Shared.ResourceCost(budget: 80, steel: 60, exotic: 10)
            ),
            new ProjectileEnhancement(
                id: "fragmentation",
                name: "Fragmentation Shell",
                description: "Larger hit tolerance, slight damage penalty",
                slot: ProjectileEnhancementSlot.Payload,
                hitToleranceBonus: 1.75,
                penetration: 0.9,
                impactCoupling: 1.0,
                defenseBonus: 0.0,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 120, steel: 70, exotic: 20)
            ),
            new ProjectileEnhancement(
                id: "countermeasures",
                name: "Countermeasure Package",
                description: "Decoys and ablatives - improves projectile survivability",
                slot: ProjectileEnhancementSlot.Armor,
                hitToleranceBonus: 1.0,
                penetration: 0.98,
                impactCoupling: 1.0,
                defenseBonus: 0.25,
                requiredTechLevel: 2,
                cost: new Development.Shared.ResourceCost(budget: 140, steel: 60, exotic: 30)
            ),
            new ProjectileEnhancement(
                id: "hardened",
                name: "Hardened Casing",
                description: "Hardened casing - major survivability boost",
                slot: ProjectileEnhancementSlot.Armor,
                hitToleranceBonus: 1.0,
                penetration: 0.96,
                impactCoupling: 1.0,
                defenseBonus: 0.50,
                requiredTechLevel: 3,
                cost: new Development.Shared.ResourceCost(budget: 260, steel: 120, exotic: 60)
            ),
        ];
    }
}
