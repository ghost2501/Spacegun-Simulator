namespace Spacegun_Simulator.Development.Shared
{
    using Spacegun_Simulator.Core;

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
        public static double[] TierEnemyMinVelocity => DevelopmentTuning.TierVelocity.TierEnemyMinVelocity;

        /// <summary>
        /// Maximum enemy velocity for each tier (m/s).
        /// Aligned with GameConstants.WaveTiers VelocityMax values.
        /// </summary>
        public static double[] TierEnemyMaxVelocity => DevelopmentTuning.TierVelocity.TierEnemyMaxVelocity;

        public static (double EnemyMin, double EnemyMax) GetTierEnemyVelocityConstraints(
            int tierIndex,
            int tierCount)
        {
            if (tierCount <= 0) tierCount = 1;

            if (tierIndex < 0 || tierIndex >= tierCount)
                tierIndex = tierCount - 1;

            return (
                TierEnemyMinVelocity[tierIndex],
                TierEnemyMaxVelocity[tierIndex]
            );
        }
    }
}
