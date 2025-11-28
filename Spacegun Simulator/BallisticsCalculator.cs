namespace Spacegun_Simulator
{
    // ============================================================================
    // BALLISTICS CALCULATOR
    // ============================================================================

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

        public static double CalculateInterceptProbability(
            GunConfiguration gun,
            ProjectileConfiguration projectile,
            EnemyTarget target,
            double muzzleVelocity)
        {
            double baseAccuracy = 0.5 + (gun.BarrelLength / 400.0) * gun.BarrelIntegrity;
            baseAccuracy = Math.Min(0.95, baseAccuracy);

            if (projectile.HasGuidance)
            {
                baseAccuracy += (1.0 - baseAccuracy) * projectile.GuidanceAccuracy;
            }

            double targetSpeed = target.Velocity;
            double speedPenalty = Math.Max(0.0, (targetSpeed - 5000) / 20000.0);

            double targetSize = target.CrossSection;
            double sizeBonus = Math.Log10(targetSize) / 10.0;

            double evasionPenalty = target.Evasiveness * 0.3;

            double finalProbability = baseAccuracy - speedPenalty + sizeBonus - evasionPenalty;

            return Math.Clamp(finalProbability, 0.05, 0.99);
        }

        public static double CalculateDamage(
            ProjectileConfiguration projectile,
            double impactVelocity,
            EnemyTarget target)
        {
            double kineticEnergy = 0.5 * projectile.Mass * impactVelocity * impactVelocity;
            double armorEffectiveness = target.ArmorThickness * target.ArmorQuality;

            double penetration = projectile.PenetrationType switch
            {
                ArmorPenetrationType.KineticEnergy => kineticEnergy / 1_000_000,
                ArmorPenetrationType.ShapedCharge => projectile.Mass * 50,
                ArmorPenetrationType.Fragmentation => projectile.Mass * 20,
                _ => kineticEnergy / 1_000_000
            };

            double damageMultiplier = penetration / armorEffectiveness;
            double baseDamage = kineticEnergy / 10_000_000;

            return baseDamage * damageMultiplier;
        }
    }
}
