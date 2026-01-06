namespace Spacegun_Simulator.Core
{
    public sealed class WeaponsUpgradesConfig
    {
        public int Version { get; set; } = 1;

        public UpgradeConfig[]? Upgrades { get; set; }
    }

    public sealed class UpgradeConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ResourceCostConfig? Cost { get; set; }
        public string[]? Prerequisites { get; set; }
        public Dictionary<string, double>? StatModifiers { get; set; }

        /// <summary>
        /// Optional filter: "Chemical" or "NonChemical".
        /// </summary>
        public string? RequiresPropulsion { get; set; }

        /// <summary>
        /// Per-upgrade parameters used by UI/game logic (e.g. multipliers, caps).
        /// </summary>
        public Dictionary<string, double>? Parameters { get; set; }

        public int? MinWeaponsTechLevel { get; set; }
        public int? MinProjectilesTechLevel { get; set; }
        public bool? RequiresGuidanceMod { get; set; }
    }
}
