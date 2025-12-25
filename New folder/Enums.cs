namespace Spacegun_Simulator
{
    // ============================================================================
    // ENUMS (All in one place)
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

    public enum RadarType
    {
        GroundBased,
        MountainTop,
        SpaceBased,
        LunarBased,
        DeepSpace
    }

    public enum DetectionQuality
    {
        None,
        Emergency,
        Degraded,
        Adequate,
        Optimal
    }
}
