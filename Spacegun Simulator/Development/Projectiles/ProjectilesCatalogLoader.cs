using System.Text.Json;
using Spacegun_Simulator.Development.Projectiles;
using Spacegun_Simulator.Development.Shared;

namespace Spacegun_Simulator.Core
{
    public static class ProjectilesCatalogLoader
    {
        public static void LoadIfExists(string relativePath = "Config/ProjectilesCatalog.json")
        {
            try
            {
                // Deserialize as raw DTOs first, then map to runtime objects.
                if (!ConfigJson.TryDeserializeFile<ProjectilesCatalogJson>(relativePath, out var cfg))
                    return;

                if (cfg is null)
                    return;

                var cores = cfg.Cores?.Select(c => new ProjectileCore(
                    id: c.Id,
                    name: c.Name,
                    description: c.Description,
                    massKg: c.MassKg,
                    requiredTechLevel: c.RequiredTechLevel,
                    cost: c.Cost?.ToResourceCost() ?? ResourceCost.None
                )).ToArray();

                var propulsion = cfg.PropulsionSystems?.Select(p => new PropulsionSystem(
                    id: p.Id,
                    name: p.Name,
                    description: p.Description,
                    deltaVCapacityMs: p.DeltaVCapacityMs,
                    burnDurationSeconds: p.BurnDurationSeconds,
                    referenceMassKg: p.ReferenceMassKg,
                    requiredTechLevel: p.RequiredTechLevel,
                    cost: p.Cost?.ToResourceCost() ?? ResourceCost.None
                )).ToArray();

                var enhancements = cfg.Enhancements?.Select(e => new ProjectileEnhancement(
                    id: e.Id,
                    name: e.Name,
                    description: e.Description,
                    hitToleranceBonus: e.HitToleranceBonus,
                    penetration: e.Penetration,
                    impactCoupling: e.ImpactCoupling,
                    defenseBonus: e.DefenseBonus,
                    requiredTechLevel: e.RequiredTechLevel,
                    cost: e.Cost?.ToResourceCost() ?? ResourceCost.None
                )).ToArray();

                ProjectilesCatalog.Apply(new ProjectilesCatalogConfig
                {
                    Version = cfg.Version,
                    Cores = cores,
                    PropulsionSystems = propulsion,
                    Enhancements = enhancements,
                });
            }
            catch
            {
                // Keep game runnable if config is malformed.
            }
        }

        private sealed class ProjectilesCatalogJson
        {
            public int Version { get; set; } = 1;
            public ProjectileCoreJson[]? Cores { get; set; }
            public PropulsionSystemJson[]? PropulsionSystems { get; set; }
            public ProjectileEnhancementJson[]? Enhancements { get; set; }
        }

        private sealed class ProjectileCoreJson
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double MassKg { get; set; }
            public int RequiredTechLevel { get; set; } = 1;
            public ResourceCostConfig? Cost { get; set; }
        }

        private sealed class PropulsionSystemJson
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double DeltaVCapacityMs { get; set; }
            public double BurnDurationSeconds { get; set; }
            public double ReferenceMassKg { get; set; }
            public int RequiredTechLevel { get; set; } = 1;
            public ResourceCostConfig? Cost { get; set; }
        }

        private sealed class ProjectileEnhancementJson
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double HitToleranceBonus { get; set; } = 1.0;
            public double Penetration { get; set; } = 1.0;
            public double ImpactCoupling { get; set; } = 1.0;
            public double DefenseBonus { get; set; }
            public int RequiredTechLevel { get; set; } = 1;
            public ResourceCostConfig? Cost { get; set; }
        }
    }
}
