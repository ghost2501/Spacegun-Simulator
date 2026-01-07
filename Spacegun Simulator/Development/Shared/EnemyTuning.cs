using Spacegun_Simulator.Core;

namespace Spacegun_Simulator.Development.Shared
{
    /// <summary>
    /// Enemy generation tunables (type pools, per-type ranges, and stealth chance).
    /// Values are config-backed via Spacegun_Simulator.Core.DevelopmentTuning.
    /// </summary>
    public static class EnemyTuning
    {
        public static int TargetCountBase => DevelopmentTuning.Enemy.TargetCountBase;
        public static int TargetCountTierBonus => DevelopmentTuning.Enemy.TargetCountTierBonus;
        public static int TargetCountRandomMaxExclusive => DevelopmentTuning.Enemy.TargetCountRandomMaxExclusive;

        // Type pools
        public static string[] EarlyTypes => DevelopmentTuning.Enemy.EarlyTypes;
        public static string[] MidTypes => DevelopmentTuning.Enemy.MidTypes;
        public static string[] LateTypes => DevelopmentTuning.Enemy.LateTypes;

        private static Dictionary<string, (double Min, double Max)>? _crossSectionCache;
        private static Dictionary<string, DevelopmentTuning.Range>? _lastCrossSectionSource;

        // Cross-section ranges per type (square meters)
        public static Dictionary<string, (double Min, double Max)> CrossSectionRanges
        {
            get
            {
                var source = DevelopmentTuning.Enemy.CrossSectionRanges;
                if (!ReferenceEquals(_lastCrossSectionSource, source) || _crossSectionCache is null)
                {
                    _lastCrossSectionSource = source;
                    var rebuilt = new Dictionary<string, (double Min, double Max)>(source.Count);
                    foreach (var (key, range) in source)
                    {
                        rebuilt[key] = (range.Min, range.Max);
                    }
                    _crossSectionCache = rebuilt;
                }

                return _crossSectionCache;
            }
        }

        // Stealth chance when tier >= 2
        public static double StealthChanceForLateTiers => DevelopmentTuning.Enemy.StealthChanceForLateTiers;
    }
}
