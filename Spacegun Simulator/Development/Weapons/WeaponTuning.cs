namespace Spacegun_Simulator.Development.Weapons
{
    /// <summary>
    /// Weapon-related tuning values. Values are config-backed via Spacegun_Simulator.Core.WeaponsTuning.
    /// </summary>
    public static class WeaponTuning
    {
        public readonly record struct WeaponsTechTuning(
            int TechLevel,
            string Name,
            PropulsionType PropulsionSystem,
            double MuzzleVelocityMultiplier,
            double BarrelWearMultiplier = 1.0,
            double FireControlQualityMultiplier = 1.0,
            double ProjectileMassMultiplier = 1.0,
            double PenetrationMultiplier = 1.0
        );

        public static double DefaultBarrelWearPerShot
        {
            get => global::Spacegun_Simulator.Core.WeaponsTuning.DefaultBarrelWearPerShot;
            set => global::Spacegun_Simulator.Core.WeaponsTuning.DefaultBarrelWearPerShot = value;
        }

        public static int BaseMuzzleVelocityMs
        {
            get => global::Spacegun_Simulator.Core.WeaponsTuning.BaseMuzzleVelocityMs;
            set => global::Spacegun_Simulator.Core.WeaponsTuning.BaseMuzzleVelocityMs = value;
        }

        public static WeaponsTechTuning[] WeaponsTechLevels => global::Spacegun_Simulator.Core.WeaponsTuning.WeaponsTechLevels;

        public static double[] WeaponsTechVelocityMultipliers => global::Spacegun_Simulator.Core.WeaponsTuning.WeaponsTechVelocityMultipliers;

        public static PropulsionType GetPropulsionSystemForTechLevel(int weaponsTechLevel)
            => global::Spacegun_Simulator.Core.WeaponsTuning.GetPropulsionSystemForTechLevel(weaponsTechLevel);

        public static double GetBaseMuzzleVelocityForTechLevel(int weaponsTechLevel)
            => global::Spacegun_Simulator.Core.WeaponsTuning.GetBaseMuzzleVelocityForTechLevel(weaponsTechLevel);
    }
}
