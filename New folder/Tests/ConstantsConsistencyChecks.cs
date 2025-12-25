using System;

namespace Spacegun_Simulator.Tests
{
    public static class ConstantsConsistencyChecks
    {
        public static void RunAllChecks()
        {
            RunWeaponTechMappingCheck();
            RunBarrelWearMappingCheck();
        }

        public static void RunWeaponTechMappingCheck()
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
                    throw new InvalidOperationException(
                        $"Weapons tech mapping mismatch for tech {techLevel}: expected {expected}, got {actual}");
            }
        }

        public static void RunBarrelWearMappingCheck()
        {
            const double eps = 1e-12;
            double expected = GameConstants.DefaultBarrelWearPerShot;

            var gun = new GunConfiguration();
            double actual = gun.BaseWearPerShot;

            if (Math.Abs(expected - actual) > eps)
                throw new InvalidOperationException(
                    $"Barrel wear mapping mismatch: expected {expected}, got {actual}");
        }
    }
}
