namespace Spacegun_Simulator
{
    // ============================================================================
    // ENEMY ARCHETYPE
    // ============================================================================
    // Defines enemy characteristics: Velocity, Mass, and Fracture Energy required
    // to destroy. These are the core parameters players must target with their gun.
    //
    // NEW: Archetypes now include BOUNDS for procedural generation.
    // This allows all enemies to be procedurally generated within archetype constraints,
    // creating variety while maintaining a consistent strategic challenge across all 25 waves.

    public class EnemyArchetype
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Base velocity multiplier for this archetype (0.6-1.6).
        /// Used to scale tier velocity ranges.
        /// </summary>
        public double VelocityMultiplier { get; set; }

        /// <summary>
        /// Mass range (metric tons) for procedural generation.
        /// Actual wave mass will be randomized within these bounds.
        /// </summary>
        public (double Min, double Max) MassRange { get; set; }

        /// <summary>
        /// Fracture Energy range (MJ) for procedural generation.
        /// Actual wave fracture energy will be randomized within these bounds.
        /// </summary>
        public (double Min, double Max) FractureEnergyRange { get; set; }

        /// <summary>
        /// Base difficulty rating (1-5 stars) for this archetype.
        /// </summary>
        public int BaseDifficultyRating { get; set; }

        public EnemyArchetype(
            string id,
            string name,
            string description,
            double velocityMultiplier,
            (double Min, double Max) massRange,
            (double Min, double Max) fractureEnergyRange,
            int baseDifficultyRating)
        {
            Id = id;
            Name = name;
            Description = description;
            VelocityMultiplier = velocityMultiplier;
            MassRange = massRange;
            FractureEnergyRange = fractureEnergyRange;
            BaseDifficultyRating = baseDifficultyRating;
        }

        /// <summary>
        /// Predefined archetype: SCOUT
        /// Fast but fragile. High velocity, low mass, low fracture energy.
        /// Strategy: Precision over power.
        /// </summary>
        public static readonly EnemyArchetype Scout = new(
            id: "scout",
            name: "Scout",
            description: "Fast probe vessel. High velocity, low mass, minimal armor.",
            velocityMultiplier: 1.3,
            massRange: (5_000, 12_000),
            fractureEnergyRange: (25_000, 45_000),
            baseDifficultyRating: 2
        );

        /// <summary>
        /// Predefined archetype: BALANCED
        /// Standard threat. Moderate velocity, moderate mass, moderate fracture energy.
        /// Strategy: Balanced approach to resources.
        /// </summary>
        public static readonly EnemyArchetype Balanced = new(
            id: "balanced",
            name: "Balanced",
            description: "Standard attack vessel. Moderate in all metrics.",
            velocityMultiplier: 1.0,
            massRange: (12_000, 18_000),
            fractureEnergyRange: (40_000, 60_000),
            baseDifficultyRating: 3
        );

        /// <summary>
        /// Predefined archetype: TITAN
        /// Slow but resilient. Low velocity, high mass, high fracture energy.
        /// Strategy: Brute force, more time to prepare.
        /// </summary>
        public static readonly EnemyArchetype Titan = new(
            id: "titan",
            name: "Titan",
            description: "Heavy capital ship. Slow moving, heavily armored.",
            velocityMultiplier: 0.6,
            massRange: (25_000, 40_000),
            fractureEnergyRange: (60_000, 90_000),
            baseDifficultyRating: 4
        );

        /// <summary>
        /// Predefined archetype: SNIPER
        /// Extreme velocity, minimal mass, moderate fracture energy.
        /// Strategy: Extreme precision, challenging intercept calculation.
        /// </summary>
        public static readonly EnemyArchetype Sniper = new(
            id: "sniper",
            name: "Sniper",
            description: "Ultra-high velocity strike craft. Minimal mass, extreme speed.",
            velocityMultiplier: 1.6,
            massRange: (3_000, 8_000),
            fractureEnergyRange: (30_000, 50_000),
            baseDifficultyRating: 3
        );

        /// <summary>
        /// All predefined archetypes.
        /// </summary>
        public static readonly EnemyArchetype[] All =
        {
            Scout,
            Balanced,
            Titan,
            Sniper
        };

        /// <summary>
        /// Select a random preset archetype.
        /// </summary>
        public static EnemyArchetype SelectRandom(Random rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            int index = rng.Next(All.Length);
            return All[index];
        }
    }
}