using Spacegun_Simulator.Development.Shared;
using Spacegun_Simulator.Core.Stats;

namespace Spacegun_Simulator.Core
{
    public static class WeaponsUpgrades
    {
        public static IReadOnlyList<UpgradeDefinition> Definitions { get; private set; } = Array.Empty<UpgradeDefinition>();

        public static void Apply(IReadOnlyList<UpgradeDefinition> defs)
        {
            Definitions = defs ?? Array.Empty<UpgradeDefinition>();
        }

        public sealed record UpgradeDefinition(
            string Id,
            string Name,
            string Description,
            ResourceCost Cost,
            string[] Prerequisites,
            int? MinWeaponsTechLevel,
            int? MinProjectilesTechLevel,
            bool RequiresGuidanceMod,
            string? RequiresPropulsion,
            Dictionary<string, double> Parameters,
            IReadOnlyList<StatModifier> Modifiers);

        public static UpgradeDefinition CreateDefault(string id) => new(
            Id: id,
            Name: id,
            Description: string.Empty,
            Cost: ResourceCost.None,
            Prerequisites: Array.Empty<string>(),
            MinWeaponsTechLevel: null,
            MinProjectilesTechLevel: null,
            RequiresGuidanceMod: false,
            RequiresPropulsion: null,
            Parameters: new Dictionary<string, double>(),
            Modifiers: Array.Empty<StatModifier>()
        );
    }
}
