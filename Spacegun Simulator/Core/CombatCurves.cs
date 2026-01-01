namespace Spacegun_Simulator.Core
{
    public static class CombatCurves
    {
        public static double ComputeEvasionChance(double maneuverability01, double fireControlQuality)
        {
            double maneuver = Math.Clamp(maneuverability01, 0.0, 1.0);
            double fireControl = Math.Max(0.0, fireControlQuality);

            // Tuned curve:
            // - Maneuverability alone can cause misses, but not overwhelming.
            // - FireControlQuality quickly suppresses evasion as it rises.
            // Typical reference points:
            // - maneuver=1, fireControl=1 => ~0.23
            // - maneuver=1, fireControl=2.5 => ~0.12
            // - maneuver=0.3, fireControl=1 => ~0.06
            double mPow = Math.Pow(maneuver, 1.25);
            double fcPow = Math.Pow(fireControl, 1.10);

            const double baseChance = 0.55;
            double chance = baseChance * mPow / (mPow + fcPow + 0.35);

            return Math.Clamp(chance, 0.0, 0.65);
        }

        public static double ComputeInterceptKillChance(double offense01, double projectileDefense01)
        {
            double offense = Math.Clamp(offense01, 0.0, 1.0);
            double defense = Math.Clamp(projectileDefense01, 0.0, 1.0);

            // Tuned curve:
            // - Offense is impactful, but cannot dominate.
            // - Defense should noticeably reduce kill chance.
            // Typical reference points:
            // - offense=0.7, defense=0.0 => ~0.30
            // - offense=0.7, defense=0.5 => ~0.19
            // - offense=0.2, defense=0.0 => ~0.11
            double oPow = Math.Pow(offense, 1.30);
            double dPow = Math.Pow(defense + 0.15, 1.50);

            const double baseChance = 0.50;
            double chance = baseChance * oPow / (oPow + dPow + 0.35);

            return Math.Clamp(chance, 0.0, 0.65);
        }
    }
}
