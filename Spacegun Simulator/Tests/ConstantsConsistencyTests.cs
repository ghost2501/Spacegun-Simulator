using System;

namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// Lightweight consistency checks for canonical constants.
    /// This is intentionally simple and safe to call from the developer test harness.
    /// </summary>
    public static class ConstantsConsistencyTests
    {
        /// <summary>
        /// Verify GunConfiguration.GetBaseMuzzleVelocityForTechLevel maps to
        /// GameConstants.WeaponsTechBaseVelocity for all supported tech levels.
        /// Throws InvalidOperationException on mismatch.
        /// </summary>
        public static void RunWeaponTechMappingCheck()
        {
            var velocities = GameConstants.WeaponsTechBaseVelocity ?? Array.Empty<double>();
            if (velocities.Length == 0)
                return; // nothing to check

            const double eps = 1e-6;
            for (int i = 0; i < velocities.Length; i++)
            {
                int techLevel = i + 1;
                double expected = velocities[i];
                double actual = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(techLevel);

                if (Math.Abs(expected - actual) > eps)
                {
                    throw new InvalidOperationException(
                        $"Weapons tech mapping mismatch for tech level {techLevel}: expected {expected}, got {actual}");
                }
            }
        }

        /// <summary>
        /// Verify GunConfiguration default BaseWearPerShot is initialized from
        /// GameConstants.DefaultBarrelWearPerShot (single canonical source).
        /// Throws InvalidOperationException on mismatch.
        /// </summary>
        public static void RunBarrelWearMappingCheck()
        {
            double expected = GameConstants.DefaultBarrelWearPerShot;
            var gun = new GunConfiguration();
            double actual = gun.BaseWearPerShot;

            const double eps = 1e-12;
            if (Math.Abs(expected - actual) > eps)
            {
                throw new InvalidOperationException(
                    $"Barrel wear mapping mismatch: expected {expected}, got {actual}");
            }
        }
    }
}