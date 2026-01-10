namespace Spacegun_Simulator.Core.Stats
{
    public readonly record struct ResolvedProjectileStats(
        int ProjectilesTechLevel,
        string? CoreId,
        string? PropulsionId,
        string? GuidanceModuleId,
        string? PayloadModuleId,
        string? ArmorModuleId,
        double MassKg,
        double PenetrationMult,
        double BaseImpactCouplingMult,
        double ModuleImpactCouplingMult,
        double ImpactCouplingMult,
        double HitToleranceMult,
        double PropulsionDeltaVCapacityMs,
        double PropulsionBurnDurationSeconds,
        double PropulsionReferenceMassKg,
        double DefenseRating01
    );
}
