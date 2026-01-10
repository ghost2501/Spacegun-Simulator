using System.Collections.Generic;

namespace Spacegun_Simulator.Core
{
    /// <summary>
    /// Config-backed enemy designation pools and static specs used during wave generation.
    /// Source of truth is Config/Enemies/EnemyDesignations.json.
    /// </summary>
    public static class EnemyDesignations
    {
        private static EnemyDesignationsValues? _current;

        public static EnemyDesignationsValues Current
        {
            get
            {
                if (_current is null)
                    throw new System.InvalidOperationException("EnemyDesignations not loaded. Ensure EnemyConfigLoader.LoadOrThrow() is called during startup.");
                return _current;
            }
        }

        public static void ReplaceAll(EnemyDesignationsValues values)
        {
            _current = values ?? throw new System.ArgumentNullException(nameof(values));
        }

        public sealed record EnemyDesignationsValues(
            int TargetCountBase,
            int TargetCountTierBonus,
            int TargetCountRandomMaxExclusive,
            string[] EarlyTypes,
            string[] MidTypes,
            string[] LateTypes,
            Dictionary<string, (double Min, double Max)> CrossSectionRanges,
            double StealthChanceForLateTiers)
        {
        }
    }
}
