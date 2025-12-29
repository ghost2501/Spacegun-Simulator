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
                GameConstants.TierEnemyMaxVelocity == null ||
                GameConstants.TierPlayerMinVelocity == null ||
                GameConstants.TierPlayerMaxVelocity == null)
            {
                throw new InvalidOperationException("One or more tier velocity arrays are null.");
            }

            if (GameConstants.TierEnemyMinVelocity.Length != tierCount ||
                GameConstants.TierEnemyMaxVelocity.Length != tierCount ||
                GameConstants.TierPlayerMinVelocity.Length != tierCount ||
                GameConstants.TierPlayerMaxVelocity.Length != tierCount)
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
                double pMin = GameConstants.TierPlayerMinVelocity[i];
                double pMax = GameConstants.TierPlayerMaxVelocity[i];

                if (!(eMin <= eMax + eps))
                    throw new InvalidOperationException($"Tier {i} enemy min > max ({eMin} > {eMax}).");

                if (!(pMin <= pMax + eps))
                    throw new InvalidOperationException($"Tier {i} player min > max ({pMin} > {pMax}).");

                // Player should at least be able to match enemy max (policy in codebase)
                if (pMin + eps < eMax)
                    throw new InvalidOperationException($"Tier {i} player min ({pMin}) is less than enemy max ({eMax}); player may not reach target velocities.");

                // WaveTier detection/velocity ranges should be consistent with Tier arrays
                if (tier.VelocityMin + eps < eMin || tier.VelocityMax - eps > eMax)
                    throw new InvalidOperationException($"WaveTiers[{i}] velocity range [{tier.VelocityMin},{tier.VelocityMax}] must fall inside [{eMin},{eMax}] tier bounds.");

                // Validate GetTierVelocityConstraints returns the expected tuple
                var tuple = GameConstants.GetTierVelocityConstraints(i);
                if (Math.Abs(tuple.EnemyMin - eMin) > eps ||
                    Math.Abs(tuple.EnemyMax - eMax) > eps ||
                    Math.Abs(tuple.PlayerMin - pMin) > eps ||
                    Math.Abs(tuple.PlayerMax - pMax) > eps)
                {
                    throw new InvalidOperationException($"GetTierVelocityConstraints returned inconsistent values for tier {i}.");
                }
            }

            // Validate out-of-bounds behaviour (negative and large index -> last tier)
            var lastExpected = GameConstants.GetTierVelocityConstraints(tierCount - 1);
            var neg = GameConstants.GetTierVelocityConstraints(-1);
            var over = GameConstants.GetTierVelocityConstraints(tierCount + 5);

            if (Math.Abs(neg.EnemyMin - lastExpected.EnemyMin) > eps ||
                Math.Abs(over.EnemyMin - lastExpected.EnemyMin) > eps)
            {
                throw new InvalidOperationException("GetTierVelocityConstraints did not clamp out-of-range indices to the last tier as expected.");
            }
        }
    }
}