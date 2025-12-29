namespace Spacegun_Simulator.Development.Shared
{
    /// <summary>
    /// Enemy generation tunables (type pools, per-type ranges, and stealth chance).
    /// Kept in Development/Shared to centralize tunables alongside other balance knobs.
    /// GameConstants forwards to these values to preserve legacy call sites.
    /// </summary>
    public static class EnemyTuning
    {
        public static int TargetCountBase = 2;
        public static int TargetCountTierBonus = 1;
        public static int TargetCountRandomMaxExclusive = 3;

        // Type pools
        public static readonly string[] EarlyTypes = { "Scout", "Fighter", "Light Cruiser" };
        public static readonly string[] MidTypes = { "Cruiser", "Destroyer", "Heavy Fighter" };
        public static readonly string[] LateTypes = { "Battlecruiser", "Dreadnought", "Carrier" };

        // Cross-section ranges per type (square meters)
        public static readonly Dictionary<string, (double Min, double Max)> CrossSectionRanges = new()
        {
            ["Scout"] = (10.0, 30.0),
            ["Fighter"] = (20.0, 50.0),
            ["Light Cruiser"] = (40.0, 80.0),
            ["Cruiser"] = (80.0, 140.0),
            ["Destroyer"] = (100.0, 180.0),
            ["Heavy Fighter"] = (50.0, 100.0),
            ["Battlecruiser"] = (150.0, 250.0),
            ["Dreadnought"] = (250.0, 400.0),
            ["Carrier"] = (300.0, 500.0)
        };

        // Evasiveness ranges by type (0..1)
        public static readonly Dictionary<string, (double Min, double Max)> EvasivenessRanges = new()
        {
            ["Scout"] = (0.6, 0.9),
            ["Fighter"] = (0.5, 0.8),
            ["Light Cruiser"] = (0.2, 0.5),
            ["Cruiser"] = (0.2, 0.5),
            ["Destroyer"] = (0.2, 0.5),
            ["Heavy Fighter"] = (0.3, 0.6),
            ["Battlecruiser"] = (0.1, 0.4),
            ["Dreadnought"] = (0.05, 0.3),
            ["Carrier"] = (0.05, 0.3)
        };

        // Stealth chance when tier >= 2
        public static double StealthChanceForLateTiers = 0.3;
    }
}
