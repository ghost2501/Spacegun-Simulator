using System;

namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// Additional consistency assertions that verify consumers use canonical constants.
    /// Thrown exceptions surface as failed checks in the developer harness or CI.
    /// </summary>
    public static class ConstantsConsistencyUnitTests
    {
        public static void RunAllChecks()
        {
            RunBarrelWearCanonicalCheck();
            RunWeaponTechMappingCheck();
        }

        private static void RunBarrelWearCanonicalCheck()
        {
            const double eps = 1e-12;
            double expected = GameConstants.DefaultBarrelWearPerShot;
            var gun = new GunConfiguration();
            double actual = gun.BaseWearPerShot;

            if (Math.Abs(expected - actual) > eps)
            {
                throw new InvalidOperationException($"Canonical barrel-wear mismatch: GameConstants.DefaultBarrelWearPerShot ({expected}) != GunConfiguration.BaseWearPerShot ({actual})");
            }
        }

        private static void RunWeaponTechMappingCheck()
        {
            var velocities = GameConstants.WeaponsTechBaseVelocity ?? Array.Empty<double>();
            if (velocities.Length == 0) return;

            const double eps = 1e-6;
            for (int i = 0; i < velocities.Length; i++)
            {
                int techLevel = i + 1;
                double expected = velocities[i];
                double actual = GunConfiguration.GetBaseMuzzleVelocityForTechLevel(techLevel);

                if (Math.Abs(expected - actual) > eps)
                {
                    throw new InvalidOperationException($"Weapons tech mapping mismatch for tech {techLevel}: expected {expected}, got {actual}");
                }
            }
        }
    }
}