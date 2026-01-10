namespace Spacegun_Simulator.Core
{
    using Spacegun_Simulator.Core.Stats;

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

        /// <summary>
        /// Optional filter: "Chemical" or "NonChemical".
        /// </summary>
        public string? RequiresPropulsion { get; set; }

        /// <summary>
        /// Per-upgrade parameters used by UI/game logic (e.g. multipliers, caps).
        /// </summary>
        public Dictionary<string, double>? Parameters { get; set; }

        /// <summary>
        /// Generic stat-key modifiers applied when the upgrade is purchased.
        /// Intended to minimize hard-coded upgrade application logic.
        /// </summary>
        public StatModifierConfig[]? Modifiers { get; set; }

        public int? MinWeaponsTechLevel { get; set; }
        public int? MinProjectilesTechLevel { get; set; }
        public bool? RequiresGuidanceMod { get; set; }
    }

    public sealed class StatModifierConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Op { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
