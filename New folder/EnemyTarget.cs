namespace Spacegun_Simulator
{
    // ============================================================================
    // ENEMY TARGET - Detection, targeting, and destruction data
    // ============================================================================
    // Contains properties essential for:
    // - Detection system calculations (CrossSection, Name)
    // - Targeting calculations (Altitude, Velocity, Evasiveness)
    // - Firing phase validation (FractureEnergy, Mass)
    // ============================================================================

    public class EnemyTarget
    {
        /// <summary>
        /// Target identifier for display and logging.
        /// Combines archetype name with ship type and instance ID.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Current distance from Earth in meters.
        /// Updated as enemy approaches during resource allocation phase.
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Target velocity in m/s for intercept calculations.
        /// </summary>
        public double Velocity { get; set; }

        /// <summary>
        /// Radar cross-section in square meters.
        /// Larger values = easier to detect and track.
        /// Used in detection range calculations.
        /// </summary>
        public double CrossSection { get; set; }

        /// <summary>
        /// Evasiveness factor (0.0 to 1.0).
        /// Affects hit probability during firing phase.
        /// </summary>
        public double Evasiveness { get; set; }

        /// <summary>
        /// Mass in metric tons from the enemy archetype.
        /// Used for difficulty assessment and damage calculations.
        /// </summary>
        public double Mass { get; set; }

        /// <summary>
        /// Fracture Energy required in megajoules (MJ) to destroy this target.
        /// This is the threshold the projectile's kinetic energy must exceed.
        /// Formula: KE = 0.5 * ProjectileMass * ProjectileVelocity² must be ≥ FractureEnergy
        /// </summary>
        public double FractureEnergy { get; set; }
    }
}
