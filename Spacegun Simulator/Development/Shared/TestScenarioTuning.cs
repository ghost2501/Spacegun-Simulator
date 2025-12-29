namespace Spacegun_Simulator.Development.Shared
{
    /// <summary>
    /// Helpers used by diagnostics/test harnesses.
    /// Kept in Development/Shared so "how we test/benchmark" lives near tuning.
    /// GameConstants forwards to these methods to preserve legacy call sites.
    /// </summary>
    public static class TestScenarioTuning
    {
        public static double GetTestPlayerVelocityForTier(int tierIndex)
        {
            return tierIndex switch
            {
                0 => 150_000.0,
                1 => 5_000_000.0,
                2 => 50_000_000.0,
                3 => 100_000_000.0,
                _ => 150_000.0
            };
        }

        public static double GetTestGunRangeForTier(int tierIndex)
        {
            return tierIndex switch
            {
                0 => 1_000_000.0,
                1 => 30_000_000.0,
                2 => 260_000_000.0,
                3 => 430_000_000.0,
                _ => 1_000_000.0
            };
        }

        public static double GetTestEngagementDistanceForTier(int tierIndex)
        {
            return tierIndex switch
            {
                0 => 1_000_000.0,
                1 => 1_000_000.0,
                2 => 2_000_000.0,
                3 => 3_000_000.0,
                _ => 1_000_000.0
            };
        }

        public static double CalculateExpectedInterceptTime(
            double engagementDistance,
            double enemyVelocity,
            double playerVelocity)
        {
            if (enemyVelocity + playerVelocity <= 0)
                return double.PositiveInfinity;

            double closingVelocity = enemyVelocity + playerVelocity;
            return engagementDistance / closingVelocity;
        }
    }
}
