using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Tests
{
    /// <summary>
    /// Defensive checks validating tier arrays and GetTierVelocityConstraints behaviour.
    /// Safe to call from the developer harness or CI as a quick consistency gate.
    /// Throws InvalidOperationException on any failure.
    /// </summary>
    public static class TierArraysConsistencyTests
    {
        public static void RunAllChecks()
        {
            const double eps = 1e-6;

            int tierCount = GameConstants.TierCount;

            if (GameConstants.WaveTiers == null)
                throw new InvalidOperationException("WaveTiers must not be null.");

            if (GameConstants.WaveTiers.Length != tierCount)
                throw new InvalidOperationException($"WaveTiers length ({GameConstants.WaveTiers.Length}) != TierCount ({tierCount}).");

            if (GameConstants.TierEnemyMinVelocity == null ||
                GameConstants.TierEnemyMaxVelocity == null)
            {
                throw new InvalidOperationException("One or more tier velocity arrays are null.");
            }

            if (GameConstants.TierEnemyMinVelocity.Length != tierCount ||
                GameConstants.TierEnemyMaxVelocity.Length != tierCount)
            {
                throw new InvalidOperationException("All tier velocity arrays must have length equal to TierCount.");
            }

            for (int i = 0; i < tierCount; i++)
            {
                var tier = GameConstants.WaveTiers[i];

                if (tier == null)
                    throw new InvalidOperationException($"WaveTiers[{i}] is null.");

                if (tier.TierIndex != i)
                    throw new InvalidOperationException($"WaveTiers[{i}].TierIndex ({tier.TierIndex}) must equal array index {i}.");

                double eMin = GameConstants.TierEnemyMinVelocity[i];
                double eMax = GameConstants.TierEnemyMaxVelocity[i];

                if (!(eMin <= eMax + eps))
                    throw new InvalidOperationException($"Tier {i} enemy min > max ({eMin} > {eMax}).");

                // WaveTiers define the campaign pacing ranges.
                // TierEnemyMin/MaxVelocity are tuning values used for diagnostics/test scenario sampling.
                // They are not required to match WaveTiers.
                if (!(tier.VelocityMin <= tier.VelocityMax + eps))
                    throw new InvalidOperationException($"WaveTiers[{i}] velocity min > max ({tier.VelocityMin} > {tier.VelocityMax}).");

                // Validate GetTierEnemyVelocityConstraints returns the expected tuple
                var tuple = GameConstants.GetTierEnemyVelocityConstraints(i);
                if (Math.Abs(tuple.EnemyMin - eMin) > eps ||
                    Math.Abs(tuple.EnemyMax - eMax) > eps)
                {
                    throw new InvalidOperationException($"GetTierEnemyVelocityConstraints returned inconsistent values for tier {i}.");
                }
            }

            // Validate out-of-bounds behaviour (negative and large index -> last tier)
            var lastExpected = GameConstants.GetTierEnemyVelocityConstraints(tierCount - 1);
            var neg = GameConstants.GetTierEnemyVelocityConstraints(-1);
            var over = GameConstants.GetTierEnemyVelocityConstraints(tierCount + 5);

            if (Math.Abs(neg.EnemyMin - lastExpected.EnemyMin) > eps ||
                Math.Abs(over.EnemyMin - lastExpected.EnemyMin) > eps)
            {
                throw new InvalidOperationException("GetTierVelocityConstraints did not clamp out-of-range indices to the last tier as expected.");
            }

            // ===== Tier target material tuning arrays =====
            var mat = DevelopmentTuning.TierTargetMaterial;
            if (mat.TierEnemyMassTonsMin.Length != tierCount ||
                mat.TierEnemyMassTonsMax.Length != tierCount ||
                mat.TierEnemyDensityKgM3Min.Length != tierCount ||
                mat.TierEnemyDensityKgM3Max.Length != tierCount ||
                mat.TierEnemyBulkModulusGpaMin.Length != tierCount ||
                mat.TierEnemyBulkModulusGpaMax.Length != tierCount)
            {
                throw new InvalidOperationException("All tier target material arrays must have length equal to TierCount.");
            }

            for (int i = 0; i < tierCount; i++)
            {
                double mMin = mat.TierEnemyMassTonsMin[i];
                double mMax = mat.TierEnemyMassTonsMax[i];
                if (!(mMin <= mMax + eps))
                    throw new InvalidOperationException($"Tier {i} enemy mass min > max ({mMin} > {mMax}).");

                double dMin = mat.TierEnemyDensityKgM3Min[i];
                double dMax = mat.TierEnemyDensityKgM3Max[i];
                if (!(dMin <= dMax + eps))
                    throw new InvalidOperationException($"Tier {i} enemy density min > max ({dMin} > {dMax}).");

                double kMin = mat.TierEnemyBulkModulusGpaMin[i];
                double kMax = mat.TierEnemyBulkModulusGpaMax[i];
                if (!(kMin <= kMax + eps))
                    throw new InvalidOperationException($"Tier {i} enemy bulk modulus min > max ({kMin} > {kMax}).");
            }
        }
    }
}