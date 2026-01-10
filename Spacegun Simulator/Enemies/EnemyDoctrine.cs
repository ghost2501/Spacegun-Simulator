namespace Spacegun_Simulator.Enemies;

public enum EnemyDoctrine
{
    None = 0,

    // Core doctrines (single-axis emphasis)
    Kinetics,
    Evasion,
    Armor,
    Stealth,
    Swarm,
    Sniper,
    Jamming,
    Siege,

    // Combo doctrines (two-axis emphasis)
    StealthEvasion,
    ArmorSiege,
}

public enum EnemyDoctrineSource
{
    None = 0,
    Campaign,
    Guest,
}

public sealed record EnemyDoctrineProfile(
    EnemyDoctrine Doctrine,
    string Name,
    string Description,
    double VelocityMultiplier,
    double AccelerationMultiplier,
    double ManeuverabilityMultiplier,
    double DefenseMultiplier,
    double OffenseMultiplier,
    double StealthChanceMultiplier,
    double RadarCrossSectionMultiplier
)
{
    public static EnemyDoctrineProfile Get(EnemyDoctrine doctrine)
    {
        return EnemyDoctrineCatalog.Get(doctrine);
    }
}

public static class EnemyDoctrineSelector
{
    public static EnemyDoctrine SelectGuestDoctrine(EnemyDoctrine campaignDoctrine, EnemyArchetype archetype, Random rng)
    {
        return EnemyDoctrineCatalog.SelectGuestDoctrine(campaignDoctrine, archetype, rng);
    }
}
