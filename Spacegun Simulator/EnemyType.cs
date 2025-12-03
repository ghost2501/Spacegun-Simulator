using System;

namespace Spacegun_Simulator
{
    // ============================================================================
    // ENEMY TYPE - Campaign-wide commitment to a single enemy archetype
    // ============================================================================
    // Generated at game start and persists for the entire campaign.
    // Player must develop a gun specifically designed to combat this enemy type.
    // Different enemy types require fundamentally different tech paths.
    //
    // This creates strategic commitment: players cannot pivot mid-campaign if
    // they discover their chosen tech path is inefficient against their enemy type.
    // ============================================================================

    public class EnemyType
    {
        /// <summary>
        /// Unique identifier for this campaign's enemy type.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The archetype this enemy type is based on (Scout, Balanced, Titan, Sniper).
        /// Defines the base characteristics and scaling behavior.
        /// </summary>
        public EnemyArchetype Archetype { get; set; } = null!;

        /// <summary>
        /// Custom name generated for this specific enemy type.
        /// </summary>
        public string CustomName { get; set; } = string.Empty;

        /// <summary>
        /// Description of this enemy type's combat characteristics.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of when this enemy type was selected (for save files).
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public EnemyType(string id, EnemyArchetype archetype, string customName, string description)
        {
            Id = id;
            Archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
            CustomName = customName;
            Description = description;
        }

        /// <summary>
        /// Generate a random campaign-wide enemy type at game start.
        /// This type persists for all 25 waves.
        /// </summary>
        public static EnemyType GenerateForCampaign(Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));

            // Select random base archetype
            var archetype = EnemyArchetype.SelectRandom(rng);

            // Generate custom name for this enemy type
            string[] prefixes = { "Viper", "Hawk", "Phantom", "Shadow", "Void", "Apex", "Prism", "Tesla", "Nova", "Nexus" };
            string[] suffixes = { "Strike", "Runner", "Reaper", "Bane", "Scourge", "Blade", "Storm", "Wave", "Lance", "Fury" };
            string customName = $"{archetype.Name}-Class {prefixes[rng.Next(prefixes.Length)]} {suffixes[rng.Next(suffixes.Length)]}";

            string description = GenerateDescription(archetype, customName);

            return new EnemyType(
                id: $"campaign_{DateTime.UtcNow.Ticks}",
                archetype: archetype,
                customName: customName,
                description: description
            );
        }

        private static string GenerateDescription(EnemyArchetype archetype, string customName)
        {
            string strategyHint = archetype.Id switch
            {
                "scout" => "Fast and evasive. Requires precision targeting systems.",
                "balanced" => "Well-rounded threat. Demands versatile gun design.",
                "titan" => "Heavily armored. Needs raw kinetic energy to overcome.",
                "sniper" => "Extreme velocity. Must develop advanced tracking systems.",
                _ => "Unknown threat profile."
            };

            return $"{customName}: {strategyHint}";
        }
    }
}