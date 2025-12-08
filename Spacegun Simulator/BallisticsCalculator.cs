namespace Spacegun_Simulator
{
    // ============================================================================
    // BALLISTICS CALCULATOR
    // ============================================================================
    // Core ballistics calculations: muzzle velocity, kinetic energy (damage),
    // and target destruction validation.
    //
    // NOTE: Hit probability calculation has been moved to the Firing Phase UI.
    // This allows player input (firing solution calculations) to directly influence
    // hit probability, creating a skill-based component to the game.

    public static class BallisticsCalculator
    {
        private const double EARTH_GRAVITY = 9.81;

        public static double CalculateMuzzleVelocity(
            GunConfiguration gun,
            ProjectileConfiguration projectile)
        {
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
        /// Calculate the base weapon accuracy capability.
        /// This represents the gun's inherent accuracy potential without player input.
        /// Used as reference for player to compare against their calculated firing solution.
        /// </summary>
        public static double GetBaseWeaponAccuracy(GunConfiguration gun)
        {
            double baseAccuracy = 0.5 + (gun.BarrelLength / 400.0) * gun.BarrelIntegrity;
            return Math.Min(0.95, baseAccuracy);
        }

        /// <summary>
        /// Calculate the theoretical best-case hit probability for a given gun/target combination.
        /// This is what the player should aspire to achieve with perfect firing solution.
        /// </summary>
        public static double GetTheoreticalMaxProbability(
            GunConfiguration gun,
            ProjectileConfiguration projectile,
            EnemyTarget target)
        {
            double baseAccuracy = GetBaseWeaponAccuracy(gun);

            // Guidance system bonus
            if (projectile.HasGuidance)
            {
                baseAccuracy += (1.0 - baseAccuracy) * projectile.GuidanceAccuracy;
            }

            // Target-specific modifiers (fixed characteristics)
            double targetSize = target.CrossSection;
            double sizeBonus = Math.Log10(targetSize) / 10.0;

            double evasionPenalty = target.Evasiveness * 0.3;

            double theoreticalMax = baseAccuracy + sizeBonus - evasionPenalty;

            return Math.Clamp(theoreticalMax, 0.05, 0.99);
        }

        /// <summary>
        /// Calculate firing solution accuracy based on player's calculated values vs optimal.
        /// </summary>
        public static double CalculateFiringSolutionAccuracy(
            float playerAngleEstimate,
            float calculatedOptimalAngle,
            float playerTimeEstimate,
            float calculatedTimeToImpact)
        {
            // Calculate angle deviation in degrees
            float angleDifference = Math.Abs(playerAngleEstimate - calculatedOptimalAngle);
            // Normalize to 0-180 range
            if (angleDifference > 180f)
                angleDifference = 360f - angleDifference;

            // Calculate time deviation as percentage
            float timeDifference = Math.Abs(playerTimeEstimate - calculatedTimeToImpact);
            float timePercentDifference = (calculatedTimeToImpact > 0.1f)
                ? (timeDifference / calculatedTimeToImpact) * 100f
                : 0;

            // Combined deviation score (lower is better)
            float totalDeviation = (angleDifference / 45f) * 50f + timePercentDifference * 0.5f;

            // Convert deviation to accuracy (0 deviation = 1.0 accuracy, 100 deviation = 0.5 accuracy)
            double accuracy = 1.0 - (totalDeviation / 100.0);

            // Clamp to realistic range
            return Math.Clamp(accuracy, 0.5, 1.0);
        }

        /// <summary>
        /// DEPRECATED: This method is replaced by the physics-based firing solution in ConsoleUI.
        /// Kept for compatibility but no longer used in the game flow.
        /// </summary>
        [Obsolete("Use physics-based firing solution from FiringSolution class instead")]
        public static double CalculateFinalHitProbability(
            GunConfiguration gun,
            ProjectileConfiguration projectile,
            EnemyTarget target,
            double playerLeadingPercentage,
            double playerVelocityCompensationPercentage)
        {
            double theoreticalMax = GetTheoreticalMaxProbability(gun, projectile, target);
            // This old method is no longer used - keeping stub for backward compatibility
            return theoreticalMax * 0.75; // Placeholder value
        }

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
        /// Calculate damage as kinetic energy in megajoules.
        /// This is the value compared against fracture energy.
        /// </summary>
        public static double CalculateDamage(
            ProjectileConfiguration projectile,
            double impactVelocity,
            EnemyTarget target)
        {
            return CalculateKineticEnergyMJ(projectile.Mass, impactVelocity);
        }

        /// <summary>
        /// Determine if the projectile delivers sufficient kinetic energy to destroy the target.
        /// Uses a 95% threshold to provide a margin for safety.
        /// </summary>
        public static bool CanDestroyTarget(double projectileKineticEnergyMJ, EnemyTarget target)
        {
            double threshold = target.FractureEnergy * 0.95;
            return projectileKineticEnergyMJ >= threshold;
        }

        /// <summary>
        /// Get a human-readable difficulty description based on stars.
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
