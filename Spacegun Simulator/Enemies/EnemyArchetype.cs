using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Enemies
{
    // ============================================================================
    // ENEMY ARCHETYPE
    // ============================================================================
    // Data model loaded from Config/Enemies/EnemyArchetypes.json.

    public class EnemyArchetype
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Base velocity multiplier for this archetype.
        /// (Current wave generation primarily uses tier constraints + doctrine, but this remains
        /// as a flavor/tuning hook for future expansion.)
        /// </summary>
        public double VelocityMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Mass range (metric tons) shown to the player as the archetype's bounds.
        /// </summary>
        public DevelopmentTuning.Range MassRange { get; set; } = new(0.0, 0.0);

        /// <summary>
        /// Fracture Energy range (MJ) shown to the player as the archetype's bounds.
        /// </summary>
        public DevelopmentTuning.Range FractureEnergyRange { get; set; } = new(0.0, 0.0);

        /// <summary>
        /// Base difficulty rating (1-5 stars) for this archetype.
        /// </summary>
        public int BaseDifficultyRating { get; set; } = 1;

        /// <summary>
        /// True if this archetype is only intended for tutorial scenarios.
        /// </summary>
        public bool IsTutorialOnly { get; set; } = false;
    }
}
