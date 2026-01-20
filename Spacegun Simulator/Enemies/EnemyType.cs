namespace Spacegun_Simulator.Enemies
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
        /// The archetype this enemy type is based on (Needle, Slug, Boulder, RKV).
        /// Defines the base characteristics and scaling behavior.
        /// </summary>
        public EnemyArchetype Archetype { get; set; } = null!;

        /// <summary>
        /// A secondary archetype used for campaign variety.
        /// Waves may occasionally swap to this archetype while keeping the campaign's primary identity.
        /// </summary>
        public EnemyArchetype? SecondaryArchetype { get; set; }

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

        /// <summary>
        /// Primary campaign doctrine applied on top of the archetype.
        /// This is a soft modifier layer intended to shape wave generation.
        /// </summary>
        public EnemyDoctrine PrimaryDoctrine { get; set; } = EnemyDoctrine.None;

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
            var archetype = EnemyArchetypeCatalog.SelectRandom(rng);

            // Select a secondary archetype for additional per-wave variety (best-effort).
            var secondaryPool = EnemyArchetypeCatalog.CampaignArchetypes
                .Where(a => a is not null && !string.Equals(a.Id, archetype.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            EnemyArchetype? secondary = secondaryPool.Length > 0 ? secondaryPool[rng.Next(secondaryPool.Length)] : null;

            var primaryDoctrine = EnemyDoctrineCatalog.SelectPrimaryDoctrine(archetype, rng);

            // Generate custom name for this enemy type
            string customName = EnemyNaming.GenerateCampaignName(archetype, primaryDoctrine, rng);

            string description = GenerateDescription(archetype, customName);

            var enemyType = new EnemyType(
                id: $"campaign_{DateTime.UtcNow.Ticks}",
                archetype: archetype,
                customName: customName,
                description: description
            );

            enemyType.SecondaryArchetype = secondary;

            enemyType.PrimaryDoctrine = primaryDoctrine;
            return enemyType;
        }

        private static string GenerateDescription(EnemyArchetype archetype, string customName)
        {
            string strategyHint = archetype.Id switch
            {
                "scout" => "Small and fast. Demands precision tracking and tight timing.",
                "balanced" => "General-purpose kinetic threat. Demands a versatile gun design.",
                "titan" => "Massive and resilient. Needs raw kinetic energy to overcome.",
                "sniper" => "Hypervelocity profile. Must develop advanced tracking systems.",
                _ => "Unknown threat profile."
            };

            return $"{customName}: {strategyHint}";
        }
    }
}
