namespace Spacegun_Simulator.Enemies
{
    // ============================================================================
    // ENEMY TARGET - Detection, targeting, and destruction data
    // ============================================================================
    // Contains properties essential for:
    // - Detection system calculations (CrossSection, Name)
    // - Targeting calculations (Altitude, Velocity, Maneuverability)
    // - Firing phase validation (FractureEnergy, Mass)
    // ============================================================================

    public class EnemyTarget
    {
        /// <summary>
        /// Target identifier for display and logging.
        /// Combines archetype name with designation type and instance ID.
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
        /// Radar cross-section area in square meters (m^2).
        /// Larger values = easier to detect and track and (in this game) a larger effective hitbox.
        /// </summary>
        public double CrossSection { get; set; }

        /// <summary>
        /// Acceleration capability in m/s^2.
        /// Full mode only; Pure mode sets this to 0.
        /// </summary>
        public double Acceleration { get; set; }

        /// <summary>
        /// Maneuverability factor (0.0 to 1.0).
        /// Full mode only; Pure mode sets this to 0.
        /// </summary>
        public double Maneuverability { get; set; }

        /// <summary>
        /// Defensive capability factor (0.0 to 1.0).
        /// Full mode only; Pure mode sets this to 0.
        /// </summary>
        public double Defense { get; set; }

        /// <summary>
        /// Offensive capability factor (0.0 to 1.0).
        /// Full mode only; Pure mode sets this to 0.
        /// Represents the target's ability to destroy incoming projectiles.
        /// </summary>
        public double Offense { get; set; }

        /// <summary>
        /// Mass in metric tons from the enemy archetype.
        /// Used for difficulty assessment and damage calculations.
        /// </summary>
        public double Mass { get; set; }

        /// <summary>
        /// Effective density in kg/m^3 used for geometry derivation.
        /// This is a gameplay/tuning property (not a rigorous material simulation).
        /// </summary>
        public double DensityKgM3 { get; set; }

        /// <summary>
        /// Effective bulk modulus in GPa used as a hardness/strength proxy.
        /// Used by the derived fracture energy model.
        /// </summary>
        public double BulkModulusGpa { get; set; }

        /// <summary>
        /// Fracture Energy required in megajoules (MJ) to destroy this target.
        /// This is the threshold the projectile's kinetic energy must exceed.
        /// Formula: KE = 0.5 * ProjectileMass * ProjectileVelocity² must be ≥ FractureEnergy
        /// </summary>
        public double FractureEnergy { get; set; }
    }
}
