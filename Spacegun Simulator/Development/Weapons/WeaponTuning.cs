namespace Spacegun_Simulator.Development.Weapons
{
    /// <summary>
    /// Weapon-related tuning values kept under Development/Weapons for discoverability.
    /// GameConstants forwards to these values to preserve legacy call sites.
    /// </summary>
    public static class WeaponTuning
    {
        /// <summary>
        /// Canonical barrel wear tunable. This can be overridden by config at runtime.
        /// </summary>
        public static double DefaultBarrelWearPerShot = 0.0005; // 0.05% per nominal shot

        /// <summary>
        /// Weapons tech base muzzle velocities (m/s).
        /// Indexing: WeaponsTechBaseVelocity[techLevel - 1].
        /// </summary>
        public static readonly double[] WeaponsTechBaseVelocity =
        {
            80_000.0,   // Tech level 1 - Chemical baseline (80 km/s)
            160_000.0,  // Tech level 2 - Railgun baseline (160 km/s)
            350_000.0   // Tech level 3 - Plasma/advanced baseline (350 km/s)
        };
    }
}
