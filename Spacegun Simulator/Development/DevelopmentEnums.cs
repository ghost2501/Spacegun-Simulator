namespace Spacegun_Simulator.Development
{
    // ============================================================================
    // DEVELOPMENT ENUMS ONLY
    // ============================================================================

    public enum PropulsionType
    {
        Chemical,
        Railgun,
        Coilgun,
        Hybrid
    }

    public enum CoolingSystem
    {
        Passive,
        ActiveAir,
        Liquid,
        Cryogenic
    }

    public enum ProjectileType
    {
        KineticPenetrator,
        Explosive,
        Fragmentation,
        Guided
    }

    public enum ArmorPenetrationType
    {
        KineticEnergy,
        ShapedCharge,
        Fragmentation
    }
}
