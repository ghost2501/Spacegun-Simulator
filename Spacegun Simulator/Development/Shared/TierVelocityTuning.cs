namespace Spacegun_Simulator.Development.Shared
{
    /// <summary>
    /// Tier-based velocity bounds used for balancing and consistency checks.
    /// Kept in Development/Shared so tuning lives with other progression knobs.
    /// GameConstants forwards to these values to preserve legacy call sites.
    /// </summary>
    public static class TierVelocityTuning
    {
        /// <summary>
        /// Minimum enemy velocity for each tier (m/s).
        /// Aligned with GameConstants.WaveTiers VelocityMin values.
        /// </summary>
        public static readonly double[] TierEnemyMinVelocity =
        {
            50_000.0,      // Tier 0
            200_000.0,     // Tier 1
            1_000_000.0,   // Tier 2
            3_000_000.0,   // Tier 3
            5_000_000.0    // Tier 4
        };

        /// <summary>
        /// Maximum enemy velocity for each tier (m/s).
        /// Aligned with GameConstants.WaveTiers VelocityMax values.
        /// </summary>
        public static readonly double[] TierEnemyMaxVelocity =
        {
            90_000.0,        // Tier 0
            500_000.0,       // Tier 1
            3_000_000.0,     // Tier 2
            6_500_000.0,     // Tier 3
            10_000_000.0     // Tier 4
        };

        /// <summary>
        /// Minimum player gun velocity for each tier (m/s).
        /// Set to the enemy max velocity so player can always reach the target.
        /// </summary>
        public static readonly double[] TierPlayerMinVelocity =
        {
            90_000.0,        // Tier 0
            500_000.0,       // Tier 1
            3_000_000.0,     // Tier 2
            6_500_000.0,     // Tier 3
            10_000_000.0     // Tier 4
        };

        /// <summary>
        /// Maximum player gun velocity for each tier (m/s) - 150% of enemy max.
        /// </summary>
        public static readonly double[] TierPlayerMaxVelocity =
        {
            135_000.0,       // Tier 0
            750_000.0,       // Tier 1
            4_500_000.0,     // Tier 2
            9_750_000.0,     // Tier 3
            15_000_000.0     // Tier 4
        };

        public static (double EnemyMin, double EnemyMax, double PlayerMin, double PlayerMax) GetTierVelocityConstraints(
            int tierIndex,
            int tierCount)
        {
            if (tierCount <= 0) tierCount = 1;

            if (tierIndex < 0 || tierIndex >= tierCount)
                tierIndex = tierCount - 1;

            return (
                TierEnemyMinVelocity[tierIndex],
                TierEnemyMaxVelocity[tierIndex],
                TierPlayerMinVelocity[tierIndex],
                TierPlayerMaxVelocity[tierIndex]
            );
        }
    }
}
