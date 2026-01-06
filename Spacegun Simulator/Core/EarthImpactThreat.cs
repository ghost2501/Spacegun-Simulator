using Spacegun_Simulator.Ballistics;
using Spacegun_Simulator.Enemies;

namespace Spacegun_Simulator.Core
{
    public static class EarthImpactThreat
    {
        public readonly record struct Report(
            double ImpactEnergyMJ,
            double CoupledImpactEnergyMJ,
            double ThresholdMJ,
            bool ExceedsThreshold);

        public static Report Compute(EnemyWave wave)
        {
            if (wave is null) throw new ArgumentNullException(nameof(wave));
            if (wave.Targets is null || wave.Targets.Count == 0)
                throw new ArgumentException("Wave has no targets.", nameof(wave));

            return Compute(wave, wave.Targets[0]);
        }

        public static Report Compute(EnemyWave wave, EnemyTarget target)
        {
            if (wave is null) throw new ArgumentNullException(nameof(wave));
            if (target is null) throw new ArgumentNullException(nameof(target));

            // Canonical: EnemyTarget.Mass is in metric tons.
            // Convert to kg for kinetic energy calculation.
            double massKg = target.Mass * 1000.0;

            // Use wave approach velocity as Earth-impact speed proxy.
            // (This is intentionally orthogonal to combat impact velocity.)
            double impactVelocityMs = wave.AverageVelocity > 0 ? wave.AverageVelocity : target.Velocity;

            double impactEnergyMj = BallisticsCalculator.CalculateKineticEnergyMJ(massKg, impactVelocityMs);

            var tuning = DevelopmentTuning.EarthThreat;
            double coupled = impactEnergyMj * Math.Max(0.0, tuning.EnemyEarthThreatCoupling);
            double threshold = Math.Max(0.0, tuning.EarthDestructionThresholdMJ);

            return new Report(
                ImpactEnergyMJ: impactEnergyMj,
                CoupledImpactEnergyMJ: coupled,
                ThresholdMJ: threshold,
                ExceedsThreshold: coupled >= threshold);
        }
    }
}
