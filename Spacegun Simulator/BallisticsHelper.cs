namespace Spacegun_Simulator
{
    // ============================================================================
    // BALLISTICS HELPER
    // ============================================================================
    // Utilities for calculating kinetic energy and determining if a projectile
    // can destroy an enemy based on fracture energy.
    
    public static class BallisticsHelper
    {
        /// <summary>
        /// Calculate kinetic energy in joules.
        /// KE = 0.5 * mass * velocity²
        /// Result is converted to megajoules (MJ) for display.
        /// </summary>
        /// <param name="projectileMassKg">Projectile mass in kilograms</param>
        /// <param name="projectileVelocityMs">Projectile velocity in m/s</param>
        /// <returns>Kinetic energy in megajoules (MJ)</returns>
        public static double CalculateKineticEnergyMJ(double projectileMassKg, double projectileVelocityMs)
        {
            double energyJoules = 0.5 * projectileMassKg * projectileVelocityMs * projectileVelocityMs;
            double energyMJ = energyJoules / 1_000_000.0;
            return energyMJ;
        }

        /// <summary>
        /// Determine if a projectile can destroy an enemy.
        /// Uses a 95% threshold to provide margin for safety.
        /// </summary>
        /// <param name="projectileKE_MJ">Projectile kinetic energy in MJ</param>
        /// <param name="fractureEnergy_MJ">Enemy fracture energy requirement in MJ</param>
        /// <returns>True if projectile KE >= 95% of fracture energy</returns>
        public static bool CanDestroyEnemy(double projectileKE_MJ, double fractureEnergy_MJ)
        {
            double threshold = fractureEnergy_MJ * 0.95;
            return projectileKE_MJ >= threshold;
        }

        /// <summary>
        /// Get a human-readable difficulty description based on stars.
        /// </summary>
        public static string GetDifficultyDescription(int stars) => stars switch
        {
            1 => "★☆☆☆☆ Very Easy",
            2 => "★★☆☆☆ Easy",
            3 => "★★★☆☆ Moderate",
            4 => "★★★★☆ Hard",
            5 => "★★★★★ Extreme",
            _ => "Unknown"
        };
    }
}