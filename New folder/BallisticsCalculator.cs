namespace Spacegun_Simulator
{
    // ============================================================================
    // BALLISTICS CALCULATOR
    // Centralized physics/math helpers: muzzle velocity estimates, projectile
    // trajectory, kinetic energy, diameter-from-mass, fracture-energy mapping.
    // All other systems should call into this class to avoid formula drift.
    // ============================================================================

    public static class BallisticsCalculator
    {
        /// <summary>
        /// Calculate kinetic energy in megajoules.
        /// KE = 0.5 * mass * velocity²
        /// </summary>
        public static double CalculateKineticEnergyMJ(double projectileMassKg, double projectileVelocityMs)
        {
            double energyJoules = 0.5 * projectileMassKg * projectileVelocityMs * projectileVelocityMs;
            return energyJoules / 1_000_000.0;
        }

        /// <summary>
        /// Calculate diameter (meters) from mass in metric tons using an assumed density.
        /// Returns diameter in meters.
        /// </summary>
        public static double CalculateDiameterFromMass(double massTons, double densityKgM3 = 500.0)
        {
            double massKg = massTons * 1000.0;
            double volumeM3 = massKg / densityKgM3;
            double radiusM = Math.Pow(3.0 * volumeM3 / (4.0 * Math.PI), 1.0 / 3.0);
            return radiusM * 2.0;
        }

        /// <summary>
        /// Compute a target fracture energy (MJ) from mass (tons).
        /// Default specific energy is chosen so 1 ton -> ~10 MJ when specificEnergyJPerKg = 10000.
        /// </summary>
        public static double CalculateFractureEnergyMJFromMass(double massTons, double specificEnergyJPerKg = 10000.0)
        {
            double massKg = massTons * 1000.0;
            double fractureJ = massKg * specificEnergyJPerKg;
            double fractureMJ = fractureJ / 1_000_000.0;
            return Math.Max(1.0, fractureMJ);
        }

        /// <summary>
        /// Convert a linear RPM-style descriptor into an approximate muzzle velocity.
        /// - rpm: revolutions per minute of a mechanical accelerator (or launcher)
        /// - linearTravelPerRevMeters: linear distance the projectile advances per revolution (m)
        /// This is a generic helper for derived launcher designs; if you have a specific
        /// propulsion model prefer CalculateMuzzleVelocity(GunConfiguration, ProjectileConfiguration).
        /// </summary>
        public static double CalculateMuzzleVelocityFromRpm(double rpm, double linearTravelPerRevMeters)
        {
            if (rpm <= 0 || linearTravelPerRevMeters <= 0) return 0.0;
            // rpm -> revs per second = rpm / 60
            return (rpm / 60.0) * linearTravelPerRevMeters;
        }

        /// <summary>
        /// Calculate the projectile position at a given flight time using a simple ballistic model:
        ///   - vz = launchVelocity * sin(elevation)
        ///   - vHorizontal = launchVelocity * cos(elevation)
        ///   - vx = vHorizontal * sin(azimuth)
        ///   - vy = vHorizontal * cos(azimuth)
        ///   - x = vx * flightTime
        ///   - y = vy * flightTime
        ///   - z = vz * flightTime - 0.5 * g * flightTime^2
        /// Uses the shared Vector3 struct defined in FiringSolution.cs (same namespace).
        /// </summary>
        public static Vector3 CalculateProjectilePositionStatic(double flightTime, double launchVelocity, double elevationDeg, double azimuthDeg)
        {
            const double GRAVITY = 9.81;

            double elevationRad = elevationDeg * Math.PI / 180.0;
            double azimuthRad = azimuthDeg * Math.PI / 180.0;

            double vz = launchVelocity * Math.Sin(elevationRad);
            double vHorizontal = launchVelocity * Math.Cos(elevationRad);

            double vx = vHorizontal * Math.Sin(azimuthRad);
            double vy = vHorizontal * Math.Cos(azimuthRad);

            double x = vx * flightTime;
            double y = vy * flightTime;
            double z = vz * flightTime - 0.5 * GRAVITY * flightTime * flightTime;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Heuristic muzzle velocity estimate from gun and projectile properties.
        /// Kept for convenience; more detailed models should use CalculateMuzzleVelocity(GunConfiguration, ProjectileConfiguration).
        /// </summary>
        public static double EstimateMuzzleVelocity(double propellantEnergyJ, double projectileMassKg, double efficiency = 0.3)
        {
            if (projectileMassKg <= 0) return 0.0;
            double kineticEnergy = propellantEnergyJ * efficiency;
            return Math.Sqrt(2.0 * kineticEnergy / projectileMassKg);
        }

        /// <summary>
        /// Existing higher-level muzzle velocity calculator that consumes Gun + Projectile models.
        /// Kept as wrapper for compatibility; prefer calling this from game code when computing runtime muzzle speeds.
        /// </summary>
        public static double CalculateMuzzleVelocity(GunConfiguration gun, ProjectileConfiguration projectile)
        {
            // Reuse existing logic from previous BallisticsCalculator.CalculateMuzzleVelocity implementation.
            double velocity = 0.0;

            switch (gun.PropulsionSystem)
            {
                case PropulsionType.Chemical:
                    double totalEnergy = gun.PropellantMass * gun.PropellantEnergyDensity * 1_000_000;
                    double kineticEnergy = totalEnergy * 0.3;
                    velocity = Math.Sqrt(2 * kineticEnergy / projectile.Mass);
                    break;

                case PropulsionType.Railgun:
                    double availableEnergy = gun.PowerCapacity * 1_000_000 * gun.CapacitorEfficiency;
                    velocity = Math.Sqrt(2 * availableEnergy / projectile.Mass);
                    break;

                case PropulsionType.Coilgun:
                    double coilEnergy = gun.PowerCapacity * 1_000_000 * gun.CapacitorEfficiency * 0.85;
                    velocity = Math.Sqrt(2 * coilEnergy / projectile.Mass);
                    break;

                case PropulsionType.Hybrid:
                    double chemEnergy = gun.PropellantMass * gun.PropellantEnergyDensity * 1_000_000 * 0.3;
                    double emEnergy = gun.PowerCapacity * 1_000_000 * gun.CapacitorEfficiency * 0.5;
                    velocity = Math.Sqrt(2 * (chemEnergy + emEnergy) / projectile.Mass);
                    break;
            }

            double barrelEfficiency = Math.Min(1.0, gun.BarrelLength / 200.0);
            velocity *= (0.5 + 0.5 * barrelEfficiency);
            velocity *= gun.BarrelIntegrity;

            return velocity;
        }

        /// <summary>
        /// Convenience: human-readable difficulty description (kept here so callers can centralize).
        /// </summary>
        public static string GetDifficultyDescription(int stars) => stars switch
        {
            1 => "★☆☆☆☆ Very Easy",
            2 => "★★☆☆☆ Easy",
            3 => "★★★☆☆ Moderate",
            4 => "★★★★☆ Hard",
            5 => "★★★★★ Extreme",
            _ => "Unknown"
        };
    }
}
