namespace Spacegun_Simulator
{
    // ============================================================================
    // BALLISTICS HELPER (thin compatibility layer)
    // Redirects to BallisticsCalculator to keep a single canonical implementation.
    // ============================================================================
    public static class BallisticsHelper
    {
        /// <summary>
        /// Calculate kinetic energy in megajoules.
        /// Delegates to BallisticsCalculator.
        /// </summary>
        public static double CalculateKineticEnergyMJ(double projectileMassKg, double projectileVelocityMs)
        {
            return BallisticsCalculator.CalculateKineticEnergyMJ(projectileMassKg, projectileVelocityMs);
        }

        /// <summary>
        /// Determine if a projectile can destroy an enemy.
        /// Uses a 95% threshold to provide margin for safety.
        /// </summary>
        public static bool CanDestroyEnemy(double projectileKE_MJ, double fractureEnergy_MJ)
        {
            double threshold = fractureEnergy_MJ * 0.95;
            return projectileKE_MJ >= threshold;
        }

        /// <summary>
        /// Get a human-readable difficulty description based on stars.
        /// Delegates to BallisticsCalculator to keep text centralized.
        /// </summary>
        public static string GetDifficultyDescription(int stars) => BallisticsCalculator.GetDifficultyDescription(stars);
    }
}