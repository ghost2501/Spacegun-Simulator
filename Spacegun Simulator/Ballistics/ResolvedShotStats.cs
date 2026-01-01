namespace Spacegun_Simulator.Ballistics
{
    /// <summary>
    /// Runtime-resolved stats used for a single firing attempt.
    /// This is the unified place where gun + projectile + upgrades are mapped into the numbers
    /// consumed by the ballistic solver and the combat model.
    /// </summary>
    public readonly record struct ResolvedShotStats(
        double ProjectileMassKg,
        double MaxLaunchVelocityMs,
        double EffectiveFractureEnergyMJ,
        double Penetration,
        double AdditionalHitToleranceMultiplier,
        double PropulsionDeltaVCapacityMs,
        double PropulsionBurnDurationSeconds,
        double PropulsionReferenceMassKg,
        double ProjectileDefenseRating
    );
}
