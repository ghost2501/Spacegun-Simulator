namespace Spacegun_Simulator.Core.Stats
{
    public readonly record struct ResolvedGunStats(
        int WeaponsTechLevel,
        double BarrelLengthM,
        double BoreDiameterM,
        string BarrelMaterial,
        double BarrelIntegrity01,
        double FireControlQuality,
        double BaseMuzzleVelocityMs,
        double RangeMultiplierFromBarrelLength,
        double EnergyBasedMaxLaunchVelocityMs,
        double TechBasedMaxLaunchVelocityMs,
        double MaxLaunchVelocityMs,
        double BaseWearPerShot01,
        double IntegrityFailureThreshold01,
        long ShotsFired,
        double CumulativeWear01
    );
}
